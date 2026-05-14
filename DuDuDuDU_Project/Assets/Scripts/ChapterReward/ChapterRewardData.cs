using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public enum ChapterRewardState
    {
        Locked = 0,
        Claimable,
        Claimed,
    }

    [Serializable]
    public struct ChapterRewardEntry
    {
        public PointType pointType;
        [Min(1)] public int amount;

        public ChapterRewardEntry(PointType pointType, int amount)
        {
            this.pointType = pointType;
            this.amount = Mathf.Max(1, amount);
        }

        public StageRewardEntry ToStageRewardEntry()
        {
            return new StageRewardEntry(pointType, amount);
        }
    }

    [Serializable]
    public class ChapterRewardMilestone
    {
        [Tooltip("비워두면 chapterIndex_stage_wave 형식으로 자동 생성됩니다.")]
        public string id;
        [Min(1)] public int chapterIndex = 1;
        [Min(1)] public int requiredStageIndex = 1;
        [Min(1)] public int requiredWaveIndex = 1;
        public List<ChapterRewardEntry> rewards = new List<ChapterRewardEntry>();

        public string StableId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(id))
                    return id;

                return string.Format("{0}_{1}_{2}", chapterIndex, requiredStageIndex, requiredWaveIndex);
            }
        }

        public string ShortRequirementText
        {
            get { return string.Format("스테이지{0}", requiredWaveIndex); }
        }

        public string RequirementText
        {
            get { return string.Format("메인{0}-{1} 클리어 시 수령 가능", requiredStageIndex, requiredWaveIndex); }
        }
    }
}
