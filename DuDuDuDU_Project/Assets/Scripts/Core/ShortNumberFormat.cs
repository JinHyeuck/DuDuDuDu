using System.Globalization;

namespace OJ.Core
{
    /// <summary>
    /// 큰 정수를 좁은 칸에 넣기 위한 축약 표기. <c>5550 → "5.550K"</c>, <c>231000000 → "231.000M"</c>.
    ///
    /// <b>BigDouble 을 들이는 것과는 무관하다.</b> AGENTS 가 금지한 것은 <b>연산</b>을
    /// 큰 수 라이브러리로 옮기는 것이고, 이것은 <c>int</c> 를 <b>글자로 바꾸는</b> 일만 한다.
    /// 값은 여전히 <c>int</c> 이고 계산에는 쓰이지 않는다.
    ///
    /// <b>소수 세 자리를 고정한다.</b> 자릿수가 들쭉날쭉하면 카드 여섯 장이 세로로
    /// 안 맞고, 그 어긋남은 폰트에 따라 보였다 안 보였다 한다.
    ///
    /// <c>CultureInfo.InvariantCulture</c> 를 명시하는 이유는 한국어 로캘이 아니라
    /// <b>소수점을 쉼표로 쓰는 로캘</b> 때문이다 — 거기서는 "5,550K" 가 되어 천 단위
    /// 구분자와 구별되지 않는다.
    /// </summary>
    public static class ShortNumberFormat
    {
        private const int Thousand = 1000;
        private const int Million = 1000000;

        public static string Format(int value)
        {
            if (value < 0)
                return "-" + Format(-value);

            if (value < Thousand)
                return value.ToString(CultureInfo.InvariantCulture);

            if (value < Million)
                return (value / (float)Thousand).ToString("0.000", CultureInfo.InvariantCulture) + "K";

            return (value / (float)Million).ToString("0.000", CultureInfo.InvariantCulture) + "M";
        }
    }
}
