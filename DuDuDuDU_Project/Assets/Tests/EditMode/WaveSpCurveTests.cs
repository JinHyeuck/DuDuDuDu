using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>웨이브별 SP 곡선(종 모양)과 그 결과인 소환 횟수 곡선을 잠근다.</summary>
    public sealed class WaveSpCurveTests
    {
        // 목표: 웨이브별 소환(뽑기) 횟수. 초반 2~3회 → 6~8웨이브 정점 7회 → 후반 1~2회.
        private static readonly int[] TargetClicks = { 3, 3, 4, 5, 6, 7, 7, 7, 6, 5, 4, 3, 2, 2, 1 };

        [Test]
        public void Table15_PeaksInTheMiddleAndIsLowAtBothEnds()
        {
            int peakWave = 1;
            int peak = 0;
            for (int w = 1; w <= 15; w++)
            {
                int m = WaveSpCurve.Multiplier(w, 15);
                if (m > peak)
                {
                    peak = m;
                    peakWave = w;
                }
            }

            Assert.That(peakWave, Is.InRange(7, 9));
            Assert.Less(WaveSpCurve.Multiplier(1, 15), 30);
            Assert.Less(WaveSpCurve.Multiplier(15, 15), 50);
            Assert.Greater(peak, 150);

            // 정점 앞은 올라가기만 하고 뒤는 내려가기만 한다(종 모양).
            for (int w = 2; w <= peakWave; w++)
                Assert.GreaterOrEqual(WaveSpCurve.Multiplier(w, 15), WaveSpCurve.Multiplier(w - 1, 15), "wave " + w);
            for (int w = peakWave + 1; w <= 15; w++)
                Assert.LessOrEqual(WaveSpCurve.Multiplier(w, 15), WaveSpCurve.Multiplier(w - 1, 15), "wave " + w);
        }

        [Test]
        public void Table15_KeepsTheTotalCloseToWhatItWasBeforeTheCurve()
        {
            // 곡선 이전에는 매 웨이브 100%. 평균이 90~100% 사이여야 총 SP 가 크게 바뀌지 않는다.
            Assert.That(WaveSpCurve.MeanPercent, Is.InRange(90.0, 100.0));
        }

        [TestCase(10)]
        [TestCase(20)]
        [TestCase(30)]
        public void OtherLengths_AreResampledToTheSameBellWithTheSameMean(int totalWaves)
        {
            long sum = 0;
            int peakWave = 1;
            int peak = 0;
            for (int w = 1; w <= totalWaves; w++)
            {
                int m = WaveSpCurve.Multiplier(w, totalWaves);
                sum += m;
                if (m > peak)
                {
                    peak = m;
                    peakWave = w;
                }
            }

            // 평균이 표 평균과 같다(반올림 오차 1% 이내).
            Assert.AreEqual(WaveSpCurve.MeanPercent, (double)sum / totalWaves, 1.0);

            // 정점이 가운데 근처, 양 끝은 정점보다 훨씬 낮다.
            Assert.That((double)peakWave / totalWaves, Is.InRange(0.4, 0.65));
            Assert.Less(WaveSpCurve.Multiplier(1, totalWaves), peak / 2);
            Assert.Less(WaveSpCurve.Multiplier(totalWaves, totalWaves), peak / 2);
        }

        [Test]
        public void OutOfRangeWavesClampAndTinyStagesAreUntouched()
        {
            Assert.AreEqual(WaveSpCurve.Multiplier(1, 15), WaveSpCurve.Multiplier(-5, 15));
            Assert.AreEqual(WaveSpCurve.Multiplier(15, 15), WaveSpCurve.Multiplier(99, 15));
            Assert.AreEqual(100, WaveSpCurve.Multiplier(1, 1));
            Assert.AreEqual(100, WaveSpCurve.Multiplier(1, 0));
        }

        [Test]
        public void WaveClearSp_AppliesTheCurveExceptInTheTutorial()
        {
            Assert.AreEqual(115 * 175 / 100, WaveSpCurve.WaveClearSp(115, 8, 15, true));
            Assert.AreEqual(115, WaveSpCurve.WaveClearSp(115, 8, 15, false));
        }

        /// <summary>
        /// 곡선 SP 로 소환 비용(10 시작, 2회마다 +1)을 계속 치르면 웨이브별 소환 횟수가 목표 모양과 ±1 이내다.
        /// 표를 바꾸다가 모양이 깨지는 것(초반이 다시 바빠진다)을 막는다.
        /// </summary>
        [Test]
        public void ClicksPerWave_FollowTheBell()
        {
            int sp = WaveSpCurve.StartSp;
            int cost = 10;
            int sinceIncrease = 0;
            int total = 0;

            for (int wave = 1; wave <= 15; wave++)
            {
                sp += WaveSpCurve.WaveClearSp(115, wave, 15, true);

                int clicks = 0;
                while (sp >= cost)
                {
                    sp -= cost;
                    clicks++;
                    sinceIncrease++;
                    if (sinceIncrease >= 2)
                    {
                        cost++;
                        sinceIncrease = 0;
                    }
                }

                total += clicks;
                Assert.That(clicks, Is.InRange(TargetClicks[wave - 1] - 1, TargetClicks[wave - 1] + 1), "wave " + wave);
            }

            // 초반(1~3웨이브)보다 중반(6~8웨이브)이 확실히 많고, 후반(13~15웨이브)은 다시 적다.
            Assert.Greater(total, 55);
        }
    }
}
