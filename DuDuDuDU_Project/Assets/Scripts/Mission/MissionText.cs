using System;
using System.Globalization;

namespace OJ.Mission
{
    /// <summary>
    /// 미션 화면의 <b>표기 규칙</b>. 뷰에서 꺼내 둔 것이라 <c>UnityEngine.UI</c> 를 보지 않는다 —
    /// 그래야 헤드리스 러너에서 테스트된다(<c>OJ.Shop.ShopText</c> 와 같은 이유).
    /// </summary>
    public static class MissionText
    {
        /// <summary>
        /// 제목. <c>{0}</c> 자리에 요구 횟수가 들어간다.
        ///
        /// <b>서식이 틀려도 던지지 않는다.</b> 에셋에 <c>{1}</c> 같은 것이 들어가면
        /// <c>string.Format</c> 이 예외를 던지는데, 그러면 <b>미션 한 줄의 오타가
        /// 목록 전체를 못 그리게 만든다.</b> 그때는 서식 문자열을 그대로 보여 주고
        /// 넘어간다 — 화면에 <c>{1}</c> 이 보이는 것이 빈 창보다 고치기 쉽다.
        /// </summary>
        public static string Title(string titleFormat, int requiredCount)
        {
            if (string.IsNullOrEmpty(titleFormat))
                return string.Empty;

            try
            {
                return string.Format(CultureInfo.InvariantCulture, titleFormat, requiredCount);
            }
            catch (FormatException)
            {
                return titleFormat;
            }
        }

        /// <summary>
        /// 진행도. <c>0/2</c> 꼴이다.
        ///
        /// <b>달성한 뒤에도 요구치를 넘겨 보여 주지 않는다</b>(<c>7/2</c> 가 아니라 <c>2/2</c>).
        /// 넘은 만큼은 다음 단계가 쓸 몫이고, 이 줄에서는 "다 됐다" 는 사실만 읽히면 된다.
        /// </summary>
        public static string Progress(int count, int requiredCount)
        {
            int shown = count;
            if (requiredCount > 0 && shown > requiredCount)
                shown = requiredCount;

            if (shown < 0)
                shown = 0;

            return shown.ToString(CultureInfo.InvariantCulture) + "/" +
                   requiredCount.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 리셋까지 남은 시간. <c>14시간 49분</c> 꼴이고, 1시간 미만이면 <c>49분</c> 만 적는다.
        ///
        /// <b>초는 적지 않는다.</b> 매초 다시 그려야 하는데 그 정보가 필요한 순간이 없다 —
        /// 유저가 알고 싶은 것은 "오늘 안에 더 할 수 있나" 하나다.
        /// 음수는 0 으로 접는다(기기 시계를 뒤로 돌린 경우).
        /// </summary>
        public static string ResetCountdown(TimeSpan remaining)
        {
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            int totalHours = (int)remaining.TotalHours;
            int minutes = remaining.Minutes;

            if (totalHours <= 0)
                return minutes.ToString(CultureInfo.InvariantCulture) + "분";

            return totalHours.ToString(CultureInfo.InvariantCulture) + "시간 " +
                   minutes.ToString(CultureInfo.InvariantCulture) + "분";
        }

        /// <summary>추가 보상 칸에 적는 숫자. 화면의 선물 상자 아래 숫자가 이것이다.</summary>
        public static string TierLabel(int requiredClearCount)
        {
            return requiredClearCount.ToString(CultureInfo.InvariantCulture);
        }
    }
}
