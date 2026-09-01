using System.Collections.Generic;
using System.Text;
using UnityEngine;
using OJ.Utils;

namespace OJ.Point
{
    public struct PointRewardEntry
    {
        public PointType PointType;
        public int Amount;

        public PointRewardEntry(PointType pointType, int amount)
        {
            PointType = pointType;
            Amount = amount;
        }
    }

    public static class PointRewardUtility
    {
        public static void GrantRewards(IReadOnlyList<PointRewardEntry> rewards)
        {
            if (PointManager.Instance == null || rewards == null)
                return;

            for (int i = 0; i < rewards.Count; i++)
            {
                PointRewardEntry reward = rewards[i];
                PointManager.Instance.Add(reward.PointType, reward.Amount, false);
            }

            PointManager.Instance.SaveAll();
        }

        public static string BuildRewardSummary(IReadOnlyList<PointRewardEntry> rewards)
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

        public static List<PointRewardEntry> MergeRewards(IReadOnlyList<PointRewardEntry> rewards)
        {
            var merged = new Dictionary<PointType, int>();
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    PointRewardEntry reward = rewards[i];
                    if (reward.Amount <= 0)
                        continue;

                    if (!merged.ContainsKey(reward.PointType))
                        merged[reward.PointType] = 0;

                    merged[reward.PointType] += reward.Amount;
                }
            }

            var list = new List<PointRewardEntry>();
            foreach (KeyValuePair<PointType, int> pair in merged)
                list.Add(new PointRewardEntry(pair.Key, pair.Value));

            list.Sort((left, right) =>
            {
                if (left.PointType == PointType.Gold)
                    return -1;
                if (right.PointType == PointType.Gold)
                    return 1;
                return left.PointType.CompareTo(right.PointType);
            });

            return list;
        }

        public static Sprite GetPointIcon(PointType pointType)
        {
            if (!StaticResource.isAlive || StaticResource.Instance == null || StaticResource.Instance.PointMetadataDatabase == null)
                return null;

            PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(pointType);
            return metadata != null ? metadata.icon : null;
        }
    }
}
