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

        /// <summary>
        /// 화면에 적을 재화 이름.
        ///
        /// <b>에셋이 정본이고 코드 표는 폴백이다.</b> <c>PointMetadataDatabase.asset</c> 의
        /// <c>displayName</c> 이 지금은 전부 영문 enum 명이라(기획서 9장 "한글 표시명 및 아이콘
        /// 등록 필요"), 등록되기 전까지는 아래 표가 화면을 한글로 잡아 준다. 등록이 끝나면
        /// 에셋 값이 이기고 이 표는 저절로 안 쓰인다 — <b>표를 지우는 것이 다음 단계지 지금이
        /// 아니다.</b>
        ///
        /// enum 명과 같은 <c>displayName</c> 은 "등록 안 됨"으로 본다. 그러지 않으면 등록되지
        /// 않은 재화가 화면에 <c>NormalScroll</c> 로 뜨는데, 그건 값이 있는 것처럼 보여서
        /// 빠뜨렸다는 사실이 드러나지 않는다.
        /// </summary>
        public static string GetPointName(PointType pointType)
        {
            if (StaticResource.isAlive && StaticResource.Instance != null &&
                StaticResource.Instance.PointMetadataDatabase != null)
            {
                PointMetadataDatabase.PointMetadata metadata =
                    StaticResource.Instance.PointMetadataDatabase.Get(pointType);

                if (metadata != null && !string.IsNullOrWhiteSpace(metadata.displayName) &&
                    metadata.displayName != pointType.ToString())
                {
                    return metadata.displayName;
                }
            }

            return FallbackName(pointType);
        }

        private static string FallbackName(PointType pointType)
        {
            switch (pointType)
            {
                case PointType.Gold: return "골드";
                case PointType.FreeGem: return "무료젬";
                case PointType.PaidGem: return "유료젬";
                case PointType.Stamina: return "고기";
                case PointType.BattleEnhanceStone: return "강화석";
                case PointType.RelicTicket: return "유물권";
                case PointType.NormalScroll: return "일반 소환권";
                case PointType.FireScroll: return "화염 소환권";
                case PointType.IceScroll: return "냉기 소환권";
                case PointType.PoisonScroll: return "독 소환권";
                case PointType.ThunderScroll: return "번개 소환권";
                case PointType.MythicScroll: return "신화석";
                case PointType.SpecialDiceCore: return "레어석";
                case PointType.WeaponScroll: return "무기 강화권";
                case PointType.HelmetScroll: return "투구 강화권";
                case PointType.ArmorScroll: return "갑옷 강화권";
                case PointType.RingScroll: return "반지 강화권";
                case PointType.ShoesScroll: return "신발 강화권";
                case PointType.NecklaceScroll: return "목걸이 강화권";
                default: return pointType.ToString();
            }
        }
    }
}
