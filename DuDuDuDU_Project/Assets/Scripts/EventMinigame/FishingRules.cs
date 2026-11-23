using System;

namespace OJ.EventMinigame
{
    /// <summary>타이밍 입력 판정.</summary>
    public enum FishTiming
    {
        Miss = 0,
        Good = 1,
        Perfect = 2,
    }

    /// <summary>
    /// 낚시 이벤트 규칙. <b>UnityEngine 도 싱글톤도 쓰지 않는다</b> — 난수는 호출부가 굴린 <c>roll</c>(0~1) 로 받는다
    /// (<c>PinballRules.DrawSlot</c> 과 같은 관례). 확률은 전부 정수 ‰ 라 헤드리스 러너(Mono)와 기기가 같은 답을 낸다.
    ///
    /// 설계 기록: <c>plans/3-purrfect-seahorse.md</c> "낚시 세부 설계".
    /// - 점수는 한 마리 점수 × 배율(한 마리를 낚고 점수만 N배).
    /// - 낚싯대 레벨이 오를수록 좋은 등급이 잘 나온다. 평균 점수 85/95/105/115 → 재화 600~850개에 평균 약 10배.
    /// - 특별 떡밥(낚싯대 1개당 0.3%)은 원본 그대로 10분 행운 +1. 혼자서는 드물어 평균 점수에는 넣지 않는다(덤).
    /// </summary>
    public static class FishingRules
    {
        public const int TierCount = 4;
        public const int MaxRodLevel = 4;

        /// <summary>낚싯대 1개의 평균 점수 기준(10배 보상 환산 단위). 초과 점수·표 설계가 이 값을 쓴다.</summary>
        public const int PointsPerCurrency = 100;

        /// <summary>떡밥이 터질 확률(‰). 낚싯대 1개당 판정한다.</summary>
        public const int BaitChancePermille = 3;

        public const int MaxLuckStacks = 4;
        public const int LuckSeconds = 600;

        public static readonly int[] Multipliers = { 1, 10, 100 };

        /// <summary>등급별 한 마리 평균 점수(무게·타이밍 배율 전). 일반 / 희귀 / 영웅 / 전설.</summary>
        public static readonly int[] TierBaseScore = { 30, 100, 300, 1500 };

        /// <summary>낚싯대 레벨(1~4)별 등급 확률(‰). 각 줄의 합은 1000.</summary>
        public static readonly int[][] TierWeightPermille =
        {
            new[] { 685, 230, 75, 10 },
            new[] { 661, 240, 85, 14 },
            new[] { 637, 250, 95, 18 },
            new[] { 613, 260, 105, 22 },
        };

        /// <summary>낚싯대 레벨 n+1 에 닿는 누적 점수. 첫 칸(0)은 Lv1.</summary>
        public static readonly int[] RodLevelScore = { 0, 10000, 30000, 70000 };

        /// <summary>무게 등급별 배율(‰). 평균(가중)이 정확히 1000 이 되게 맞췄다.</summary>
        public static readonly int[] WeightFactorPermille = { 500, 800, 1000, 1400, 2800 };

        /// <summary>무게 등급이 나올 확률(‰). 합 1000.</summary>
        public static readonly int[] WeightClassPermille = { 220, 250, 305, 175, 50 };

        /// <summary>타이밍 배율(‰). 미스 / 굿 / 퍼펙트.</summary>
        public static readonly int[] TimingFactorPermille = { 500, 1000, 1500 };

        /// <summary>평균 플레이어의 판정 분포(‰). 평균 배율 1.05 — 표와 평균 점수가 이 가정으로 맞춰져 있다.</summary>
        public static readonly int[] AssumedTimingPermille = { 200, 500, 300 };

        public const int SpeciesPerTier = 3;

        /// <summary>어종 이름. <b>임시 이름이다</b> — 사용자 확인 전.</summary>
        public static readonly string[][] SpeciesNames =
        {
            new[] { "피라미", "붕어", "메기" },
            new[] { "잉어", "장어", "가물치" },
            new[] { "철갑상어", "황금 잉어", "큰 연어" },
            new[] { "고대 용어", "심해 아귀", "전설의 청새치" },
        };

