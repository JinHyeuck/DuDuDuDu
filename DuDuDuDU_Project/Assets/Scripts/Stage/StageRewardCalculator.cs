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

        /// <summary>
        /// 클리어 1회가 주는 핀볼 티켓.
        ///
        /// <b>여기가 티켓의 유일한 수급처다.</b> 이 줄이 생기기 전에는 프로젝트 어디에서도
        /// <c>PointType.PinballTicket</c> 을 주지 않아서, 치트 말고는 핀볼을 돌릴 수 없었다
        /// (재화 표는 대표 수급처를 StageClear 로 적어 두었는데 구현이 비어 있었다).
        ///
        /// 1장인 근거: 하루 클리어 96회(방치 24 + 소탕 72)에 1장씩이면 <b>하루 96발</b>이고,
        /// 정책 4장의 r = 0.3 을 맞추려면 핀볼이 고기 108개를 돌려줘야 하므로
        /// 발당 기댓값이 약 1.1 이 된다. 이 셋은 한 묶음이라 하나만 고치면 r 이 어긋난다
        /// (<c>Docs/CurrencyPolicy.md</c> 8장).
        /// </summary>
        public const int PinballTicketPerClear = 1;

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
            AddDistinctRewards(rewards, EquipmentScrollTypes, new[] { 3 });
            rewards.Add(new PointRewardEntry(PointType.PinballTicket, PinballTicketPerClear));
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
                rewards.Add(new PointRewardEntry(PointType.MythicStone, 25));
            }

            if ((rewardFlags & StageRewardTierFlags.Perfect) != 0)
            {
                rewards.Add(new PointRewardEntry(PointType.Gold, 500 + StageRewardFormula.StageBonus(stageIndex)));
                AddDistinctRewards(rewards, ElementScrollTypes, new[] { 50, 50, 50 });
                rewards.Add(new PointRewardEntry(PointType.FreeGem, 300));
                rewards.Add(new PointRewardEntry(PointType.MythicStone, 40));
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

            // 레어석은 여기서 주지 않는다. 티어 4 재화라 재화 표상 대표 수급처가
            // Pinball_Bonus 이고(정책 2장·3.2), 소탕은 티어 2 공급처다. 예전에는 소탕이
            // 하루 720개를 뿌려서 보너스 라운드보다 다섯 배 많았고, 그러면 "상위 재화의
            // 반복 수급처는 Pinball_Bonus 하나"라는 구조가 말뿐이 된다.
            PointType equipmentType = EquipmentScrollTypes[random.Next(0, EquipmentScrollTypes.Length)];
            AddScaledReward(rewards, equipmentType, 3, multiplier);

            AddScaledReward(rewards, PointType.PinballTicket, PinballTicketPerClear, multiplier);
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
