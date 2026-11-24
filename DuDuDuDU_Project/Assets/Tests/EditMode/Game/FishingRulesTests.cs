using NUnit.Framework;
using OJ.EventMinigame;

namespace OJ.Game.Tests
{
    /// <summary>낚시 규칙: 등급표, 점수, 낚싯대 레벨, 떡밥, 평균 10배.</summary>
    public sealed class FishingRulesTests
    {
        [Test]
        public void 레벨별_등급_확률의_합은_1000이다()
        {
            for (int level = 0; level < FishingRules.MaxRodLevel; level++)
            {
                int sum = 0;
                foreach (int p in FishingRules.TierWeightPermille[level])
                    sum += p;

                Assert.AreEqual(1000, sum, "level " + (level + 1));
            }
        }

        [Test]
        public void 무게_등급_확률의_합은_1000이고_평균_배율은_정확히_1이다()
        {
            int sum = 0;
            long weighted = 0;
            for (int i = 0; i < FishingRules.WeightClassPermille.Length; i++)
            {
                sum += FishingRules.WeightClassPermille[i];
                weighted += (long)FishingRules.WeightClassPermille[i] * FishingRules.WeightFactorPermille[i];
            }

            Assert.AreEqual(1000, sum);
            Assert.AreEqual(1000000L, weighted);
        }

        [Test]
        public void 타이밍_가정_분포의_합은_1000이다()
        {
            int sum = 0;
            foreach (int p in FishingRules.AssumedTimingPermille)
                sum += p;

            Assert.AreEqual(1000, sum);
        }

        [Test]
        public void 높은_레벨일수록_좋은_등급이_잘_나온다()
        {
            for (int level = 1; level < FishingRules.MaxRodLevel; level++)
            {
                Assert.Less(FishingRules.TierWeightPermille[level][0], FishingRules.TierWeightPermille[level - 1][0], "일반 level " + (level + 1));
                Assert.Greater(FishingRules.TierWeightPermille[level][3], FishingRules.TierWeightPermille[level - 1][3], "전설 level " + (level + 1));
            }
        }

        [Test]
        public void 낚싯대_레벨은_누적_점수_구간으로_정해진다()
        {
            Assert.AreEqual(1, FishingRules.RodLevelFromScore(0));
            Assert.AreEqual(1, FishingRules.RodLevelFromScore(9999));
            Assert.AreEqual(2, FishingRules.RodLevelFromScore(10000));
            Assert.AreEqual(2, FishingRules.RodLevelFromScore(29999));
            Assert.AreEqual(3, FishingRules.RodLevelFromScore(30000));
            Assert.AreEqual(3, FishingRules.RodLevelFromScore(69999));
            Assert.AreEqual(4, FishingRules.RodLevelFromScore(70000));
            Assert.AreEqual(4, FishingRules.RodLevelFromScore(long.MaxValue));
        }

        [Test]
        public void 행운은_표를_한_칸씩_올리되_최고_레벨에서_멈춘다()
        {
            Assert.AreEqual(1, FishingRules.EffectiveLevel(1, 0));
            Assert.AreEqual(3, FishingRules.EffectiveLevel(1, 2));
            Assert.AreEqual(4, FishingRules.EffectiveLevel(3, 5));
            Assert.AreEqual(1, FishingRules.EffectiveLevel(0, -3));
        }

        [Test]
        public void 등급_추첨은_구간대로_뽑는다()
        {
            // 레벨 1: 일반 0~684 / 희귀 685~914 / 영웅 915~989 / 전설 990~999
            Assert.AreEqual(0, FishingRules.DrawTier(1, 0f));
            Assert.AreEqual(0, FishingRules.DrawTier(1, 0.6f));
            Assert.AreEqual(1, FishingRules.DrawTier(1, 0.7f));
            Assert.AreEqual(2, FishingRules.DrawTier(1, 0.92f));
            Assert.AreEqual(3, FishingRules.DrawTier(1, 0.995f));
        }

        [Test]
        public void 등급_추첨은_범위_밖_난수와_레벨을_가둔다()
        {
            Assert.AreEqual(3, FishingRules.DrawTier(1, 1f));
            Assert.AreEqual(3, FishingRules.DrawTier(1, 5f));
            Assert.AreEqual(0, FishingRules.DrawTier(1, -1f));
            Assert.AreEqual(FishingRules.DrawTier(1, 0.7f), FishingRules.DrawTier(-3, 0.7f));
            Assert.AreEqual(FishingRules.DrawTier(4, 0.7f), FishingRules.DrawTier(99, 0.7f));
        }

        [Test]
        public void 어종_추첨은_고르게_가두어_뽑는다()
        {
            Assert.AreEqual(0, FishingRules.DrawSpecies(0f));
            Assert.AreEqual(1, FishingRules.DrawSpecies(0.5f));
            Assert.AreEqual(2, FishingRules.DrawSpecies(0.99f));
            Assert.AreEqual(2, FishingRules.DrawSpecies(1f));
            Assert.AreEqual(0, FishingRules.DrawSpecies(-0.5f));
        }

        [Test]
        public void 한_마리_점수는_등급_무게_타이밍을_곱해_반올림한다()
        {
            Assert.AreEqual(30, FishingRules.FishScore(0, 2, FishTiming.Good));
            Assert.AreEqual(6300, FishingRules.FishScore(3, 4, FishTiming.Perfect));
            Assert.AreEqual(375, FishingRules.FishScore(3, 0, FishTiming.Miss));

            // 30 × 0.5 × 0.5 = 7.5 → 반올림 8
            Assert.AreEqual(8, FishingRules.FishScore(0, 0, FishTiming.Miss));
        }

