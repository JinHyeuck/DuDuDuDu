using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>후반 웨이브 체력 가산이 중반까지는 아무것도 바꾸지 않고, 그 뒤부터만 오르는지 잠근다.</summary>
    public sealed class LateWaveSurgeTests
    {
        [Test]
        public void Fifteen_StartsAfterWaveEight()
        {
            Assert.That(LateWaveSurge.StartWave(15), Is.EqualTo(8));
        }

        [TestCase(10)]
        [TestCase(15)]
        [TestCase(20)]
        [TestCase(30)]
        public void UpToStartWave_HpIsUntouched(int totalWaves)
        {
            int start = LateWaveSurge.StartWave(totalWaves);
            for (int w = 1; w <= start; w++)
            {
                Assert.That(LateWaveSurge.HpPercent(w, totalWaves), Is.EqualTo(100), "wave " + w);
                Assert.That(LateWaveSurge.ApplyHp(1234, w, totalWaves), Is.EqualTo(1234), "wave " + w);
            }
        }

        [TestCase(10)]
        [TestCase(15)]
        [TestCase(20)]
        [TestCase(30)]
        public void AfterStartWave_RisesEveryWaveUpToTheFinalBonus(int totalWaves)
        {
            int start = LateWaveSurge.StartWave(totalWaves);
            for (int w = start + 1; w <= totalWaves; w++)
                Assert.Greater(LateWaveSurge.HpPercent(w, totalWaves), LateWaveSurge.HpPercent(w - 1, totalWaves), "wave " + w);

            Assert.That(LateWaveSurge.HpPercent(totalWaves, totalWaves), Is.EqualTo(100 + LateWaveSurge.FinalBonusPercent));
        }

        [Test]
        public void Fifteen_FirstSurgedWaveIsAlreadyFelt()
        {
            // 9웨이브부터 체감되게 오른다(+11%). 너무 완만하면 "중반에서 후반으로 넘어가는" 느낌이 안 난다.
            Assert.That(LateWaveSurge.HpPercent(9, 15), Is.InRange(107, 117));
        }

        [Test]
        public void SingleWaveTower_IsNeverSurged()
        {
            Assert.That(LateWaveSurge.HpPercent(1, 1), Is.EqualTo(100));
            Assert.That(LateWaveSurge.ApplyHp(77, 1, 1), Is.EqualTo(77));
        }

        [Test]
        public void ApplyHp_NeverDropsBelowOne()
        {
            Assert.That(LateWaveSurge.ApplyHp(0, 15, 15), Is.EqualTo(1));
        }
    }
}
