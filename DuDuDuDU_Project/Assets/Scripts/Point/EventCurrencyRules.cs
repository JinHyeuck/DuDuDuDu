using System;

namespace OJ.Point
{
    /// <summary>
    /// "열린 이벤트 재화" 지급용 표지를 그날 열린 이벤트의 구체적인 재화로 푸는 규칙. 엔진을 쓰지 않는다.
    ///
    /// <b>이벤트 시스템이 서기 전의 배선이다.</b> 이벤트 3종이 3일씩 돌아가며 열린다(9일 주기)는 합의만 코드로 옮겼고,
    /// 이벤트가 실제로 열리고 닫히는 데이터가 생기면 <see cref="Resolve"/> 의 날짜 계산을 그쪽 조회로 바꾼다.
    /// 호출부(<c>PointManager.Add</c>)와 보상표는 그대로 둔다.
    /// </summary>
    public static class EventCurrencyRules
    {
        /// <summary>이벤트 하나가 열려 있는 일수.</summary>
        public const int DaysPerEvent = 3;

        public const int EventCount = 3;

        /// <summary>한 바퀴(모든 이벤트가 한 번씩 열리는) 일수 = 9.</summary>
        public const int CycleDays = DaysPerEvent * EventCount;

        /// <summary>지급용 표지인가. true 면 보관하지 않고 구체적인 이벤트 재화로 풀어서 넣는다.</summary>
        public static bool IsOpenEvent(PointType pointType)
        {
            return pointType == PointType.OpenEventCurrency;
        }

        /// <summary>구체적인 이벤트 재화인가(보관되는 재화).</summary>
        public static bool IsEventCurrency(PointType pointType)
        {
            return pointType == PointType.EventCurrency1
                   || pointType == PointType.EventCurrency2
                   || pointType == PointType.EventCurrency3;
        }

        /// <summary>
        /// 0 부터 세는 날짜 번호(<paramref name="dayIndex"/>)에 열린 이벤트의 재화.
        /// 음수 날짜도 같은 주기로 접는다.
        /// </summary>
        /// <summary>
        /// 열린 이벤트가 있는가. 재화 상점의 이벤트 재화 상품은 열린 이벤트 중에만 보인다.
        /// <b>이벤트 시스템이 아직 없어 항상 true 다</b>(3일 × 3종이 쉬지 않고 돈다) — 시스템이 생기면 여기 한 곳을 고친다.
        /// </summary>
        public static bool IsEventOpen(DateTime utcNow)
        {
            return true;
        }

        public static PointType Resolve(int dayIndex)
        {
            int inCycle = ((dayIndex % CycleDays) + CycleDays) % CycleDays;
            switch (inCycle / DaysPerEvent)
            {
                case 0: return PointType.EventCurrency1;
                case 1: return PointType.EventCurrency2;
                default: return PointType.EventCurrency3;
            }
        }

        /// <summary>
        /// UTC 시각의 날짜 번호. 기준일(1970-01-01)부터 센다 — 이벤트 시스템이 서면 서버가 정한 시작일로 바꾼다.
        /// </summary>
        public static int DayIndex(DateTime utcNow)
        {
            return (int)Math.Floor((utcNow.Date - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalDays);
        }

        /// <summary>
        /// 지급 표지면 그 순간 열린 이벤트의 재화로, 아니면 그대로 돌려준다.
        /// </summary>
        public static PointType ResolveReward(PointType pointType, DateTime utcNow)
        {
            return IsOpenEvent(pointType) ? Resolve(DayIndex(utcNow)) : pointType;
        }
    }
}
