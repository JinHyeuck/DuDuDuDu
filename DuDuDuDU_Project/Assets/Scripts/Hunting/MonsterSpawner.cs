using UnityEngine;
using System.Collections.Generic;

namespace OJ
{
    public class MonsterSpawner : MonoBehaviour
    {
        public static MonsterSpawner Instance;

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
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            defaultMonsterPrefabs = monsterPrefab != null
                ? new List<Monster>(monsterPrefab)
                : new List<Monster>();
            defaultBossMonsterPrefabs = bossMonsterPrefab != null
                ? new List<Monster>(bossMonsterPrefab)
                : new List<Monster>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            CacheSpawnCamera();
            StageTheme theme = GameManager.Instance != null && GameManager.Instance.CurrentStageData != null
                ? GameManager.Instance.CurrentStageData.theme
                : StageTheme.DarkForest;
            ConfigureTheme(theme);
        }

        public void ConfigureTheme(StageTheme theme)
        {
            if (poolsInitialized && configuredTheme == theme)
                return;

            StageThemeResource resource = StaticResource.Instance.GetStageThemeResource(theme);
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
            if (GameManager.Instance == null || GameManager.Instance.inGameState != InGameState.Wave)
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

            GameObject obj = Instantiate(monsterPrefab.Find(x => x.MonsterID == monsterIdx).gameObject);
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

            GameObject obj = Instantiate(bossMonsterPrefab.Find(x => x.MonsterID == monsterIdx).gameObject);
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
            int monsterHp = GameManager.Instance != null ? GameManager.Instance.GetCurrentWaveMonsterHp() : 1;
            int monsterDefense = GameManager.Instance != null ? GameManager.Instance.GetCurrentWaveMonsterDefense() : 0;
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
            int monsterHp = GameManager.Instance != null ? GameManager.Instance.GetCurrentWaveBossHp() : 1;
            int monsterDefense = GameManager.Instance != null ? GameManager.Instance.GetCurrentWaveBossDefense() : 0;
            float monsterScale = GameManager.Instance != null ? GameManager.Instance.GetCurrentWaveBossScale() : 1.45f;
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
                    GameObject obj = Instantiate(monster.gameObject);
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

            int threshold = GameManager.Instance != null && GameManager.Instance.CurrentStageData != null
                ? GameManager.Instance.CurrentStageData.GetBossSpawnThreshold()
                : Mathf.Max(1, Mathf.CeilToInt(GetRegularSpawnTarget() * 0.5f));

            return regularSpawnCount >= threshold;
        }

        private bool IsBossWave()
        {
            return GameManager.Instance != null && GameManager.Instance.IsBossWave();
        }

        private int GetRegularSpawnTarget()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentStageData == null)
                return 0;

            return Mathf.Max(1, GameManager.Instance.CurrentStageData.monstersPerWave);
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
