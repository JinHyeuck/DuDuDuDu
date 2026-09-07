using UnityEngine;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.Stage;
using OJ.Tower;
using OJ.Utils;

namespace OJ.Hunting
{
    public class MonsterSpawner : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. 이 스포너는 BattleScene 에만 사는 매니저이고
        // 스코프 빌드는 씬의 모든 Start 앞이므로, Start 이후 코드에서는 null 이 될 수 없다.
        // 그래서 아래 호출부에 매니저 존재 여부를 묻는 null 검사를 남기지 않는다 —
        // 남기면 "전투 씬인데 GameManager 가 없다"는 사고를 조용히 삼킨다.
        [Inject] private IBattleRefs battle;

        // 8.3b: 몬스터는 런타임에 찍히므로 스코프가 씬을 훑던 시점에는 존재하지 않는다.
        // 리졸버로 찍어야 Monster 쪽 [Inject] 가 채워진다. 주입 시점이 Awake 뒤라
        // 이 필드는 Start 이후(ConfigureTheme/SpawnNext)에서만 쓴다.
        [Inject] private IObjectResolver resolver;

        public int poolSize = 20;

        private Dictionary<int, Queue<Monster>> monsterPools = new Dictionary<int, Queue<Monster>>();
        private List<int> monsterIdList = new List<int>();
        private Dictionary<int, Queue<Monster>> bossMonsterPools = new Dictionary<int, Queue<Monster>>();
        private List<int> bossMonsterIdList = new List<int>();
        private readonly HashSet<Monster> bossMonsterInstances = new HashSet<Monster>();

        // 현상금 전용 프리팹(prefabOverride)을 위한 풀. 보스를 빌려 쓰는 기본 경로는
        // 보스 풀을 그대로 타므로 여기 들어오지 않는다.
        private readonly Dictionary<int, Queue<Monster>> bountyMonsterPools = new Dictionary<int, Queue<Monster>>();
        private readonly HashSet<Monster> bountyMonsterInstances = new HashSet<Monster>();

        public List<Monster> monsterPrefab;
        public List<Monster> bossMonsterPrefab;
        public float spawnInterval = 2f;
        public float spawnXRange = 7f;
        public float spawnY = 5f;
        [SerializeField] private bool useCameraBoundsForSpawnX = true;
        [SerializeField] private float spawnHorizontalPadding = 0.35f;

        private float timer = 0f;
        private Camera spawnCamera;

        private int regularSpawnCount = 0;
        private bool bossSpawnedInWave = false;

        /// <summary>
        /// 현상금이 나오기 전에 먼저 내보낼 일반 몬스터 수.
        ///
        /// 웨이브가 열리자마자 현상금이 나오면 화면이 "이걸 잡으라는 건가" 로만 읽히고,
        /// 보스처럼 절반 지점(<c>GetBossSpawnThreshold</c>)까지 미루면 느린 걸음으로
        /// 벽까지 가는 시간이 웨이브를 넘어간다. 둘 사이의 값이다.
        /// </summary>
        [SerializeField, Min(0)] private int bountySpawnAfterRegularCount = 2;
        private List<Monster> defaultMonsterPrefabs;
        private List<Monster> defaultBossMonsterPrefabs;
        private bool poolsInitialized;
        private StageTheme configuredTheme;

        void Awake()
        {
            defaultMonsterPrefabs = monsterPrefab != null
                ? new List<Monster>(monsterPrefab)
                : new List<Monster>();
            defaultBossMonsterPrefabs = bossMonsterPrefab != null
                ? new List<Monster>(bossMonsterPrefab)
                : new List<Monster>();
        }

        private void Start()
        {
            CacheSpawnCamera();
            // 남긴 null 검사는 매니저가 아니라 데이터에 대한 것이다. 스테이지가 아직
            // 정해지지 않은 시점에 Start 가 돌 수 있고, 그때 DarkForest 로 시작하는 것은
            // 기존 동작이다.
            StageTheme theme = battle.Game.CurrentStageData != null
                ? battle.Game.CurrentStageData.theme
                : StageTheme.DarkForest;
            ConfigureTheme(theme);
        }

