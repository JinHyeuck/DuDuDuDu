using UnityEngine;
using System.Collections.Generic;

namespace OJ.Hunting
{
    public class DamageTextPool : MonoBehaviour
    {
        public GameObject damageTextPrefab;
        public int poolSize = 20;
        private List<GameObject> pool = new List<GameObject>();

        private void Awake()
        {
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

        /// <summary>
        /// 떠 있는 숫자를 전부 거둔다. 웨이브 되돌리기가 부른다.
        ///
        /// <b>여기는 자식을 훑을 필요가 없다.</b> 총알·이펙트 풀과 달리 <see cref="pool"/> 이
        /// 켜진 것과 꺼진 것을 <b>모두</b> 들고 있고(<see cref="GetDamageText"/> 가 꺼진 것을
        /// 찾아 쓰는 구조다), 큐가 아니라 목록이라 중복으로 들어갈 자리도 없다.
        /// </summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                    pool[i].SetActive(false);
            }
        }
    }

}
