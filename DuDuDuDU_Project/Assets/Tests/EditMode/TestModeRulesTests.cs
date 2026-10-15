using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class TestModeRulesTests
    {
        [TestCase(0, 50)]
        [TestCase(1, 55)]
        [TestCase(2, 60)]
        [TestCase(13, 115)]
        [TestCase(-1, 50)]
        public void SlotUnlockCost_RisesByFive(int unlocked, int expected)
        {
            Assert.AreEqual(expected, TestModeRules.SlotUnlockCost(unlocked));
        }

        [TestCase(0, 200)]
        [TestCase(1, 400)]
        [TestCase(2, 600)]
        [TestCase(3, -1)]
        [TestCase(-1, -1)]
        public void FireRateUpgradeCost_ThreeSteps(int level, int expected)
        {
            Assert.AreEqual(expected, TestModeRules.FireRateUpgradeCost(level));
        }

        [Test]
        public void FireRateDelay_StepsDown()
        {
            Assert.AreEqual(0.6f, TestModeRules.FireRateDelay(0, 0.6f));
            Assert.AreEqual(0.4f, TestModeRules.FireRateDelay(1, 0.6f));
            Assert.AreEqual(0.2f, TestModeRules.FireRateDelay(2, 0.6f));
            Assert.AreEqual(0.1f, TestModeRules.FireRateDelay(3, 0.6f));
        }
    }
}
