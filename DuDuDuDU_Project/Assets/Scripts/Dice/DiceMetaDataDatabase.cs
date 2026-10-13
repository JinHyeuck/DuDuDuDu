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

            /// <summary>
            /// 성급별 아이콘. [0] 이 1성이다. 비어 있거나 그 성급 칸이 없으면 <see cref="icon"/> 을 쓴다.
            /// 기본 다이스(일반·불·얼음·번개·독)가 머지로 성급이 오르면 그림이 바뀐다
            /// (Ui_Merge_{속성}_1~4). 예전에는 그림 하나에 "x4" 숫자만 올렸다.
            /// </summary>
            public Sprite[] starIcons = new Sprite[0];

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

            /// <summary>
            /// <b>다이스 공격력은 정수가 아니라 장비 총 공격력에 곱하는 비율이다.</b>
            ///
            /// 예전 <c>baseAttack</c> / <c>levelUpAttackIncrease</c>(둘 다 int)를 대신한다.
            /// 그때는 <c>다이스 정수 + 장비 정수</c> 였고, 장비가 자라면 다이스 항이 묻혀서
            /// 레벨 12 와 레벨 1 의 차이가 후반에 4% 로 줄었다. 지금은
            /// <c>장비 총 공격력 × (base + (레벨-1) × perLevel)</c> 이라 비율이 어느 시점에도 같다.
            /// 산술은 <c>OJ.Core.DamageFormula</c> 에 있다.
            ///
            /// 기준점은 <b>노말 다이스 1레벨 = 1(100%)</b> 이고, 나머지는 그보다 넓게/길게
            /// 때리는 만큼 깎는다(얼음 0.7 / 번개 0.4 처럼). 진화 단계는 하위 다이스의
            /// ×2.5(레어) · ×5(신화)로 둔다 — <c>DiceMetaDataProvider</c> 의 코드 기본값 표에
            /// 그 사다리가 적혀 있고, 이 에셋이 정본이다.
            ///
            /// <b>음수를 넣지 말 것.</b> DamageFormula 가 0 으로 자르지만, 자른다는 것은
            /// 에셋의 실수가 "데미지 1" 로 조용히 흡수된다는 뜻이다.
            /// </summary>
            [Header("Damage (장비 공격력 대비 비율)")]
            [Tooltip("레벨 1 에서 장비 총 공격력에 곱하는 비율. 1 = 100%")]
            public float baseAttackPercent = 1f;

            [Tooltip("레벨 한 칸당 더해지는 비율. 0.1 = 레벨마다 +10%p")]
            public float attackPercentPerLevel = 0.1f;

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
