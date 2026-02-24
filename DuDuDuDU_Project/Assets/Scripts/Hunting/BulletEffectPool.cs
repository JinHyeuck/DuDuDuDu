using UnityEngine;
using System.Collections.Generic;

namespace OJ
{
    public class BulletEffectPool : MonoBehaviour
    {
        public static BulletEffectPool Instance;

        public int poolSize = 5;
        private Dictionary<DiceType, Dictionary<EffectID, Queue<BulletEffect>>> effectpool = new Dictionary<DiceType, Dictionary<EffectID, Queue<BulletEffect>>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            InitializePool();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void InitializePool()
        {
            effectpool.Clear();

            for (int dicetype = DiceType.Normal.Enum32ToInt(); dicetype < DiceType.Max.Enum32ToInt(); ++dicetype)
            {
                DiceType diceType = dicetype.IntToEnum32<DiceType>();
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
                        GameObject obj = Instantiate(bulletEffectObj.gameObject, transform);
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
