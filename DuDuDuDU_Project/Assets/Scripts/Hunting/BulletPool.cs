using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;
    public GameObject bulletPrefab;
    public int poolSize = 20;
    private Queue<Bullet> pool = new Queue<Bullet>();

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(bulletPrefab);
            obj.SetActive(false);
            pool.Enqueue(obj.GetComponent<Bullet>());
        }
    }

    public Bullet GetBullet()
    {
        if (pool.Count > 0)
        {
            Bullet queuebullet = pool.Dequeue();
            queuebullet.gameObject.SetActive(true);
            return queuebullet;
        }

        GameObject obj = Instantiate(bulletPrefab);
        return obj.GetComponent<Bullet>();
    }

    public void PoolBullet(Bullet bullet)
    {
        bullet.gameObject.SetActive(false);
        pool.Enqueue(bullet);
    }
}
