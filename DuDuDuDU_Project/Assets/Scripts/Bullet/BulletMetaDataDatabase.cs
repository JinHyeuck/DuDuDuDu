using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    [CreateAssetMenu(fileName = "BulletMetaDataDatabase", menuName = "Bullet/MetaData Database")]
    public class BulletMetaDataDatabase : ScriptableObject
    {
        [Serializable]
        public class BulletLevelMilestone
        {
            public int level;
            [TextArea(2, 4)] public string description;
        }

        [Serializable]
        public class BulletMeta
        {
            public DiceType diceType;
            public string displayName;
            [TextArea(2, 4)] public string description;
            public Sprite icon;

            [Header("Damage")]
            public int baseAttack;
            public int levelUpAttackIncrease;
            public float dicePipAttackFactor;

            [Header("Upgrade Cost")]
            public int baseGoldCost = 100;
            public int goldCostPerLevel = 40;
            public int baseScrollCost = 5;
            public int scrollCostPerLevel = 1;

            [Header("Milestone Effects")]
            public List<BulletLevelMilestone> milestones = new List<BulletLevelMilestone>();
        }

        [SerializeField] private List<BulletMeta> metas = new List<BulletMeta>();
        private readonly Dictionary<DiceType, BulletMeta> metaMap = new Dictionary<DiceType, BulletMeta>();

        private void OnEnable()
        {
            RebuildMap();
        }

        public BulletMeta Get(DiceType diceType)
        {
            if (metaMap.Count != metas.Count)
                RebuildMap();

            metaMap.TryGetValue(diceType, out BulletMeta meta);
            return meta;
        }

        public bool TryGet(DiceType diceType, out BulletMeta meta)
        {
            if (metaMap.Count != metas.Count)
                RebuildMap();

            return metaMap.TryGetValue(diceType, out meta);
        }

        private void RebuildMap()
        {
            metaMap.Clear();

            for (int i = 0; i < metas.Count; i++)
            {
                BulletMeta meta = metas[i];
                if (meta == null)
                    continue;
                metaMap[meta.diceType] = meta;
            }
        }
    }
}
