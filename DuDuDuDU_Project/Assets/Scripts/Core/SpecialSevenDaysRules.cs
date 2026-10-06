using System;
using System.Collections.Generic;
using System.Globalization;

namespace OJ.Core
{
    /// <summary>특별한 7일 한 칸의 상태. 화면이 이것 하나로 그린다. (기획서 ShopPackageDesign 4.3)</summary>
    public enum SpecialSevenDaysCellState
    {
        /// <summary>아직 그날이 안 왔다. 회색.</summary>
        Upcoming = 0,

        /// <summary>그날이 왔지만 안 샀다. 자물쇠 + <b>수량은 보인다</b>(4.3 — "이미 쌓인 내 보상").</summary>
        Accrued = 1,

        /// <summary>샀고, 그날이 왔고, 아직 안 받았다.</summary>
        Claimable = 2,

        /// <summary>받았다.</summary>
        Claimed = 3,
    }

    /// <summary>
    /// 특별한 7일 출석의 순수 규칙. (기획서 BMDesign 6장 · ShopPackageDesign 4장)
    ///
    /// <b>출석은 접속이 아니라 날짜로 센다.</b> 6.1-1 "첫날에 사지 않았더라도 오픈일 기준으로 출석은
    /// 계속 적립" — 시작일로부터 n일이 지났으면 n+1일차까지 열린다. 접속한 날만 세면 하루 빠진 사람이
    /// 8일째에도 7일차에 못 닿고, "뒤늦게 사도 손해가 없다"(6.1)가 깨진다.
    ///
    /// <b>시작일은 유저별이다</b>(BMDesign 8장 표 "기준일: 유저별"). 처음 앱을 켠 날이다.
    ///
    /// <b>날짜는 로컬이다.</b> 일일 미션·상점 리셋과 "오늘" 이 한 가지 뜻이어야 한다.
    /// </summary>
    public static class SpecialSevenDaysRules
    {
        /// <summary>칸 수. 기획서의 "7일" 이다 — 데이터가 이보다 길어도 7칸만 쓴다.</summary>
        public const int DayCount = 7;

        /// <summary>날짜 키. <c>yyyy-MM-dd</c>, 문화권을 안 타게 <see cref="CultureInfo.InvariantCulture"/>.</summary>
        public static string DayKey(DateTime localNow)
        {
            return localNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 오늘이 몇 일차인가(1부터). 시작일이 비었거나 깨졌으면 <b>1일차</b>로 본다 —
        /// 부르는 쪽이 그 자리에서 오늘을 시작일로 적는다.
        ///
        /// 기기 시계를 뒤로 돌려 오늘이 시작일보다 앞이면 1 로 접는다. 0일차·음수 일차는 없다.
        /// <b>위로는 접지 않는다</b> — 8 이상이면 "기간이 지났다" 는 뜻이고 그것을 판정에 쓴다.
        /// </summary>
        public static int CurrentDay(string startDayKey, DateTime localNow)
        {
            if (!TryParseDayKey(startDayKey, out DateTime start))
                return 1;

            int days = (localNow.Date - start.Date).Days;
            return days < 0 ? 1 : days + 1;
        }

        public static bool TryParseDayKey(string dayKey, out DateTime date)
        {
            return DateTime.TryParseExact(dayKey, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out date);
        }

        /// <summary>
        /// 한 칸의 상태. <b>받음이 가장 먼저다</b> — 받은 기록이 있으면 시계를 뒤로 돌려도 "완료" 다.
        /// </summary>
        public static SpecialSevenDaysCellState CellState(int day, int currentDay, bool purchased, bool claimed)
        {
            if (claimed)
                return SpecialSevenDaysCellState.Claimed;

            if (day > currentDay)
                return SpecialSevenDaysCellState.Upcoming;

            return purchased ? SpecialSevenDaysCellState.Claimable : SpecialSevenDaysCellState.Accrued;
        }

        /// <summary>
        /// 살 수 있는가. 안 샀고, 7일차가 끝나기 전이다.
        /// 6.1 의 "결제 시점이 7일 내내 열려 있다" 의 <b>7일</b>이 여기다 — 8일차부터 상품이 닫힌다.
        /// </summary>
        public static bool CanPurchase(int currentDay, bool purchased)
        {
            return !purchased && currentDay <= DayCount;
        }

        /// <summary>받은 날 수(1~7 안의 것만, 중복 없이). 세이브가 깨져 범위 밖·중복이 있어도 7을 못 넘는다.</summary>
        public static int ClaimedCount(IReadOnlyCollection<int> claimedDays)
        {
            if (claimedDays == null)
                return 0;

            int mask = 0;
            foreach (int day in claimedDays)
            {
                if (day >= 1 && day <= DayCount)
                    mask |= 1 << day;
            }

            int count = 0;
            for (int day = 1; day <= DayCount; day++)
            {
                if ((mask & (1 << day)) != 0)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// 로비 입구를 보이는가. (기획서 ShopPackageDesign 2.2 — "오픈 후 7일간, 또는 미수령 보상 존재 시")
        ///
        /// 샀으면 7칸을 다 받을 때까지 — 8일차 이후에도 남은 칸이 있으면 남긴다(받을 것이 사라지면 안 된다).
        /// 안 샀으면 살 수 있는 동안만.
        /// </summary>
        public static bool IsEntryVisible(int currentDay, bool purchased, int claimedCount)
        {
            return purchased ? claimedCount < DayCount : CanPurchase(currentDay, false);
        }

        /// <summary>지금 받을 수 있는 날(오름차순). 안 샀으면 비어 있다.</summary>
        public static List<int> ClaimableDays(int currentDay, bool purchased, IReadOnlyCollection<int> claimedDays)
        {
            var days = new List<int>();
            if (!purchased)
                return days;

            int last = Math.Min(currentDay, DayCount);
            for (int day = 1; day <= last; day++)
            {
                if (claimedDays == null || !Contains(claimedDays, day))
                    days.Add(day);
            }

            return days;
        }

        /// <summary>
        /// 오늘 로비에서 창을 저절로 띄울 것인가. (기획서 ShopPackageDesign 2.3 — "오늘자 미수령 보상 존재", 하루 1회)
        ///
        /// 안 샀어도 오늘 칸이 쌓였으면 띄운다 — 미구매 유저에게 "쌓인 내 보상" 을 보여 주는 것이
        /// 이 상품의 핵심 장치다(6.1). 샀으면 받을 것이 있을 때만.
        /// </summary>
        public static bool ShouldAutoOpen(int currentDay, bool purchased, IReadOnlyCollection<int> claimedDays,
            string lastAutoOpenDayKey, string todayKey)
        {
            if (string.Equals(lastAutoOpenDayKey, todayKey, StringComparison.Ordinal))
                return false;

            if (!purchased)
                return CanPurchase(currentDay, false);

            return ClaimableDays(currentDay, true, claimedDays).Count > 0;
        }

        private static bool Contains(IReadOnlyCollection<int> days, int day)
        {
            foreach (int d in days)
            {
                if (d == day)
                    return true;
            }

            return false;
        }
    }
}
