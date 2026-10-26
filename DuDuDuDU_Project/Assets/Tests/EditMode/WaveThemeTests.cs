using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class WaveThemeTests
    {
        [TestCase(1, WaveInterlude.SoulBonus)]
        [TestCase(2, WaveInterlude.SoulBonus)]
        [TestCase(3, WaveInterlude.BountyChallenge)]
        [TestCase(4, WaveInterlude.SoulBonus)]
        [TestCase(5, WaveInterlude.SlotsOrSoul)]
        [TestCase(6, WaveInterlude.BountyChallenge)]
        [TestCase(7, WaveInterlude.SoulBonus)]
        [TestCase(8, WaveInterlude.SoulBonus)]
        [TestCase(9, WaveInterlude.BountyChallenge)]
        [TestCase(10, WaveInterlude.BossReward)]
        // 같은 패턴이 30웨이브까지 되풀이된다.
        [TestCase(11, WaveInterlude.SoulBonus)]
        [TestCase(15, WaveInterlude.SlotsOrSoul)]
        [TestCase(19, WaveInterlude.BountyChallenge)]
        [TestCase(20, WaveInterlude.BossReward)]
        [TestCase(25, WaveInterlude.SlotsOrSoul)]
        [TestCase(30, WaveInterlude.BossReward)]
        public void FollowsTheTenWaveCycle(int wave, WaveInterlude expected)
        {
            Assert.AreEqual(expected, WaveTheme.After(wave));
        }

        [Test]
        public void OnlyEveryTenthWaveIsBoss()
        {
            for (int w = 1; w <= 30; w++)
                Assert.AreEqual(w % 10 == 0, WaveTheme.IsBossWave(w), "웨이브 " + w);
        }

        [Test]
        public void BossWaveMatchesTheSoloBossIntervalOfTen()
        {
            for (int w = 1; w <= 30; w++)
                Assert.AreEqual(WaveTheme.IsBossWave(w), SoloBossRules.IsSoloBossWave(WaveTheme.Cycle, 30, w), "웨이브 " + w);
        }
    }
}
