using System;

namespace OJ.Core
{
    /// <summary>
    /// 세로로 긴 화면(22:9 등)에서 전투 카메라가 얼마나 보여야 하는가. 엔진을 쓰지 않는다.
    ///
    /// 전투 UI 는 캔버스가 가로 기준(1080)으로 스케일돼 화면 아래에서 위로 쌓인다. 카메라가
    /// <c>orthoSize</c> 를 고정하면 세로가 긴 기기에서 월드가 가로로 더 좁게 잘려 확대돼 보이므로,
    /// <b>월드 한 칸이 화면에서 차지하는 픽셀을 가로 기준으로 고정</b>하고 늘어난 높이만큼
    /// 카메라 위쪽을 더 보여 준다. 아래 끝은 그대로여서 플레이어·벽·보드의 월드 자리가 안 움직인다.
    ///
    /// 9:16 보다 가로로 넓은 화면은 건드리지 않는다(원래 값 그대로).
    /// </summary>
    public static class TallScreenView
    {
        /// <summary>기준 비율(가로/세로). 1080x1920.</summary>
        public const float ReferenceAspect = 1080f / 1920f;

        /// <summary>
        /// 화면 크기에 맞는 카메라 반높이. 기준 비율보다 세로로 길수록 커진다.
        /// 입력이 잘못되면 <paramref name="baseOrthoSize"/> 를 그대로 돌려준다.
        /// </summary>
        public static float OrthoSize(float screenWidth, float screenHeight, float baseOrthoSize)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || baseOrthoSize <= 0f)
                return baseOrthoSize;

            float aspect = screenWidth / screenHeight;
            if (aspect >= ReferenceAspect)
                return baseOrthoSize;

            return baseOrthoSize * ReferenceAspect / aspect;
        }

        /// <summary>
        /// 아래 끝(<c>baseCenterY - baseOrthoSize</c>)을 고정했을 때 늘어난 반높이에 맞는 카메라 중심 Y.
        /// </summary>
        public static float CenterY(float baseCenterY, float baseOrthoSize, float orthoSize)
        {
            return baseCenterY + (orthoSize - baseOrthoSize);
        }

        /// <summary>
        /// 배경 그림을 화면(가로 <paramref name="viewWidth"/> x 세로 <paramref name="viewHeight"/>)에
        /// 빈틈없이 덮으려면 곱해야 하는 배율. 이미 덮으면 1 — 줄이지는 않는다.
        /// </summary>
        public static float CoverScale(float spriteWidth, float spriteHeight, float viewWidth, float viewHeight)
        {
            if (spriteWidth <= 0f || spriteHeight <= 0f)
                return 1f;

            return Math.Max(1f, Math.Max(viewWidth / spriteWidth, viewHeight / spriteHeight));
        }
    }
}
