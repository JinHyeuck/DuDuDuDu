using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class SlotUnlockRulesTests
    {
        [TestCase(0, 50)]
        [TestCase(1, 55)]
        [TestCase(2, 60)]
        [TestCase(13, 115)]
        [TestCase(-1, 50)]
        public void Cost_RisesByFive(int unlocked, int expected)
        {
            Assert.AreEqual(expected, SlotUnlockRules.Cost(unlocked));
        }
    }
}
