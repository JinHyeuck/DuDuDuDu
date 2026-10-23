using NUnit.Framework;

namespace OJ.Core.Tests
{
    public class DiceLevelResetRulesTests
    {
        [Test]
        public void LevelOneHasNothingToReset()
        {
            Assert.That(DiceLevelResetRules.CanReset(1), Is.False);
            Assert.That(DiceLevelResetRules.CanReset(2), Is.True);
            Assert.That(DiceLevelResetRules.CanReset(12), Is.True);
        }

        [Test]
        public void UpToLevelSixIsFree()
        {
            for (int level = 1; level <= 6; level++)
                Assert.That(DiceLevelResetRules.GemCostFor(level), Is.EqualTo(0), "level " + level);
        }

        [Test]
        public void FromLevelSevenCostsOneHundredGems()
        {
            for (int level = 7; level <= 12; level++)
                Assert.That(DiceLevelResetRules.GemCostFor(level), Is.EqualTo(100), "level " + level);
        }

        [Test]
        public void RefundSumsEveryLevelUpPaid()
        {
            // 1->2 는 10/1, 2->3 은 20/2, 3->4 는 30/3.
            var refund = DiceLevelResetRules.Refund(4, from => (from * 10, from));

            Assert.That(refund.gold, Is.EqualTo(60));
            Assert.That(refund.scroll, Is.EqualTo(6));
        }

        [Test]
        public void RefundAtLevelOneIsZeroAndNeverAsksForCost()
        {
            var refund = DiceLevelResetRules.Refund(1, from =>
            {
                Assert.Fail("레벨 1 은 값을 물을 일이 없다.");
                return (0, 0);
            });

            Assert.That(refund, Is.EqualTo((0, 0)));
        }

        [Test]
        public void RefundDoesNotIncludeTheLevelYouAreAt()
        {
            int highestAsked = 0;
            DiceLevelResetRules.Refund(5, from =>
            {
                highestAsked = System.Math.Max(highestAsked, from);
                return (1, 1);
            });

            Assert.That(highestAsked, Is.EqualTo(4));
        }

        [Test]
        public void NegativeCostsAreIgnored()
        {
            var refund = DiceLevelResetRules.Refund(3, from => (-50, -5));

            Assert.That(refund, Is.EqualTo((0, 0)));
        }

        [Test]
        public void HugeRefundIsClampedInsteadOfOverflowing()
        {
            var refund = DiceLevelResetRules.Refund(12, from => (int.MaxValue, int.MaxValue));

            Assert.That(refund.gold, Is.EqualTo(int.MaxValue));
            Assert.That(refund.scroll, Is.EqualTo(int.MaxValue));
        }
    }
}
