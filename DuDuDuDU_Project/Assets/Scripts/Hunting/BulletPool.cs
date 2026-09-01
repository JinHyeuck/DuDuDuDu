using UnityEngine;
using System.Collections.Generic;
using VContainer;
using VContainer.Unity;

namespace OJ.Hunting
{
    public class BulletPool : MonoBehaviour
    {
        public GameObject bulletPrefab;
        public int poolSize = 20;
        private Queue<Bullet> pool = new Queue<Bullet>();

        // 찍어낸 총알에도 [Inject] 가 채워지도록 리졸버를 통해 생성한다
        [Inject] private IObjectResolver resolver;

        void Start()
        {
            // 이 컴포넌트는 씬에 놓여 있어 스코프의 sceneLoaded 순회로 채워진다 —
            // 즉 자기 Awake 뒤다. 그래서 풀 예열을 Start 로 내렸다.
            // Awake 에서 찍으면 resolver 가 아직 null 이다
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = resolver.Instantiate(bulletPrefab, transform);
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

            GameObject obj = resolver.Instantiate(bulletPrefab, transform);
            return obj.GetComponent<Bullet>();
        }

        public void PoolBullet(Bullet bullet)
        {
            bullet.gameObject.SetActive(false);
            pool.Enqueue(bullet);
        }
    }

}
