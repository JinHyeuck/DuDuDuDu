using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class SoloBossRulesTests
    {
        // 간격 0 = 옛 규칙: 마지막 웨이브만 보스.
        [TestCase(0, 15, 14, false)]
        [TestCase(0, 15, 15, true)]
        [TestCase(0, 10, 5, false)]
        // 간격 5: 5 · 10 · 15 가 보스.
        [TestCase(5, 15, 4, false)]
        [TestCase(5, 15, 5, true)]
        [TestCase(5, 15, 7, false)]
        [TestCase(5, 15, 10, true)]
        [TestCase(5, 15, 15, true)]
        [TestCase(5, 15, 1, false)]
        public void IsBossWave(int interval, int total, int wave, bool expected)
        {
            Assert.AreEqual(expected, SoloBossRules.IsBossWave(interval, total, wave));
        }

        [Test]
        public void SoloOnlyWhenIntervalIsSet()
        {
            Assert.IsFalse(SoloBossRules.IsSoloBossWave(0, 15, 15));
            Assert.IsTrue(SoloBossRules.IsSoloBossWave(5, 15, 15));
            Assert.IsFalse(SoloBossRules.IsSoloBossWave(5, 15, 14));
        }

        [TestCase(5, 15, 5, 0)]
        [TestCase(5, 15, 10, 1)]
        [TestCase(5, 15, 15, 2)]
        [TestCase(5, 12, 12, 2)] // 마지막 웨이브가 배수가 아니면 앞 보스 다음 번호
        public void BossOrdinal(int interval, int total, int wave, int expected)
        {
            Assert.AreEqual(expected, SoloBossRules.BossOrdinal(interval, total, wave));
        }

        [Test]
        public void SoloBossHpIsWholeWaveHpTimesFactor()
        {
            Assert.AreEqual(594, SoloBossRules.SoloBossHp(33, 18, 1f));
            Assert.AreEqual(535, SoloBossRules.SoloBossHp(33, 18, 0.9f));
            Assert.AreEqual(1, SoloBossRules.SoloBossHp(0, 0, 0f) >= 1 ? 1 : 0);
        }
    }
}
