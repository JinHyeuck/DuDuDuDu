using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    [CreateAssetMenu(fileName = "DiceMetaDataDatabase", menuName = "Dice/MetaData Database")]
    public class DiceMetaDataDatabase : ScriptableObject
    {
        [Serializable]
        public class DiceLevelMilestone
        {
            public int level;
            [TextArea(2, 4)] public string description;
        }

        [Serializable]
        public class DiceRecipeMaterial
        {
            public DiceType diceType;
            public int star = 1;
            public int count = 1;
        }

        [Serializable]
        public class DiceMeta
        {
            public DiceType diceType;
            public string displayName;
            [TextArea(2, 4)] public string description;
            public Sprite icon;
            public Color color = Color.white;
            public Sprite projectileSprite;
            public BulletEffect primaryEffect;
            public List<BulletEffect> effectPrefabs = new List<BulletEffect>();
            public bool isMythic;
            public bool summonable = true;
            public bool canMerge = true;
            public bool showStarUI = true;
            public List<DiceRecipeMaterial> recipeMaterials = new List<DiceRecipeMaterial>();

            [Header("Damage")]
            public int baseAttack;
            public int levelUpAttackIncrease;
            public float dicePipAttackFactor;

            [Header("Combat")]
            public float baseCooldown = 3f;

            [Header("Upgrade Cost")]
            public int baseGoldCost = 100;
            public int goldCostPerLevel = 40;
            public int baseScrollCost = 5;
            public int scrollCostPerLevel = 1;

            [Header("Milestone Effects")]
            public List<DiceLevelMilestone> milestones = new List<DiceLevelMilestone>();
        }

        [SerializeField] private List<DiceMeta> metas = new List<DiceMeta>();
        private readonly Dictionary<DiceType, DiceMeta> metaMap = new Dictionary<DiceType, DiceMeta>();

        private void OnEnable()
        {
            RebuildMap();
        }

        public DiceMeta Get(DiceType diceType)
        {
            if (metaMap.Count != metas.Count)
                RebuildMap();

            metaMap.TryGetValue(diceType, out DiceMeta meta);
            return meta;
        }

        public bool TryGet(DiceType diceType, out DiceMeta meta)
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
                DiceMeta meta = metas[i];
                if (meta == null)
                    continue;
                metaMap[meta.diceType] = meta;
            }
        }
    }
}
