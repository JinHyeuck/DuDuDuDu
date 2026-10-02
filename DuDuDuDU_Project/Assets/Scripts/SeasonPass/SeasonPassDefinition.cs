using System;
using System.Collections.Generic;
using UnityEngine;
using OJ.Point;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 보상 한 덩이. <c>PointRewardEntry</c> 는 직렬화 속성이 없어 여기서 따로 둔다
    /// (<c>OJ.Pinball.PinballReward</c>·<c>OJ.Mission.MissionReward</c> 와 같은 사정이다).
    /// </summary>
    [Serializable]
    public struct SeasonPassReward
    {
        public PointType pointType;

        [Min(1)] public int amount;

        public SeasonPassReward(PointType pointType, int amount)
        {
            this.pointType = pointType;
            this.amount = Mathf.Max(1, amount);
        }

        public PointRewardEntry ToPointRewardEntry()
        {
            return new PointRewardEntry(pointType, amount);
        }

        public static List<PointRewardEntry> ToPointRewards(IReadOnlyList<SeasonPassReward> rewards)
        {
            var list = new List<PointRewardEntry>();
            if (rewards == null)
                return list;

            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].amount > 0)
                    list.Add(rewards[i].ToPointRewardEntry());
            }

            return list;
        }
    }

    /// <summary>
    /// 패스 한 단계. 무료 트랙과 유료 트랙이 <b>한 줄에 같이</b> 있다.
    ///
    /// <b>레벨 번호를 필드로 두지 않는다.</b> 목록에서의 위치가 곧 레벨이다(0번이 1레벨).
    /// 번호를 따로 적으면 목록 순서와 어긋날 수 있고, 그러면 화면의 "3" 과 실제로 열리는
    /// 보상이 달라진다 — 그 사고는 수령해 보기 전에는 드러나지 않는다.
    ///
    /// <b>한쪽이 비어도 된다.</b> 스샷의 5레벨처럼 유료만 있는 칸이 있다.
    /// 빈 쪽은 화면에 칸을 그리지 않는다.
    /// </summary>
    [Serializable]
    public sealed class SeasonPassLevel
    {
        [Tooltip("무료 트랙 보상. 비우면 그 칸은 그리지 않는다.")]
        public List<SeasonPassReward> freeRewards = new List<SeasonPassReward>();

        [Tooltip("유료 트랙 보상. 비우면 그 칸은 그리지 않는다.")]
        public List<SeasonPassReward> premiumRewards = new List<SeasonPassReward>();
    }

    /// <summary>
    /// 시즌 하나. <b>달마다 새 보상</b>이라 시즌마다 이 덩이가 하나씩 생긴다.
    ///
    /// <b><see cref="id"/> 가 세이브와 맞물린다.</b> <c>yyyy-MM</c> 이고
    /// <c>SeasonPassRules.SeasonId</c> 가 만드는 값과 같은 형식이어야 한다 —
    /// 다르면 그 달에 시즌이 없는 것으로 보고 기본 시즌으로 내려간다.
    /// </summary>
    [Serializable]
    public sealed class SeasonPassSeason
    {
        [Tooltip("yyyy-MM. 예: 2026-10")]
        public string id = "2026-10";

        [Tooltip("화면 제목 아래에 적는 이름. 예: 시즌 1 : 달토끼 깜짝쥐")]
        public string displayName = "시즌 1";

        [Tooltip("1레벨부터 차례로. 목록에서의 위치가 곧 레벨이다.")]
        public List<SeasonPassLevel> levels = new List<SeasonPassLevel>();

        public int MaxLevel => levels != null ? levels.Count : 0;

        /// <summary>그 레벨(1부터)의 보상. 범위를 벗어나면 null.</summary>
        public SeasonPassLevel GetLevel(int level)
        {
            if (levels == null || level < 1 || level > levels.Count)
                return null;

            return levels[level - 1];
        }
    }
}
