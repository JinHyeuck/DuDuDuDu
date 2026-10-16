using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class SavingsAndLuckyDiceRulesTests
    {
        // ── 통장 ────────────────────────────────────────────────

        [TestCase(0, 30)]
        [TestCase(1, 40)]
        [TestCase(2, 50)]
        [TestCase(3, 50)]
        [TestCase(100, 50)]
        [TestCase(-4, 30)]
        public void Savings_DepositRisesToTheCapAndStaysThere(int completed, int expected)
        {
            Assert.AreEqual(expected, SavingsRules.DepositFor(completed));
        }

        [TestCase(30, 33)]
        [TestCase(40, 44)]
        [TestCase(50, 55)]
        [TestCase(0, 0)]
        [TestCase(-5, 0)]
        public void Savings_PayoutIsPrincipalPlusTenPercent(int deposit, int expected)
        {
            Assert.AreEqual(expected, SavingsRules.PayoutFor(deposit));
        }

        [Test]
        public void Savings_TakesFortyFiveSeconds()
        {
            Assert.AreEqual(45f, SavingsRules.DurationSeconds);
        }

        // ── 행운 주사위 ───────────────────────────────────────

        [Test]
        public void LuckyDice_ChancesSumToOneHundred_AndFallAsTheRollGrows()
        {
            int sum = 0;
            for (int stones = LuckyDiceRules.MinStones; stones <= LuckyDiceRules.MaxStones; stones++)
            {
                sum += LuckyDiceRules.Chance(stones);

                if (stones > LuckyDiceRules.MinStones)
                    Assert.LessOrEqual(LuckyDiceRules.Chance(stones), LuckyDiceRules.Chance(stones - 1), stones + "개");
            }

            Assert.AreEqual(100, sum);
        }

        [Test]
        public void LuckyDice_OutOfRangeStonesHaveNoChance()
        {
            Assert.AreEqual(0, LuckyDiceRules.Chance(0));
            Assert.AreEqual(0, LuckyDiceRules.Chance(11));
        }

        [Test]
        public void LuckyDice_RollOverEveryValueMatchesTheTableExactly()
        {
            var counts = new int[LuckyDiceRules.MaxStones + 1];
            for (int roll = 0; roll < 100; roll++)
                counts[LuckyDiceRules.Roll(roll)]++;

            for (int stones = LuckyDiceRules.MinStones; stones <= LuckyDiceRules.MaxStones; stones++)
                Assert.AreEqual(LuckyDiceRules.Chance(stones), counts[stones], stones + "개");
        }

        [Test]
        public void LuckyDice_CostsAHundredAndExpectsAboutFourStones()
        {
            Assert.AreEqual(100, LuckyDiceRules.CostSp);
            Assert.AreEqual(1, LuckyDiceRules.Roll(0));
            Assert.AreEqual(10, LuckyDiceRules.Roll(99));
            Assert.AreEqual(403, LuckyDiceRules.ExpectedStonesTimes100());
        }
    }
}
