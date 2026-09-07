namespace OJ.Core
{
    /// <summary>
    /// 웨이브 되돌리기의 <b>되감기 속도</b>. 배속에 따라 달라진다.
    ///
    /// <b>왜 배속을 보는가.</b> 되감기 길이는 "유저가 실제로 본 시간" 을 기준으로 잡는다 —
    /// 웨이브가 길었으면 되감기도 길다. 그런데 3배속으로 돌리던 사람은 이미 <b>빨리 보는 것에
    /// 익숙한 상태</b>라, 1배속과 같은 배율로 되감으면 그 사람에게만 유독 느리게 느껴진다.
    ///
    /// <b>비례식이 아니라 표다.</b> 2배속의 5는 2배(=4)도 2.5배도 아닌 그냥 5다 —
    /// 기획이 정한 값이라 식으로 유도하지 않는다. 식으로 바꾸는 순간 표의 어느 칸이
    /// 의도이고 어느 칸이 계산의 부산물인지 구별할 수 없게 된다.
    /// </summary>
    public static class WaveRewindFormula
    {
        /// <summary>
        /// 되감기 배율. 웨이브를 <b>실제로 본 시간</b>을 이 값으로 나눈 만큼 되감기가 걸린다.
        ///
        /// 배속은 1·2·3 만 쓰이지만(<c>GameManager.OnClick_Speed</c>) 그 사이나 밖의 값이
        /// 들어와도 답이 있어야 한다 — 경계를 <c>&gt;=</c> 로 잡아 3 이상은 전부 6 이다.
        /// </summary>
        public static float PlaybackSpeed(float timeSpeed)
        {
            if (timeSpeed >= 3f)
                return 6f;

            if (timeSpeed >= 2f)
                return 5f;

            return 2f;
        }

        /// <summary>
        /// 되감기가 실제로 걸리는 시간.
        ///
        /// <b>상한이 필요한 이유.</b> 원래는 "웨이브가 길면 되감기도 길다" 만으로 잡았는데,
        /// 1배속으로 60초 버틴 웨이브가 30초짜리 되감기가 됐다. 되감기는 <b>판단을 다시 하러
        /// 돌아가는 길</b>이지 감상하는 시간이 아니다 — 길어지는 순간 되돌리기를 쓰는 것
        /// 자체가 손해로 느껴진다.
        ///
        /// <b>상한에 걸려도 커서는 웨이브 전체를 훑는다.</b> 짧아지는 것은 실제 걸리는
        /// 시간뿐이고, 되감는 <i>구간</i>은 그대로다. 그래서 긴 웨이브는 그냥 더 빨리 감긴다.
        /// </summary>
        /// <param name="waveElapsed">웨이브를 실제로 본 시간(초).</param>
        /// <param name="playbackSpeed"><see cref="PlaybackSpeed"/> 가 준 배율.</param>
        /// <param name="maxSeconds">상한. <b>0 이하면 상한 없음</b>이다.</param>
        public static float Duration(float waveElapsed, float playbackSpeed, float maxSeconds)
        {
            if (waveElapsed <= 0f)
                return 0f;

            // 배율이 0 이하로 들어오는 경로는 없지만(PlaybackSpeed 가 항상 양수다),
            // 나누기가 무한대를 내는 자리라 방어한다. 그때는 본 속도 그대로 되감는다.
            float natural = playbackSpeed > 0f ? waveElapsed / playbackSpeed : waveElapsed;

            if (maxSeconds > 0f && natural > maxSeconds)
                return maxSeconds;

            return natural;
        }
    }
}
