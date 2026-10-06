using System;
using System.Collections.Generic;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 특별한 7일 출석을 잠근다.
    ///
    /// <b>여기서 잠그는 핵심</b>은 기획서 BMDesign 6.1 의 두 줄이다 —
    /// 안 사도 출석은 쌓이고(수량은 보이되 자물쇠), 뒤늦게 사도 쌓인 것을 전부 받는다.
    /// 둘 다 "며칠 지난 뒤" 에만 밟히는 경로라 개발 중에 눌러 보기 어렵다.
    /// </summary>
    public sealed class SpecialSevenDaysRulesTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 6, 9, 0, 0);
        private const string StartKey = "2026-10-06";

        // ── 일차 ───────────────────────────────────────────────────────

        [Test]
        public void StartDayIsDayOne()
        {
            Assert.AreEqual(1, SpecialSevenDaysRules.CurrentDay(StartKey, Start));
            Assert.AreEqual(1, SpecialSevenDaysRules.CurrentDay(StartKey, Start.Date.AddHours(23).AddMinutes(59)));
        }

        /// <summary>날짜로 센다 — 23시 59분 뒤 1분이면 2일차다(경과 24시간이 아니다).</summary>
        [Test]
        public void MidnightAdvancesTheDay()
        {
            Assert.AreEqual(2, SpecialSevenDaysRules.CurrentDay(StartKey, Start.Date.AddDays(1)));
            Assert.AreEqual(7, SpecialSevenDaysRules.CurrentDay(StartKey, Start.AddDays(6)));
        }

        /// <summary>위로는 접지 않는다 — 8 이상이 "기간이 지났다" 를 말한다.</summary>
        [Test]
        public void DayIsNotCappedAtSeven()
        {
            Assert.AreEqual(10, SpecialSevenDaysRules.CurrentDay(StartKey, Start.AddDays(9)));
        }

        [Test]
        public void ClockTurnedBackStaysOnDayOne()
        {
            Assert.AreEqual(1, SpecialSevenDaysRules.CurrentDay(StartKey, Start.AddDays(-3)));
        }

        [Test]
        public void MissingOrBrokenStartIsDayOne()
        {
            Assert.AreEqual(1, SpecialSevenDaysRules.CurrentDay(string.Empty, Start));
            Assert.AreEqual(1, SpecialSevenDaysRules.CurrentDay(null, Start));
            Assert.AreEqual(1, SpecialSevenDaysRules.CurrentDay("10/06/2026", Start));
        }

        [Test]
        public void DayKeyRoundTrips()
        {
            string key = SpecialSevenDaysRules.DayKey(Start);
            Assert.AreEqual(StartKey, key);
            Assert.IsTrue(SpecialSevenDaysRules.TryParseDayKey(key, out DateTime parsed));
            Assert.AreEqual(Start.Date, parsed);
        }

        // ── 칸 상태 ────────────────────────────────────────────────────

        /// <summary>안 샀으면 지난 날은 "쌓임"(자물쇠 + 수량) — 미도래와 구분돼야 한다(4.3).</summary>
        [Test]
        public void UnpurchasedPastDaysAreAccrued()
        {
            Assert.AreEqual(SpecialSevenDaysCellState.Accrued, SpecialSevenDaysRules.CellState(1, 3, false, false));
            Assert.AreEqual(SpecialSevenDaysCellState.Accrued, SpecialSevenDaysRules.CellState(3, 3, false, false));
            Assert.AreEqual(SpecialSevenDaysCellState.Upcoming, SpecialSevenDaysRules.CellState(4, 3, false, false));
        }

        [Test]
        public void PurchasedPastDaysAreClaimable()
        {
            Assert.AreEqual(SpecialSevenDaysCellState.Claimable, SpecialSevenDaysRules.CellState(2, 3, true, false));
            Assert.AreEqual(SpecialSevenDaysCellState.Upcoming, SpecialSevenDaysRules.CellState(4, 3, true, false));
        }

        /// <summary>받은 기록이 시계보다 먼저다 — 시계를 뒤로 돌려도 받은 칸이 미도래로 안 돌아간다.</summary>
        [Test]
        public void ClaimedWinsOverClock()
        {
            Assert.AreEqual(SpecialSevenDaysCellState.Claimed, SpecialSevenDaysRules.CellState(5, 1, true, true));
        }

        // ── 구매 · 수령 ────────────────────────────────────────────────

        [Test]
        public void PurchaseWindowIsSevenDays()
        {
            Assert.IsTrue(SpecialSevenDaysRules.CanPurchase(1, false));
            Assert.IsTrue(SpecialSevenDaysRules.CanPurchase(7, false));
            Assert.IsFalse(SpecialSevenDaysRules.CanPurchase(8, false));
            Assert.IsFalse(SpecialSevenDaysRules.CanPurchase(3, true));
        }

        /// <summary>6.1 — 5일차에 사면 1~5일차가 그 자리에서 전부 열린다.</summary>
        [Test]
        public void LatePurchaseUnlocksEveryAccruedDay()
        {
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 },
                SpecialSevenDaysRules.ClaimableDays(5, true, new List<int>()));
        }

        [Test]
        public void ClaimableSkipsClaimedAndStopsAtSeven()
        {
            CollectionAssert.AreEqual(new[] { 2, 4, 5, 6, 7 },
                SpecialSevenDaysRules.ClaimableDays(12, true, new List<int> { 1, 3 }));
        }

        [Test]
        public void UnpurchasedHasNothingToClaim()
        {
            Assert.IsEmpty(SpecialSevenDaysRules.ClaimableDays(7, false, new List<int>()));
        }

        /// <summary>세이브가 깨져 범위 밖·중복이 들어 있어도 받은 수가 7을 못 넘는다.</summary>
        [Test]
        public void ClaimedCountIgnoresJunk()
        {
            Assert.AreEqual(2, SpecialSevenDaysRules.ClaimedCount(new List<int> { 1, 1, 0, 8, -2, 7 }));
            Assert.AreEqual(0, SpecialSevenDaysRules.ClaimedCount(null));
        }

        // ── 입구 ───────────────────────────────────────────────────────

        [Test]
        public void EntryShowsWhilePurchasable()
        {
            Assert.IsTrue(SpecialSevenDaysRules.IsEntryVisible(7, false, 0));
            Assert.IsFalse(SpecialSevenDaysRules.IsEntryVisible(8, false, 0));
        }

        /// <summary>샀으면 기간이 지나도 다 받을 때까지 남는다(2.2 "또는 미수령 보상 존재 시").</summary>
        [Test]
        public void EntryStaysAfterWindowUntilAllClaimed()
        {
            Assert.IsTrue(SpecialSevenDaysRules.IsEntryVisible(20, true, 6));
            Assert.IsFalse(SpecialSevenDaysRules.IsEntryVisible(20, true, 7));
            Assert.IsFalse(SpecialSevenDaysRules.IsEntryVisible(7, true, 7));
        }

        // ── 자동 노출 ──────────────────────────────────────────────────

        [Test]
        public void AutoOpensOncePerDay()
        {
            Assert.IsTrue(SpecialSevenDaysRules.ShouldAutoOpen(2, false, new List<int>(), "2026-10-06", "2026-10-07"));
            Assert.IsFalse(SpecialSevenDaysRules.ShouldAutoOpen(2, false, new List<int>(), "2026-10-07", "2026-10-07"));
        }

        [Test]
        public void PurchasedAutoOpensOnlyWithSomethingToClaim()
        {
            Assert.IsFalse(SpecialSevenDaysRules.ShouldAutoOpen(2, true, new List<int> { 1, 2 }, string.Empty, "2026-10-07"));
            Assert.IsTrue(SpecialSevenDaysRules.ShouldAutoOpen(3, true, new List<int> { 1, 2 }, string.Empty, "2026-10-08"));
        }

        [Test]
        public void UnpurchasedStopsAutoOpeningAfterWindow()
        {
            Assert.IsFalse(SpecialSevenDaysRules.ShouldAutoOpen(8, false, new List<int>(), string.Empty, "2026-10-13"));
        }
    }
}
