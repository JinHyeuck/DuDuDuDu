using System;
using System.Globalization;

namespace OJ.Core
{
    /// <summary>
    /// 고기 멤버십과 무료 소탕 한도의 순수 규칙. (기획서 BMDesign 5장 · ShopPackageDesign 5장)
    ///
    /// <b>멤버십은 기간제 아이템이다.</b> 스토어 자동 갱신 구독이 아니라 산 순간부터 30일이
    /// 흐르고, 남은 기간 중에 또 사면 <b>만료일 뒤로</b> 붙는다 — 3일 남았을 때 재구매를 열어 두는데
    /// (기획서 5.2-4) 그때 사는 사람이 남은 3일을 잃으면 일찍 산 사람이 손해를 본다.
    ///
    /// <b>시각은 UTC tick 이다.</b> 기간은 경과 시간으로 재는 것이 맞다(23시에 산 30일권이
    /// 다음 날 0시에 하루를 잃으면 안 된다). 반면 소탕 한도는 <b>날짜</b>로 리셋한다 —
    /// 일일 미션·상점 리셋과 "오늘" 이 한 가지 뜻이어야 한다.
    /// </summary>
    public static class MembershipRules
    {
        /// <summary>
        /// 저장된 만료 시각이 없다는 뜻. 한 번도 산 적이 없으면 이 값이다.
        /// </summary>
        public const long NoExpiry = 0L;

        /// <summary>지금 멤버십이 살아 있는가. 경계는 <b>미만</b>이다 — 만료 시각 그 순간부터는 끝이다.</summary>
        public static bool IsActive(long expiryUtcTicks, long nowUtcTicks)
        {
            return expiryUtcTicks > nowUtcTicks;
        }

        /// <summary>
        /// <paramref name="days"/> 일을 더한 새 만료 시각.
        /// 살아 있으면 <b>만료일 뒤로</b> 붙이고, 끝났으면 지금부터 센다.
        /// </summary>
        public static long Extend(long expiryUtcTicks, long nowUtcTicks, int days)
        {
            long start = IsActive(expiryUtcTicks, nowUtcTicks) ? expiryUtcTicks : nowUtcTicks;
            return start + TimeSpan.FromDays(Math.Max(0, days)).Ticks;
        }

        /// <summary>
        /// 화면에 적는 남은 일수. <b>올림</b>이다 — 29일 1시간 남았으면 "30일" 이고,
        /// 1분 남았어도 "1일" 이다. 내림으로 두면 산 직후부터 "29일" 로 보여 하루를 덜 준 것처럼 읽힌다.
        /// 끝났으면 0.
        /// </summary>
        public static int RemainingDays(long expiryUtcTicks, long nowUtcTicks)
        {
            if (!IsActive(expiryUtcTicks, nowUtcTicks))
                return 0;

            long left = expiryUtcTicks - nowUtcTicks;
            return (int)((left + TimeSpan.TicksPerDay - 1) / TimeSpan.TicksPerDay);
        }

        /// <summary>
        /// 다시 살 수 있는가. 없거나, 남은 일수가 <paramref name="rebuyWindowDays"/> 이하일 때다
        /// (기획서 5.2-4 — 만료 3일 전부터 재구매 버튼). 그 전에는 카드가 "잔여 n일" 만 보인다.
        /// </summary>
        public static bool CanRebuy(long expiryUtcTicks, long nowUtcTicks, int rebuyWindowDays)
        {
            return RemainingDays(expiryUtcTicks, nowUtcTicks) <= Math.Max(0, rebuyWindowDays);
        }

        /// <summary>
        /// 스테이지 입장료. 멤버십이면 0 이다(기획서 5.2-2 — 헤비 유저를 잡는 축).
        /// </summary>
        public static int StageEntryCost(int baseCost, bool member)
        {
            return member ? 0 : Math.Max(0, baseCost);
        }

        // ── 무료 소탕 한도 ─────────────────────────────────────────────

        /// <summary>한도가 없다는 뜻. 멤버십이면 이 값을 돌려준다.</summary>
        public const int Unlimited = int.MaxValue;

        /// <summary>
        /// 소탕 횟수를 세는 날짜 키. <c>yyyy-MM-dd</c>, <b>로컬 날짜</b>다.
        /// 문화권을 안 타게 <see cref="CultureInfo.InvariantCulture"/> 를 명시한다.
        /// </summary>
        public static string DayKey(DateTime localNow)
        {
            return localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 오늘 쓴 횟수. 저장된 날짜가 오늘이 아니면 0 이다 — 날이 바뀐 뒤 처음 읽는 순간
        /// 리셋되므로 자정에 따로 도는 타이머가 필요 없다.
        /// </summary>
        public static int UsedToday(string storedDayKey, int storedCount, string todayKey)
        {
            return string.Equals(storedDayKey, todayKey, StringComparison.Ordinal)
                ? Math.Max(0, storedCount)
                : 0;
        }

        /// <summary>
        /// 오늘 더 돌 수 있는 소탕 횟수. 멤버십이면 <see cref="Unlimited"/> 다(기획서 5.2-1).
        /// </summary>
        public static int SweepsLeftToday(int dailyLimit, int usedToday, bool member)
        {
            if (member)
                return Unlimited;

            return Math.Max(0, Math.Max(0, dailyLimit) - Math.Max(0, usedToday));
        }

        /// <summary>
        /// 소탕 창이 고를 수 있는 최대 횟수 — 고기로 돌 수 있는 횟수와 오늘 남은 한도 중 작은 쪽.
        /// <b>둘을 따로 자르지 않는다</b>: 창이 고기로만 MAX 를 잡고 버튼에서 한도로 깎으면
        /// 표시된 횟수와 실제로 돈 횟수가 달라진다.
        /// </summary>
        public static int MaxSweepCount(int affordableCount, int sweepsLeftToday)
        {
            return Math.Max(0, Math.Min(affordableCount, sweepsLeftToday));
        }
    }
}
