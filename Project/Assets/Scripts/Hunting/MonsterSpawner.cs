using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    public Monster monsterPrefab;
    public float spawnInterval = 2f;
    public float spawnXRange = 7f;
    public float spawnY = 5f;

    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            SpawnMonster();
            timer = 0f;
        }
    }
    int hp = 1;
    void SpawnMonster()
    {
        Vector2 spawnPos = new Vector2(Random.Range(-spawnXRange, spawnXRange), spawnY);

        GameObject clone = Instantiate(monsterPrefab.gameObject, spawnPos, Quaternion.identity);
        Monster monster = clone.GetComponent<Monster>();
        monster.SetHp(hp);
        hp++;
    }
}
