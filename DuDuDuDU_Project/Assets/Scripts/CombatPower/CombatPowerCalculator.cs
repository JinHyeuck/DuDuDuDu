using System;
using UnityEngine;
using OJ.Dice;
using OJ.Equipment;
using OJ.Relic;

namespace OJ.CombatPower
{
    /// <summary>
    /// Permanent progression only. Run-scoped element levels, dice stars and mythic crafting
    /// are deliberately excluded from combat power.
    /// </summary>
    public static class CombatPowerCalculator
    {
        public readonly struct Breakdown
        {
            public readonly long Dice;
            public readonly long Equipment;
            public readonly long Gems;
            public readonly long Relics;

            public long Total => Math.Max(0L, Dice + Equipment + Gems + Relics);

            public Breakdown(long dice, long equipment, long gems, long relics)
            {
                Dice = Math.Max(0L, dice);
                Equipment = Math.Max(0L, equipment);
                Gems = Math.Max(0L, gems);
                Relics = Math.Max(0L, relics);
            }
        }

        public static long Current => Calculate().Total;

        public static Breakdown Calculate()
        {
            return new Breakdown(
                CalculateDicePower(),
                CalculateEquipmentPower(),
                CalculateGemPower(),
                CalculateRelicPower());
        }

        private static long CalculateDicePower()
        {
            long total = 0L;
            DiceLevelManager manager = DiceLevelManager.Instance;

            foreach (DiceType diceType in Enum.GetValues(typeof(DiceType)))
            {
                if (diceType == DiceType.Max)
                    continue;

                int level = manager != null ? manager.GetLevel(diceType) : 1;
                if ((int)diceType >= 200)
                    total += ScoreLevel(level, 180, 65, 4);
                else if ((int)diceType >= 100)
                    total += ScoreLevel(level, 120, 45, 3);
                else
                    total += ScoreLevel(level, 100, 40, 3);
            }

            return total;
        }

        private static long CalculateEquipmentPower()
        {
            long total = 0L;
            EquipmentManager manager = EquipmentManager.Instance;
            if (manager == null)
                return total;

            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                int level = manager.GetLevel(equipmentType);
                total += ScoreLevel(level, 150, 60, 5);
                total += (long)manager.GetEquipmentAttack(equipmentType) * 25L;
            }

            return total;
        }

        private static long CalculateGemPower()
        {
            long total = 0L;
            EquipmentManager manager = EquipmentManager.Instance;
            if (manager == null)
                return total;

            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                for (int slotIndex = 0; slotIndex < Define.MaxEquipmentSlot; slotIndex++)
                {
                    if (!manager.IsSlotUnlocked(equipmentType, slotIndex))
                        continue;

                    string gemId = manager.GetEquippedGemId(equipmentType, slotIndex);
                    if (string.IsNullOrEmpty(gemId) ||
                        !manager.TryGetGemDefinition(gemId, out GemDefinition definition) ||
                        definition == null)
                    {
                        continue;
                    }

                    total += GetGemRarityPower(definition.rarity);
                    if (definition.effects == null)
                        continue;

                    for (int i = 0; i < definition.effects.Count; i++)
                        total += GetGemEffectPower(definition.effects[i]);
                }
            }

            return total;
        }

        private static long CalculateRelicPower()
        {
            long total = 0L;
            RelicManager manager = RelicManager.Instance;
            if (manager == null)
                return total;

            var definitions = manager.GetDefinitions();
            if (definitions == null)
                return total;

            for (int i = 0; i < definitions.Count; i++)
            {
                RelicDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                int level = manager.GetLevel(definition.relicId);
                if (level <= 0)
                    continue;

                long rarityBase = GetRelicRarityPower(definition.rarity);
                total += rarityBase * level;
                total += (long)level * Math.Max(0, level - 1) * rarityBase / 20L;
            }

            return total;
        }

        private static long ScoreLevel(int level, long basePower, long powerPerLevel, long growthPerLevel)
        {
            long step = Math.Max(0, level - 1);
            return basePower + step * powerPerLevel + (step * Math.Max(0L, step - 1L) / 2L) * growthPerLevel;
        }

        private static long GetGemRarityPower(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return 100L;
                case Rarity.Common: return 180L;
                case Rarity.Normal: return 350L;
                case Rarity.Rare: return 700L;
                case Rarity.Epic: return 1400L;
                case Rarity.Mythic: return 2800L;
                default: return 100L;
            }
        }

        private static long GetGemEffectPower(GemEffect effect)
        {
            if (effect == null)
                return 0L;

            float percent = Mathf.Max(0f, effect.percentValue);
            int flat = Mathf.Max(0, effect.flatValue);

            switch (effect.statType)
            {
                case GemStatType.AttackPercent:
                case GemStatType.CooldownReducePercent:
                case GemStatType.FinalDamagePercent:
                    return Mathf.RoundToInt(percent * 10000f) + flat * 25L;
                case GemStatType.FireExplosionRangePercent:
                    return Mathf.RoundToInt(percent * 4000f) + flat * 10L;
                case GemStatType.AttackFlat:
                    return flat * 25L;
                case GemStatType.FirstNWavesDamageFlat:
                    return flat * Math.Max(1, effect.intParam) * 5L;
                case GemStatType.FireExplosionTargetCountFlat:
                case GemStatType.ThunderChainCountFlat:
                    return flat * 400L;
                case GemStatType.WellHpOnKill:
                    return flat * 100L;
                case GemStatType.GoldOnKill:
                    return flat * 50L;
                default:
                    return Mathf.RoundToInt(percent * 3000f) + flat * 20L;
            }
        }

        private static long GetRelicRarityPower(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Normal: return 120L;
                case Rarity.Rare: return 220L;
                case Rarity.Epic: return 400L;
                case Rarity.Mythic: return 750L;
                default: return 100L;
            }
        }
    }
}
