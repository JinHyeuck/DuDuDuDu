using System;
using System.Collections.Generic;
using UnityEngine;
using OJ.Hunting;

namespace OJ.Dice
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
        public class DiceMeta
        {
            public DiceType diceType;
            public ElementType[] elementType = new ElementType[0];
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

            // 조합식(recipeMaterials)은 진화 개편에서 사라졌다. 상위 다이스는 이제 재료를
            // 모아 만드는 것이 아니라 하위 다이스 하나가 재화를 내고 올라간다 —
            // 그 배선은 데이터가 아니라 OJ.Dice.DiceEvolution 의 표에 있다.
            // 에셋 YAML 에 남은 recipeMaterials 키는 Unity 가 다음 저장 때 떨군다.

            [Header("Damage")]
            public int baseAttack;
            public int levelUpAttackIncrease;

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