        [Test]
        public void 한_마리_점수는_범위_밖_인자에도_터지지_않는다()
        {
            Assert.AreEqual(FishingRules.FishScore(3, 4, FishTiming.Perfect), FishingRules.FishScore(99, 99, (FishTiming)99));
            Assert.AreEqual(FishingRules.FishScore(0, 0, FishTiming.Miss), FishingRules.FishScore(-1, -1, (FishTiming)(-1)));
        }

        [Test]
        public void 배율은_한_마리_점수에_곱한다()
        {
            Assert.AreEqual(30000L, FishingRules.CastScore(300, 100));
            Assert.AreEqual(300L, FishingRules.CastScore(300, 1));
            Assert.AreEqual(0L, FishingRules.CastScore(0, 10));
            Assert.AreEqual(0L, FishingRules.CastScore(300, 0));
        }

        [Test]
        public void 떡밥은_낚싯대마다_따로_판정한다()
        {
            Assert.AreEqual(100, FishingRules.BaitSuccesses(100, () => 0.002f));
            Assert.AreEqual(0, FishingRules.BaitSuccesses(100, () => 0.004f));
            Assert.AreEqual(0, FishingRules.BaitSuccesses(100, () => 0.5f));
        }

        [Test]
        public void 떡밥_판정은_낚싯대가_없거나_난수원이_없으면_0이다()
        {
            Assert.AreEqual(0, FishingRules.BaitSuccesses(0, () => 0f));
            Assert.AreEqual(0, FishingRules.BaitSuccesses(-3, () => 0f));
            Assert.AreEqual(0, FishingRules.BaitSuccesses(10, null));
        }

        [Test]
        public void 떡밥_행운은_4단계에서_멈춘다()
        {
            Assert.AreEqual(1, FishingRules.AddLuck(0, 1));
            Assert.AreEqual(4, FishingRules.AddLuck(3, 5));
            Assert.AreEqual(2, FishingRules.AddLuck(-1, 2));
            Assert.AreEqual(2, FishingRules.AddLuck(2, -4));
        }

        [Test]
        public void 무게는_어종_기준에_등급_배율과_지터를_곱한다()
        {
            // 피라미 300g × 1.0 × 0.9 / 1.1
            Assert.AreEqual(270L, FishingRules.WeightGrams(0, 0, 2, 0f));
            Assert.AreEqual(330L, FishingRules.WeightGrams(0, 0, 2, 1f));
        }

        [Test]
        public void 상위_퍼센트는_그_무게_등급_이상의_확률이다()
        {
            Assert.AreEqual(1000, FishingRules.TopPermille(0));
            Assert.AreEqual(225, FishingRules.TopPermille(3));
            Assert.AreEqual(50, FishingRules.TopPermille(4));
        }

        [Test]
        public void 어종_id는_등급과_어종으로_정해진다()
        {
            Assert.AreEqual(0, FishingRules.SpeciesId(0, 0));
            Assert.AreEqual(12, FishingRules.SpeciesId(1, 2));
            Assert.AreEqual(32, FishingRules.SpeciesId(3, 2));
        }

        [Test]
        public void 어종_표는_등급마다_어종이_빠짐없이_있다()
        {
            Assert.AreEqual(FishingRules.TierCount, FishingRules.SpeciesNames.Length);
            Assert.AreEqual(FishingRules.TierCount, FishingRules.SpeciesBaseGrams.Length);
            for (int t = 0; t < FishingRules.TierCount; t++)
            {
                Assert.AreEqual(FishingRules.SpeciesPerTier, FishingRules.SpeciesNames[t].Length, "tier " + t);
                Assert.AreEqual(FishingRules.SpeciesPerTier, FishingRules.SpeciesBaseGrams[t].Length, "tier " + t);
            }
        }

        /// <summary>낚싯대 레벨 1~4 의 평균 점수 목표 85/95/105/115 (±3%). 정수로 전부 세어 구한 값이다.</summary>
        [TestCase(1, 85000)]
        [TestCase(2, 95000)]
        [TestCase(3, 105000)]
        [TestCase(4, 115000)]
        public void 레벨별_평균_점수가_목표에_맞다(int level, int targetMilli)
        {
            long milli = FishingRules.ExpectedScorePerRodMilli(level);

            Assert.That(milli, Is.EqualTo((long)targetMilli).Within(targetMilli * 3 / 100), "level " + level);
        }

        /// <summary>
        /// 재화를 600개(무료)·850개(무료 + 하루 1회 구매 1번)를 쓸 때 평균 보상이 쓴 가치의 10배 근처인가.
        /// 낚싯대 레벨이 점수로 오르는 경로를 평균 점수로 따라간다.
        /// </summary>
        [TestCase(600)]
        [TestCase(850)]
        public void 재화를_다_쓰면_평균_보상이_10배_근처다(int rods)
        {
            long scoreMilli = 0;
            for (int i = 0; i < rods; i++)
            {
                int level = FishingRules.RodLevelFromScore(scoreMilli / 1000);
                scoreMilli += FishingRules.ExpectedScorePerRodMilli(level);
            }

            // 점수 1점 = 20데시원, 재화 1개 = 200데시원 → 배수 = 점수 × 20 / (rods × 200).
            long valueDeci = scoreMilli / 1000 * 20;
            long spentDeci = (long)rods * EventLedger.CurrencyValue;
            long multiplierTenths = valueDeci * 10 / spentDeci;

            Assert.That(multiplierTenths, Is.InRange(95L, 105L), "rods " + rods);
        }
    }
}
