using System;
using System.Globalization;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 시즌 패스 화면의 <b>표기 규칙</b>. 뷰에서 꺼내 둔 것이라 <c>UnityEngine.UI</c> 를
    /// 보지 않는다 — 그래야 헤드리스 러너에서 테스트된다
    /// (<c>OJ.Shop.ShopText</c>·<c>OJ.Mission.MissionText</c> 와 같은 이유).
    /// </summary>
    public static class SeasonPassText
    {
        /// <summary>
        /// 시즌 종료까지 남은 시간. <c>8일 17시간</c> 꼴이고, 하루 미만이면 <c>17시간</c> 만 적는다.
        ///
        /// <b>분은 적지 않는다.</b> 시즌은 한 달짜리라 분 단위가 바뀌는 것이 정보가 아니고,
        /// 매 분 다시 그려야 한다. 마지막 한 시간이 중요해지는 날 그때 늘린다.
        /// </summary>
        public static string Remaining(TimeSpan remaining)
        {
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            int days = remaining.Days;
            int hours = remaining.Hours;

            if (days <= 0)
                return hours.ToString(CultureInfo.InvariantCulture) + "시간";

            return days.ToString(CultureInfo.InvariantCulture) + "일 " +
                   hours.ToString(CultureInfo.InvariantCulture) + "시간";
        }

        /// <summary>
        /// 포인트 게이지의 글자. <c>340 / 1000</c> 꼴이다.
        ///
        /// <b>최대 레벨에서는 "MAX" 다.</b> 거기서는 포인트를 더 쌓아도 오를 곳이 없는데
        /// 숫자를 계속 보여 주면 "왜 안 오르지" 가 된다.
        /// </summary>
        public static string PointGauge(int into, int perLevel, int level, int maxLevel)
        {
            if (maxLevel > 0 && level >= maxLevel)
                return "MAX";

            return into.ToString(CultureInfo.InvariantCulture) + " / " +
                   perLevel.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>로비 버튼에 적는 한 줄. <c>Lv.3 · 8일 남음</c> 대신 쉼표를 쓴다(글꼴에 가운데점이 없다).</summary>
        public static string LobbySummary(int level, TimeSpan remaining)
        {
            return "Lv." + level.ToString(CultureInfo.InvariantCulture) + ", " + Remaining(remaining) + " 남음";
        }
    }
}
