using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class SavingsAndLuckyDiceRulesTests
    {
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
