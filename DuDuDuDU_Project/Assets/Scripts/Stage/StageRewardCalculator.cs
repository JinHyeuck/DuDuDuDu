using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public static class StageRewardCalculator
    {
        private static readonly PointType[] ElementScrollTypes =
        {
            PointType.NormalScroll,
            PointType.FireScroll,
            PointType.IceScroll,
            PointType.PoisonScroll,
            PointType.ThunderScroll,
        };

        private static readonly PointType[] EquipmentScrollTypes =
        {
            PointType.WeaponScroll,
            PointType.HelmetScroll,
            PointType.ArmorScroll,
            PointType.RingScroll,
            PointType.ShoesScroll,
            PointType.NecklaceScroll,
        };

        public static StageClearGrade GetClearGrade(int currentWallHp, int totalWallHp)
        {
            if (totalWallHp <= 0)
                return StageClearGrade.Minimum;

            float ratio = Mathf.Clamp01((float)currentWallHp / totalWallHp);
            if (ratio >= 0.999f)
                return StageClearGrade.Perfect;
            if (ratio >= 0.5f)
                return StageClearGrade.Half;
            return StageClearGrade.Minimum;
        }

        public static StageRewardTierFlags GetRewardFlagsForGrade(StageClearGrade clearGrade)
        {
            switch (clearGrade)
            {
                case StageClearGrade.Minimum:
                    return StageRewardTierFlags.Minimum;
                case StageClearGrade.Half:
                    return StageRewardTierFlags.Minimum | StageRewardTierFlags.Half;
                case StageClearGrade.Perfect:
                    return StageRewardTierFlags.Minimum | StageRewardTierFlags.Half | StageRewardTierFlags.Perfect;
                default:
                    return StageRewardTierFlags.None;
            }
        }

        public static List<PointRewardEntry> BuildNormalClearRewards(int stageIndex)
        {
            var rewards = new List<PointRewardEntry>
            {
                new PointRewardEntry(PointType.Gold, GetGuaranteedNormalGold(stageIndex)),
            };

            AddDistinctRewards(rewards, ElementScrollTypes, new[] { 20, 40 });
            rewards.Add(new PointRewardEntry(PointType.MythicScroll, Random.Range(5, 11)));
            AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 3 });
            return rewards;
        }

        public static int GetGuaranteedNormalGold(int stageIndex)
        {
            return 150 + GetStageBonus(stageIndex);
        }

        public static int GetAccumulatedGuaranteedGold(int stageIndex, int clearedWaves, int totalWaves)
        {
            int safeTotalWaves = Mathf.Max(1, totalWaves);
            int safeClearedWaves = Mathf.Clamp(clearedWaves, 0, safeTotalWaves);
            float ratio = (float)safeClearedWaves / safeTotalWaves;
            return Mathf.FloorToInt(GetGuaranteedNormalGold(stageIndex) * ratio);
        }

        public static List<PointRewardEntry> ScaleRewards(IReadOnlyList<PointRewardEntry> rewards, float multiplier)
        {
            var scaledRewards = new List<PointRewardEntry>();
            if (rewards == null || rewards.Count == 0)
                return scaledRewards;

            float clampedMultiplier = Mathf.Clamp01(multiplier);
            for (int i = 0; i < rewards.Count; i++)
            {
                PointRewardEntry reward = rewards[i];
                int scaledAmount = Mathf.FloorToInt(reward.Amount * clampedMultiplier);
                if (scaledAmount <= 0)
                    continue;

                scaledRewards.Add(new PointRewardEntry(reward.PointType, scaledAmount));
            }

            return scaledRewards;
        }

        public static List<PointRewardEntry> BuildBonusRewards(int stageIndex, StageRewardTierFlags rewardFlags)
        {
            var rewards = new List<PointRewardEntry>();

            if ((rewardFlags & StageRewardTierFlags.Minimum) != 0)
            {
                rewards.Add(new PointRewardEntry(PointType.Gold, 300 + GetStageBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50 });
                AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 10 });
            }

            if ((rewardFlags & StageRewardTierFlags.Half) != 0)
            {
                rewards.Add(new PointRewardEntry(PointType.Gold, 400 + GetStageBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50 });
                AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 10, 10 });
                rewards.Add(new PointRewardEntry(PointType.MythicScroll, 15));
            }

            if ((rewardFlags & StageRewardTierFlags.Perfect) != 0)
            {
                rewards.Add(new PointRewardEntry(PointType.Gold, 500 + GetStageBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50, 50 });
                rewards.Add(new PointRewardEntry(PointType.Dia, 150));
                rewards.Add(new PointRewardEntry(PointType.MythicScroll, 10));
            }

            return rewards;
        }

        private static int GetStageBonus(int stageIndex)
        {
            return ((Mathf.Max(1, stageIndex) - 1) / 10) * 5;
        }

        private static void AddDistinctRewards(List<PointRewardEntry> rewards, PointType[] pool, int[] amounts)
        {
            if (rewards == null || pool == null || amounts == null || amounts.Length == 0)
                return;

            int count = Mathf.Min(pool.Length, amounts.Length);
            PointType[] shuffled = (PointType[])pool.Clone();
            Shuffle(shuffled);

            for (int i = 0; i < count; i++)
                rewards.Add(new PointRewardEntry(shuffled[i], amounts[i]));
        }

        private static void Shuffle(PointType[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int swapIndex = Random.Range(i, values.Length);
                PointType temp = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temp;
            }
        }
    }
}
