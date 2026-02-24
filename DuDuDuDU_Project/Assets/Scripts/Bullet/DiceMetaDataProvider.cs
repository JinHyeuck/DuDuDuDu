using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public static class DiceMetaDataProvider
    {
        private const float CooldownBalanceMultiplier = 2f;
        private static DiceMetaDataDatabase database;
        private static Dictionary<DiceType, DiceMetaDataDatabase.DiceMeta> defaults;

        public static DiceMetaDataDatabase Database
        {
            get
            {
                if (database == null)
                {
                    if (StaticResource.Instance != null && StaticResource.Instance.DiceMetaDataDatabase != null)
                        database = StaticResource.Instance.DiceMetaDataDatabase;
                }

                if (database == null)
                    database = Resources.Load<DiceMetaDataDatabase>("DiceMetaDataDatabase");

                return database;
            }
        }

        public static DiceMetaDataDatabase.DiceMeta GetMeta(DiceType diceType)
        {
            if (Database != null && Database.TryGet(diceType, out var meta))
                return meta;

            EnsureDefaults();
            defaults.TryGetValue(diceType, out var fallback);
            return fallback;
        }

        public static (int goldCost, int scrollCost) GetUpgradeCost(DiceType diceType, int currentLevel)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return (0, 0);

            int level = Mathf.Max(1, currentLevel);
            int gold = Mathf.Max(0, meta.baseGoldCost + (level - 1) * meta.goldCostPerLevel);
            int scroll = Mathf.Max(0, meta.baseScrollCost + (level - 1) * meta.scrollCostPerLevel);
            return (gold, scroll);
        }

        public static Color GetColor(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            return meta != null ? meta.color : Color.white;
        }

        public static Sprite GetIcon(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            return meta != null ? meta.icon : null;
        }

        public static Sprite GetProjectileSprite(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            return meta != null ? meta.projectileSprite : null;
        }

        public static BulletEffect GetPrimaryEffect(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            return meta != null ? meta.primaryEffect : null;
        }

        public static List<BulletEffect> GetEffectPrefabs(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.effectPrefabs != null)
                return meta.effectPrefabs;

            return null;
        }

        public static int CalculateDamage(DiceType diceType, int dicePip, int bulletLevel)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return 0;

            int pips = Mathf.Max(1, dicePip);
            int level = Mathf.Max(1, bulletLevel);

            float attackBase = meta.baseAttack + (level * meta.levelUpAttackIncrease);
            float scaled = attackBase * (pips * Mathf.Max(0.01f, meta.dicePipAttackFactor));
            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

        public static float GetBaseCooldown(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.baseCooldown > 0f)
                return meta.baseCooldown;

            return 3f;
        }

        public static float GetCooldown(DiceType diceType, int diceStar)
        {
            float baseCooldown = Mathf.Clamp(GetBaseCooldown(diceType), 0.1f, 10f);
            int star = Mathf.Max(1, diceStar);
            return baseCooldown * Mathf.Pow(1.2f, star - 1) * CooldownBalanceMultiplier;
        }

        private static void EnsureDefaults()
        {
            if (defaults != null)
                return;

            defaults = new Dictionary<DiceType, DiceMetaDataDatabase.DiceMeta>
            {
                { DiceType.Normal, CreateDefault(DiceType.Normal, "Normal Dice", "균형형 단일 공격 탄환", 12, 3, 1.20f, 120, 50, 8, 2, 2.4f, new []{
                    (6, "다이스 눈금당 추가 공격력 +30%"),
                    (13, "대미지 2배")
                }) },
                { DiceType.Fire, CreateDefault(DiceType.Fire, "Fire Dice", "폭발형 범위 공격 탄환", 10, 4, 1.10f, 140, 60, 10, 2, 3.1f, new []{
                    (3, "범위 50% 증가")
                }) },
                { DiceType.Ice, CreateDefault(DiceType.Ice, "Ice Dice", "감속/제어 특화 탄환", 9, 3, 1.00f, 130, 55, 9, 2, 3.8f, new []{
                    (8, "일정 확률로 1초 빙결")
                }) },
                { DiceType.Poison, CreateDefault(DiceType.Poison, "Poison Dice", "지속 피해/약화 탄환", 8, 2, 0.95f, 125, 50, 9, 2, 3.4f, new []{
                    (9, "타격 시 적 방어력 20% 감소")
                }) },
                { DiceType.Thunder, CreateDefault(DiceType.Thunder, "Thunder Dice", "연쇄 타격 특화 탄환", 11, 3, 1.15f, 150, 65, 11, 2, 2.7f, new []{
                    (5, "추가 대상 1명 탐색 후 연쇄 타격")
                }) }
            };
        }

        private static DiceMetaDataDatabase.DiceMeta CreateDefault(
            DiceType diceType,
            string displayName,
            string description,
            int baseAttack,
            int levelUpAttackIncrease,
            float dicePipAttackFactor,
            int baseGoldCost,
            int goldCostPerLevel,
            int baseScrollCost,
            int scrollCostPerLevel,
            float baseCooldown,
            (int level, string desc)[] milestones)
        {
            var meta = new DiceMetaDataDatabase.DiceMeta
            {
                diceType = diceType,
                displayName = displayName,
                description = description,
                baseAttack = baseAttack,
                levelUpAttackIncrease = levelUpAttackIncrease,
                dicePipAttackFactor = dicePipAttackFactor,
                baseGoldCost = baseGoldCost,
                goldCostPerLevel = goldCostPerLevel,
                baseScrollCost = baseScrollCost,
                scrollCostPerLevel = scrollCostPerLevel,
                baseCooldown = baseCooldown
            };

            for (int i = 0; i < milestones.Length; i++)
            {
                meta.milestones.Add(new DiceMetaDataDatabase.DiceLevelMilestone
                {
                    level = milestones[i].level,
                    description = milestones[i].desc
                });
            }

            return meta;
        }
    }
}
