using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class SavingsAndLuckyDiceRulesTests
    {
        // ── 저금통: 선택(기본) ────────────────────────────────────

        [TestCase(100, 115, 15, 1825)]
        [TestCase(100, 80, 15, 1300)]
        [TestCase(0, 0, 0, 0)]
        [TestCase(-5, 115, 15, 1725)]
        public void SavingsChoice_StageTotalIsInitialPlusWaveClearTimesWaves(int initial, int waveClear, int waves, int expected)
        {
            Assert.AreEqual(expected, SavingsRules.StageSpTotal(initial, waveClear, waves));
        }

        [Test]
        public void SavingsChoice_InstantIsThreeToFivePercentOfTheStageTotal()
        {
            // 1~10스테이지 총량 1,825 SP → 54 ~ 91 SP. 양 끝 난수가 정확히 그 값을 낸다.
            SavingsRules.RollOffer(1825, 0, 0, out int low, out _);
            SavingsRules.RollOffer(1825, SavingsRules.InstantMaxPermille - SavingsRules.InstantMinPermille, 0, out int high, out _);

            Assert.AreEqual(54, low);
            Assert.AreEqual(91, high);
        }

        [Test]
        public void SavingsChoice_EveryRollStaysInRangeAndDelayedIsOneTenthToUnderThreeTimesInstant()
        {
            for (int a = 0; a < 21; a++)
            {
                for (int b = 0; b < 190; b++)
                {
                    SavingsRules.RollOffer(1825, a, b, out int instant, out int delayed);

                    Assert.GreaterOrEqual(instant, 54, a + "/" + b);
                    Assert.LessOrEqual(instant, 91, a + "/" + b);
                    Assert.Greater(delayed, instant, a + "/" + b);
                    Assert.GreaterOrEqual(delayed, instant * 110 / 100, a + "/" + b);
                    Assert.Less(delayed, instant * 3, a + "/" + b);
                }
            }
        }

        [Test]
        public void SavingsChoice_SmallStageTotalIsLiftedToTheFloor()
        {
            // 튜토리얼 판은 총량이 작아 3~5% 가 한 자리 수다 — 하한이 받쳐 준다.
            SavingsRules.RollOffer(226, 0, 0, out int instant, out int delayed);

            Assert.AreEqual(SavingsRules.MinInstant, instant);
            Assert.Greater(delayed, instant);
        }

        [Test]
        public void SavingsChoice_NegativeAndHugeRollsWrapIntoTheSameRange()
        {
            SavingsRules.RollOffer(1825, -7, -300, out int instant, out int delayed);
            Assert.That(instant, Is.InRange(54, 91));
            Assert.That(delayed, Is.InRange(instant, instant * 3 - 1));

            SavingsRules.RollOffer(1825, int.MaxValue, int.MaxValue, out instant, out delayed);
            Assert.That(instant, Is.InRange(54, 91));
            Assert.That(delayed, Is.InRange(instant, instant * 3 - 1));
        }

        [Test]
        public void SavingsChoice_CooldownIsFortyFiveSeconds()
        {
            Assert.AreEqual(45f, SavingsRules.ChoiceCooldownSeconds);
        }

        // ── 저금통: 3단(이전 방식) ──────────────────────────────────────────────────────────────────────────

        [TestCase(0, 30, 45, 30f)]
        [TestCase(1, 40, 65, 45f)]
        [TestCase(2, 50, 90, 60f)]
        public void Savings_OptionsMatchTheTable(int option, int deposit, int payout, float seconds)
        {
            Assert.AreEqual(deposit, SavingsRules.Deposit(option));
            Assert.AreEqual(payout, SavingsRules.Payout(option));
            Assert.AreEqual(seconds, SavingsRules.Duration(option));
            Assert.AreEqual(payout - deposit, SavingsRules.Gain(option));
        }

        [Test]
        public void Savings_BiggerAndLongerOptionsPayBetterAndFaster()
        {
            for (int option = 1; option < SavingsRules.OptionCount; option++)
            {
                int prev = option - 1;

                // 원금 대비 비율(백분율, 정수 비교)과 초당 이익이 모두 오른다. 큰 선택지를 고를 이유가 있어야 한다.
                Assert.Greater(SavingsRules.Gain(option) * 100 / SavingsRules.Deposit(option),
                    SavingsRules.Gain(prev) * 100 / SavingsRules.Deposit(prev));
                Assert.Greater(SavingsRules.Gain(option) / SavingsRules.Duration(option),
                    SavingsRules.Gain(prev) / SavingsRules.Duration(prev));
            }
        }

        [Test]
        public void Savings_EveryOptionReturnsMoreThanItCosts()
        {
            for (int option = 0; option < SavingsRules.OptionCount; option++)
                Assert.Greater(SavingsRules.Payout(option), SavingsRules.Deposit(option));
        }

        [TestCase(-3, 0)]
        [TestCase(0, 0)]
        [TestCase(2, 2)]
        [TestCase(9, 2)]
        public void Savings_OptionIsClamped(int option, int expected)
        {
            Assert.AreEqual(expected, SavingsRules.ClampOption(option));
        }

        [Test]
        public void Savings_CheapestDepositIsTheFirstOption()
        {
            Assert.AreEqual(SavingsRules.Deposit(0), SavingsRules.CheapestDeposit);
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
