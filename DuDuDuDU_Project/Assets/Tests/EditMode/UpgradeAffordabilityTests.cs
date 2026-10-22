using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class UpgradeAffordabilityTests
    {
        [TestCase(false, 130, 10, 130, 10, true)]
        [TestCase(false, 500, 99, 130, 10, true)]
        [TestCase(false, 129, 10, 130, 10, false)]
        [TestCase(false, 130, 9, 130, 10, false)]
        [TestCase(false, 0, 0, 0, 0, true)]
        [TestCase(true, 9999, 9999, 130, 10, false)]
        public void CanUpgrade_NeedsBothCurrenciesAndNotMax(bool isMax, int gold, int scroll, int costGold, int costScroll, bool expected)
        {
            Assert.AreEqual(expected, UpgradeAffordability.CanUpgrade(isMax, gold, scroll, costGold, costScroll));
        }
    }
}
