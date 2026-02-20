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

        public List<Monster> monsterPrefab;
        public float spawnInterval = 2f;
        public float spawnXRange = 7f;
        public float spawnY = 5f;

        private float timer = 0f;

        private int SpawnCount = 0;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            for (int i = 0; i < monsterPrefab.Count; ++i)
            {
                Monster monster = monsterPrefab[i];

                if (monsterPools.ContainsKey(monster.MonsterID) == true)
                    continue;

                monsterPools.Add(monster.MonsterID, new Queue<Monster>());

                monsterIdList.Add(monster.MonsterID);

                for (int pools = 0; pools < poolSize; pools++)
                {
                    GameObject obj = Instantiate(monster.gameObject);
                    obj.SetActive(false);
                    monsterPools[monster.MonsterID].Enqueue(obj.GetComponent<Monster>());
                }
            }
        }

        public void PlayWave()
        {
            SpawnCount = 0;
            timer = 0;
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.inGameState != InGameState.Wave)
                return;

            if (GameManager.Instance.WaveMonsterCount <= SpawnCount)
                return;

            timer += Time.deltaTime;
            if (timer >= spawnInterval)
            {
                SpawnMonster();
                timer = 0f;
            }
        }

        public Monster GetMonster()
        {
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

        public void PoolMonster(Monster monster)
        {
            monster.gameObject.SetActive(false);
            monsterPools[monster.MonsterID].Enqueue(monster);
        }

        int hp = 1;
        void SpawnMonster()
        {
            Vector2 spawnPos = new Vector2(Random.Range(-spawnXRange, spawnXRange), spawnY);

            //GameObject clone = Instantiate(monsterPrefab.gameObject, spawnPos, Quaternion.identity);
            //clone.gameObject.SetActive(true);
            Monster monster = GetMonster();
            monster.OnSpawn();
            monster.transform.position = spawnPos;
            monster.transform.rotation = Quaternion.identity;
            monster.SetHp(hp);
            hp++;

            SpawnCount++;
        }

        // Backward-compatible wrappers (remove after call sites are fully migrated).
        public Monster GetBullet() => GetMonster();
        public void PoolBullet(Monster monster) => PoolMonster(monster);
    }

}
