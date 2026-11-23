using System;
using NUnit.Framework;
using OJ.EventMinigame;

namespace OJ.Game.Tests
{
    /// <summary>이벤트 화면 문구와 화면이 쓰는 작은 규칙(타이밍 위치, 회차 종료 시각, 룰렛 칸 기록).</summary>
    public sealed class EventMinigameTextTests
    {
        [Test]
        public void 이벤트와_재화_이름이_짝이다()
        {
            Assert.AreEqual("낚시", EventMinigameText.EventName(EventKind.Fishing));
            Assert.AreEqual("낚싯대", EventMinigameText.CurrencyName(EventKind.Fishing));
            Assert.AreEqual("곡괭이", EventMinigameText.CurrencyName(EventKind.TreasureHunt));
            Assert.AreEqual("보트", EventMinigameText.CurrencyName(EventKind.Rafting));
        }

        [TestCase(0, 1234, "1,234")]
        [TestCase(0, 0, "0")]
        [TestCase(0, 400000, "400,000")]
        public void 정수는_쉼표를_찍는다(int unused, long value, string expected)
        {
            Assert.AreEqual(expected, EventMinigameText.Number(value));
        }

        [TestCase(270L, "270g")]
        [TestCase(999L, "999g")]
        [TestCase(1000L, "1.00kg")]
        [TestCase(1505L, "1.50kg")]
        [TestCase(26400L, "26.40kg")]
        public void 무게는_g와_kg로_적는다(long grams, string expected)
        {
            Assert.AreEqual(expected, EventMinigameText.Weight(grams));
        }

        [Test]
        public void 남은_시간은_큰_단위부터_적는다()
        {
            Assert.AreEqual("2일 3시간", EventMinigameText.Remaining(new TimeSpan(2, 3, 20, 0)));
            Assert.AreEqual("5시간 7분", EventMinigameText.Remaining(new TimeSpan(5, 7, 0)));
            Assert.AreEqual("12분", EventMinigameText.Remaining(TimeSpan.FromMinutes(12)));
            Assert.AreEqual("1분", EventMinigameText.Remaining(TimeSpan.FromSeconds(20)));
            Assert.AreEqual("0분", EventMinigameText.Remaining(TimeSpan.Zero));
            Assert.AreEqual("0분", EventMinigameText.Remaining(TimeSpan.FromMinutes(-5)));
        }

        [Test]
        public void 보상_묶음은_있는_항목만_한_줄로_적는다()
        {
            var bundle = new EventBundle(10, 0, 4000, 2, 0, 0, 0, 1);

            Assert.AreEqual("핀볼 10발 · 골드 4,000 · 일반 뽑기권 2장 · 레어·킹 카피 1장", EventMinigameText.Describe(bundle));
            Assert.AreEqual("-", EventMinigameText.Describe(default(EventBundle)));
        }

        [Test]
        public void 룰렛_칸_이름은_무료만_표시한다()
        {
            Assert.AreEqual("1m", EventMinigameText.RaftingCell(0));
            Assert.AreEqual("무료 5m", EventMinigameText.RaftingCell(RaftingRules.FreeCell));
            Assert.AreEqual("20m", EventMinigameText.RaftingCell(7));
            Assert.AreEqual("20m", EventMinigameText.RaftingCell(99));
        }

        [Test]
        public void 도감_한_줄은_못_낚은_어종을_가린다()
        {
            Assert.AreEqual("[일반] 피라미  ???", EventMinigameText.CodexLine(0, 0, 0));
            Assert.AreEqual("[희귀] 잉어  6.00kg", EventMinigameText.CodexLine(1, 0, 6000));
        }

        [TestCase(1000, "100")]
        [TestCase(50, "5")]
        [TestCase(225, "22.5")]
        public void 퍼센트는_정수면_소수점을_뗀다(int permille, string expected)
        {
            Assert.AreEqual(expected, EventMinigameText.PercentFromPermille(permille));
        }

        [Test]
        public void 낚시_결과_문구에_신기록과_떡밥과_레벨업이_붙는다()
        {
            var result = new FishingCastResult
            {
                Valid = true, Tier = 3, Species = 0, WeightClass = 4, Timing = FishTiming.Perfect, Multiplier = 10,
                CastScore = 63000, WeightGrams = 700000, NewRecord = true, TopPermille = 50, Baits = 1, LuckStacks = 2,
                RodLevelBefore = 1, RodLevelAfter = 2,
            };

            string text = EventMinigameText.FishingResultLine(result);

            StringAssert.Contains("[전설] 고대 용어", text);
            StringAssert.Contains("700.00kg", text);
            StringAssert.Contains("퍼펙트!", text);
            StringAssert.Contains("+63,000점  (x10)", text);
            StringAssert.Contains("상위 5%", text);
            StringAssert.Contains("행운 2단계", text);
            StringAssert.Contains("Lv.2", text);
        }

