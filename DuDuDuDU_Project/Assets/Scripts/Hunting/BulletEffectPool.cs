using UnityEngine;
using System.Collections.Generic;

namespace OJ
{
    public class BulletEffectPool : MonoSingleton<BulletEffectPool>
    {
        public int poolSize = 5;
        private Dictionary<DiceType, Dictionary<EffectID, Queue<BulletEffect>>> effectpool = new Dictionary<DiceType, Dictionary<EffectID, Queue<BulletEffect>>>();

        protected override void Init()
        {
            for (int dicetype = DiceType.Normal.Enum32ToInt(); dicetype < DiceType.Max.Enum32ToInt(); ++dicetype)
            {
                DiceType diceType = dicetype.IntToEnum32<DiceType>();
                if (effectpool.ContainsKey(diceType) == false)
                    effectpool.Add(diceType, new Dictionary<EffectID, Queue<BulletEffect>>());

                DiceTypeResourceManager.TypeVisual typeVisual = StaticResource.Instance.DiceTypeResourceManager.GetTypeVisual(diceType);

                for (int effects = 0; effects < typeVisual.bulletEffectDatas.Count; ++effects)
                {
                    BulletEffect bulletEffectObj = typeVisual.bulletEffectDatas[effects];

                    EffectID ID = bulletEffectObj.myEffectType;

                    if (effectpool[diceType].ContainsKey(ID) == true)
                        continue;

                    effectpool[diceType].Add(ID, new Queue<BulletEffect>());

                    for (int i = 0; i < poolSize; i++)
                    {
                        GameObject obj = Instantiate(bulletEffectObj.gameObject, transform);
                        obj.SetActive(false);

                        effectpool[diceType][ID].Enqueue(obj.GetComponent<BulletEffect>());
                    }
                }

            }
        }

        public BulletEffect GetBullet(DiceType diceType, EffectID effectID = EffectID.S)
        {
            Queue<BulletEffect> pool = effectpool[diceType][effectID];

            if (pool.Count > 0)
            {
                BulletEffect queuebullet = pool.Dequeue();
                queuebullet.gameObject.SetActive(true);
                return queuebullet;
            }

            DiceTypeResourceManager.TypeVisual typeVisual = StaticResource.Instance.DiceTypeResourceManager.GetTypeVisual(diceType);

            BulletEffect bulletEffectObj = typeVisual.bulletEffectDatas.Find(x => x.myEffectType == effectID);
            GameObject obj = Instantiate(bulletEffectObj.gameObject, transform);
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
