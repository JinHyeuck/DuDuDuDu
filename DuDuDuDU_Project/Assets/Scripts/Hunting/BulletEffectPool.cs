using UnityEngine;
using System.Collections.Generic;

namespace OJ
{
    public class BulletEffectPool : MonoSingleton<BulletEffectPool>
    {
        public int poolSize = 5;
        private Dictionary<DiceType, Queue<BulletEffect>> effectpool = new Dictionary<DiceType, Queue<BulletEffect>>();

        protected override void Init()
        {
            for (int dicetype = DiceType.Normal.Enum32ToInt(); dicetype < DiceType.Max.Enum32ToInt(); ++dicetype)
            {
                DiceType diceType = dicetype.IntToEnum32<DiceType>();
                if (effectpool.ContainsKey(diceType) == false)
                    effectpool.Add(diceType, new Queue<BulletEffect>());

                for (int i = 0; i < poolSize; i++)
                {
                    BulletEffect bulletEffectObj = StaticResource.Instance.DiceTypeResourceManager.GetBulletEffect(diceType);
                    GameObject obj = Instantiate(bulletEffectObj.gameObject, transform);
                    obj.SetActive(false);

                    effectpool[diceType].Enqueue(obj.GetComponent<BulletEffect>());
                }
            }
        }

        public BulletEffect GetBullet(DiceType diceType)
        {
            Queue<BulletEffect> pool = effectpool[diceType];

            if (pool.Count > 0)
            {
                BulletEffect queuebullet = pool.Dequeue();
                queuebullet.gameObject.SetActive(true);
                return queuebullet;
            }

            BulletEffect bulletEffectObj = StaticResource.Instance.DiceTypeResourceManager.GetBulletEffect(diceType);
            GameObject obj = Instantiate(bulletEffectObj.gameObject, transform);
            return obj.GetComponent<BulletEffect>();
        }

        public void PoolBullet(BulletEffect bullet)
        {
            Queue<BulletEffect> pool = effectpool[bullet.myDiceType];

            bullet.gameObject.SetActive(false);
            pool.Enqueue(bullet);
        }
    }

}
