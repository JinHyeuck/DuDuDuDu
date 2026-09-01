using System;

namespace OJ.Core
{
    /// <summary>
    /// <c>UnityEngine.Mathf</c> 를 대신한다. (MIGRATION_BASELINE 11.1)
    ///
    /// <b>왜 <c>System.Math</c> 를 바로 쓰지 않나.</b> 의미가 다르다.
    /// <c>Mathf.Max(float.NaN, 1f)</c> 는 <b>1f</b> 를 돌려주지만
    /// <c>Math.Max(float.NaN, 1f)</c> 는 <b>NaN</b> 을 돌려준다. Unity 의 구현이
    /// <c>a &gt; b ? a : b</c> 인데 NaN 비교가 항상 거짓이라 b 로 떨어지기 때문이다.
    /// 이 프로젝트의 계산식에는 0 나누기에서 NaN 이 나올 수 있는 자리가 실제로 있어서
    /// (벽 HP 비율), 그 차이가 화면에 나타난다.
    ///
    /// 그래서 <b>Unity 구현을 그대로 옮겼다.</b> 여기서 하는 일은 "더 나은 수학"이 아니라
    /// <b>같은 수학을 엔진 없이</b> 하는 것이다. 값이 바뀌면 그것은 이 파일의 버그다.
    ///
    /// <b>무엇을 얻나.</b> <c>OJ.Core</c> 가 <c>UnityEngine</c> 을 참조하지 않게 되어
    /// 규칙 계층이 엔진과 완전히 분리된다. 컴파일이 빨라지고, 무엇보다 <b>엔진 타입이
    /// 규칙 안으로 새어 드는 것을 컴파일러가 막는다.</b>
    ///
    /// 값이 그대로인지는 골든 테스트가 검증한다.
    /// </summary>
    public static class OJMath
    {
        /// <summary>
        /// <c>Mathf.Max</c> 와 같다 — <c>a &gt; b ? a : b</c>.
        /// <b>NaN 이 들어오면 b 를 돌려준다.</b> <c>Math.Max</c> 와 갈리는 지점이다.
        /// </summary>
        public static float Max(float a, float b) => a > b ? a : b;

        public static int Max(int a, int b) => a > b ? a : b;

        public static float Min(float a, float b) => a < b ? a : b;

        public static int Min(int a, int b) => a < b ? a : b;

        /// <summary>
        /// <c>Mathf.Clamp</c> 와 같다.
        ///
        /// <b><c>min &gt; max</c> 일 때의 결과가 <c>Math.Clamp</c> 와 다르다.</b>
        /// 이쪽은 조용히 <paramref name="max"/> 를 돌려주고, <c>Math.Clamp</c> 는 예외를 던진다.
        /// 기존 동작을 지키려고 Unity 쪽을 따랐다.
        /// </summary>
        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
                value = min;
            else if (value > max)
                value = max;

            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
                value = min;
            else if (value > max)
                value = max;

            return value;
        }

        public static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            return value > 1f ? 1f : value;
        }

        /// <summary>
        /// <c>Mathf.RoundToInt</c> 와 같다 — <c>(int)Math.Round(f)</c>.
        ///
        /// <b>은행가 반올림이다.</b> 0.5 는 가까운 <i>짝수</i>로 간다(2.5 → 2, 3.5 → 4).
        /// <c>(int)(f + 0.5f)</c> 로 바꾸면 값이 달라지므로 그렇게 하지 말 것.
        /// </summary>
        public static int RoundToInt(float f) => (int)Math.Round(f);

        public static int CeilToInt(float f) => (int)Math.Ceiling(f);

        public static int FloorToInt(float f) => (int)Math.Floor(f);

        public static float Pow(float f, float p) => (float)Math.Pow(f, p);

        public static float Abs(float f) => Math.Abs(f);

        public static int Abs(int value) => Math.Abs(value);
    }
}
