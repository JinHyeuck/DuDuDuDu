using System.Collections.Generic;
using OJ.Core;
using UnityEngine;
using OJ.Point;

namespace OJ.Stage
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

        // 비율 판정은 StageRewardFormula.ClearGradeTier 로 옮겼다. StageClearGrade 는
        // Assembly-CSharp 타입이라 OJ.Core 에서 볼 수 없어 int 티어로 돌려받는다.
        // 티어(0/1/2)와 enum 값(Minimum=1, Half=2, Perfect=3)이 어긋나 있으므로
        // (StageClearGrade)tier 로 캐스팅하면 안 되고 아래처럼 명시적으로 매핑해야 한다.
        public static StageClearGrade GetClearGrade(int currentWallHp, int totalWallHp)
        {
            switch (StageRewardFormula.ClearGradeTier(currentWallHp, totalWallHp))
            {
                case 2:
                    return StageClearGrade.Perfect;
                case 1:
                    return StageClearGrade.Half;
                default:
                    return StageClearGrade.Minimum;
            }
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
            rewards.Add(new PointRewardEntry(PointType.SpecialDiceCore, Random.Range(5, 11)));
            AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 3 });
            return rewards;
        }

        public static List<PointRewardEntry> BuildAutoBattleRewards(int stageIndex, double clearCount, int seed)
        {
            var rewards = new List<PointRewardEntry>();
            if (stageIndex < 1 || clearCount <= 0d)
                return rewards;

            double safeClearCount = System.Math.Min(24d, clearCount);
            int fullClearCount = Mathf.FloorToInt((float)safeClearCount);
            float partialClearRatio = Mathf.Clamp01((float)(safeClearCount - fullClearCount));
            var random = new System.Random(seed);

            for (int i = 0; i < fullClearCount; i++)
                AddAutoBattleClearRewards(rewards, stageIndex, 1f, random);

            if (partialClearRatio > 0f)
                AddAutoBattleClearRewards(rewards, stageIndex, partialClearRatio, random);

            return PointRewardUtility.MergeRewards(rewards);
        }

        public static int GetGuaranteedNormalGold(int stageIndex)
        {
            return StageRewardFormula.GuaranteedNormalGold(stageIndex);
        }

        public static int GetAccumulatedGuaranteedGold(int stageIndex, int clearedWaves, int totalWaves)
        {
            return StageRewardFormula.AccumulatedGuaranteedGold(stageIndex, clearedWaves, totalWaves);
        }

        public static List<PointRewardEntry> ScaleRewards(IReadOnlyList<PointRewardEntry> rewards, float multiplier)
        {
            var scaledRewards = new List<PointRewardEntry>();
            if (rewards == null || rewards.Count == 0)
                return scaledRewards;

            // 원래는 Mathf.Clamp01(multiplier) 를 루프 밖으로 뽑아 뒀지만, Clamp01 은
            // 입력만 보는 순수 함수라 매 회 다시 계산해도 결과 float 비트가 같다.
            // 그래서 ScaleAmount 안으로 들어가도 산술은 그대로다.
            for (int i = 0; i < rewards.Count; i++)
            {
                PointRewardEntry reward = rewards[i];
                int scaledAmount = StageRewardFormula.ScaleAmount(reward.Amount, multiplier);
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
                rewards.Add(new PointRewardEntry(PointType.Gold, 300 + StageRewardFormula.StageBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50 });
                AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 10 });
            }

            if ((rewardFlags & StageRewardTierFlags.Half) != 0)
            {
                rewards.Add(new PointRewardEntry(PointType.Gold, 400 + StageRewardFormula.StageBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50 });
                AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 10, 10 });
                rewards.Add(new PointRewardEntry(PointType.MythicScroll, 15));
            }

            if ((rewardFlags & StageRewardTierFlags.Perfect) != 0)
            {
                rewards.Add(new PointRewardEntry(PointType.Gold, 500 + StageRewardFormula.StageBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50, 50 });
                rewards.Add(new PointRewardEntry(PointType.Dia, 150));
                rewards.Add(new PointRewardEntry(PointType.MythicScroll, 10));
            }

            return rewards;
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

        private static void AddAutoBattleClearRewards(
            List<PointRewardEntry> rewards,
            int stageIndex,
            float multiplier,
            System.Random random)
        {
            AddScaledReward(rewards, PointType.Gold, GetGuaranteedNormalGold(stageIndex), multiplier);

            PointType[] elementTypes = (PointType[])ElementScrollTypes.Clone();
            Shuffle(elementTypes, random);
            AddScaledReward(rewards, elementTypes[0], 20, multiplier);
            AddScaledReward(rewards, elementTypes[1], 40, multiplier);

            AddScaledReward(rewards, PointType.SpecialDiceCore, random.Next(5, 11), multiplier);

            PointType equipmentType = EquipmentScrollTypes[random.Next(0, EquipmentScrollTypes.Length)];
            AddScaledReward(rewards, equipmentType, 3, multiplier);
        }

        private static void AddScaledReward(
            List<PointRewardEntry> rewards,
            PointType pointType,
            int amount,
            float multiplier)
        {
            int scaledAmount = StageRewardFormula.ScaleAmount(amount, multiplier);
            if (scaledAmount > 0)
                rewards.Add(new PointRewardEntry(pointType, scaledAmount));
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

        private static void Shuffle(PointType[] values, System.Random random)
        {
            for (int i = 0; i < values.Length; i++)
            {
                int swapIndex = random.Next(i, values.Length);
                PointType temp = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = temp;
            }
        }
    }
}
