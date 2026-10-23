using NUnit.Framework;

namespace OJ.Core.Tests
{
    public class ElementUpgradeRulesTests
    {
        [Test]
        public void FirstUpgradeCostsTwentyFive()
        {
            Assert.That(ElementUpgradeRules.NextCost(0), Is.EqualTo(25));
        }

        [Test]
        public void EachUpgradeAddsFive()
        {
            Assert.That(ElementUpgradeRules.NextCost(1), Is.EqualTo(30));
            Assert.That(ElementUpgradeRules.NextCost(2), Is.EqualTo(35));
            Assert.That(ElementUpgradeRules.NextCost(10), Is.EqualTo(75));
        }

        [Test]
        public void NegativeCountIsTreatedAsZero()
        {
            Assert.That(ElementUpgradeRules.NextCost(-3), Is.EqualTo(25));
            Assert.That(ElementUpgradeRules.TotalBonusPercent(-3), Is.EqualTo(0));
        }

        [Test]
        public void EachUpgradeAddsTenPercent()
        {
            Assert.That(ElementUpgradeRules.TotalBonusPercent(0), Is.EqualTo(0));
            Assert.That(ElementUpgradeRules.TotalBonusPercent(1), Is.EqualTo(10));
            Assert.That(ElementUpgradeRules.TotalBonusPercent(7), Is.EqualTo(70));
        }
    }
}
