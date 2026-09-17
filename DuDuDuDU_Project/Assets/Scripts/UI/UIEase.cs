namespace OJ.UI
{
    /// <summary>
    /// UI 연출에 쓰는 이징 곡선.
    ///
    /// <b>왜 한 곳에 모으나.</b> 연출이 화면마다 제각각 계산되면 같은 "팝" 이 창마다 다르게
    /// 보인다. 곡선을 여기 두면 세기를 고칠 때 한 줄만 고쳐도 전부 따라온다.
    ///
    /// 전부 <c>t=0</c> 에서 0, <c>t=1</c> 에서 1 이다 — 끝값을 따로 못박지 않아도 어긋나지 않는다.
    /// (<see cref="OutBack"/> 은 중간에만 1 을 넘는다.)
    /// </summary>
    public static class UIEase
    {
        /// <summary>
        /// 기본 오버슈트 세기. 표준값(1.70158)에 가깝게 잡았다 —
        /// <b>어두운 분위기라고 움직임까지 얌전할 이유는 없다.</b> 값이 작으면 멎는 지점이
        /// 흐릿해서 밍밍해지고, 그건 분위기가 아니라 그냥 심심한 것이 된다.
        /// </summary>
        public const float DefaultOvershoot = 1.5f;

        /// <summary>빠르게 차올랐다 부드럽게 멎는다. 1 을 넘지 않는다.</summary>
        public static float OutQuad(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv;
        }

        /// <summary><see cref="OutQuad"/> 보다 초반이 더 빠르다. 1 을 넘지 않는다.</summary>
        public static float OutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        /// <summary>
        /// 목표를 넘었다 돌아온다. <b>이것이 "팝" 의 정체다.</b>
        /// <paramref name="overshoot"/> 가 0 이면 오버슈트가 사라지고 3차 감속만 남는다.
        /// </summary>
        public static float OutBack(float t, float overshoot = DefaultOvershoot)
        {
            float inv = t - 1f;
            return 1f + (overshoot + 1f) * inv * inv * inv + overshoot * inv * inv;
        }

        /// <summary>
        /// 0 에서 솟았다 0 으로 돌아온다(펀치). 값이 무엇을 <b>더한다</b>는 뜻이라,
        /// 배율에 쓰려면 <c>1 + Punch(t) * 세기</c> 꼴로 더해야 한다.
        ///
        /// 한 번만 솟고 흔들리지 않는다 — 감쇠 사인파는 값이 여러 번 오가서 숫자를 읽기 어렵다.
        /// </summary>
        public static float Punch(float t)
        {
            // t=0 → 0, t=0.5 → 1, t=1 → 0 인 부드러운 산.
            float x = t * 2f;
            if (x > 1f)
                x = 2f - x;

            return OutQuad(x);
        }
    }
}
