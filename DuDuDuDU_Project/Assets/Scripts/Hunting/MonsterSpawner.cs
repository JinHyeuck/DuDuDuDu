using UnityEngine;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;
using OJ.DI;
using OJ.Stage;
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

            if (IsWaveSpawnCompleted())
                return;

            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                SpawnNext();
                timer = 0f;
            }
        }

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
            if (ShouldSpawnBossNow())
            {
                SpawnBossMonster();
                return;
            }

            if (regularSpawnCount < GetRegularSpawnTarget())
                SpawnRegularMonster();
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
            monsterPools.Clear();
            bossMonsterPools.Clear();
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
