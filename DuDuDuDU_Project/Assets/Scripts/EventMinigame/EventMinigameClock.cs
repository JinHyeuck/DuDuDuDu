using System;
using OJ.Point;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 이벤트 화면이 쓰는 "지금". 평소에는 <see cref="DateTime.UtcNow"/> 와 같다.
    /// <see cref="DevOffset"/> 은 <b>개발 확인 전용</b>이다 — 이벤트가 3일마다 도는 탓에 오늘 열리지 않은 이벤트를
    /// 플레이해 볼 수 없어서, 시계를 앞으로 밀어 그 이벤트가 열린 날로 간다. 저장하지 않으므로 앱을 다시 켜면 0 이다.
    /// (핀볼이 이벤트 재화를 주는 쪽은 별도로 실제 시각을 본다 — 재화는 <c>PointManager.Add</c> 로 직접 넣어 확인할 것.)
    /// </summary>
    public static class EventMinigameClock
    {
        public static TimeSpan DevOffset;

        public static DateTime UtcNow
        {
            get { return DateTime.UtcNow + DevOffset; }
        }

        /// <summary>
        /// <paramref name="kind"/> 이벤트가 열리도록 시계를 얼마나 밀어야 하는지. 이미 열려 있으면 0,
        /// 아니면 그 종류의 <b>다음 회차 시작 12시간 뒤</b>로 보낸다(회차 끝자락에 떨어지지 않게).
        /// 개발 치트용이라 <paramref name="realUtcNow"/> 를 인자로 받는다 — 헤드리스 테스트가 날짜를 고정할 수 있다.
        /// </summary>
        public static TimeSpan OffsetToOpen(EventKind kind, DateTime realUtcNow)
        {
            int day = EventCurrencyRules.DayIndex(realUtcNow);
            int occurrence = EventMinigameRules.OccurrenceId(day);

            for (int i = 0; i < EventMinigameRules.KindCount; i++)
            {
                int target = occurrence + i;
                if (EventMinigameRules.KindOf(target * EventMinigameRules.DaysPerOccurrence) != kind)
                    continue;

                if (i == 0)
                    return TimeSpan.Zero;

                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                DateTime start = epoch.AddDays((double)target * EventMinigameRules.DaysPerOccurrence).AddHours(12);
                return start - realUtcNow;
            }

            return TimeSpan.Zero;
        }
    }
}
