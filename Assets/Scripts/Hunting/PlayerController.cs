using System.Collections.Generic;
using UnityEngine;
using OJ;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    public Transform firePoint;
    public float fireRate = 0.5f;
    private float timer = 0f;
    private int shotcount = 0;

    private List<DiceType> diceTypes = new List<DiceType>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (diceTypes.Count <= 0)
            return;

        timer += Time.deltaTime;
        if (timer >= fireRate)
        {
            if (shotcount >= diceTypes.Count)
                shotcount = 0;

            ShootAtClosest(diceTypes[shotcount]);
            timer = 0f;

            shotcount++;
        }
    }

    public void RefreshDice()
    {
        diceTypes.Clear();

        foreach (var pair in DiceTypeStarManager.Instance.typeCountTotals)
        {
            for (int i = 0; i < pair.Value; ++i)
            {
                diceTypes.Add(pair.Key);
            }
        }

        diceTypes.Shuffle();

        fireRate = 1.0f / (float)diceTypes.Count;
        shotcount = 0;
    }

    void ShootAtClosest(DiceType diceType)
    {
        Monster target = MonsterManager.Instance.GetClosestMonster(firePoint.position);
        if (target == null) return;

        Vector2 dir = (target.transform.position - firePoint.position).normalized;

        Bullet bulletObj = BulletPool.Instance.GetBullet();
        bulletObj.transform.position = firePoint.position;
        bulletObj.transform.rotation = Quaternion.identity;
        bulletObj.SetBulletStat(diceType);
        bulletObj.Shoot(dir);
    }
}
