using NUnit.Framework;

namespace OJ.Core.Tests
{
    public class BossWaveRulesTests
    {
        [Test]
        public void LastWaveIsAlwaysBossWave()
        {
            for (int stage = 1; stage <= 10; stage++)
                Assert.That(BossWaveRules.IsBossWave(stage, 15, 15), Is.True, "스테이지 " + stage);
        }

        [Test]
        public void BeforeStageFive_OnlyLastWaveIsBossWave()
        {
            for (int stage = 1; stage < BossWaveRules.PeriodicFromStage; stage++)
            {
                for (int wave = 0; wave < 15; wave++)
                    Assert.That(BossWaveRules.IsBossWave(stage, wave, 15), Is.False, $"스테이지 {stage} 웨이브 {wave}");
            }
        }

        [Test]
        public void FromStageFive_EveryFifthWaveIsBossWave()
        {
            Assert.That(BossWaveRules.IsBossWave(5, 5, 15), Is.True);
            Assert.That(BossWaveRules.IsBossWave(5, 10, 15), Is.True);
            Assert.That(BossWaveRules.IsBossWave(9, 5, 15), Is.True);
        }

        [Test]
        public void FromStageFive_OtherWavesAreNotBossWaves()
        {
            for (int wave = 1; wave < 15; wave++)
            {
                if (wave % 5 == 0)
                    continue;
                Assert.That(BossWaveRules.IsBossWave(5, wave, 15), Is.False, "웨이브 " + wave);
            }
        }

        [Test]
        public void WaveZeroIsNeverBossWave()
        {
            Assert.That(BossWaveRules.IsBossWave(5, 0, 15), Is.False);
        }

        [Test]
        public void TowerFloorAndTutorialKeepLastWaveRule()
        {
            Assert.That(BossWaveRules.IsBossWave(1, 1, 1), Is.True);
            Assert.That(BossWaveRules.IsBossWave(1, 3, 4), Is.False);
            Assert.That(BossWaveRules.IsBossWave(1, 4, 4), Is.True);
        }

        [Test]
        public void ShortStageStillEndsOnBoss()
        {
            Assert.That(BossWaveRules.IsBossWave(6, 5, 5), Is.True);
            Assert.That(BossWaveRules.IsBossWave(6, 4, 5), Is.False);
        }
    }
}
