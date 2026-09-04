using UnityEngine;
using System.Collections.Generic;
using OJ.Dice;
using VContainer;
using VContainer.Unity;

namespace OJ.Hunting
{
    public class BulletEffectPool : MonoBehaviour
    {
        // 이 풀이 찍어내는 BulletEffect 는 씬 로드 뒤에 태어나므로 BattleScope 의
        // 씬 루트 순회에 걸리지 않는다. 그래서 리졸버를 들고 있다가 직접 주입해 만든다.
        // 이 필드는 BattleScope 가 이 컴포넌트를 해석할 때 채워지므로 Awake 에서는 아직 null 이다.
        [Inject] private IObjectResolver resolver;

        public int poolSize = 5;
        private Dictionary<DiceType, Dictionary<EffectID, Queue<BulletEffect>>> effectpool = new Dictionary<DiceType, Dictionary<EffectID, Queue<BulletEffect>>>();

        // 프리워밍은 원래부터 Start 다. resolver 가 채워지는 시점(sceneLoaded)보다 뒤이므로
        // 그대로 둔다 — Awake 로 올리면 resolver 가 null 이다.
        private void Start()
        {
            InitializePool();
        }

        private void InitializePool()
        {
            effectpool.Clear();

            foreach (DiceType diceType in System.Enum.GetValues(typeof(DiceType)))
            {
                if (diceType == DiceType.Max)
                    continue;

                if (effectpool.ContainsKey(diceType) == false)
                    effectpool.Add(diceType, new Dictionary<EffectID, Queue<BulletEffect>>());

                List<BulletEffect> effectPrefabs = DiceMetaDataProvider.GetEffectPrefabs(diceType);
                if (effectPrefabs == null)
                    continue;

                for (int effects = 0; effects < effectPrefabs.Count; ++effects)
                {
                    BulletEffect bulletEffectObj = effectPrefabs[effects];
                    if (bulletEffectObj == null)
                        continue;

                    EffectID ID = bulletEffectObj.myEffectType;

                    if (effectpool[diceType].ContainsKey(ID) == true)
                        continue;

                    effectpool[diceType].Add(ID, new Queue<BulletEffect>());

                    for (int i = 0; i < poolSize; i++)
                    {
                        // 부모(this.transform)를 반드시 넘겨 종전과 같은 계층을 유지한다.
                        // 부모 없는 오버로드는 스코프 밑에 찍었다가 SetParent(null) 하는 분기라
                        // 이펙트가 풀 밑이 아니라 씬 루트로 흩어지고, 스코프가 IsRoot 면
                        // DontDestroyOnLoad 까지 타서 전투가 끝나도 남는다.
                        GameObject obj = resolver.Instantiate(bulletEffectObj.gameObject, transform);
                        obj.SetActive(false);

                        effectpool[diceType][ID].Enqueue(obj.GetComponent<BulletEffect>());
                    }
                }

            }
        }

        public BulletEffect GetBullet(DiceType diceType, EffectID effectID = EffectID.S)
        {
            if (!effectpool.TryGetValue(diceType, out var effectMap))
                return null;

            if (!effectMap.TryGetValue(effectID, out var pool))
                return null;

            if (pool.Count > 0)
            {
                BulletEffect queuebullet = pool.Dequeue();
                queuebullet.gameObject.SetActive(true);
                return queuebullet;
            }

            List<BulletEffect> effectPrefabs = DiceMetaDataProvider.GetEffectPrefabs(diceType);
            if (effectPrefabs == null)
                return null;

            BulletEffect bulletEffectObj = effectPrefabs.Find(x => x != null && x.myEffectType == effectID);
            if (bulletEffectObj == null)
                return null;
            // 풀이 비어 즉석에서 더 찍는 경로. 프리워밍분과 같은 부모·같은 주입을 받아야
            // 나중에 PoolBullet 으로 돌아왔을 때 구분이 없다.
            GameObject obj = resolver.Instantiate(bulletEffectObj.gameObject, transform);
            return obj.GetComponent<BulletEffect>();
        }

        public void PoolBullet(BulletEffect bullet)
        {
            Queue<BulletEffect> pool = effectpool[bullet.myDiceType][bullet.myEffectType];

            bullet.gameObject.SetActive(false);
            pool.Enqueue(bullet);
        }
    }

}
