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
        private static readonly Dictionary<DiceType, DiceMetaDataDatabase.DiceMeta> mergedMetaCache = new Dictionary<DiceType, DiceMetaDataDatabase.DiceMeta>();

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
            EnsureDefaults();
            defaults.TryGetValue(diceType, out var fallback);

            if (Database != null && Database.TryGet(diceType, out var meta))
            {
                if (fallback == null)
                    return meta;

                if (!mergedMetaCache.TryGetValue(diceType, out var merged))
                {
                    merged = MergeMeta(meta, fallback);
                    mergedMetaCache[diceType] = merged;
                }

                return merged;
            }

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
                DiceType.Stun,
                DiceType.ArmorBreak,
                DiceType.Wind,
                DiceType.Time,
                DiceType.KingNormal,
                DiceType.KingFire,
                DiceType.KingIce,
                DiceType.KingPoison,
                DiceType.KingThunder
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
                case DiceType.Tornado:
                    return DiceType.Normal;
                case DiceType.Stun:
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

            float scaled = attackBase * pips * GlobalDamageBalanceMultiplier;
            scaled *= GetLevelDamageMultiplier(diceType, level);
            scaled *= GetKingSynergyDamageMultiplier(diceType);
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

            if (ElementUpgradeManager.Instance != null)
                scaled *= ElementUpgradeManager.Instance.GetTotalBonusMultiplier(diceType);

            if (RelicManager.Instance != null)
                scaled *= RelicManager.Instance.GetDamageMultiplier(diceType);

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
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(diceType) : 1;
            cooldown *= GetLevelCooldownMultiplier(diceType, level);

            if (EquipmentManager.Instance != null)
            {
                float reducePercent = EquipmentManager.Instance.GetCooldownReductionPercent(diceType);
                cooldown *= Mathf.Max(0.05f, 1f - reducePercent);
            }

            if (RelicManager.Instance != null)
            {
                float relicReducePercent = RelicManager.Instance.GetCooldownReductionPercent() * 0.01f;
                cooldown *= Mathf.Max(0.05f, 1f - relicReducePercent);
            }

            return cooldown;
        }

        public static float GetLevelDamageMultiplier(DiceType diceType, int level)
        {
            float multiplier = 1f;
            if (level >= 3)
            {
                switch (diceType)
                {
                    case DiceType.Normal:
                    case DiceType.Thunder:
                    case DiceType.Fire:
                    case DiceType.Ice:
                    case DiceType.Poison:
                    case DiceType.Stun:
                    case DiceType.ArmorBreak:
                        multiplier *= 1.1f;
                        break;
                    case DiceType.Tornado:
                        break;
                    case DiceType.KingNormal:
                        multiplier *= 1.3f;
                        break;
                    case DiceType.KingFire:
                    case DiceType.KingIce:
                    case DiceType.KingPoison:
                        multiplier *= 1.2f;
                        break;
                    case DiceType.KingThunder:
                        break;
                }
            }

            if (diceType == DiceType.Tornado && level >= 12)
                multiplier *= 1.3f;
            if (diceType == DiceType.KingFire && level >= 12)
                multiplier *= 1.3f;

            return multiplier;
        }

        public static float GetLevelCooldownMultiplier(DiceType diceType, int level)
        {
            float multiplier = 1f;
            switch (diceType)
            {
                case DiceType.Normal:
                    if (level >= 6) multiplier *= 0.9f;
                    break;
                case DiceType.Thunder:
                    if (level >= 9) multiplier *= 0.9f;
                    break;
                case DiceType.Fire:
                    if (level >= 12) multiplier *= 0.8f;
                    break;
                case DiceType.Ice:
                    if (level >= 12) multiplier *= 0.8f;
                    break;
                case DiceType.Tornado:
                    if (level >= 9) multiplier *= 0.8f;
                    break;
                case DiceType.Stun:
                    if (level >= 9) multiplier *= 0.9f;
                    break;
                case DiceType.ArmorBreak:
                    if (level >= 9) multiplier *= 0.8f;
                    break;
                case DiceType.Time:
                    if (level >= 9) multiplier *= 0.9f;
                    break;
            }

            return multiplier;
        }

        public static int GetThunderTargetCount(int level)
        {
            int count = 2;
            if (level >= 6)
                count += 1;
            return count;
        }

        public static float GetFireExplosionRangeMultiplier(int level)
        {
            float multiplier = level >= 9 ? 1.1f : 1f;
            int kingFireLevel = GetKingLevel(DiceType.KingFire);
            if (IsKingSummoned(DiceType.KingFire) && kingFireLevel >= 6)
                multiplier *= 1.2f;
            return multiplier;
        }

        public static float GetWindPushChancePercent(int level)
        {
            float chance = 40f + Mathf.Max(1, level) * 1f;
            if (level >= 9)
                chance += 10f;
            return chance;
        }

        public static float GetWindPushChancePercent(DiceType diceType, int level)
        {
            float chance = GetWindPushChancePercent(level);
            if (ElementUpgradeManager.Instance != null)
                chance *= ElementUpgradeManager.Instance.GetTotalBonusMultiplier(diceType);

            return chance;
        }

        public static int GetWindTargetCount(int level)
        {
            return level >= 12 ? 3 : 2;
        }

        public static float GetWindDistanceMultiplier(int level)
        {
            return level >= 3 ? 1.1f : 1f;
        }

        public static float GetTimeCooldownReducePercent(int level)
        {
            float percent = 10f + Mathf.Max(1, level) * 1f;
            if (level >= 3)
                percent += 5f;
            if (level >= 12)
                percent += 10f;
            return percent;
        }

        public static float GetTimeCooldownReducePercent(DiceType diceType, int level)
        {
            float percent = GetTimeCooldownReducePercent(level);
            if (ElementUpgradeManager.Instance != null)
                percent *= ElementUpgradeManager.Instance.GetTotalBonusMultiplier(diceType);

            return percent;
        }

        public static int GetTimeTargetCount(int level)
        {
            return level >= 6 ? 3 : 2;
        }

        public static float GetStunChancePercent(int level)
        {
            return level >= 6 ? 50f : 40f;
        }

        public static int GetArmorBreakPercent(int level)
        {
            return level >= 6 ? 40 : 30;
        }

        public static float GetGlobalCriticalChancePercent()
        {
            if (!IsKingSummoned(DiceType.KingNormal))
                return 0f;

            int kingNormalLevel = GetKingLevel(DiceType.KingNormal);
            return kingNormalLevel >= 9 ? 10f : 0f;
        }

        public static float GetGlobalCriticalDamageMultiplier()
        {
            if (!IsKingSummoned(DiceType.KingNormal))
                return 2f;

            int kingNormalLevel = GetKingLevel(DiceType.KingNormal);
            return kingNormalLevel >= 12 ? 2.2f : 2f;
        }

        public static float GetKingSynergyDamageMultiplier(DiceType diceType)
        {
            switch (diceType)
            {
                case DiceType.Normal:
                    return IsKingSummoned(DiceType.KingNormal) && GetKingLevel(DiceType.KingNormal) >= 6 ? 1.2f : 1f;
                case DiceType.Thunder:
                    return IsKingSummoned(DiceType.KingThunder) && GetKingLevel(DiceType.KingThunder) >= 6 ? 1.2f : 1f;
                case DiceType.Fire:
                    return 1f;
                case DiceType.Ice:
                    return 1f;
                case DiceType.Poison:
                    return 1f;
                default:
                    return 1f;
            }
        }

        public static float GetPoisonDamageMultiplier(DiceType diceType, int level)
        {
            float multiplier = level >= 6 ? 1.5f : 1f;
            if (diceType == DiceType.Poison && IsKingSummoned(DiceType.KingPoison) && GetKingLevel(DiceType.KingPoison) >= 6)
                multiplier *= 1.5f;
            return multiplier;
        }

        public static float GetSlowDuration(DiceType diceType, int level)
        {
            float duration = 2f;
            if (diceType == DiceType.Ice && level >= 9)
                duration *= 1.5f;
            if (diceType == DiceType.KingIce)
                duration *= IsKingSummoned(DiceType.KingIce) && GetKingLevel(DiceType.KingIce) >= 6 ? 1.5f : 1f;
            return duration;
        }

        public static float GetPoisonDuration(DiceType diceType)
        {
            return 4f;
        }

        public static bool HasKingIceDamageBonus()
        {
            return IsKingSummoned(DiceType.KingIce) && GetKingLevel(DiceType.KingIce) >= 12;
        }

        public static bool HasKingPoisonDamageBonus()
        {
            return IsKingSummoned(DiceType.KingPoison) && GetKingLevel(DiceType.KingPoison) >= 12;
        }

        private static int GetKingLevel(DiceType diceType)
        {
            return DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(diceType) : 1;
        }

        private static bool IsKingSummoned(DiceType diceType)
        {
            return DiceTypeStarManager.Instance != null && DiceTypeStarManager.Instance.GetTypeCount(diceType) > 0;
        }

        private static void EnsureDefaults()
        {
            if (defaults != null)
                return;

            defaults = new Dictionary<DiceType, DiceMetaDataDatabase.DiceMeta>
            {
                { DiceType.Normal, CreateDefault(DiceType.Normal, "Normal Dice", "적 1명에게 12 + (레벨 x 3) 대미지를 줍니다.", 12, 3, 120, 50, 8, 2, 2.4f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "쿨타임 10% 감소"),
                    (9, "공격 시 20% 확률로 SP +5"),
                    (12, "공격 시 20% 확률로 대미지 2배")
                }) },
                { DiceType.Fire, CreateDefault(DiceType.Fire, "Fire Dice", "적 1명에게 10 + (레벨 x 4) 대미지를 주고 주변 적에게 폭발 피해를 줍니다.", 10, 4, 140, 60, 10, 2, 3.1f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "공격 시 20% 확률로 한 번 더 폭발"),
                    (9, "폭발 범위 10% 증가"),
                    (12, "쿨타임 20% 감소")
                }) },
                { DiceType.Ice, CreateDefault(DiceType.Ice, "Ice Dice", "적 1명에게 9 + (레벨 x 3) 대미지를 주고 둔화를 부여합니다.", 9, 3, 130, 55, 9, 2, 3.8f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "공격 시 30% 확률로 범위 피해"),
                    (9, "둔화 지속시간 50% 증가"),
                    (12, "쿨타임 20% 감소")
                }) },
                { DiceType.Poison, CreateDefault(DiceType.Poison, "Poison Dice", "적 1명에게 8 + (레벨 x 2) 대미지를 주고 중독을 부여합니다.", 8, 2, 125, 50, 9, 2, 3.4f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "중독 피해량 50% 증가"),
                    (9, "공격 시 40% 확률로 범위 피해"),
                    (12, "중독된 적이 받는 피해 10% 증가")
                }) },
                { DiceType.Thunder, CreateDefault(DiceType.Thunder, "Thunder Dice", "적 1명에게 11 + (레벨 x 3) 대미지를 주고 최대 2명에게 번개가 전이됩니다.", 11, 3, 150, 65, 11, 2, 2.7f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "전이 대상 +1"),
                    (9, "쿨타임 10% 감소"),
                    (12, "공격한 적 주변 1명에게 50% 추가 번개 피해")
                }) },
                { DiceType.Tornado, CreateDefault(DiceType.Tornado, "Tornado Dice", "적 1명에게 9 + (레벨 x 3) 대미지를 주고 주변 적을 끌어당깁니다.", 9, 3, 145, 62, 10, 2, 3.0f, new []{
                    (3, "범위 10% 증가"),
                    (6, "적을 2초 동안 흡입"),
                    (9, "쿨타임 20% 감소"),
                    (12, "최종 대미지 30% 증가")
                }, new [] { ElementType.Normal, ElementType.Water },
                    false, false, false, (DiceType.Normal, 2, 1), (DiceType.Ice, 2, 1)) },
                { DiceType.Stun, CreateDefault(DiceType.Stun, "Stun Dice", "적 1명에게 8 + (레벨 x 2) 대미지를 주고 40% 확률로 스턴시킵니다.", 8, 2, 140, 58, 10, 2, 3.5f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "스턴 확률 10% 증가"),
                    (9, "쿨타임 10% 감소"),
                    (12, "스턴된 적이 받는 피해 20% 증가")
                }, new [] { ElementType.Light, ElementType.Dark },
                    false, false, false, (DiceType.Thunder, 2, 1), (DiceType.Poison, 2, 1)) },
                { DiceType.ArmorBreak, CreateDefault(DiceType.ArmorBreak, "Armor Break Dice", "적 1명에게 10 + (레벨 x 3) 대미지를 주고 방어력을 30% 감소시킵니다.", 10, 3, 150, 64, 11, 2, 3.2f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "방어력 감소 10% 증가"),
                    (9, "쿨타임 20% 감소"),
                    (12, "방깎 상태 적이 받는 피해 10% 증가")
                }, new [] { ElementType.Fire, ElementType.Dark },
                    false, false, false, (DiceType.Fire, 2, 1), (DiceType.Poison, 2, 1)) },
                { DiceType.Wind, CreateDefault(DiceType.Wind, "Wind Dice", "적 2명을 40 + (레벨 x 1)% 확률로 밀어냅니다. 대미지는 없습니다.", 0, 0, 135, 56, 9, 2, 2.9f, new []{
                    (3, "밀어내는 거리 10% 증가"),
                    (6, "밀리는 적이 받는 피해 10% 증가"),
                    (9, "밀어내기 확률 10% 추가 증가"),
                    (12, "밀어내는 대상 +1")
                }, new [] { ElementType.Water, ElementType.Fire },
                    false, false, false, (DiceType.Ice, 2, 1), (DiceType.Fire, 2, 1)) },
                { DiceType.Time, CreateDefault(DiceType.Time, "Time Dice", "다른 무작위 다이스 2개의 남은 쿨타임을 10 + (레벨 x 1)% 감소시킵니다. 대미지는 없습니다.", 0, 0, 170, 70, 12, 3, 4.0f, new []{
                    (3, "쿨타임 감소량 5% 추가 증가"),
                    (6, "대상 +1"),
                    (9, "자신의 쿨타임 10% 감소"),
                    (12, "쿨타임 감소량 10% 추가 증가")
                }, new [] { ElementType.Normal, ElementType.Light },
                    false, false, false, (DiceType.Normal, 2, 1), (DiceType.Thunder, 2, 1)) },
                { DiceType.KingNormal, CreateMythicDefault(DiceType.KingNormal, "King Normal", "적 1명과 주변 적에게 첫 타 70%, 이후 0.2초 간격으로 10%씩 3연타를 가합니다.", 104, 20, 3.0f, new []{
                    (3, "최종 대미지 30% 증가"),
                    (6, "소환 중인 동안 NormalDice 최종 대미지 20% 증가"),
                    (9, "소환 중인 동안 모든 다이스 크리티컬 확률 10% 증가"),
                    (12, "소환 중인 동안 모든 다이스 크리티컬 대미지 20% 증가")
                },
                    (DiceType.Normal, 4, 1), (DiceType.Tornado, 1, 1)) },
                { DiceType.KingFire, CreateMythicDefault(DiceType.KingFire, "King Fire", "적 1명에게 110 + (레벨 x 22) 대미지를 주고 강화 폭발을 일으킵니다.", 110, 22, 3.3f, new []{
                    (3, "최종 대미지 20% 증가"),
                    (6, "소환 중인 동안 FireDice 폭발 범위 20% 증가"),
                    (9, "폭발이 30% 확률로 한 번 더 발생"),
                    (12, "폭발 최종 피해 30% 증가")
                },
                    (DiceType.Fire, 4, 1), (DiceType.ArmorBreak, 1, 1)) },
                { DiceType.KingIce, CreateMythicDefault(DiceType.KingIce, "King Ice", "적 1명에게 100 + (레벨 x 20) 대미지를 주고 강한 둔화를 부여합니다.", 100, 20, 3.5f, new []{
                    (3, "최종 대미지 20% 증가"),
                    (6, "IceDice 둔화 지속시간 50% 증가"),
                    (9, "공격 시 빙결 부여"),
                    (12, "둔화된 적이 받는 피해 15% 증가")
                },
                    (DiceType.Ice, 4, 1), (DiceType.Wind, 1, 1)) },
                { DiceType.KingPoison, CreateMythicDefault(DiceType.KingPoison, "King Poison", "적 1명에게 98 + (레벨 x 20) 대미지를 주고 중독과 둔화를 부여합니다.", 98, 20, 3.3f, new []{
                    (3, "최종 대미지 20% 증가"),
                    (6, "소환 중인 동안 PoisonDice 중독 피해량 50% 증가"),
                    (9, "중독 적용 시 30% 확률로 주변 적 1명에게 전이"),
                    (12, "중독된 적이 받는 피해 15% 증가")
                },
                    (DiceType.Poison, 4, 1), (DiceType.Stun, 1, 1)) },
                { DiceType.KingThunder, CreateMythicDefault(DiceType.KingThunder, "King Thunder", "적 1명에게 116 + (레벨 x 24) 대미지를 주고 최대 4명에게 번개가 전이됩니다.", 116, 24, 3.0f, new []{
                    (3, "전이 대상 +2"),
                    (6, "소환 중인 동안 ThunderDice 최종 대미지 20% 증가"),
                    (9, "30% 확률로 추가 1명에게 50% 피해"),
                    (12, "맞은 적이 받는 피해 15% 증가")
                },
                    (DiceType.Thunder, 4, 1), (DiceType.Time, 1, 1)) }
            };
        }

        private static DiceMetaDataDatabase.DiceMeta MergeMeta(
            DiceMetaDataDatabase.DiceMeta assetMeta,
            DiceMetaDataDatabase.DiceMeta fallback)
        {
            var merged = new DiceMetaDataDatabase.DiceMeta
            {
                diceType = fallback.diceType,
                elementType = assetMeta.elementType != null && assetMeta.elementType.Length > 0
                    ? assetMeta.elementType
                    : fallback.elementType,
                displayName = fallback.displayName,
                description = fallback.description,
                icon = assetMeta.icon,
                color = assetMeta.color,
                projectileSprite = assetMeta.projectileSprite,
                primaryEffect = assetMeta.primaryEffect,
                effectPrefabs = assetMeta.effectPrefabs,
                isMythic = fallback.isMythic,
                summonable = fallback.summonable,
                canMerge = fallback.canMerge,
                showStarUI = fallback.showStarUI,
                recipeMaterials = new List<DiceMetaDataDatabase.DiceRecipeMaterial>(),
                baseAttack = fallback.baseAttack,
                levelUpAttackIncrease = fallback.levelUpAttackIncrease,
                baseCooldown = fallback.baseCooldown,
                baseGoldCost = fallback.baseGoldCost,
                goldCostPerLevel = fallback.goldCostPerLevel,
                baseScrollCost = fallback.baseScrollCost,
                scrollCostPerLevel = fallback.scrollCostPerLevel,
                milestones = new List<DiceMetaDataDatabase.DiceLevelMilestone>()
            };

            for (int i = 0; i < fallback.recipeMaterials.Count; i++)
            {
                var recipe = fallback.recipeMaterials[i];
                merged.recipeMaterials.Add(new DiceMetaDataDatabase.DiceRecipeMaterial
                {
                    diceType = recipe.diceType,
                    star = recipe.star,
                    count = recipe.count
                });
            }

            for (int i = 0; i < fallback.milestones.Count; i++)
            {
                var milestone = fallback.milestones[i];
                merged.milestones.Add(new DiceMetaDataDatabase.DiceLevelMilestone
                {
                    level = milestone.level,
                    description = milestone.description
                });
            }

            return merged;
        }

        private static DiceMetaDataDatabase.DiceMeta CreateDefault(
            DiceType diceType,
            string displayName,
            string description,
            int baseAttack,
            int levelUpAttackIncrease,
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
            float baseCooldown,
            (int level, string desc)[] milestones,
            params (DiceType type, int star, int count)[] recipe)
        {
            var meta = new DiceMetaDataDatabase.DiceMeta
            {
                diceType = diceType,
                displayName = displayName,
                description = description,
                baseAttack = baseAttack,
                levelUpAttackIncrease = levelUpAttackIncrease,
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

            for (int i = 0; i < milestones.Length; i++)
            {
                meta.milestones.Add(new DiceMetaDataDatabase.DiceLevelMilestone
                {
                    level = milestones[i].level,
                    description = milestones[i].desc
                });
            }

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
