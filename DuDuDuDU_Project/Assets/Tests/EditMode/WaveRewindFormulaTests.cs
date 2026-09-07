using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 되감기 배율 표를 잠근다.
    ///
    /// 값이 비례식이 아니라 <b>기획이 고른 표</b>라서, 나중에 누가 "2배속이면 4배 아닌가"
    /// 하고 고치기 쉽다. 그때 이 테스트가 그것이 실수인지 결정인지 물어본다.
    /// </summary>
    [TestFixture]
    public sealed class WaveRewindFormulaTests
    {
        [TestCase(1f, 2f)]
        [TestCase(2f, 5f)]
        [TestCase(3f, 6f)]
        public void 배속별_되감기_배율(float timeSpeed, float expected)
        {
            Assert.AreEqual(expected, WaveRewindFormula.PlaybackSpeed(timeSpeed));
        }

        [Test]
        public void 배속이_1_미만이어도_가장_느린_배율을_준다()
        {
            // 일시정지(0) 중에 되돌리는 경로가 실제로 있다 — 확인 창이 timeScale 을 0 으로
            // 두기 때문이다. 0 을 넣었을 때 답이 0 이면 되감기가 영영 안 끝난다.
            Assert.AreEqual(2f, WaveRewindFormula.PlaybackSpeed(0f));
            Assert.AreEqual(2f, WaveRewindFormula.PlaybackSpeed(0.5f));
        }

        [Test]
        public void 배속이_3_보다_커도_6_에서_멈춘다()
        {
            Assert.AreEqual(6f, WaveRewindFormula.PlaybackSpeed(4f));
            Assert.AreEqual(6f, WaveRewindFormula.PlaybackSpeed(10f));
        }

        // --- 되감기 길이 ---------------------------------------------------------------

        [Test]
        public void 짧은_웨이브는_상한에_안_걸린다()
        {
            // 3배속으로 6초 본 웨이브 → 6/6 = 1초. 상한 2초에 안 닿는다.
            Assert.AreEqual(1f, WaveRewindFormula.Duration(6f, 6f, 2f), 0.0001f);
        }

        [Test]
        public void 긴_웨이브는_상한에서_잘린다()
        {
            // 1배속으로 60초 버틴 웨이브. 상한이 없으면 30초짜리 되감기가 된다.
            Assert.AreEqual(30f, WaveRewindFormula.Duration(60f, 2f, 0f), 0.0001f);
            Assert.AreEqual(2f, WaveRewindFormula.Duration(60f, 2f, 2f), 0.0001f);
        }

        [Test]
        public void 상한이_0_이하면_상한이_없다()
        {
            Assert.AreEqual(30f, WaveRewindFormula.Duration(60f, 2f, 0f), 0.0001f);
            Assert.AreEqual(30f, WaveRewindFormula.Duration(60f, 2f, -1f), 0.0001f);
        }

        [Test]
        public void 웨이브가_0초면_되감을_것이_없다()
        {
            // 웨이브를 시작하자마자 되돌린 경우. 0 을 안 돌려주면 연출이
            // 몇 프레임짜리 깜빡임으로 돌아 고장처럼 보인다.
            Assert.AreEqual(0f, WaveRewindFormula.Duration(0f, 2f, 2f));
            Assert.AreEqual(0f, WaveRewindFormula.Duration(-5f, 2f, 2f));
        }

        [Test]
        public void 길이는_상한을_절대_넘지_않는다()
        {
            const float Max = 2f;
            foreach (float watched in new[] { 0.1f, 1f, 5f, 60f, 600f })
            {
                foreach (float timeSpeed in new[] { 1f, 2f, 3f })
                {
                    float speed = WaveRewindFormula.PlaybackSpeed(timeSpeed);
                    float duration = WaveRewindFormula.Duration(watched, speed, Max);
                    Assert.LessOrEqual(duration, Max, $"본 시간={watched} 배속={timeSpeed}");
                    Assert.GreaterOrEqual(duration, 0f, $"본 시간={watched} 배속={timeSpeed}");
                }
            }
        }

        [Test]
        public void 배율이_0_이어도_무한대가_안_나온다()
        {
            // PlaybackSpeed 가 0 을 줄 일은 없지만, 나누기가 무한대를 내는 자리다.
            float duration = WaveRewindFormula.Duration(10f, 0f, 0f);
            Assert.IsFalse(float.IsInfinity(duration));
            Assert.AreEqual(10f, duration, 0.0001f);
        }

        [Test]
        public void 배율은_언제나_양수다()
        {
            // 0 이나 음수가 나오면 되감기 길이 계산(본 시간 / 배율)이 무한대나 음수가 된다.
            foreach (float speed in new[] { -1f, 0f, 1f, 1.5f, 2f, 2.9f, 3f, 100f })
                Assert.Greater(WaveRewindFormula.PlaybackSpeed(speed), 0f, $"timeSpeed={speed}");
        }
    }
}
