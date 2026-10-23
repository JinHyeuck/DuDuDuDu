using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>교전선이 기여도 패널 윗선 높이로 떨어지고, 화면 비율이 달라도 패널을 따라가는지 잠근다.</summary>
    public sealed class EngageLineTests
    {
        // 패널 윗선이 화면 아래에서 1670 캔버스 단위 위 (1080x1920 기준).
        private const float PanelTop = 1670f;

        [Test]
        public void Reference1080x1920_PutsTheLineBelowTheSpawnHeight()
        {
            float y = EngageLine.WorldY(PanelTop, 1080f, 1080f, 1920f, 0f, 5f);
            Assert.That(y, Is.EqualTo(1670f / 1920f * 10f - 5f).Within(0.0001f));
            Assert.Less(y, 5f);
            Assert.Greater(y, 0f);
        }

        [Test]
        public void FixedCameraOnTallerScreen_PullsTheLineDown()
        {
            float standard = EngageLine.WorldY(PanelTop, 1080f, 1080f, 1920f, 0f, 5f);
            float tall = EngageLine.WorldY(PanelTop, 1080f, 1080f, 2400f, 0f, 5f);
            Assert.Less(tall, standard);
        }

        [Test]
        public void FittedCameraOnTallScreen_KeepsTheLineAtTheSameWorldHeight()
        {
            float standard = EngageLine.WorldY(PanelTop, 1080f, 1080f, 1920f, 0f, 5f);

            foreach (float height in new[] { 2160f, 2400f, 2640f })
            {
                float ortho = TallScreenView.OrthoSize(1080f, height, 5f);
                float centerY = TallScreenView.CenterY(0f, 5f, ortho);
                float tall = EngageLine.WorldY(PanelTop, 1080f, 1080f, height, centerY, ortho);
                Assert.That(tall, Is.EqualTo(standard).Within(0.0001f), "height " + height);
            }
        }

        [Test]
        public void CameraOffset_ShiftsTheLine()
        {
            float centered = EngageLine.WorldY(PanelTop, 1080f, 1080f, 1920f, 0f, 5f);
            float shifted = EngageLine.WorldY(PanelTop, 1080f, 1080f, 1920f, 1f, 5f);
            Assert.That(shifted - centered, Is.EqualTo(1f).Within(0.0001f));
        }

        [TestCase(0f, 1920f, 5f)]
        [TestCase(1080f, 0f, 5f)]
        [TestCase(1080f, 1920f, 0f)]
        public void InvalidScreen_EngagesEveryone(float screenWidth, float screenHeight, float orthoSize)
        {
            float y = EngageLine.WorldY(PanelTop, 1080f, screenWidth, screenHeight, 0f, orthoSize);
            Assert.IsTrue(EngageLine.IsEngaged(5f, y));
        }

        [Test]
        public void IsEngaged_AboveTheLineWaits_OnOrBelowFights()
        {
            Assert.IsFalse(EngageLine.IsEngaged(3.8f, 3.7f));
            Assert.IsTrue(EngageLine.IsEngaged(3.7f, 3.7f));
            Assert.IsTrue(EngageLine.IsEngaged(-2f, 3.7f));
        }
    }
}
