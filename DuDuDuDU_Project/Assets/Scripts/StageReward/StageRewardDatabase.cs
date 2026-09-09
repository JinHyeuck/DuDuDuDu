using System.Collections.Generic;
using UnityEngine;
using OJ.Stage;

namespace OJ.StageReward
{
    [CreateAssetMenu(fileName = "StageRewardDatabase", menuName = "Stage Reward/Database")]
    public class StageRewardDatabase : ScriptableObject
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

        [SerializeField] private List<StageRewardMilestone> milestones = new List<StageRewardMilestone>();

        public IReadOnlyList<StageRewardMilestone> Milestones => milestones;
        public int Count => milestones != null ? milestones.Count : 0;

        private void OnEnable()
        {
            EnsureValid();
        }

        /// <summary>
        /// 이 마일스톤이 그 스테이지의 <b>마지막</b>(요구 웨이브가 가장 높은) 칸인가.
        ///
        /// 다이스 언락이 붙는 자리를 "전 웨이브 클리어" 로 고정하기 위한 것이다.
        /// (등급 id 는 "perfect" 지만 StageClearGrade.Perfect = HP 100% 와는 다른 조건이다)
        /// <b>id 문자열로 판정하지 않는다</b> — <c>StableId</c> 는 비어 있으면 자동 생성되는
        /// 값이라 규칙이 바뀌면 조용히 죽는다. 요구 웨이브는 그 마일스톤이 무엇인지를
        /// 실제로 정하는 값이라 흔들리지 않는다.
        /// </summary>
        public bool IsFinalMilestoneOf(StageRewardMilestone milestone)
        {
            if (milestone == null)
                return false;

            EnsureValid();

            for (int i = 0; i < milestones.Count; i++)
            {
                StageRewardMilestone other = milestones[i];
                if (other == null || other.requiredStageIndex != milestone.requiredStageIndex)
                    continue;

                if (other.requiredWaveIndex > milestone.requiredWaveIndex)
                    return false;
            }

            return true;
        }

        public StageRewardMilestone GetMilestone(int index)
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
                StageRewardMilestone milestone = milestones[i];
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
                int totalWaves = stageData != null ? Mathf.Max(1, stageData.totalWaves) : 10;

                AddMilestone(i, "minimum", GetRequiredWaveIndex(totalWaves, 1f / 3f), BuildMinimumRewards(i));
                AddMilestone(i, "half", GetRequiredWaveIndex(totalWaves, 2f / 3f), BuildHalfRewards(i));
                AddMilestone(i, "perfect", totalWaves, BuildPerfectRewards(i));
            }
        }

        private void AddMilestone(int stageIndex, string tierId, int requiredWaveIndex, List<StageRewardEntry> rewards)
        {
            int safeStageIndex = Mathf.Max(1, stageIndex);
            milestones.Add(new StageRewardMilestone
            {
                id = string.Format("stage_{0}_{1}", safeStageIndex, tierId),
                stageIndex = safeStageIndex,
                requiredStageIndex = safeStageIndex,
                requiredWaveIndex = Mathf.Max(1, requiredWaveIndex),
                rewards = rewards,
            });
        }

        private static List<StageRewardEntry> BuildMinimumRewards(int stageIndex)
        {
            var rewards = new List<StageRewardEntry>
            {
                new StageRewardEntry(PointType.Gold, 300 + GetStageBonus(stageIndex)),
            };

            AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50 }, stageIndex);
            AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 10 }, stageIndex);
            return rewards;
        }

        private static List<StageRewardEntry> BuildHalfRewards(int stageIndex)
        {
            var rewards = new List<StageRewardEntry>
            {
                new StageRewardEntry(PointType.Gold, 400 + GetStageBonus(stageIndex)),
            };

            AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50 }, stageIndex);
            AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 10, 10 }, stageIndex);
            rewards.Add(new StageRewardEntry(PointType.MythicScroll, 15));
            return rewards;
        }

        private static List<StageRewardEntry> BuildPerfectRewards(int stageIndex)
        {
            var rewards = new List<StageRewardEntry>
            {
                new StageRewardEntry(PointType.Gold, 500 + GetStageBonus(stageIndex)),
            };

            AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50, 50 }, stageIndex);
            rewards.Add(new StageRewardEntry(PointType.FreeGem, 150));
            rewards.Add(new StageRewardEntry(PointType.MythicScroll, 10));

            return rewards;
        }

        private static int GetRequiredWaveIndex(int totalWaves, float ratio)
        {
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, totalWaves) * ratio), 1, Mathf.Max(1, totalWaves));
        }

        private static int GetStageBonus(int stageIndex)
        {
            return ((Mathf.Max(1, stageIndex) - 1) / 10) * 5;
        }

        private static void AddDistinctRewards(
            List<StageRewardEntry> rewards,
            PointType[] pool,
            int[] amounts,
            int stageIndex)
        {
            if (rewards == null || pool == null || amounts == null || pool.Length == 0 || amounts.Length == 0)
                return;

            int count = Mathf.Min(pool.Length, amounts.Length);
            int startIndex = (Mathf.Max(1, stageIndex) - 1) % pool.Length;
            for (int i = 0; i < count; i++)
                rewards.Add(new StageRewardEntry(pool[(startIndex + i) % pool.Length], amounts[i]));
        }

        private void EnsureValid()
        {
            if (milestones == null)
                milestones = new List<StageRewardMilestone>();
        }
    }
}