        // ── 화면이 쓰는 규칙 ──────────────────────────────────────────

        [Test]
        public void 게이지_위치로_타이밍을_가른다()
        {
            Assert.AreEqual(FishTiming.Perfect, FishingRules.TimingFromPosition(0.5f));
            Assert.AreEqual(FishTiming.Perfect, FishingRules.TimingFromPosition(0.36f));
            Assert.AreEqual(FishTiming.Good, FishingRules.TimingFromPosition(0.3f));
            Assert.AreEqual(FishTiming.Good, FishingRules.TimingFromPosition(0.7f));
            Assert.AreEqual(FishTiming.Miss, FishingRules.TimingFromPosition(0.05f));
            Assert.AreEqual(FishTiming.Miss, FishingRules.TimingFromPosition(1f));
        }

        /// <summary>일정한 속도로 왕복하는 표식의 위치는 고르게 퍼진다 → 퍼펙트 30% · 굿 50% · 미스 20%.</summary>
        [Test]
        public void 고르게_누르면_타이밍_분포가_장부_가정과_같다()
        {
            int perfect = 0, good = 0, miss = 0;
            const int steps = 10000;
            for (int i = 0; i < steps; i++)
            {
                switch (FishingRules.TimingFromPosition((i + 0.5f) / steps))
                {
                    case FishTiming.Perfect: perfect++; break;
                    case FishTiming.Good: good++; break;
                    default: miss++; break;
                }
            }

            Assert.That(perfect, Is.EqualTo(3000).Within(2));
            Assert.That(good, Is.EqualTo(5000).Within(2));
            Assert.That(miss, Is.EqualTo(2000).Within(2));
            Assert.AreEqual(FishingRules.AssumedTimingPermille[2], perfect / 10);
            Assert.AreEqual(FishingRules.AssumedTimingPermille[1], good / 10);
            Assert.AreEqual(FishingRules.AssumedTimingPermille[0], miss / 10);
        }

        [Test]
        public void 회차_종료_시각은_다음_3일_묶음의_UTC_0시다()
        {
            // 2026-11-20 은 UTC 날짜 번호 20412, 회차 6804 (끝나는 곳 = 20412 가 포함된 3일 묶음의 다음 0시).
            DateTime now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
            DateTime end = EventMinigameRules.OccurrenceEndUtc(now);

            Assert.AreEqual(0, end.Hour);
            Assert.AreEqual(0, end.Minute);
            Assert.Greater(end, now);
            Assert.LessOrEqual((end - now).TotalDays, 3.0);
            Assert.AreNotEqual(EventMinigameRules.OccurrenceId(Rules.DayIndex(now)), EventMinigameRules.OccurrenceId(Rules.DayIndex(end)));
            Assert.AreEqual(EventMinigameRules.OccurrenceId(Rules.DayIndex(now)) + 1, EventMinigameRules.OccurrenceId(Rules.DayIndex(end)));
        }

        [Test]
        public void 회차_종료_직전과_직후는_다른_회차다()
        {
            DateTime now = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
            DateTime end = EventMinigameRules.OccurrenceEndUtc(now);

            Assert.AreEqual(EventMinigameRules.OccurrenceId(Rules.DayIndex(now)), EventMinigameRules.OccurrenceId(Rules.DayIndex(end.AddSeconds(-1))));
        }

        [Test]
        public void 룰렛_연쇄는_돈_칸을_차례로_적는다()
        {
            var cells = new System.Collections.Generic.List<int>();
            int spins;
            float[] rolls = { 0.7f, 0.7f, 0.99f };
            int index = 0;

            long distance = RaftingRules.SpinChain(10, () => rolls[index++], cells, out spins);

            CollectionAssert.AreEqual(new[] { RaftingRules.FreeCell, RaftingRules.FreeCell, 7 }, cells);
            Assert.AreEqual(300L, distance);
            Assert.AreEqual(3, spins);
        }

        private static class Rules
        {
            public static int DayIndex(DateTime utc)
            {
                return OJ.Point.EventCurrencyRules.DayIndex(utc);
            }
        }
    }
}
