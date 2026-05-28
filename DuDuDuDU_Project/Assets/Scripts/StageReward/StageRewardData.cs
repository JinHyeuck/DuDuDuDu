using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace OJ
{
    public enum StageRewardState
    {
        Locked = 0,
        Claimable,
        Claimed,
    }

    [Serializable]
    public struct StageRewardEntry
    {
        public PointType pointType;
        [Min(1)] public int amount;

        public StageRewardEntry(PointType pointType, int amount)
        {
            this.pointType = pointType;
            this.amount = Mathf.Max(1, amount);
        }

        public PointRewardEntry ToPointRewardEntry()
        {
            return new PointRewardEntry(pointType, amount);
        }
    }

    [Serializable]
    public class StageRewardMilestone
    {
        [Tooltip("비워두면 stageIndex_stage_wave 형식으로 자동 생성합니다.")]
        public string id;
        [FormerlySerializedAs("chapterIndex")]
        [Min(1)] public int stageIndex = 1;
        [Min(1)] public int requiredStageIndex = 1;
        [Min(1)] public int requiredWaveIndex = 1;
        public List<StageRewardEntry> rewards = new List<StageRewardEntry>();

        public string StableId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(id))
                    return id;

                return string.Format("{0}_{1}_{2}", stageIndex, requiredStageIndex, requiredWaveIndex);
            }
        }

        public string ShortRequirementText
        {
            get { return string.Format("{0}웨이브", requiredWaveIndex); }
        }

        public string RequirementText
        {
            get { return string.Format("스테이지{0}-{1} 클리어 시 수령 가능", requiredStageIndex, requiredWaveIndex); }
        }
    }
}
