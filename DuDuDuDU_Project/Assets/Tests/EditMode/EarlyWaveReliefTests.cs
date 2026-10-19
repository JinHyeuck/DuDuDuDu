using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>초반 3웨이브 완화(몬스터 수 줄임 · 등장 간격 늘림)를 잠근다.</summary>
    public sealed class EarlyWaveReliefTests
    {
        [TestCase(18, 1, 9)]
        [TestCase(18, 2, 12)]
        [TestCase(18, 3, 14)]
        [TestCase(18, 4, 18)]
        [TestCase(40, 1, 20)]
        [TestCase(40, 3, 32)]
        [TestCase(40, 15, 40)]
        public void Monsters_AreReducedOnlyInTheFirstThreeWaves(int stageCount, int wave, int expected)
        {
            Assert.AreEqual(expected, EarlyWaveRelief.MonstersFor(stageCount, wave));
        }

        [TestCase(6, 1, 3)]
        [TestCase(7, 1, 4)]
        [TestCase(6, 2, 4)]
        [TestCase(2, 1, 2)]
        [TestCase(1, 1, 1)]
        public void Monsters_NeverDropBelowTheFloorAndNeverExceedTheStageValue(int stageCount, int wave, int expected)
        {
            Assert.AreEqual(expected, EarlyWaveRelief.MonstersFor(stageCount, wave));
        }

        [Test]
        public void Monsters_AreAlwaysAtLeastOne()
        {
            Assert.AreEqual(1, EarlyWaveRelief.MonstersFor(0, 1));
            Assert.AreEqual(1, EarlyWaveRelief.MonstersFor(-5, 2));
        }

        [Test]
        public void Relief_GrowsWeakerWaveByWaveAndEndsAfterTheThird()
        {
            for (int wave = 2; wave <= EarlyWaveRelief.ReliefWaves; wave++)
            {
                Assert.Greater(EarlyWaveRelief.CountPercent(wave), EarlyWaveRelief.CountPercent(wave - 1), "count " + wave);
                Assert.Less(EarlyWaveRelief.IntervalPercent(wave), EarlyWaveRelief.IntervalPercent(wave - 1), "interval " + wave);
            }

            Assert.AreEqual(100, EarlyWaveRelief.CountPercent(EarlyWaveRelief.ReliefWaves + 1));
            Assert.AreEqual(100, EarlyWaveRelief.IntervalPercent(EarlyWaveRelief.ReliefWaves + 1));
            Assert.AreEqual(100, EarlyWaveRelief.CountPercent(0));
            Assert.AreEqual(100, EarlyWaveRelief.IntervalPercent(-3));
        }

        [Test]
        public void Interval_IsSlowerEarlyAndUnchangedFromTheFourthWave()
        {
            Assert.Greater(EarlyWaveRelief.IntervalFor(1f, 1), 1.5f);
            Assert.Greater(EarlyWaveRelief.IntervalFor(1f, 3), 1f);
            Assert.AreEqual(2f, EarlyWaveRelief.IntervalFor(2f, 4), 0.0001f);
        }
    }
}
