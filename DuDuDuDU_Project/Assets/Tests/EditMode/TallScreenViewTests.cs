using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>세로로 긴 화면에서 카메라가 가로 픽셀 밀도를 지키며 위로만 늘어나는지 잠근다.</summary>
    public sealed class TallScreenViewTests
    {
        [Test]
        public void ReferenceScreen_KeepsTheBaseSize()
        {
            Assert.That(TallScreenView.OrthoSize(1080f, 1920f, 5f), Is.EqualTo(5f).Within(0.0001f));
        }

        [TestCase(1080f, 1600f)]
        [TestCase(1920f, 1080f)]
        [TestCase(1536f, 2048f)]
        public void WiderThanReference_IsLeftAlone(float width, float height)
        {
            Assert.That(TallScreenView.OrthoSize(width, height, 5f), Is.EqualTo(5f));
        }

        [Test]
        public void TwentyTwoByNine_ShowsMoreWorldOnTop()
        {
            // 1080x2640: 세로 2640px 을 1920px 일 때와 같은 밀도(192px/유닛)로 그리면 13.75 유닛 = 반높이 6.875.
            Assert.That(TallScreenView.OrthoSize(1080f, 2640f, 5f), Is.EqualTo(6.875f).Within(0.0001f));
        }

        [TestCase(2160f)]
        [TestCase(2400f)]
        [TestCase(2640f)]
        public void PixelsPerWorldUnit_StaysConstant(float height)
        {
            float ortho = TallScreenView.OrthoSize(1080f, height, 5f);
            Assert.That(height / (2f * ortho), Is.EqualTo(192f).Within(0.001f));
        }

        [Test]
        public void BottomEdgeStaysPut()
        {
            float ortho = TallScreenView.OrthoSize(1080f, 2640f, 5f);
            float centerY = TallScreenView.CenterY(0f, 5f, ortho);
            Assert.That(centerY - ortho, Is.EqualTo(-5f).Within(0.0001f));
        }

        [TestCase(0f, 1920f, 5f)]
        [TestCase(1080f, 0f, 5f)]
        [TestCase(1080f, 1920f, 0f)]
        public void InvalidInput_ReturnsTheBase(float width, float height, float baseOrtho)
        {
            Assert.That(TallScreenView.OrthoSize(width, height, baseOrtho), Is.EqualTo(baseOrtho));
        }

        [Test]
        public void CoverScale_NeverShrinks_AndGrowsToCoverTheLongerSide()
        {
            Assert.That(TallScreenView.CoverScale(6.58f, 11.7f, 5.625f, 10f), Is.EqualTo(1f));
            Assert.That(TallScreenView.CoverScale(6.58f, 11.7f, 5.625f, 13.75f), Is.EqualTo(13.75f / 11.7f).Within(0.0001f));
            Assert.That(TallScreenView.CoverScale(6.58f, 11.7f, 9f, 10f), Is.EqualTo(9f / 6.58f).Within(0.0001f));
        }

        [Test]
        public void CoverScale_BadSpriteSize_IsOne()
        {
            Assert.That(TallScreenView.CoverScale(0f, 11.7f, 5f, 10f), Is.EqualTo(1f));
        }
    }
}