        /// <summary>어종 기준 무게(g). 무게 등급 배율이 곱해진다.</summary>
        public static readonly int[][] SpeciesBaseGrams =
        {
            new[] { 300, 1500, 3000 },
            new[] { 6000, 4500, 8000 },
            new[] { 40000, 25000, 15000 },
            new[] { 250000, 400000, 600000 },
        };

        /// <summary>낚싯대 레벨(1~4). 누적 점수로 정해진다.</summary>
        public static int RodLevelFromScore(long score)
        {
            int level = 1;
            for (int i = 1; i < RodLevelScore.Length; i++)
            {
                if (score >= RodLevelScore[i])
                    level = i + 1;
            }

            return level;
        }

        /// <summary>행운 단계만큼 등급표를 한 칸씩 올려 쓴다. 최고 레벨 표에서 멈춘다.</summary>
        public static int EffectiveLevel(int rodLevel, int luckStacks)
        {
            int level = rodLevel < 1 ? 1 : rodLevel;
            int luck = luckStacks < 0 ? 0 : luckStacks;
            long sum = (long)level + luck;
            return sum > MaxRodLevel ? MaxRodLevel : (int)sum;
        }

        /// <summary>등급(0~3)을 뽑는다. <paramref name="level"/> 은 1~4(범위 밖은 가둔다).</summary>
        public static int DrawTier(int level, float roll)
        {
            int index = level < 1 ? 0 : (level > MaxRodLevel ? MaxRodLevel - 1 : level - 1);
            return DrawPermille(TierWeightPermille[index], roll);
        }

        public static int DrawSpecies(float roll)
        {
            int index = (int)(roll * SpeciesPerTier);
            if (index < 0)
                index = 0;
            if (index >= SpeciesPerTier)
                index = SpeciesPerTier - 1;
            return index;
        }

        public static int DrawWeightClass(float roll)
        {
            return DrawPermille(WeightClassPermille, roll);
        }

        /// <summary>퍼펙트로 치는 구간의 반폭(0~1 위치 기준, 가운데 0.5 에서 ±0.15 = 전체의 30%).</summary>
        public const float PerfectHalfWidth = 0.15f;

        /// <summary>굿으로 치는 구간의 반폭(±0.40 = 전체의 80%, 퍼펙트 바깥 50%).</summary>
        public const float GoodHalfWidth = 0.40f;

        /// <summary>
        /// 게이지 위 표식의 위치(0~1)로 타이밍을 판정한다. <b>표식이 일정한 속도로 왕복하면 위치가 시간에 대해 고르게 퍼지므로</b>
        /// 아무렇게나 눌러도 퍼펙트 30% · 굿 50% · 미스 20% 이고, 그것이 <see cref="AssumedTimingPermille"/> 과 같다 —
        /// 평균 점수 장부가 이 가정 위에 서 있다. 구간 폭을 바꾸면 그 가정도 같이 바꿔야 한다.
        /// </summary>
        public static FishTiming TimingFromPosition(float position01)
        {
            float distance = position01 - 0.5f;
            if (distance < 0f)
                distance = -distance;

            if (distance <= PerfectHalfWidth)
                return FishTiming.Perfect;

            return distance <= GoodHalfWidth ? FishTiming.Good : FishTiming.Miss;
        }

        /// <summary>한 마리의 점수. 등급 점수 × 무게 배율 × 타이밍 배율(반올림).</summary>
        public static int FishScore(int tier, int weightClass, FishTiming timing)
        {
            int t = Clamp(tier, 0, TierCount - 1);
            int w = Clamp(weightClass, 0, WeightFactorPermille.Length - 1);
            int m = Clamp((int)timing, 0, TimingFactorPermille.Length - 1);

            long numerator = (long)TierBaseScore[t] * WeightFactorPermille[w] * TimingFactorPermille[m];
            return (int)((numerator + 500000L) / 1000000L);
        }

