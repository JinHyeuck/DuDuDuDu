using System;
using System.Collections.Generic;
using UnityEngine;
using OJ.Point;

namespace OJ.Mission
{
    /// <summary>
    /// 미션·업적 보상 한 덩이. <c>PointRewardEntry</c> 는 직렬화 속성이 없어 여기서 따로 둔다
    /// (<c>OJ.Pinball.PinballReward</c> 와 같은 사정이다).
    /// </summary>
    [Serializable]
    public struct MissionReward
    {
        public PointType pointType;

        [Min(1)] public int amount;

        public MissionReward(PointType pointType, int amount)
        {
            this.pointType = pointType;
            this.amount = Mathf.Max(1, amount);
        }

        public PointRewardEntry ToPointRewardEntry()
        {
            return new PointRewardEntry(pointType, amount);
        }

        /// <summary>목록을 지급 가능한 형태로 옮긴다. null 과 0 이하는 걸러진다.</summary>
        public static List<PointRewardEntry> ToPointRewards(IReadOnlyList<MissionReward> rewards)
        {
            var list = new List<PointRewardEntry>();
            if (rewards == null)
                return list;

            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].amount <= 0)
                    continue;

                list.Add(rewards[i].ToPointRewardEntry());
            }

            return list;
        }
    }
}
