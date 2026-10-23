namespace OJ.Core
{
    /// <summary>
    /// 몬스터를 쏘기 시작하는 높이(월드 Y). 엔진을 쓰지 않는다.
    ///
    /// <b>왜 필요한가.</b> 몬스터는 화면 맨 위에서 나오는데, 다이스가 나오자마자 쏘면
    /// 유저 눈에는 "보이기도 전에 죽는다" 로 읽힌다. 그래서 왼쪽 기여도 패널의 윗선 높이에
    /// 몬스터가 닿은 뒤부터 표적으로 삼는다 — 화면에서 보이는 그 선이 곧 교전선이다.
    ///
    /// <b>월드 좌표로 바꾸는 식.</b> UI 캔버스는 가로 기준(1080)으로 스케일되고, 패널은 화면
    /// <b>아래</b>에 붙어 있다(세로가 긴 기기에서 위로 늘어나는 쪽이 빈 공간이어야 하므로).
    /// 패널 윗선이 화면 아래에서 캔버스 단위로 얼마 위인지를 알면, 화면 픽셀로 바꾼 뒤 화면 높이로
    /// 나눈 비율에 카메라 전체 높이(2 × orthoSize)를 곱해 카메라 아래에서의 월드 높이가 나온다.
    /// 카메라 아래 끝(<c>cameraY - orthoSize</c>)을 더하면 월드 Y 다.
    ///
    /// 카메라도 아래 끝을 고정하고 위로 늘어나므로(<see cref="TallScreenView"/>), 화면 비율이
    /// 달라도 교전선의 월드 Y 는 같은 값에 머문다.
    /// </summary>
    public static class EngageLine
    {
        /// <summary>
        /// 교전선의 월드 Y. 화면 크기를 모르면(0 이하) <c>+무한</c> 을 돌려 모두 교전 가능으로 둔다 —
        /// 잘못된 입력이 "아무도 못 쏜다" 로 번지는 쪽이 훨씬 나쁘다.
        /// </summary>
        public static float WorldY(
            float panelTopFromBottom,
            float referenceWidth,
            float screenWidth,
            float screenHeight,
            float cameraY,
            float orthoSize)
        {
            if (referenceWidth <= 0f || screenWidth <= 0f || screenHeight <= 0f || orthoSize <= 0f)
                return float.PositiveInfinity;

            float pixelsFromBottom = panelTopFromBottom * (screenWidth / referenceWidth);
            float screenFraction = pixelsFromBottom / screenHeight;
            return (cameraY - orthoSize) + screenFraction * 2f * orthoSize;
        }

        /// <summary>선 위(Y 가 더 큼)에 있으면 아직 표적이 아니다. 선 위에 정확히 걸치면 교전이다.</summary>
        public static bool IsEngaged(float monsterY, float lineY)
        {
            return monsterY <= lineY;
        }
    }
}
