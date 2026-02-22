using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public static class BulletMetaDataProvider
    {
        private static BulletMetaDataDatabase database;
        private static Dictionary<DiceType, BulletMetaDataDatabase.BulletMeta> defaults;

        public static BulletMetaDataDatabase Database
        {
            get
            {
                if (database == null)
                {
                    if (StaticResource.Instance != null && StaticResource.Instance.BulletMetaDataDatabase != null)
                        database = StaticResource.Instance.BulletMetaDataDatabase;
                }

                if (database == null)
                    database = Resources.Load<BulletMetaDataDatabase>("BulletMetaDataDatabase");

                return database;
            }
        }

        public static BulletMetaDataDatabase.BulletMeta GetMeta(DiceType diceType)
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

        private static void EnsureDefaults()
        {
            if (defaults != null)
                return;

            defaults = new Dictionary<DiceType, BulletMetaDataDatabase.BulletMeta>
            {
                { DiceType.Normal, CreateDefault(DiceType.Normal, "Normal Bullet", "균형형 단일 공격 탄환", 12, 3, 1.20f, 120, 50, 8, 2, new []{
                    (6, "다이스 눈금당 추가 공격력 +30%"),
                    (13, "대미지 2배")
                }) },
                { DiceType.Fire, CreateDefault(DiceType.Fire, "Fire Bullet", "폭발형 범위 공격 탄환", 10, 4, 1.10f, 140, 60, 10, 2, new []{
                    (3, "범위 50% 증가")
                }) },
                { DiceType.Ice, CreateDefault(DiceType.Ice, "Ice Bullet", "감속/제어 특화 탄환", 9, 3, 1.00f, 130, 55, 9, 2, new []{
                    (8, "일정 확률로 1초 빙결")
                }) },
                { DiceType.Poison, CreateDefault(DiceType.Poison, "Poison Bullet", "지속 피해/약화 탄환", 8, 2, 0.95f, 125, 50, 9, 2, new []{
                    (9, "타격 시 적 방어력 20% 감소")
                }) },
                { DiceType.Thunder, CreateDefault(DiceType.Thunder, "Thunder Bullet", "연쇄 타격 특화 탄환", 11, 3, 1.15f, 150, 65, 11, 2, new []{
                    (5, "추가 대상 1명 탐색 후 연쇄 타격")
                }) }
            };
        }

        private static BulletMetaDataDatabase.BulletMeta CreateDefault(
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
            (int level, string desc)[] milestones)
        {
            var meta = new BulletMetaDataDatabase.BulletMeta
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
                scrollCostPerLevel = scrollCostPerLevel
            };

            for (int i = 0; i < milestones.Length; i++)
            {
                meta.milestones.Add(new BulletMetaDataDatabase.BulletLevelMilestone
                {
                    level = milestones[i].level,
                    description = milestones[i].desc
                });
            }

            return meta;
        }
    }
}
