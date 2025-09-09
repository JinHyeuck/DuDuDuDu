using UnityEngine;
using System.Collections.Generic;

public class DamageTextPool : MonoBehaviour
{
    public static DamageTextPool Instance;
    public GameObject damageTextPrefab;
    public int poolSize = 20;
    private List<GameObject> pool = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(damageTextPrefab, transform);
            obj.SetActive(false);
            pool.Add(obj);
        }
    }

    public GameObject GetDamageText()
    {
        foreach (GameObject dt in pool)
        {
            if (!dt.activeInHierarchy)
            {
                dt.SetActive(true);
                return dt;
            }
        }

        GameObject obj = Instantiate(damageTextPrefab, transform);
        pool.Add(obj);
        return obj;
    }
}
