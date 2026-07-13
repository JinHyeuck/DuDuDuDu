using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    [Serializable]
    public class RelicRarityWeight
    {
        public Rarity rarity;
        [Min(0f)] public float weight;
    }

    [CreateAssetMenu(fileName = "RelicDatabase", menuName = "Relic/Database")]
    public class RelicDatabase : ScriptableObject
    {
        [Header("Growth")]
        [Min(1)] public int maxLevel = 20;

        [Header("Summon Cost")]
        [Min(0)] public int baseGoldCost = 500;
        [Min(0)] public int goldCostPerSummon = 100;
        [Min(0)] public int baseTicketCost = 1;
        [Min(1)] public int ticketCostIncreaseInterval = 10;
        [Min(0)] public int ticketCostIncreaseAmount = 1;

        [Header("Summon Rate")]
        public List<RelicRarityWeight> rarityWeights = new List<RelicRarityWeight>();

        [Header("Rarity Background")]
        public Sprite normalBackground;
        public Sprite rareBackground;
        public Sprite epicBackground;
        public Sprite mythicBackground;

        [Header("Relics")]
        public List<RelicDefinition> relics = new List<RelicDefinition>();

        private readonly Dictionary<RelicId, RelicDefinition> relicMap = new Dictionary<RelicId, RelicDefinition>();

        private void OnEnable()
        {
            RebuildMap();
        }

        public RelicDefinition Get(RelicId relicId)
        {
            if (relicMap.Count != relics.Count)
                RebuildMap();

            relicMap.TryGetValue(relicId, out RelicDefinition definition);
            return definition;
        }

        public bool TryGet(RelicId relicId, out RelicDefinition definition)
        {
            if (relicMap.Count != relics.Count)
                RebuildMap();

            return relicMap.TryGetValue(relicId, out definition);
        }

        public RelicSummonCost GetSummonCost(int summonCount)
        {
            int count = Mathf.Max(0, summonCount);
            int gold = Mathf.Max(0, baseGoldCost + goldCostPerSummon * count);
            int ticket = Mathf.Max(0, baseTicketCost);

            if (ticketCostIncreaseInterval > 0 && ticketCostIncreaseAmount > 0)
                ticket += (count / ticketCostIncreaseInterval) * ticketCostIncreaseAmount;

            return new RelicSummonCost(gold, ticket);
        }

        public Sprite GetBackground(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare:
                    return rareBackground;
                case Rarity.Epic:
                    return epicBackground;
                case Rarity.Mythic:
                    return mythicBackground;
                default:
                    return normalBackground;
            }
        }

        private void RebuildMap()
        {
            relicMap.Clear();
            for (int i = 0; i < relics.Count; i++)
            {
                RelicDefinition definition = relics[i];
                if (definition == null || definition.relicId == RelicId.None)
                    continue;

                relicMap[definition.relicId] = definition;
            }
        }

        public static RelicDatabase CreateRuntimeDefault()
        {
            RelicDatabase database = CreateInstance<RelicDatabase>();
            database.name = "RuntimeRelicDatabase";
            database.maxLevel = 20;
            database.baseGoldCost = 500;
            database.goldCostPerSummon = 100;
            database.baseTicketCost = 1;
            database.ticketCostIncreaseInterval = 10;
            database.ticketCostIncreaseAmount = 1;
            database.rarityWeights = CreateDefaultWeights();
            database.normalBackground = LoadRelicSprite("Passive_Normal");
            database.rareBackground = LoadRelicSprite("Passive_Rare");
            database.epicBackground = LoadRelicSprite("Passive_Epic");
            database.mythicBackground = LoadRelicSprite("Passive_Mystic");
            database.relics = CreateDefaultRelics();
            database.RebuildMap();
            return database;
        }

        public static List<RelicRarityWeight> CreateDefaultWeights()
        {
            return new List<RelicRarityWeight>
            {
                new RelicRarityWeight { rarity = Rarity.Normal, weight = 60f },
                new RelicRarityWeight { rarity = Rarity.Rare, weight = 30f },
                new RelicRarityWeight { rarity = Rarity.Epic, weight = 8f },
                new RelicRarityWeight { rarity = Rarity.Mythic, weight = 2f },
            };
        }

        public static List<RelicDefinition> CreateDefaultRelics()
        {
            return new List<RelicDefinition>
            {
                Create(RelicId.BeginnerPouch, 1, Rarity.Normal, "초심자의 주머니", "전투 시작 SP +{0}", "기본 시작 SP가 100이면 110으로 시작", 10f, 2f),
                Create(RelicId.BattleVault, 2, Rarity.Normal, "전투 금고", "웨이브 클리어 시 {0}% 확률로 강화 코인 +1", "강화 코인 기본 획득에 추가로 1개를 더 얻을 수 있음", 20f, 1f),
                Create(RelicId.LuckyMagazine, 3, Rarity.Normal, "행운 탄창", "공격 시 {0}% 확률로 이번 공격 최종 피해 +{1}%", "100 피해를 줄 공격이 발동하면 180 피해", 10f, 0.5f, 80f, 4f),
                Create(RelicId.RepairHammer, 4, Rarity.Normal, "수리 망치", "웨이브 종료 시 벽 체력 {0}% 회복", "벽 최대 체력이 100이면 웨이브 종료 시 2 회복", 2f, 0.15f),
                Create(RelicId.QuickHands, 5, Rarity.Normal, "빠른 손목", "모든 주사위 쿨다운 {0}% 감소", "쿨다운 10초 주사위가 9.7초마다 공격", 3f, 0.15f),
                Create(RelicId.LootMap, 6, Rarity.Normal, "전리품 지도", "스테이지 클리어 보상 골드/스크롤 +{0}%", "골드 1,000 보상 클리어 시 1,050 획득", 5f, 0.5f),

                Create(RelicId.EmberJar, 7, Rarity.Rare, "불씨 항아리", "Fire/King Fire 폭발 범위 +{0}%, 폭발 대상 +{1}", "Fire 폭발이 더 넓어지고 맞출 수 있는 적이 증가", 10f, 0.5f, 1f, 0.05f),
                Create(RelicId.FrostNail, 8, Rarity.Rare, "서리못", "Ice로 둔화된 적이 받는 피해 +{0}%", "Ice에 맞아 느려진 적에게 다른 주사위 피해도 증가", 8f, 0.5f),
                Create(RelicId.LightningRodRing, 9, Rarity.Rare, "피뢰침 고리", "Thunder/King Thunder 연쇄 대상 +{0}", "Thunder가 2명에게 튕기던 상황이면 3명에게 튕김", 1f, 0.05f),
                Create(RelicId.PoisonIncense, 10, Rarity.Rare, "독향로", "Poison 상태의 적이 죽으면 주변 적 {0}명에게 독 전이", "독에 걸린 적이 죽으면 가까운 적도 독에 걸림", 1f, 0.05f, 1f, 0.02f),
                Create(RelicId.TornadoAnchor, 11, Rarity.Rare, "회오리 닻", "Tornado로 끌린 적은 {2}초간 받는 피해 +{0}%", "회오리에 끌린 적에게 잠시 동안 모든 피해 증가", 8f, 0.5f, 0f, 0f, 3f, 0.1f),
                Create(RelicId.ParalysisNeedle, 12, Rarity.Rare, "마비 바늘", "Stun 확률 +{0}%, 기절한 적이 받는 피해 +{1}%", "기절 확률이 오르고 기절 중인 적에게 피해 증가", 5f, 0.25f, 10f, 0.5f),
                Create(RelicId.CrackHammer, 13, Rarity.Rare, "균열 망치", "ArmorBreak 방어 감소량 +{0}%p, 지속시간 +{2}초", "방어 감소 30% 효과가 35%가 되고 더 오래 유지", 5f, 0.25f, 0f, 0f, 1f, 0.05f),
                Create(RelicId.TailwindFeather, 14, Rarity.Rare, "순풍 깃털", "Wind 밀치기 확률 +{0}%, 밀린 적이 받는 피해 +{1}%", "Wind로 밀린 적에게 잠시 동안 피해 증가", 8f, 0.4f, 8f, 0.5f),

                Create(RelicId.MergeInsurance, 15, Rarity.Epic, "합성 보험증", "주사위 합성 시 {0}% 확률로 SP +{1} 획득", "주사위 합성 후 발동하면 SP 획득", 10f, 0.5f, 5f, 1f),
                Create(RelicId.TwinSummonStone, 16, Rarity.Epic, "쌍둥이 소환석", "주사위 소환 시 {0}% 확률로 같은 타입 1성 주사위 추가 생성", "Fire를 소환했는데 발동하면 Fire 1성이 하나 더 생성", 8f, 0.4f),
                Create(RelicId.JustOneHit, 17, Rarity.Epic, "딱 한 대", "각 웨이브마다 첫 주사위 공격 최종 피해 +{0}%", "웨이브 첫 공격 100 피해가 130 피해로 증가", 30f, 2f),
                Create(RelicId.LuckyGesture, 18, Rarity.Epic, "행운의 손짓", "주사위 소환 시 {0}% 확률로 2성 주사위 등장", "Normal 1성 대신 Normal 2성이 바로 등장", 5f, 0.25f),
                Create(RelicId.KingBlueprint, 19, Rarity.Epic, "왕의 설계도", "전투 중 첫 신화 주사위 제작 시 SP +{0} 획득", "첫 King Fire 제작에 성공하면 SP 획득", 30f, 3f),
                Create(RelicId.FullBoardPressure, 20, Rarity.Epic, "만석의 압력", "보드가 가득 찬 동안 모든 주사위 최종 피해 +{0}%", "24칸이 모두 차 있으면 모든 주사위 피해 증가", 15f, 0.75f),

                Create(RelicId.AdvanceDeployment, 21, Rarity.Mythic, "선제 배치", "전투 시작 시 랜덤 기본 주사위 {0}성 1개 지급", "전투 시작과 동시에 Fire 2성 하나가 보드에 생성", 2f, 0f, 0f, 3f),
                Create(RelicId.LuckyInvitation, 22, Rarity.Mythic, "행운의 초대장", "주사위 소환 시 {0}% 확률로 이번 소환은 비용 증가 횟수에 포함되지 않음", "소환 비용 10에서 소환했는데 발동하면 다음 비용도 10 유지", 10f, 0.5f),
                Create(RelicId.CrownResonance, 23, Rarity.Mythic, "왕관 공명", "King 계열 신화 주사위가 있으면 같은 속성 기본 주사위 최종 피해 +{0}%", "King Fire가 있으면 Fire 주사위 피해 증가", 20f, 1f),
                Create(RelicId.LastWall, 24, Rarity.Mythic, "최후의 성벽", "벽이 처음 파괴될 때 체력 1로 만들며, 해당 웨이브 동안 모든 주사위 쿨다운 {0}% 감소", "벽이 처음으로 파괴될 상황에서 체력 1로 버티고 그 웨이브가 끝날 때까지 공격이 빨라짐", 25f, 1f),
            };
        }

        private static RelicDefinition Create(
            RelicId id,
            int index,
            Rarity rarity,
            string displayName,
            string description,
            string example,
            float baseValue,
            float levelUpValue,
            float baseSecondaryValue = 0f,
            float levelUpSecondaryValue = 0f,
            float baseDuration = 0f,
            float levelUpDuration = 0f)
        {
            return new RelicDefinition
            {
                relicId = id,
                index = index,
                rarity = rarity,
                displayName = displayName,
                description = description,
                example = example,
                icon = LoadRelicSprite("Relic_" + index),
                baseValue = baseValue,
                levelUpValue = levelUpValue,
                baseSecondaryValue = baseSecondaryValue,
                levelUpSecondaryValue = levelUpSecondaryValue,
                baseDuration = baseDuration,
                levelUpDuration = levelUpDuration,
            };
        }

        private static Sprite LoadRelicSprite(string spriteName)
        {
            return Resources.Load<Sprite>("Art/Relic/" + spriteName);
        }
    }
}
