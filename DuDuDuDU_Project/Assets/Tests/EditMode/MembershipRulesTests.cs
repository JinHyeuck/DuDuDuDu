using System;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 고기 멤버십과 무료 소탕 한도를 잠근다.
    ///
    /// <b>여기서 잠그는 핵심 둘.</b>
    /// 하나는 <b>연장</b>이다 — 만료 3일 전에 다시 사면 남은 3일이 사라지면 안 된다.
    /// 다른 하나는 <b>날짜 리셋</b>이다 — 하루에 한 번만 밟히는 경로라 틀려도 안 드러난다.
    /// </summary>
    public sealed class MembershipRulesTests
    {
        private static readonly long Now = new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc).Ticks;
        private static readonly long Day = TimeSpan.TicksPerDay;

        // ── 기간 ───────────────────────────────────────────────────────

        [Test]
        public void NeverBoughtIsInactive()
        {
            Assert.IsFalse(MembershipRules.IsActive(MembershipRules.NoExpiry, Now));
            Assert.AreEqual(0, MembershipRules.RemainingDays(MembershipRules.NoExpiry, Now));
        }

        /// <summary>만료 시각 그 순간부터는 끝이다.</summary>
        [Test]
        public void ExpiryIsExclusive()
        {
            Assert.IsTrue(MembershipRules.IsActive(Now + 1, Now));
            Assert.IsFalse(MembershipRules.IsActive(Now, Now));
        }

        [Test]
        public void FirstPurchaseStartsNow()
        {
            long expiry = MembershipRules.Extend(MembershipRules.NoExpiry, Now, 30);
            Assert.AreEqual(Now + 30 * Day, expiry);
            Assert.AreEqual(30, MembershipRules.RemainingDays(expiry, Now));
        }

        /// <summary>남은 기간 중에 사면 만료일 뒤로 붙는다. 일찍 산 사람이 손해 보지 않는다.</summary>
        [Test]
        public void RebuyWhileActiveStacksOnExpiry()
        {
            long expiry = Now + 3 * Day;
            Assert.AreEqual(Now + 33 * Day, MembershipRules.Extend(expiry, Now, 30));
        }

        [Test]
        public void RebuyAfterExpiryStartsNow()
        {
            long expired = Now - 5 * Day;
            Assert.AreEqual(Now + 30 * Day, MembershipRules.Extend(expired, Now, 30));
        }

        /// <summary>남은 일수는 올림이다. 산 직후가 "29일" 이면 하루를 덜 준 것처럼 읽힌다.</summary>
        [Test]
        public void RemainingDaysRoundsUp()
        {
            Assert.AreEqual(30, MembershipRules.RemainingDays(Now + 30 * Day - 1, Now));
            Assert.AreEqual(1, MembershipRules.RemainingDays(Now + 1, Now));
            Assert.AreEqual(2, MembershipRules.RemainingDays(Now + Day + 1, Now));
        }

        [Test]
        public void RebuyOpensThreeDaysBeforeExpiry()
        {
            Assert.IsFalse(MembershipRules.CanRebuy(Now + 4 * Day, Now, 3));
            Assert.IsTrue(MembershipRules.CanRebuy(Now + 3 * Day, Now, 3));
            Assert.IsTrue(MembershipRules.CanRebuy(MembershipRules.NoExpiry, Now, 3));
        }

        // ── 입장료 ─────────────────────────────────────────────────────

        [Test]
        public void MemberEntersFree()
        {
            Assert.AreEqual(0, MembershipRules.StageEntryCost(5, true));
            Assert.AreEqual(5, MembershipRules.StageEntryCost(5, false));
        }

        // ── 무료 소탕 한도 ─────────────────────────────────────────────

        [Test]
        public void DayKeyIsInvariantDate()
        {
            Assert.AreEqual("2026-10-06", MembershipRules.DayKey(new DateTime(2026, 10, 6, 23, 59, 0)));
        }

        /// <summary>저장된 날짜가 오늘이 아니면 0 부터 다시 센다.</summary>
        [Test]
        public void UsageResetsWhenDayChanges()
        {
            Assert.AreEqual(40, MembershipRules.UsedToday("2026-10-06", 40, "2026-10-06"));
            Assert.AreEqual(0, MembershipRules.UsedToday("2026-10-05", 40, "2026-10-06"));
            Assert.AreEqual(0, MembershipRules.UsedToday(string.Empty, 40, "2026-10-06"));
        }

        [Test]
        public void SweepsLeftCountsDownToZero()
        {
            Assert.AreEqual(72, MembershipRules.SweepsLeftToday(72, 0, false));
            Assert.AreEqual(2, MembershipRules.SweepsLeftToday(72, 70, false));
            Assert.AreEqual(0, MembershipRules.SweepsLeftToday(72, 90, false));
        }

        [Test]
        public void MemberHasNoSweepLimit()
        {
            Assert.AreEqual(MembershipRules.Unlimited, MembershipRules.SweepsLeftToday(72, 500, true));
        }

        /// <summary>창의 MAX 는 고기와 한도 중 작은 쪽이다.</summary>
        [Test]
        public void MaxSweepCountIsTheSmallerCap()
        {
            Assert.AreEqual(10, MembershipRules.MaxSweepCount(10, 72));
            Assert.AreEqual(2, MembershipRules.MaxSweepCount(10, 2));
            Assert.AreEqual(0, MembershipRules.MaxSweepCount(10, 0));
            Assert.AreEqual(500, MembershipRules.MaxSweepCount(500, MembershipRules.Unlimited));
        }
    }
}
