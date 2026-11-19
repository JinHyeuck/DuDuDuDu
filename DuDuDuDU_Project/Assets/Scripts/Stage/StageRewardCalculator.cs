using System.Collections.Generic;
using OJ.Core;
using UnityEngine;
using OJ.Point;

namespace OJ.Stage
{
    public static class StageRewardCalculator
    {
        private static readonly PointType[] EquipmentScrollTypes =
        {
            PointType.WeaponScroll,
            PointType.HelmetScroll,
            PointType.ArmorScroll,
            PointType.RingScroll,
            PointType.ShoesScroll,
            PointType.NecklaceScroll,
        };

        // 클리어 1회가 주던 원소 스크롤(2 / 4)은 카피 개편(2026-11)으로 없앴다. 속성 스크롤은 이제 기본 카피이고
        // 반복 수급처는 핀볼 하나다 — 하루 96회 클리어에 6장씩이면 하루 약 576장이라 기본 카피 하루 60장이라는
        // 장부와 맞지 않는다. 다시 넣을 때는 확률 드롭으로, 하루 합계가 장부 안에 들어가는지부터 볼 것.

        /// <summary>
        /// 클리어 1회가 주는 핀볼 티켓.
        ///
        /// <b>여기가 티켓의 유일한 수급처다.</b> 이 줄이 생기기 전에는 프로젝트 어디에서도
        /// <c>PointType.PinballTicket</c> 을 주지 않아서, 치트 말고는 핀볼을 돌릴 수 없었다
        /// (재화 표는 대표 수급처를 StageClear 로 적어 두었는데 구현이 비어 있었다).
        ///
        /// 12장인 근거(2026-11): 가치 장부가 <b>하루 2,000발</b>을 기준으로 잡혀 있다. 하루 클리어 168회(방치 24 + 소탕 144)에
        /// 12장씩이면 하루 2,016발이다. 고기 루프의 r = 하루 발수 × 발당 고기 ÷ 소탕이 태우는 고기(고기 축제 하루 720) 이므로
        /// 발당 고기를 1.24 → 0.11 로 내려야 r 이 0.31 에 머문다
        /// (<c>PinballRewardDatabase.StaminaPerSlot</c>). 이 셋은 한 묶음이라 하나만 고치면 r 이 어긋난다
        /// (<c>Docs/CurrencyPolicy.md</c> 8장, <c>SweepEconomyTests</c> 가 잠근다).
        /// </summary>
        public const int PinballTicketPerClear = 12;

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

            // 속성 스크롤(기본 카피)은 주지 않는다 — AddAutoBattleClearRewards 의 같은 자리 주석 참고.
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

            // 속성 스크롤(= 기본 카피)은 여기서 주지 않는다. 카피 개편(2026-11)으로 기본 카피의 반복 수급처는
            // 핀볼 하나다 — 소탕·방치가 클리어마다(하루 96회) 스크롤을 주던 시절의 양(하루 약 576장)은 기본 카피
            // 하루 60장이라는 장부와 맞지 않아 통째로 뺐다.
            //
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
