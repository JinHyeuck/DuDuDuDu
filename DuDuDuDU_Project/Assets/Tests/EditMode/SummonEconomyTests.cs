using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 소환 경제의 목표를 잠근다(2026-10-25): <b>웨이브당 소환 클릭 수가 초반 2회에서 후반 4회로</b> 늘고,
    /// 성급은 오를 때마다 데미지가 3배다. 1스테이지 값(시작 SP 20 · 웨이브 클리어 SP 115 · 15웨이브 · 소환 비용 10 에서 시작해 2회마다 +1)으로 센다.
    /// </summary>
    public sealed class SummonEconomyTests
    {
        [SetUp]
        public void TurnOn() => BalanceProfile.Active = true;

        [TearDown]
        public void TurnOff() => BalanceProfile.Active = false;

        private const int StartSp = 20;
        private const int WaveClearSp = 115;
        private const int Waves = 15;

        /// <summary>웨이브마다 그 웨이브에 쓸 수 있는 SP 로 몇 번 소환하는가(앞 웨이브에서 번 SP 는 다음 웨이브에 쓴다).</summary>
        private static int[] ClicksPerWave()
        {
            var clicks = new int[Waves];
            int sp = StartSp;
            int cost = 10;
            int done = 0;
            for (int w = 1; w <= Waves; w++)
            {
                while (sp >= cost)
                {
                    sp -= cost;
                    clicks[w - 1]++;
                    done++;
                    if (done % 2 == 0)
                        cost++;
                }

                // 이 웨이브를 막으면 SP 가 들어온다 — 다음 웨이브에 쓴다.
                sp += WaveSpCurve.WaveClearSp(WaveClearSp, w, Waves, true);
            }

            return clicks;
        }

        [Test]
        public void ClicksStartAtTwoAndEndAtFour()
        {
            int[] clicks = ClicksPerWave();
            Assert.AreEqual(2, clicks[0], "1웨이브");
            Assert.AreEqual(2, clicks[1], "2웨이브");
            Assert.GreaterOrEqual(clicks[Waves - 1], 4, "15웨이브");
            Assert.LessOrEqual(clicks[Waves - 1], 5, "15웨이브");
        }

        [Test]
        public void ClicksNeverDropAndStayInsideTwoToFive()
        {
            int[] clicks = ClicksPerWave();
            for (int w = 0; w < Waves; w++)
            {
                Assert.GreaterOrEqual(clicks[w], 2, (w + 1) + "웨이브");
                Assert.LessOrEqual(clicks[w], 5, (w + 1) + "웨이브");
                if (w > 0)
                    Assert.GreaterOrEqual(clicks[w] + 1, clicks[w - 1], (w + 1) + "웨이브가 앞보다 두 번 넘게 줄었다");
            }
        }

        [Test]
        public void TotalSummonsAreAboutFortyFive()
        {
            int total = 0;
            foreach (int c in ClicksPerWave())
                total += c;

            Assert.GreaterOrEqual(total, 40);
            Assert.LessOrEqual(total, 52);
        }

        [Test]
        public void FifteenWaveTableMatchesTheGeneratedOne()
        {
            Assert.AreEqual(new[] { 19, 21, 23, 37, 41, 45, 49, 53, 57, 61, 87, 94, 101, 108, 108 }, WaveSpCurve.TableV2For(15));
            for (int w = 1; w <= 15; w++)
                Assert.AreEqual(WaveSpCurve.TableV2For(15)[w - 1], WaveSpCurve.Multiplier(w, 15));
        }

        [Test]
        public void ThirtyWaveStageKeepsTwoToFourClicks()
        {
            int[] table = WaveSpCurve.TableV2For(30);
            int sp = StartSp;
            int cost = 10;
            int done = 0;
            int first = 0, last = 0;
            for (int w = 1; w <= 30; w++)
            {
                int clicks = 0;
                while (sp >= cost)
                {
                    sp -= cost;
                    clicks++;
                    done++;
                    if (done % 2 == 0)
                        cost++;
                }

                if (w == 1) first = clicks;
                if (w == 30) last = clicks;
                sp += WaveSpCurve.WaveClearSp(WaveClearSp, w, 30, true);
            }

            Assert.AreEqual(30, table.Length);
            Assert.AreEqual(2, first);
            Assert.GreaterOrEqual(last, 4);
            Assert.LessOrEqual(last, 5);
            Assert.GreaterOrEqual(done, 80);
            Assert.LessOrEqual(done, 100);
        }

        /// <summary>소환 비용은 오를 때마다 +2 다. 꺼져 있으면 항상 +1.</summary>
        [TestCase(10, 2)]
        [TestCase(24, 2)]
        [TestCase(60, 2)]
        public void SummonCostStepGrowsWithCost(int cost, int expected)
        {
            Assert.AreEqual(expected, BalanceProfile.SummonCostStep(cost));

            BalanceProfile.Active = false;
            Assert.AreEqual(1, BalanceProfile.SummonCostStep(cost));
        }

        /// <summary>처음 두 번은 10 SP 씩이고, 그 뒤로는 소환할 때마다 +2 다.</summary>
        [Test]
        public void SummonCostIsFlatTwiceThenRisesEverySummon()
        {
            int cost = 10;
            int sinceIncrease = 0;
            var paid = new System.Collections.Generic.List<int>();
            for (int n = 1; n <= 40; n++)
            {
                paid.Add(cost);
                sinceIncrease++;
                if (sinceIncrease >= BalanceProfile.SummonsPerCostIncrease(cost, 2))
                {
                    cost += BalanceProfile.SummonCostStep(cost);
                    sinceIncrease = 0;
                }
            }

            CollectionAssert.AreEqual(new[] { 10, 10, 12, 14, 16, 18 }, paid.GetRange(0, 6));
            Assert.AreEqual(86, paid[39], "마흔 번째 소환 비용");

            BalanceProfile.Active = false;
            Assert.AreEqual(2, BalanceProfile.SummonsPerCostIncrease(30, 2));
        }

        [Test]
        public void CooldownIsShorterOnlyWhenOn()
        {
            Assert.Less(BalanceProfile.CooldownMultiplier, 1f);
            Assert.Greater(BalanceProfile.CooldownMultiplier, 0.3f);
        }

        [Test]
        public void OffKeepsOldRules()
        {
            BalanceProfile.Active = false;
            Assert.AreEqual(50, SlotUnlockRules.Cost(0));
            Assert.AreEqual(30, SummonStarRules.UpgradeCost(1));
            Assert.AreEqual(1f, BalanceProfile.TierMultiplier(2));
        }

        [Test]
        public void OnLowersCostsAndBoostsRareAndKing()
        {
            Assert.AreEqual(30, SlotUnlockRules.Cost(0));
            Assert.AreEqual(25, SummonStarRules.UpgradeCost(1));
            Assert.AreEqual(40, SummonStarRules.UpgradeCost(2));
            Assert.AreEqual(1f, BalanceProfile.TierMultiplier(0));
            Assert.AreEqual(3f, BalanceProfile.TierMultiplier(1));
            Assert.AreEqual(2.55f, BalanceProfile.TierMultiplier(2));
        }

        [TestCase(1, 1f)]
        [TestCase(2, 3f)]
        [TestCase(3, 9f)]
        [TestCase(4, 27f)]
        [TestCase(0, 1f)]
        public void StarMultiplierTriplesPerStar(int star, float expected)
        {
            Assert.AreEqual(expected, DamageFormula.StarMultiplier(star));
        }
    }
}
