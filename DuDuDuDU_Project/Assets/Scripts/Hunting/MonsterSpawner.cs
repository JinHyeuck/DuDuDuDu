using UnityEngine;
using System.Collections.Generic;

public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance;

    public int poolSize = 20;
    private Queue<Monster> pool = new Queue<Monster>();

    public Monster monsterPrefab;
    public float spawnInterval = 2f;
    public float spawnXRange = 7f;
    public float spawnY = 5f;

    private float timer = 0f;

    void Awake() 
    { 
        Instance = this;
    }

    private void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(monsterPrefab.gameObject);
            obj.SetActive(false);
            pool.Enqueue(obj.GetComponent<Monster>());
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnMonster();
            timer = 0f;
        }
    }

    public Monster GetBullet()
    {
        if (pool.Count > 0)
        {
            Monster queuebullet = pool.Dequeue();
            queuebullet.gameObject.SetActive(true);
            return queuebullet;
        }

        GameObject obj = Instantiate(monsterPrefab.gameObject);
        return obj.GetComponent<Monster>();
    }

    public void PoolBullet(Monster bullet)
    {
        bullet.gameObject.SetActive(false);
        pool.Enqueue(bullet);
    }

    int hp = 1;
    void SpawnMonster()
    {
        Vector2 spawnPos = new Vector2(Random.Range(-spawnXRange, spawnXRange), spawnY);

        //GameObject clone = Instantiate(monsterPrefab.gameObject, spawnPos, Quaternion.identity);
        //clone.gameObject.SetActive(true);
        Monster monster = GetBullet();
        monster.OnSpawn();
        monster.transform.position = spawnPos;
        monster.transform.rotation = Quaternion.identity;
        monster.SetHp(hp);
        hp++;
    }
}
