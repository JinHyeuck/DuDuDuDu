using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OJ
{
    public struct StageRewardEntry
    {
        public PointType PointType;
        public int Amount;

        public StageRewardEntry(PointType pointType, int amount)
        {
            PointType = pointType;
            Amount = amount;
        }
    }

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

        public static List<StageRewardEntry> BuildNormalClearRewards(int stageIndex)
        {
            int chapterBonus = GetChapterBonus(stageIndex);
            var rewards = new List<StageRewardEntry>
            {
                new StageRewardEntry(PointType.Gold, 150 + chapterBonus),
            };

            AddDistinctRewards(rewards, ElementScrollTypes, new[] { 20, 40 });
            rewards.Add(new StageRewardEntry(PointType.MythicScroll, Random.Range(5, 11)));
            AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 3 });
            return rewards;
        }

        public static List<StageRewardEntry> BuildBonusRewards(int stageIndex, StageRewardTierFlags rewardFlags)
        {
            var rewards = new List<StageRewardEntry>();

            if ((rewardFlags & StageRewardTierFlags.Minimum) != 0)
            {
                rewards.Add(new StageRewardEntry(PointType.Gold, 300 + GetChapterBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50 });
                AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 10 });
            }

            if ((rewardFlags & StageRewardTierFlags.Half) != 0)
            {
                rewards.Add(new StageRewardEntry(PointType.Gold, 400 + GetChapterBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50 });
                AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 10, 10 });
                rewards.Add(new StageRewardEntry(PointType.MythicScroll, 15));
            }

            if ((rewardFlags & StageRewardTierFlags.Perfect) != 0)
            {
                rewards.Add(new StageRewardEntry(PointType.Gold, 500 + GetChapterBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50, 50 });
                rewards.Add(new StageRewardEntry(PointType.Dia, 150));
                rewards.Add(new StageRewardEntry(PointType.MythicScroll, 10));
            }

            return rewards;
        }

        public static void GrantRewards(IReadOnlyList<StageRewardEntry> rewards)
        {
            if (PointManager.Instance == null || rewards == null)
                return;

            for (int i = 0; i < rewards.Count; i++)
            {
                StageRewardEntry reward = rewards[i];
                PointManager.Instance.Add(reward.PointType, reward.Amount, false);
            }

            PointManager.Instance.SaveAll();
        }

        public static string BuildRewardSummary(IReadOnlyList<StageRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return "No rewards";

            var builder = new StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(rewards[i].PointType);
                builder.Append(" x");
                builder.Append(rewards[i].Amount);
            }

            return builder.ToString();
        }

        private static int GetChapterBonus(int stageIndex)
        {
            return ((Mathf.Max(1, stageIndex) - 1) / 10) * 5;
        }

        private static void AddDistinctRewards(List<StageRewardEntry> rewards, PointType[] pool, int[] amounts)
        {
            if (rewards == null || pool == null || amounts == null || amounts.Length == 0)
                return;

            int count = Mathf.Min(pool.Length, amounts.Length);
            PointType[] shuffled = (PointType[])pool.Clone();
            Shuffle(shuffled);

            for (int i = 0; i < count; i++)
                rewards.Add(new StageRewardEntry(shuffled[i], amounts[i]));
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
