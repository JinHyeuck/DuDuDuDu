using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public static class DiceMetaDataProvider
    {
        private const float GlobalDamageBalanceMultiplier = 0.5f;
        private const float KingDiceDamageMultiplier = 2f;

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

            if (meta.baseGoldCost <= 0 && meta.goldCostPerLevel <= 0 && meta.baseScrollCost <= 0 && meta.scrollCostPerLevel <= 0)
            {
                if (TryGetFallbackUpgradeCost(diceType, currentLevel, out var fallbackCost))
                    return fallbackCost;
            }

            int level = Mathf.Max(1, currentLevel);
            int gold = Mathf.Max(0, meta.baseGoldCost + (level - 1) * meta.goldCostPerLevel);
            int scroll = Mathf.Max(0, meta.baseScrollCost + (level - 1) * meta.scrollCostPerLevel);
            return (gold, scroll);
        }

        public static Color GetColor(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.color.a > 0f)
                return meta.color;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.color;
            }

            return Color.white;
        }

        public static Sprite GetIcon(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.icon != null)
                return meta.icon;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.icon;
            }

            return null;
        }

        public static Sprite GetProjectileSprite(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.projectileSprite != null)
                return meta.projectileSprite;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.projectileSprite;
            }

            return null;
        }

        public static BulletEffect GetPrimaryEffect(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.primaryEffect != null)
                return meta.primaryEffect;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.primaryEffect;
            }

            return null;
        }

        public static List<BulletEffect> GetEffectPrefabs(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.effectPrefabs != null && meta.effectPrefabs.Count > 0)
                return meta.effectPrefabs;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.effectPrefabs;
            }

            return null;
        }

        public static bool IsMythic(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            return meta != null && meta.isMythic;
        }

        public static bool IsSummonable(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return true;
            return meta.summonable && !meta.isMythic;
        }

        public static bool CanMerge(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return true;
            return meta.canMerge;
        }

        public static bool ShowStarUI(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return true;
            return meta.showStarUI;
        }

        public static IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> GetRecipeMaterials(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta == null || meta.recipeMaterials == null)
                return null;
            return meta.recipeMaterials;
        }

        public static List<DiceType> GetMythicTypes()
        {
            return new List<DiceType>
            {
                DiceType.Tornado,
                DiceType.Paralysis,
                DiceType.ArmorBreak,
                DiceType.Wind,
                DiceType.Time,
                DiceType.KingNormal,
                DiceType.KingFire,
                DiceType.KingIce,
                DiceType.KingPoison,
                DiceType.KingThunder,
                DiceType.KingMixed
            };
        }

        public static DiceType GetBaseElementType(DiceType diceType)
        {
            switch (diceType)
            {
                case DiceType.KingNormal:
                    return DiceType.Normal;
                case DiceType.KingFire:
                    return DiceType.Fire;
                case DiceType.KingIce:
                    return DiceType.Ice;
                case DiceType.KingPoison:
                    return DiceType.Poison;
                case DiceType.KingThunder:
                    return DiceType.Thunder;
                case DiceType.KingMixed:
                    return DiceType.Normal;
                case DiceType.Tornado:
                    return DiceType.Normal;
                case DiceType.Paralysis:
                    return DiceType.Thunder;
                case DiceType.ArmorBreak:
                    return DiceType.Fire;
                case DiceType.Wind:
                    return DiceType.Ice;
                case DiceType.Time:
                    return DiceType.Normal;
                default:
                    return diceType;
            }
        }

        public static int CalculateDamage(DiceType diceType, int dicePip, int bulletLevel)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return 0;

            int pips = Mathf.Max(1, dicePip);
            int level = Mathf.Max(1, bulletLevel);

            float attackBase = meta.baseAttack + (level * meta.levelUpAttackIncrease);
            if (EquipmentManager.Instance != null)
                attackBase += EquipmentManager.Instance.GetTotalEquipmentAttack();

            float scaled = attackBase * (pips * Mathf.Max(0.01f, meta.dicePipAttackFactor)) * GlobalDamageBalanceMultiplier;
            if (IsKingDice(diceType))
                scaled *= KingDiceDamageMultiplier;

            if (EquipmentManager.Instance != null)
            {
                float attackPercent = EquipmentManager.Instance.GetAttackPercentBonus(diceType);
                int attackFlat = EquipmentManager.Instance.GetAttackFlatBonus(diceType);

                int currentWave = 0;
                if (GameManager.Instance != null)
                    currentWave = GameManager.Instance.CurrentWaveIndex;
                int earlyWaveFlat = EquipmentManager.Instance.GetFirstNWavesDamageFlatBonus(diceType, currentWave);
                float finalDamagePercent = EquipmentManager.Instance.GetFinalDamagePercentBonus(diceType);

                scaled *= (1f + attackPercent);
                scaled += attackFlat + earlyWaveFlat;
                scaled *= (1f + finalDamagePercent);
            }

            return Mathf.Max(1, Mathf.RoundToInt(scaled));
        }

        private static bool IsKingDice(DiceType diceType)
        {
            switch (diceType)
            {
                case DiceType.KingNormal:
                case DiceType.KingFire:
                case DiceType.KingIce:
                case DiceType.KingThunder:
                case DiceType.KingPoison:
                case DiceType.KingMixed:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetFallbackUpgradeCost(DiceType diceType, int currentLevel, out (int goldCost, int scrollCost) cost)
        {
            switch (diceType)
            {
                case DiceType.KingNormal:
                    cost = BuildUpgradeCost(currentLevel, 260, 90, 15, 3);
                    return true;
                case DiceType.KingFire:
                    cost = BuildUpgradeCost(currentLevel, 270, 92, 16, 3);
                    return true;
                case DiceType.KingIce:
                    cost = BuildUpgradeCost(currentLevel, 255, 88, 15, 3);
                    return true;
                case DiceType.KingThunder:
                    cost = BuildUpgradeCost(currentLevel, 280, 96, 16, 3);
                    return true;
                case DiceType.KingPoison:
                    cost = BuildUpgradeCost(currentLevel, 250, 86, 15, 3);
                    return true;
                case DiceType.KingMixed:
                    cost = BuildUpgradeCost(currentLevel, 340, 120, 24, 4);
                    return true;
                default:
                    cost = (0, 0);
                    return false;
            }
        }

        private static (int goldCost, int scrollCost) BuildUpgradeCost(int currentLevel, int baseGold, int goldPerLevel, int baseScroll, int scrollPerLevel)
        {
            int level = Mathf.Max(1, currentLevel);
            int gold = Mathf.Max(0, baseGold + (level - 1) * goldPerLevel);
            int scroll = Mathf.Max(0, baseScroll + (level - 1) * scrollPerLevel);
            return (gold, scroll);
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
            float cooldown = baseCooldown * Mathf.Pow(1.2f, star - 1) * CooldownBalanceMultiplier;

            if (EquipmentManager.Instance != null)
            {
                float reducePercent = EquipmentManager.Instance.GetCooldownReductionPercent(diceType);
                cooldown *= Mathf.Max(0.05f, 1f - reducePercent);
            }

            return cooldown;
        }

        private static void EnsureDefaults()
        {
            if (defaults != null)
                return;

            defaults = new Dictionary<DiceType, DiceMetaDataDatabase.DiceMeta>
            {
                { DiceType.Normal, CreateDefault(DiceType.Normal, "Normal Dice", "단일 타격형. 적중 시 기본 이펙트, 안정적인 1대1 화력.", 12, 3, 1.20f, 120, 50, 8, 2, 2.4f, new []{
                    (6, "다이스 눈금당 추가 공격력 +30%"),
                    (13, "대미지 2배")
                }) },
                { DiceType.Fire, CreateDefault(DiceType.Fire, "Fire Dice", "폭발 범위형. 타격 지점 주변 최대 10명 추가 타격.", 10, 4, 1.10f, 140, 60, 10, 2, 3.1f, new []{
                    (3, "범위 50% 증가")
                }) },
                { DiceType.Ice, CreateDefault(DiceType.Ice, "Ice Dice", "감속 제어형. 적중 시 감속 부여.", 9, 3, 1.00f, 130, 55, 9, 2, 3.8f, new []{
                    (8, "일정 확률로 1초 빙결")
                }) },
                { DiceType.Poison, CreateDefault(DiceType.Poison, "Poison Dice", "지속 피해형. 적중 시 중독 부여.", 8, 2, 0.95f, 125, 50, 9, 2, 3.4f, new []{
                    (9, "타격 시 적 방어력 20% 감소")
                }) },
                { DiceType.Thunder, CreateDefault(DiceType.Thunder, "Thunder Dice", "연쇄 타격형. 기본 2명(장비 보너스 적용)에게 체인 공격.", 11, 3, 1.15f, 150, 65, 11, 2, 2.7f, new []{
                    (5, "추가 대상 1명 탐색 후 연쇄 타격")
                }) },
                { DiceType.Tornado, CreateDefault(DiceType.Tornado, "Tornado Dice", "회오리형. 적중 지점 주변 적을 중심으로 끌어당김.", 9, 3, 1.05f, 145, 62, 10, 2, 3.0f, new []{
                    (7, "끌어당김 강도 증가")
                }, new [] { ElementType.Normal, ElementType.Dark, ElementType.Water },
                    false, false, false, (DiceType.Normal, 2, 1), (DiceType.Ice, 1, 2), (DiceType.Poison, 1, 1)) },
                { DiceType.Paralysis, CreateDefault(DiceType.Paralysis, "Paralysis Dice", "제어형. 적중 대상을 잠시 마비시킴.", 8, 2, 1.00f, 140, 58, 10, 2, 3.5f, new []{
                    (6, "마비 지속시간 증가")
                }, new [] { ElementType.Dark, ElementType.Water },
                    false, false, false, (DiceType.Thunder, 2, 1), (DiceType.Poison, 1, 1), (DiceType.Ice, 1, 1)) },
                { DiceType.ArmorBreak, CreateDefault(DiceType.ArmorBreak, "Armor Break Dice", "약화형. 적중 대상 방어력을 일정 시간 감소.", 10, 3, 1.05f, 150, 64, 11, 2, 3.2f, new []{
                    (8, "방어력 감소량 증가")
                }, new [] { ElementType.Dark, ElementType.Fire },
                    false, false, false, (DiceType.Fire, 2, 1), (DiceType.Poison, 2, 1)) },
                { DiceType.Wind, CreateDefault(DiceType.Wind, "Wind Dice", "관통형 바람. 전방 박스 범위 적을 밀어냄.", 7, 2, 0.95f, 135, 56, 9, 2, 2.9f, new []{
                    (5, "바람 지속시간 증가")
                }, new [] { ElementType.Water, ElementType.Light },
                    false, false, false, (DiceType.Ice, 2, 1), (DiceType.Thunder, 1, 1), (DiceType.Normal, 1, 1)) },
                { DiceType.Time, CreateDefault(DiceType.Time, "Time Dice", "지원형. 공격 대신 다른 다이스 쿨타임 감소.", 1, 0, 0.10f, 170, 70, 12, 3, 4.0f, new []{
                    (9, "추가 쿨타임 감소 +1초")
                }, new [] { ElementType.Light, ElementType.Normal },
                    false, false, false, (DiceType.Normal, 2, 1), (DiceType.Thunder, 2, 1), (DiceType.Ice, 2, 1)) },
                { DiceType.KingNormal, CreateMythicDefault(DiceType.KingNormal, "King Normal", "강화 단일형. 주변 3명 추가 타격(반경 1.3).", 104, 20, 1.38f, 3.0f,
                    (DiceType.Normal, 3, 1), (DiceType.Normal, 4, 1), (DiceType.Tornado, 1, 2)) },
                { DiceType.KingFire, CreateMythicDefault(DiceType.KingFire, "King Fire", "강화 폭발형. 범위 1.6, 최대 14명(+보너스+2) 추가 타격.", 110, 22, 1.34f, 3.3f,
                    (DiceType.Fire, 3, 1), (DiceType.Fire, 4, 1), (DiceType.ArmorBreak, 1, 2)) },
                { DiceType.KingIce, CreateMythicDefault(DiceType.KingIce, "King Ice", "강화 제어형. 주변 3명 추가 타격, 감속 2중첩.", 100, 20, 1.30f, 3.5f,
                    (DiceType.Ice, 3, 1), (DiceType.Ice, 4, 1), (DiceType.Wind, 1, 2), (DiceType.Paralysis, 1, 2)) },
                { DiceType.KingPoison, CreateMythicDefault(DiceType.KingPoison, "King Poison", "강화 중독형. 주변 2명 추가 타격, 중독+감속 동시 부여.", 98, 20, 1.28f, 3.3f,
                    (DiceType.Poison, 3, 1), (DiceType.Poison, 4, 1), (DiceType.ArmorBreak, 1, 2), (DiceType.Paralysis, 1, 1)) },
                { DiceType.KingThunder, CreateMythicDefault(DiceType.KingThunder, "King Thunder", "강화 연쇄형. 기본 체인 대상 +2.", 116, 24, 1.38f, 3.0f,
                    (DiceType.Thunder, 3, 1), (DiceType.Thunder, 4, 1), (DiceType.Paralysis, 1, 3), (DiceType.Time, 1, 2)) },
                { DiceType.KingMixed, CreateMythicDefault(DiceType.KingMixed, "King Mixed", "복합 원소형. 체인+범위 확산 타격, 적중 시 감속+중독, 5속성 이펙트 동시 발동.", 136, 28, 1.45f, 3.6f,
                    (DiceType.KingNormal, 1, 1), (DiceType.KingFire, 1, 1), (DiceType.KingIce, 1, 1), (DiceType.KingPoison, 1, 1), (DiceType.KingThunder, 1, 1)) }
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
            (int level, string desc)[] milestones,
            ElementType[] elementTypes = null,
            bool summonable = true,
            bool canMerge = true,
            bool showStarUI = true,
            params (DiceType type, int star, int count)[] recipe)
        {
            var meta = new DiceMetaDataDatabase.DiceMeta
            {
                diceType = diceType,
                elementType = elementTypes ?? new ElementType[0],
                displayName = displayName,
                description = description,
                summonable = summonable,
                canMerge = canMerge,
                showStarUI = showStarUI,
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

            if (recipe != null)
            {
                for (int i = 0; i < recipe.Length; i++)
                {
                    meta.recipeMaterials.Add(new DiceMetaDataDatabase.DiceRecipeMaterial
                    {
                        diceType = recipe[i].type,
                        star = Mathf.Max(1, recipe[i].star),
                        count = Mathf.Max(1, recipe[i].count)
                    });
                }
            }

            return meta;
        }

        private static DiceMetaDataDatabase.DiceMeta CreateMythicDefault(
            DiceType diceType,
            string displayName,
            string description,
            int baseAttack,
            int levelUpAttackIncrease,
            float dicePipAttackFactor,
            float baseCooldown,
            params (DiceType type, int star, int count)[] recipe)
        {
            var meta = new DiceMetaDataDatabase.DiceMeta
            {
                diceType = diceType,
                displayName = displayName,
                description = description,
                baseAttack = baseAttack,
                levelUpAttackIncrease = levelUpAttackIncrease,
                dicePipAttackFactor = dicePipAttackFactor,
                baseGoldCost = 0,
                goldCostPerLevel = 0,
                baseScrollCost = 0,
                scrollCostPerLevel = 0,
                baseCooldown = baseCooldown,
                isMythic = true,
                summonable = false,
                canMerge = false,
                showStarUI = false
            };

            for (int i = 0; i < recipe.Length; i++)
            {
                meta.recipeMaterials.Add(new DiceMetaDataDatabase.DiceRecipeMaterial
                {
                    diceType = recipe[i].type,
                    star = Mathf.Max(1, recipe[i].star),
                    count = Mathf.Max(1, recipe[i].count)
                });
            }

            return meta;
        }
    }
}
