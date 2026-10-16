using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class SummonStarRulesTests
    {
        [Test]
        public void EveryLevel_ChancesSumToOneHundred()
        {
            for (int level = SummonStarRules.MinLevel; level <= SummonStarRules.MaxLevel; level++)
            {
                int sum = 0;
                for (int star = 1; star <= SummonStarRules.MaxStar; star++)
                    sum += SummonStarRules.Chance(level, star);

                Assert.AreEqual(100, sum, "Lv" + level);
            }
        }

        [Test]
        public void LevelOne_IsAlwaysOneStar_AndTheLastLevel_MatchesTheSpec()
        {
            Assert.AreEqual(10, SummonStarRules.MaxLevel);
            Assert.AreEqual(100, SummonStarRules.Chance(1, 1));
            Assert.AreEqual(40, SummonStarRules.Chance(10, 1));
            Assert.AreEqual(35, SummonStarRules.Chance(10, 2));
            Assert.AreEqual(25, SummonStarRules.Chance(10, 3));
        }

        [Test]
        public void ThreeStar_OpensAtLevelFour_AndHigherLevelsNeverLowerIt()
        {
            Assert.AreEqual(0, SummonStarRules.Chance(3, 3));
            Assert.Greater(SummonStarRules.Chance(4, 3), 0);

            for (int level = 2; level <= SummonStarRules.MaxLevel; level++)
            {
                Assert.GreaterOrEqual(SummonStarRules.Chance(level, 3), SummonStarRules.Chance(level - 1, 3));
                Assert.Less(SummonStarRules.Chance(level, 1), SummonStarRules.Chance(level - 1, 1));
            }
        }

        [TestCase(1, 30)]
        [TestCase(2, 60)]
        [TestCase(3, 90)]
        [TestCase(4, 120)]
        [TestCase(5, 150)]
        [TestCase(9, 270)]
        [TestCase(10, -1)]
        [TestCase(99, -1)]
        [TestCase(0, 30)]
        public void UpgradeCost_AddsThirtyEachLevel_AndStopsAtMax(int level, int expected)
        {
            Assert.AreEqual(expected, SummonStarRules.UpgradeCost(level));
        }

        [Test]
        public void RollStar_FollowsTheCumulativeTable()
        {
            // Lv10: 0..39 → 1성, 40..74 → 2성, 75..99 → 3성.
            Assert.AreEqual(1, SummonStarRules.RollStar(10, 0));
            Assert.AreEqual(1, SummonStarRules.RollStar(10, 39));
            Assert.AreEqual(2, SummonStarRules.RollStar(10, 40));
            Assert.AreEqual(2, SummonStarRules.RollStar(10, 74));
            Assert.AreEqual(3, SummonStarRules.RollStar(10, 75));
            Assert.AreEqual(3, SummonStarRules.RollStar(10, 99));

            // Lv1 은 무엇을 굴려도 1성이다.
            for (int roll = 0; roll < 100; roll++)
                Assert.AreEqual(1, SummonStarRules.RollStar(1, roll));
        }

        [Test]
        public void RollStar_OverAllRolls_MatchesTheTableExactly()
        {
            for (int level = SummonStarRules.MinLevel; level <= SummonStarRules.MaxLevel; level++)
            {
                var counts = new int[SummonStarRules.MaxStar + 1];
                for (int roll = 0; roll < 100; roll++)
                    counts[SummonStarRules.RollStar(level, roll)]++;

                for (int star = 1; star <= SummonStarRules.MaxStar; star++)
                    Assert.AreEqual(SummonStarRules.Chance(level, star), counts[star], "Lv" + level + " " + star + "성");
            }
        }
    }
}
