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

        /// <summary>
        /// 날아다니는 총알을 전부 거둔다. 웨이브 되돌리기가 부른다.
        ///
        /// <b>활성 총알 목록이 없어서 자식을 훑는다.</b> <see cref="pool"/> 은 <b>쉬는</b>
        /// 총알만 들고 있고, 나간 총알은 아무도 안 세고 있다 — 스스로 화면 밖으로 나가거나
        /// 맞으면 돌아오는 구조라 그럴 필요가 없었다. 되돌리기는 그 "언젠가"를 기다릴 수
        /// 없으므로(관리 단계 화면 위로 총알이 날아간다) 여기서 계층을 직접 본다.
        /// 찍는 곳이 둘 다 <c>transform</c> 을 부모로 주므로 빠지는 총알은 없다.
        ///
        /// <b>꺼져 있는 자식은 건드리지 않는다.</b> 그것은 이미 큐에 든 것이고,
        /// 다시 <see cref="PoolBullet"/> 하면 <b>같은 총알이 큐에 두 번</b> 들어가
        /// 나중에 두 곳에서 동시에 쓰인다.
        /// </summary>
        public void ReleaseAll()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (!child.activeSelf)
                    continue;

                Bullet bullet = child.GetComponent<Bullet>();
                if (bullet != null)
                    PoolBullet(bullet);
            }
        }
    }

}
