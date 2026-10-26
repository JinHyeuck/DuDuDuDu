using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class MidWaveRampTests
    {
        [TestCase(1, 100)]
        [TestCase(3, 100)]   // 시작 웨이브까지는 그대로
        [TestCase(5, 190)]   // 3 → 7 사이 직선: 3 + 2/4 → 1 + 1.8 x 0.5 = 1.9
        [TestCase(7, 280)]
        [TestCase(12, 280)]  // 이후 유지
        public void RampsLinearlyThenHolds(int wave, int expectedPercent)
        {
            Assert.AreEqual(expectedPercent, MidWaveRamp.Percent(wave, 3, 7, 2.8f));
        }

        [Test]
        public void MultiplierOfOneOrLessDoesNothing()
        {
            Assert.AreEqual(100, MidWaveRamp.Percent(10, 3, 7, 1f));
            Assert.AreEqual(100, MidWaveRamp.Percent(10, 3, 7, 0.5f));
            Assert.AreEqual(123, MidWaveRamp.Apply(123, 10, 0, 0, 1f));
        }

        [Test]
        public void ApplyScalesAndNeverDropsBelowOne()
        {
            Assert.AreEqual(280, MidWaveRamp.Apply(100, 9, 3, 7, 2.8f));
            Assert.AreEqual(100, MidWaveRamp.Apply(100, 2, 3, 7, 2.8f));
            Assert.GreaterOrEqual(MidWaveRamp.Apply(1, 9, 3, 7, 2.8f), 1);
        }
    }
}
