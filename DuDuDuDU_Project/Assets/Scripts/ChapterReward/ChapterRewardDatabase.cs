using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    [CreateAssetMenu(fileName = "ChapterRewardDatabase", menuName = "Chapter Reward/Database")]
    public class ChapterRewardDatabase : ScriptableObject
    {
        [SerializeField] private List<ChapterRewardMilestone> milestones = new List<ChapterRewardMilestone>();

        public IReadOnlyList<ChapterRewardMilestone> Milestones => milestones;
        public int Count => milestones != null ? milestones.Count : 0;

        private void OnEnable()
        {
            EnsureValid();
        }

        public ChapterRewardMilestone GetMilestone(int index)
        {
            EnsureValid();

            if (index < 0 || index >= milestones.Count)
                return null;

            return milestones[index];
        }

        public int IndexOf(string milestoneId)
        {
            EnsureValid();

            if (string.IsNullOrWhiteSpace(milestoneId))
                return -1;

            for (int i = 0; i < milestones.Count; i++)
            {
                ChapterRewardMilestone milestone = milestones[i];
                if (milestone != null && milestone.StableId == milestoneId)
                    return i;
            }

            return -1;
        }

        public void PopulateDefaults(int count)
        {
            milestones.Clear();

            int safeCount = Mathf.Max(1, count);
            for (int i = 1; i <= safeCount; i++)
            {
                StageData stageData = StageDatabaseProvider.GetStage(i);
                int waveIndex = stageData != null ? Mathf.Max(1, stageData.totalWaves) : 10;

                milestones.Add(new ChapterRewardMilestone
                {
                    id = string.Format("default_{0}", i),
                    chapterIndex = i,
                    requiredStageIndex = i,
                    requiredWaveIndex = waveIndex,
                    rewards = BuildDefaultRewards(i),
                });
            }
        }

        private static List<ChapterRewardEntry> BuildDefaultRewards(int index)
        {
            int safeIndex = Mathf.Max(1, index);
            var rewards = new List<ChapterRewardEntry>
            {
                new ChapterRewardEntry(PointType.Dia, 25 + (safeIndex * 5)),
            };

            if (safeIndex % 3 == 0)
                rewards.Add(new ChapterRewardEntry(PointType.MythicScroll, 3 + safeIndex));
            else
                rewards.Add(new ChapterRewardEntry(PointType.Gold, 250 + (safeIndex * 50)));

            return rewards;
        }

        private void EnsureValid()
        {
            if (milestones == null)
                milestones = new List<ChapterRewardMilestone>();
        }
    }
}