        public void ConfigureTheme(StageTheme theme)
        {
            if (poolsInitialized && configuredTheme == theme)
                return;

            // ConfigureTheme 은 GameManager.InitializeStage 안에서 불린다. 여기서 터지면
            // 스테이지 초기화가 통째로 끊긴다. StaticResource 부재는 MonoSingleton 이 운다.
            StaticResource staticResource = StaticResource.Instance;
            StageThemeResource resource = staticResource != null
                ? staticResource.GetStageThemeResource(theme)
                : null;
            monsterPrefab = BuildRegularPrefabList(resource);
            bossMonsterPrefab = BuildBossPrefabList(resource);

            RebuildPools();
            configuredTheme = theme;
            poolsInitialized = true;
        }

        public void PlayWave()
        {
            regularSpawnCount = 0;
            bossSpawnedInWave = false;
            timer = 0;
        }

        void Update()
        {
            // Update 는 Start 뒤에만 돌고 주입은 Start 앞에 끝난다. battle.Game 은 여기서 산다.
            if (battle.Game.inGameState != InGameState.Wave)
                return;

            // 무한의 탑은 규칙이 다르다 — 보스도 현상금도 없고, 층 계획이 마리 수와
            // 간격을 정한다. 여기서 갈라 놓지 않으면 아래 세 판정(현상금·보스·일반)이
            // 전부 탑에 맞지 않는 답을 낸다.
            if (battle.Tower.IsActive)
            {
                TickTowerSpawn();
                return;
            }

            if (IsWaveSpawnCompleted())
                return;

            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                SpawnNext();
                timer = 0f;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 무한의 탑
        //
        // <b>풀과 위치는 여기 남는다.</b> 탑이 자기 스포너를 갖게 하면 몬스터 풀이
        // 두 벌이 되고, 화면 밖 이탈·재사용 규칙을 두 곳에서 지켜야 한다.
        // 여기서 정하는 것은 "무엇을 몇 마리, 어떤 간격으로" 뿐이고 그 답은
        // <c>TowerRunManager</c> 가 준다.
        // ──────────────────────────────────────────────────────────────

        private void TickTowerSpawn()
        {
            if (!battle.Tower.HasMoreToSpawn)
                return;

            timer += Time.deltaTime;
            if (timer < battle.Tower.Plan.SpawnInterval)
                return;

            timer = 0f;
            SpawnTowerMonster();
        }

        private void SpawnTowerMonster()
        {
            TowerFloorPlan plan = battle.Tower.Plan;

            // 덩치가 큰 콘셉트(정예·고방어)는 보스 프리팹을 쓴다. 전용 아트가 없는
            // 지금, 30마리짜리 물량과 1마리짜리 정예가 같은 모습으로 나오면 층의
            // 성격이 화면에서 안 읽힌다 — 기획서 1.3 의 "적 정보를 읽고" 가 무너진다.
            Monster monster = plan.ScaleMultiplier >= TowerBossLookScale
                ? GetBossMonster()
                : GetMonster();

            if (monster == null)
                return;

            TowerMonsterSpec spec = battle.Tower.GetSpawnSpec();

            monster.OnSpawn();
            monster.transform.position = GetSpawnPosition();
            monster.transform.rotation = Quaternion.identity;
            monster.SetCombatStats(spec.Hp, spec.Defense, spec.Scale);

            // OnSpawn 이 ApplyMoveSpeed 를 되돌려 놓으므로 반드시 그 뒤다.
            monster.ConfigureAsTowerMonster(spec);

            battle.Tower.NotifySpawned();
        }

        /// <summary>
        /// 분열 자식을 내보낸다. <c>TowerRunManager.NotifyMonsterDefeated</c> 가 부른다.
        ///
        /// <b>부모가 죽은 자리에서 좌우로 흩어 놓는다.</b> 같은 점에 겹쳐 놓으면
        /// 스프라이트가 하나로 보여 두 마리인 줄 모르고, 범위 공격 하나가 언제나
        /// 둘을 같이 맞혀 "단일과 범위의 균형" 이라는 이 기믹의 요구가 사라진다.
        /// </summary>
        /// <returns>
        /// <b>실제로 내보낸 자식 수.</b> 약속한 수보다 적을 수 있고, 그때는 부르는 쪽이
        /// 그만큼 목표 처치 수를 깎아야 한다 — 안 그러면 나오지도 않은 자식을 기다리며
        /// 층이 영영 안 끝난다.
        /// </returns>
        public int SpawnTowerSplitChildren(Monster parent, Vector3 deathPosition)
        {
            if (parent == null || !battle.Tower.IsActive)
                return 0;

            int count = parent.TowerSplitChildCount;
            if (count <= 0)
                return 0;

            TowerFloorPlan plan = battle.Tower.Plan;
            int childHp = plan.SplitChildHp;
            float childScale = Mathf.Max(0.1f, plan.ScaleMultiplier * SplitChildScaleRatio);

            for (int i = 0; i < count; i++)
            {
                Monster child = GetMonster();
                if (child == null)
                {
                    // 풀이 비었고 새로 찍지도 못했다. 여기서 조용히 빠져나가면
                    // <b>목표는 그대로인데 자식이 모자라</b> 층이 끝나지 않는다.
                    // 몇 마리를 못 냈는지 돌려줘 부르는 쪽이 목표를 맞추게 한다.
                    Debug.LogError("[탑] 분열 자식을 " + count + "마리 중 " + i +
                                   "마리만 내보냈다. 목표 처치 수를 그만큼 줄인다.");
                    return i;
                }

                child.OnSpawn();

                float offsetX = (i - (count - 1) * 0.5f) * SplitChildSpreadX;
                child.transform.position = new Vector3(deathPosition.x + offsetX, deathPosition.y, deathPosition.z);
                child.transform.rotation = Quaternion.identity;

                // 방어력은 부모와 같다. 낮추면 분열 구간이 "부모만 단단한 층" 이 되어
                // 자식을 처리하는 범위 공격의 값어치가 사라진다.
                child.SetCombatStats(childHp, plan.MonsterDefense, childScale);

                // 자식은 조금 빠르다. 부모가 죽은 자리에서 다시 내려오기 시작하므로
                // 같은 속도면 벽까지 가는 시간이 층마다 두 배로 늘어난다.
                child.ConfigureAsTowerSplitChild(plan.MoveSpeedMultiplier * SplitChildSpeedRatio);
            }

            return count;
        }

        /// <summary>이 배수를 넘는 층은 보스 프리팹으로 나온다. 덩치로 콘셉트를 읽히게 하는 값이다.</summary>
        private const float TowerBossLookScale = 1.3f;

        private const float SplitChildScaleRatio = 0.65f;
        private const float SplitChildSpeedRatio = 1.25f;
        private const float SplitChildSpreadX = 0.7f;

        public Monster GetMonster()
        {
            if (monsterIdList.Count == 0)
            {
                Debug.LogError($"No regular monsters are configured for stage theme {configuredTheme}.");
                return null;
            }

            int monsterIdx = monsterIdList[Random.Range(0, monsterIdList.Count)];

            Queue<Monster> pool = monsterPools[monsterIdx];

            if (pool.Count > 0)
            {
                Monster queuebullet = pool.Dequeue();
                queuebullet.gameObject.SetActive(true);
                return queuebullet;
            }

            // 부모를 스포너 자신으로 준다. 부모 없는 오버로드는 resolver.ApplicationOrigin 의
            // IsRoot 를 보고 갈라지는데, VContainerSettings 에셋이 생기는 순간 그 분기가
            // DontDestroyOnLoad 로 넘어가 몬스터가 로비까지 따라온다(BattleScope 주석 참고).
            // 부모를 넘기는 오버로드는 그 분기를 아예 타지 않으므로 에셋 유무에 흔들리지 않는다.
            // 스포너는 씬 루트에 회전 0 / 스케일 1 로 있고, 아래 SpawnRegularMonster 가
            // 월드 position·rotation 을 곧바로 덮어쓰므로 몬스터가 서는 자리는 그대로다.
            GameObject obj = resolver.Instantiate(monsterPrefab.Find(x => x.MonsterID == monsterIdx).gameObject, transform);
            return obj.GetComponent<Monster>();
        }

        public Monster GetBossMonster()
        {
            if (bossMonsterIdList.Count == 0)
                return GetMonster();

            int monsterIdx = bossMonsterIdList[Random.Range(0, bossMonsterIdList.Count)];
            Queue<Monster> pool = bossMonsterPools[monsterIdx];

            if (pool.Count > 0)
            {
                Monster bossMonster = pool.Dequeue();
                bossMonster.gameObject.SetActive(true);
                return bossMonster;
            }

            // 위와 같은 이유로 스포너를 부모로 준다. 보스 스케일은 SetCombatStats 가
            // localScale 로 따로 먹이는데 부모 스케일이 1 이라 lossyScale 도 그대로다.
            GameObject obj = resolver.Instantiate(bossMonsterPrefab.Find(x => x.MonsterID == monsterIdx).gameObject, transform);
            Monster spawnedBoss = obj.GetComponent<Monster>();
            bossMonsterInstances.Add(spawnedBoss);
            return spawnedBoss;
        }

        public void PoolMonster(Monster monster)
        {
            if (monster == null)
                return;

            monster.gameObject.SetActive(false);

            if (bountyMonsterInstances.Contains(monster))
            {
                if (!bountyMonsterPools.TryGetValue(monster.MonsterID, out Queue<Monster> bountyPool))
                {
                    bountyPool = new Queue<Monster>();
                    bountyMonsterPools.Add(monster.MonsterID, bountyPool);
                }

                bountyPool.Enqueue(monster);
                return;
            }

            if (bossMonsterPools.TryGetValue(monster.MonsterID, out Queue<Monster> bossPool)
                && bossMonsterInstances.Contains(monster))
            {
                bossPool.Enqueue(monster);
                return;
            }

            if (monsterPools.TryGetValue(monster.MonsterID, out Queue<Monster> pool))
                pool.Enqueue(monster);
        }

        private void SpawnNext()
        {
            // 현상금을 보스보다 먼저 본다. 보스 웨이브에는 현상금이 아예 나오지 않으므로
            // (BountyFormula.CanSpawnOnWave) 둘이 같은 웨이브에서 겨루는 일은 없고,
            // 순서를 이렇게 두면 그 사실이 코드에서도 한눈에 보인다.
            if (ShouldSpawnBountyNow())
            {
                SpawnBountyMonster();
                return;
            }

            if (ShouldSpawnBossNow())
            {
                SpawnBossMonster();
                return;
            }

            if (regularSpawnCount < GetRegularSpawnTarget())
                SpawnRegularMonster();
        }

        private bool ShouldSpawnBountyNow()
        {
            BountyManager bounty = battle.Bounty;
            if (bounty == null || !bounty.ShouldSpawn)
                return false;

            // 기준선을 그 웨이브의 일반 몬스터 수로 눌러 준다. 안 그러면 몬스터가
            // 기준보다 적은 웨이브에서 조건이 영영 참이 되지 않아, 나오지도 않은 현상금을
            // 기다리며 웨이브가 멈춘다 — BountyFormula.SpawnThreshold 주석 참조.
            int threshold = BountyFormula.SpawnThreshold(
                bountySpawnAfterRegularCount, GetRegularSpawnTarget());

            return regularSpawnCount >= threshold;
        }

        /// <summary>
        /// 현상금을 내보낸다. <b>일반·보스와 달리 웨이브 목표 수를 늘리지 않는다</b> —
        /// <c>regularSpawnCount</c> 를 건드리지 않는 것이 그 뜻이다.
        ///
        /// 프리팹은 정의에 따로 꽂아 두지 않았으면 그 테마의 보스를 빌려 쓴다.
        /// 전용 아트가 붙기 전까지 크기로만 구분되는데, 아무것도 안 나오는 것보다는 낫고
        /// 정의에 <c>prefabOverride</c> 를 꽂는 순간 이 경로는 지나가지 않는다.
        /// </summary>
        private void SpawnBountyMonster()
        {
            BountyManager bounty = battle.Bounty;
            BountyDefinition definition = bounty.GetDefinition(bounty.ActiveGrade);
            if (definition == null)
            {
                Debug.LogError("[현상금] 등급 " + bounty.ActiveGrade + " 의 정의가 없어 소환하지 않는다.");
                return;
            }

            Monster monster = GetBountyMonster(definition);
            if (monster == null)
                return;

            monster.OnSpawn();
            monster.transform.position = GetSpawnPosition();
            monster.transform.rotation = Quaternion.identity;

            // 방어력은 0 이다. 값을 주면 아머브레이크 계통이 사실상 강제 채용이 되어
            // "무엇을 잡을까" 가 "무엇을 뽑았나" 로 바뀐다.
            monster.SetCombatStats(bounty.GetHp(bounty.ActiveGrade), 0, definition.scaleMultiplier);

            // OnSpawn 이 ApplyMoveSpeed 를 되돌려 놓으므로 반드시 그 뒤다.
            monster.ConfigureAsBounty(definition.moveSpeedMultiplier);

            bounty.NotifySpawned(monster);
        }

        private Monster GetBountyMonster(BountyDefinition definition)
        {
            if (definition.prefabOverride == null)
                return GetBossMonster();

            int id = definition.prefabOverride.MonsterID;
            if (bountyMonsterPools.TryGetValue(id, out Queue<Monster> pool) && pool.Count > 0)
            {
                Monster pooled = pool.Dequeue();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            // 풀에 없으면 그 자리에서 찍는다. 현상금은 웨이브당 한 마리라 예열 없이도
            // 늘어나지 않는다 — 그래서 RebuildPools 가 미리 만들어 두지 않는다.
            GameObject obj = resolver.Instantiate(definition.prefabOverride.gameObject, transform);
            Monster spawned = obj.GetComponent<Monster>();
            if (!bountyMonsterPools.ContainsKey(id))
                bountyMonsterPools.Add(id, new Queue<Monster>());

            bountyMonsterInstances.Add(spawned);
            return spawned;
        }

        private void SpawnRegularMonster()
        {
            Vector2 spawnPos = GetSpawnPosition();

            Monster monster = GetMonster();
            if (monster == null)
                return;

            monster.OnSpawn();
            monster.transform.position = spawnPos;
            monster.transform.rotation = Quaternion.identity;
            // 폴백 1/0 은 GameManager 가 없을 때만 쓰이던 값이다. 전투 씬에서 그 상황은
            // 성립하지 않으므로 폴백을 지운다 — 남기면 스탯 0 짜리 몬스터로 조용히 굴러간다.
            int monsterHp = battle.Game.GetCurrentWaveMonsterHp();
            int monsterDefense = battle.Game.GetCurrentWaveMonsterDefense();
            monster.SetCombatStats(monsterHp, monsterDefense);

            regularSpawnCount++;
        }

        private void SpawnBossMonster()
        {
            Vector2 spawnPos = GetSpawnPosition();
            Monster monster = GetBossMonster();
            if (monster == null)
                return;

            monster.OnSpawn();
            monster.transform.position = spawnPos;
            monster.transform.rotation = Quaternion.identity;
            // 위와 같은 이유로 폴백(1/0/1.45)을 지운다. 보스가 스탯 없이 나오는 것보다
            // GameManager 가 없다는 사실이 그 자리에서 터지는 편이 낫다.
            int monsterHp = battle.Game.GetCurrentWaveBossHp();
            int monsterDefense = battle.Game.GetCurrentWaveBossDefense();
            float monsterScale = battle.Game.GetCurrentWaveBossScale();
            monster.SetCombatStats(monsterHp, monsterDefense, monsterScale);
            bossSpawnedInWave = true;
        }

        private void InitializePools(
            List<Monster> prefabs,
            Dictionary<int, Queue<Monster>> pools,
            List<int> idList,
            bool isBossPool)
        {
            if (prefabs == null)
                return;

            for (int i = 0; i < prefabs.Count; ++i)
            {
                Monster monster = prefabs[i];
                if (monster == null || pools.ContainsKey(monster.MonsterID))
                    continue;

                pools.Add(monster.MonsterID, new Queue<Monster>());
                idList.Add(monster.MonsterID);

                for (int j = 0; j < poolSize; j++)
                {
                    // 풀에 재워두는 몬스터도 리졸버로 찍는다. 나중에 꺼내 쓸 때만의 문제가
                    // 아니라 바로 아래 SetActive(false) 가 Monster.OnDisable 을 돌리는데,
                    // 거기서 battle.Monsters 를 만진다 — 주입 없이 찍으면 그 자리에서 터진다.
                    // 부모는 스포너 — 위 GetMonster 와 같은 이유이고, 위치는 꺼내 쓰는
                    // 쪽(SpawnRegular/BossMonster)이 정하므로 여기선 상관없다.
                    GameObject obj = resolver.Instantiate(monster.gameObject, transform);
                    obj.SetActive(false);
                    Monster instance = obj.GetComponent<Monster>();
                    pools[monster.MonsterID].Enqueue(instance);
                    if (isBossPool)
                        bossMonsterInstances.Add(instance);
                }
            }
        }

        private void RebuildPools()
        {
            DestroyPooledMonsters(monsterPools);
            DestroyPooledMonsters(bossMonsterPools);
            DestroyPooledMonsters(bountyMonsterPools);
            monsterPools.Clear();
            bossMonsterPools.Clear();
            bountyMonsterPools.Clear();
            bountyMonsterInstances.Clear();
            monsterIdList.Clear();
            bossMonsterIdList.Clear();
            bossMonsterInstances.Clear();

            InitializePools(monsterPrefab, monsterPools, monsterIdList, false);
            InitializePools(bossMonsterPrefab, bossMonsterPools, bossMonsterIdList, true);
        }

        private static void DestroyPooledMonsters(Dictionary<int, Queue<Monster>> pools)
        {
            foreach (Queue<Monster> pool in pools.Values)
            {
                while (pool.Count > 0)
                {
                    Monster monster = pool.Dequeue();
                    if (monster != null)
                        Destroy(monster.gameObject);
                }
            }
        }

        private List<Monster> BuildRegularPrefabList(StageThemeResource resource)
        {
            var result = new List<Monster>();
            if (resource != null && resource.Monsters != null)
            {
                for (int i = 0; i < resource.Monsters.Length; i++)
                {
                    if (resource.Monsters[i] != null)
                        result.Add(resource.Monsters[i]);
                }
            }

            if (result.Count == 0)
                result.AddRange(defaultMonsterPrefabs);

            return result;
        }

        private List<Monster> BuildBossPrefabList(StageThemeResource resource)
        {
            var result = new List<Monster>();
            if (resource != null && resource.BossMonster != null)
                result.Add(resource.BossMonster);
            else
                result.AddRange(defaultBossMonsterPrefabs);

            return result;
        }

        private bool IsWaveSpawnCompleted()
        {
            // 현상금을 아직 안 내보냈으면 완료가 아니다. 이 항을 빠뜨리면 일반 몬스터가
            // 목표 수를 먼저 채운 웨이브에서 현상금이 <b>영영 안 나오고</b>, 그러면
            // 나오지 않은 것을 기다리다 웨이브도 끝나지 않는다.
            BountyManager bounty = battle.Bounty;
            if (bounty != null && bounty.ShouldSpawn)
                return false;

            int regularTarget = GetRegularSpawnTarget();
            return regularSpawnCount >= regularTarget && (!IsBossWave() || bossSpawnedInWave);
        }

        private bool ShouldSpawnBossNow()
        {
            if (!IsBossWave() || bossSpawnedInWave)
                return false;

            // CurrentStageData 는 매니저가 아니라 데이터다. 아직 안 정해졌을 수 있으므로
            // 계산식 폴백은 그대로 둔다.
            int threshold = battle.Game.CurrentStageData != null
                ? battle.Game.CurrentStageData.GetBossSpawnThreshold()
                : Mathf.Max(1, Mathf.CeilToInt(GetRegularSpawnTarget() * 0.5f));

            return regularSpawnCount >= threshold;
        }

        private bool IsBossWave()
        {
            return battle.Game.IsBossWave();
        }

        private int GetRegularSpawnTarget()
        {
            // 스테이지 데이터가 없으면 0 을 돌려 스폰을 멈추는 기존 동작은 유지한다.
            if (battle.Game.CurrentStageData == null)
                return 0;

            return Mathf.Max(1, battle.Game.CurrentStageData.monstersPerWave);
        }

        private Vector2 GetSpawnPosition()
        {
            float minX = -spawnXRange;
            float maxX = spawnXRange;

            if (TryGetVisibleSpawnBounds(out float visibleMinX, out float visibleMaxX))
            {
                minX = visibleMinX;
                maxX = visibleMaxX;
            }

            if (minX > maxX)
            {
                float centerX = (minX + maxX) * 0.5f;
                minX = centerX;
                maxX = centerX;
            }

            return new Vector2(Random.Range(minX, maxX), spawnY);
        }

        private bool TryGetVisibleSpawnBounds(out float minX, out float maxX)
        {
            minX = -spawnXRange;
            maxX = spawnXRange;

            if (!useCameraBoundsForSpawnX)
                return false;

            CacheSpawnCamera();
            if (spawnCamera == null)
                return false;

            Vector3 leftEdge = spawnCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, GetCameraDepth()));
            Vector3 rightEdge = spawnCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, GetCameraDepth()));
            minX = leftEdge.x + spawnHorizontalPadding;
            maxX = rightEdge.x - spawnHorizontalPadding;
            return true;
        }

        private void CacheSpawnCamera()
        {
            if (spawnCamera != null)
                return;

            spawnCamera = Camera.main;
        }

        private float GetCameraDepth()
        {
            if (spawnCamera == null)
                return 0f;

            if (spawnCamera.orthographic)
                return 0f;

            return Mathf.Abs(transform.position.z - spawnCamera.transform.position.z);
        }

        // Backward-compatible wrappers (remove after call sites are fully migrated).
        public Monster GetBullet() => GetMonster();
        public void PoolBullet(Monster monster) => PoolMonster(monster);
    }

}