        /// <summary>
        /// 한 번 던진 점수. <b>한 마리 점수에 배율을 곱한다</b>(배율 N 은 낚싯대 N개를 쓰고 점수가 N배).
        /// 넘치면 long 최대에서 멈춘다.
        /// </summary>
        public static long CastScore(int fishScore, int multiplier)
        {
            if (fishScore <= 0 || multiplier <= 0)
                return 0;

            return (long)fishScore * multiplier;
        }

        /// <summary>
        /// 낚싯대 <paramref name="rods"/> 개를 쓴 한 번의 낚시에서 터진 떡밥 수. 낚싯대마다 따로 판정한다.
        /// </summary>
        public static int BaitSuccesses(int rods, Func<float> roll)
        {
            if (rods <= 0 || roll == null)
                return 0;

            int hits = 0;
            for (int i = 0; i < rods; i++)
            {
                if ((int)(roll() * 1000f) < BaitChancePermille)
                    hits++;
            }

            return hits;
        }

        public static int AddLuck(int current, int baits)
        {
            long sum = (long)(current < 0 ? 0 : current) + (baits < 0 ? 0 : baits);
            return sum > MaxLuckStacks ? MaxLuckStacks : (int)sum;
        }

        /// <summary>어종 id(저장 키). 등급 × 10 + 어종 번호.</summary>
        public static int SpeciesId(int tier, int species)
        {
            return tier * 10 + species;
        }

        /// <summary>무게(g). 어종 기준 × 무게 등급 × 지터(90~110%).</summary>
        public static long WeightGrams(int tier, int species, int weightClass, float jitterRoll)
        {
            int t = Clamp(tier, 0, TierCount - 1);
            int s = Clamp(species, 0, SpeciesPerTier - 1);
            int w = Clamp(weightClass, 0, WeightFactorPermille.Length - 1);

            int jitter = 900 + (int)(Clamp01(jitterRoll) * 200f);
            if (jitter > 1100)
                jitter = 1100;

            long grams = (long)SpeciesBaseGrams[t][s] * WeightFactorPermille[w] / 1000L;
            return grams * jitter / 1000L;
        }

        /// <summary>"상위 N%" 표시용: 이 무게 등급 이상이 나올 확률(‰). 신기록 연출에 쓴다.</summary>
        public static int TopPermille(int weightClass)
        {
            int w = Clamp(weightClass, 0, WeightClassPermille.Length - 1);
            int sum = 0;
            for (int i = w; i < WeightClassPermille.Length; i++)
                sum += WeightClassPermille[i];
            return sum;
        }

        /// <summary>
        /// 낚싯대 레벨 <paramref name="level"/> 에서 평균 플레이어가 낚싯대 1개로 얻는 점수의 천 배.
        /// 등급 × 무게 × 타이밍을 전부 세어 정수로 구한다 — 표와 레벨 구간이 10배에 맞는지 테스트가 이 값을 본다.
        /// </summary>
        public static long ExpectedScorePerRodMilli(int level)
        {
            int index = level < 1 ? 0 : (level > MaxRodLevel ? MaxRodLevel - 1 : level - 1);
            int[] tierP = TierWeightPermille[index];

            long total = 0;
            for (int t = 0; t < TierCount; t++)
            {
                for (int w = 0; w < WeightClassPermille.Length; w++)
                {
                    for (int m = 0; m < TimingFactorPermille.Length; m++)
                    {
                        long p = (long)tierP[t] * WeightClassPermille[w] * AssumedTimingPermille[m];
                        total += p * FishScore(t, w, (FishTiming)m);
                    }
                }
            }

            // p 의 분모는 1000^3 이고 결과를 ×1000 하므로 1000^2 로 나눈다.
            return total / 1000000L;
        }

        private static int DrawPermille(int[] weights, float roll)
        {
            int total = 0;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];

            int target = (int)(roll * total);
            if (target < 0)
                target = 0;
            if (target >= total)
                target = total - 1;

            int accumulated = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                accumulated += weights[i];
                if (target < accumulated)
                    return i;
            }

            return weights.Length - 1;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }
    }
}
