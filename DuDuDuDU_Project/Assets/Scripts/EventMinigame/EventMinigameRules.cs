using System;
using OJ.Point;

namespace OJ.EventMinigame
{
    /// <summary>이벤트 종류. 숫자는 <c>EventCurrency1~3</c> 의 순서와 같다.</summary>
    public enum EventKind
    {
        Fishing = 0,
        TreasureHunt = 1,
        Rafting = 2,
    }

    /// <summary>
    /// 이벤트 3종 공통 규칙. 열린 이벤트 판정은 기존 <see cref="EventCurrencyRules"/>(UTC 날짜 번호로 3일 × 3종)를
    /// 그대로 따른다 — 한국시간 오전 9시가 UTC 0시라서 원본의 하루 경계와도 맞는다.
    /// </summary>
    public static class EventMinigameRules
    {
        public const int DaysPerOccurrence = EventCurrencyRules.DaysPerEvent;
        public const int KindCount = EventCurrencyRules.EventCount;

        /// <summary>
        /// 회차 번호. 같은 종류도 9일 뒤에는 다른 회차이므로 종류가 아니라 <b>3일 묶음 번호</b>로 센다.
        /// 저장된 진행도가 이 값과 다르면 지난 회차 것이라 비운다.
        /// </summary>
        public static int OccurrenceId(int dayIndex)
        {
            return FloorDiv(dayIndex, DaysPerOccurrence);
        }

        /// <summary>그날 열린 이벤트의 종류. 음수 날짜도 같은 주기로 접는다.</summary>
        public static EventKind KindOf(int dayIndex)
        {
            int occurrence = OccurrenceId(dayIndex);
            int folded = ((occurrence % KindCount) + KindCount) % KindCount;
            return (EventKind)folded;
        }

        /// <summary>지금 회차가 끝나는 시각(UTC). 다음 회차가 시작되는 UTC 0시이고, 한국시간으로는 오전 9시다.</summary>
        public static DateTime OccurrenceEndUtc(DateTime utcNow)
        {
            int nextOccurrence = OccurrenceId(EventCurrencyRules.DayIndex(utcNow)) + 1;
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays((long)nextOccurrence * DaysPerOccurrence);
        }

        public static PointType CurrencyOf(EventKind kind)
        {
            return PointType.EventCurrency1 + (int)kind;
        }

        public static bool TryKindOfCurrency(PointType type, out EventKind kind)
        {
            kind = EventKind.Fishing;
            if (!EventCurrencyRules.IsEventCurrency(type))
                return false;

            kind = (EventKind)((int)type - (int)PointType.EventCurrency1);
            return true;
        }

        /// <summary>
        /// 마지막 마일스톤을 넘긴 진행도를 핀볼 발수로 바꾼다. <paramref name="unitsPerShot"/> 는 핀볼 1발에 드는
        /// 진행도(재화 3개 상당). 소수점은 버린다.
        /// </summary>
        public static long OverflowToShots(long excess, long unitsPerShot)
        {
            if (excess <= 0 || unitsPerShot <= 0)
                return 0;

            return excess / unitsPerShot;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            if ((value % divisor != 0) && (value < 0))
                q--;
            return q;
        }
    }
}
