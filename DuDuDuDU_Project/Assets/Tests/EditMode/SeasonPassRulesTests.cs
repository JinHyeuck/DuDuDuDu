using System;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 시즌 패스의 순수 규칙을 잠근다.
    ///
    /// <b>여기서 잠그는 핵심 둘.</b>
    /// 하나는 <b>시즌 경계</b>다 — 달력 월이라 길이가 28~31일로 들쭉날쭉하고, 월말 23:59 와
    /// 월초 00:00 이 다른 시즌이어야 한다. 하루에 한 번만 밟히는 경로라 틀려도 안 드러난다.
    /// 다른 하나는 <b>소급 지급</b>이다 — 유료 트랙을 늦게 사도 지나간 레벨을 받을 수 있어야
    /// 결제 시점이 시즌 내내 열려 있다(기획서 7.3).
    /// </summary>
    public sealed class SeasonPassRulesTests
    {
        // ── 시즌 경계 ──────────────────────────────────────────────────

        [Test]
        public void SeasonIdIsYearAndMonth()
        {
            Assert.AreEqual("2026-10", SeasonPassRules.SeasonId(new DateTime(2026, 10, 2, 13, 45, 0)));
            Assert.AreEqual("2026-01", SeasonPassRules.SeasonId(new DateTime(2026, 1, 1, 0, 0, 0)));
        }

        /// <summary>월말 마지막 순간과 월초 첫 순간은 다른 시즌이어야 한다.</summary>
        [Test]
        public void MonthBoundarySplitsSeasons()
        {
            string endOfOctober = SeasonPassRules.SeasonId(new DateTime(2026, 10, 31, 23, 59, 59));
            string startOfNovember = SeasonPassRules.SeasonId(new DateTime(2026, 11, 1, 0, 0, 0));

            Assert.AreEqual("2026-10", endOfOctober);
            Assert.AreEqual("2026-11", startOfNovember);
            Assert.AreNotEqual(endOfOctober, startOfNovember);
        }

        [Test]
        public void SeasonEndIsFirstMomentOfNextMonth()
        {
            Assert.AreEqual(
                new DateTime(2026, 11, 1),
                SeasonPassRules.SeasonEnd(new DateTime(2026, 10, 2, 13, 45, 0)));

            // 12월은 해를 넘긴다.
            Assert.AreEqual(
                new DateTime(2027, 1, 1),
                SeasonPassRules.SeasonEnd(new DateTime(2026, 12, 31, 23, 59, 59)));
        }

        /// <summary>윤년 2월도 그 달의 끝으로 간다. 날짜를 더해 계산하면 여기서 틀린다.</summary>
        [Test]
        public void LeapFebruaryEndsOnMarchFirst()
        {
            Assert.AreEqual(
                new DateTime(2028, 3, 1),
                SeasonPassRules.SeasonEnd(new DateTime(2028, 2, 29, 12, 0, 0)));
        }

        [Test]
        public void TimeUntilSeasonEndCountsDown()
        {
            TimeSpan remaining = SeasonPassRules.TimeUntilSeasonEnd(new DateTime(2026, 10, 22, 7, 0, 0));

            // 10/22 07:00 → 11/1 00:00 = 9일 17시간
            Assert.AreEqual(9, remaining.Days);
            Assert.AreEqual(17, remaining.Hours);
        }

        /// <summary>기기 시계를 앞으로 돌린 경우. 음수 시간을 그대로 내보내지 않는다.</summary>
        [Test]
        public void TimeUntilSeasonEndNeverGoesNegative()
        {
            // SeasonEnd 는 언제나 now 보다 뒤라 음수가 나올 수 없지만, 그 보장이 깨져도
            // 0 으로 접히는지 본다(경계를 계산으로 옮기는 변경이 들어올 수 있다).
            Assert.GreaterOrEqual(
                SeasonPassRules.TimeUntilSeasonEnd(new DateTime(2026, 10, 31, 23, 59, 59)),
                TimeSpan.Zero);
        }

        // ── 레벨 ───────────────────────────────────────────────────────

        [Test]
        public void LevelStartsAtOne()
        {
            Assert.AreEqual(1, SeasonPassRules.LevelFor(0, 1000, 30));
            Assert.AreEqual(1, SeasonPassRules.LevelFor(999, 1000, 30));
            Assert.AreEqual(2, SeasonPassRules.LevelFor(1000, 1000, 30));
            Assert.AreEqual(3, SeasonPassRules.LevelFor(2500, 1000, 30));
        }

        [Test]
        public void LevelStopsAtMax()
        {
            Assert.AreEqual(30, SeasonPassRules.LevelFor(29000, 1000, 30));
            Assert.AreEqual(30, SeasonPassRules.LevelFor(999999, 1000, 30));
        }

        /// <summary>요구치가 0 이면 데이터 사고다. 전부 최대 레벨로 열어 보상을 쏟으면 안 된다.</summary>
        [Test]
        public void ZeroPointsPerLevelDoesNotUnlockEverything()
        {
            Assert.AreEqual(1, SeasonPassRules.LevelFor(999999, 0, 30));
            Assert.AreEqual(0, SeasonPassRules.PointsIntoLevel(999999, 0, 30));
            Assert.AreEqual(0f, SeasonPassRules.LevelProgress(999999, 0, 30), 0.0001f);
        }

        [Test]
        public void PointsIntoLevelIsTheRemainder()
        {
            Assert.AreEqual(0, SeasonPassRules.PointsIntoLevel(0, 1000, 30));
            Assert.AreEqual(250, SeasonPassRules.PointsIntoLevel(250, 1000, 30));
            Assert.AreEqual(500, SeasonPassRules.PointsIntoLevel(1500, 1000, 30));
        }

        /// <summary>
        /// 최대 레벨에서는 게이지가 꽉 찬 채로 멎어야 한다. 남은 포인트를 보여 주면
        /// "쌓이는데 안 오른다" 로 읽힌다.
        /// </summary>
        [Test]
        public void MaxLevelShowsFullGauge()
        {
            Assert.AreEqual(1000, SeasonPassRules.PointsIntoLevel(99999, 1000, 30));
            Assert.AreEqual(1f, SeasonPassRules.LevelProgress(99999, 1000, 30), 0.0001f);
        }

        [Test]
        public void PointsRequiredForLevel()
        {
            Assert.AreEqual(0, SeasonPassRules.PointsRequiredFor(1, 1000));
            Assert.AreEqual(1000, SeasonPassRules.PointsRequiredFor(2, 1000));
            Assert.AreEqual(29000, SeasonPassRules.PointsRequiredFor(30, 1000));
        }

        // ── 수령 ───────────────────────────────────────────────────────

        [Test]
        public void CannotClaimUnreachedLevel()
        {
            Assert.IsFalse(SeasonPassRules.CanClaim(
                level: 5, reachedLevel: 3, alreadyClaimed: false, premiumTrack: false, premiumUnlocked: false));
        }

        [Test]
        public void CannotClaimTwice()
        {
            Assert.IsFalse(SeasonPassRules.CanClaim(
                level: 2, reachedLevel: 5, alreadyClaimed: true, premiumTrack: false, premiumUnlocked: false));
        }

        [Test]
        public void FreeTrackNeedsNoPurchase()
        {
            Assert.IsTrue(SeasonPassRules.CanClaim(
                level: 2, reachedLevel: 5, alreadyClaimed: false, premiumTrack: false, premiumUnlocked: false));
        }

        [Test]
        public void PremiumTrackNeedsPurchase()
        {
            Assert.IsFalse(SeasonPassRules.CanClaim(
                level: 2, reachedLevel: 5, alreadyClaimed: false, premiumTrack: true, premiumUnlocked: false));
        }

        /// <summary>
        /// <b>기획서 7.3 의 소급 지급.</b> 유료 트랙을 늦게 사도 이미 지나간 레벨을 전부
        /// 받을 수 있어야 한다. 여기가 "언제 샀는가" 를 보기 시작하면 결제 시점이
        /// 시즌 초로 몰린다.
        /// </summary>
        [Test]
        public void PremiumUnlockGrantsPastLevelsRetroactively()
        {
            for (int level = 1; level <= 5; level++)
            {
                Assert.IsTrue(
                    SeasonPassRules.CanClaim(level, reachedLevel: 5, alreadyClaimed: false,
                        premiumTrack: true, premiumUnlocked: true),
                    level + "레벨을 소급해서 받지 못한다.");
            }
        }
    }
}
