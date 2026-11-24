using System;
using NUnit.Framework;
using OJ.EventMinigame;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>이벤트 3종 공통 규칙: 열린 이벤트 종류·회차·초과 환산.</summary>
    public sealed class EventMinigameRulesTests
    {
        [Test]
        public void 이벤트_종류는_열린_이벤트_재화의_순서와_같다()
        {
            for (int day = -20; day < 30; day++)
            {
                PointType expected = EventCurrencyRules.Resolve(day);
                Assert.AreEqual(expected, EventMinigameRules.CurrencyOf(EventMinigameRules.KindOf(day)), "day " + day);
            }
        }

        [Test]
        public void 종류는_3일마다_바뀌고_9일에_한_바퀴다()
        {
            EventKind[] expected = { EventKind.Fishing, EventKind.TreasureHunt, EventKind.Rafting };

            for (int day = 0; day < 27; day++)
                Assert.AreEqual(expected[(day / 3) % 3], EventMinigameRules.KindOf(day), "day " + day);
        }

        [Test]
        public void 회차_번호는_3일마다_오른다()
        {
            Assert.AreEqual(0, EventMinigameRules.OccurrenceId(0));
            Assert.AreEqual(0, EventMinigameRules.OccurrenceId(2));
            Assert.AreEqual(1, EventMinigameRules.OccurrenceId(3));
            Assert.AreEqual(-1, EventMinigameRules.OccurrenceId(-1));
            Assert.AreEqual(-1, EventMinigameRules.OccurrenceId(-3));
            Assert.AreEqual(-2, EventMinigameRules.OccurrenceId(-4));
        }

        [Test]
        public void 같은_종류도_9일_뒤에는_다른_회차다()
        {
            Assert.AreEqual(EventMinigameRules.KindOf(1), EventMinigameRules.KindOf(10));
            Assert.AreNotEqual(EventMinigameRules.OccurrenceId(1), EventMinigameRules.OccurrenceId(10));
        }

        [Test]
        public void 이벤트_재화만_종류로_바뀐다()
        {
            EventKind kind;

            Assert.IsTrue(EventMinigameRules.TryKindOfCurrency(PointType.EventCurrency2, out kind));
            Assert.AreEqual(EventKind.TreasureHunt, kind);

            Assert.IsFalse(EventMinigameRules.TryKindOfCurrency(PointType.Gold, out kind));
            Assert.IsFalse(EventMinigameRules.TryKindOfCurrency(PointType.OpenEventCurrency, out kind));
        }

        [Test]
        public void 초과_진행도는_단위로_나눠_소수점을_버린다()
        {
            Assert.AreEqual(2, EventMinigameRules.OverflowToShots(899, 300));
            Assert.AreEqual(3, EventMinigameRules.OverflowToShots(900, 300));
            Assert.AreEqual(0, EventMinigameRules.OverflowToShots(299, 300));
        }

        [Test]
        public void 초과가_없거나_단위가_잘못되면_0이다()
        {
            Assert.AreEqual(0, EventMinigameRules.OverflowToShots(0, 300));
            Assert.AreEqual(0, EventMinigameRules.OverflowToShots(-5, 300));
            Assert.AreEqual(0, EventMinigameRules.OverflowToShots(500, 0));
        }

        [Test]
        public void 개발_시계는_각_이벤트가_열린_날로_민다()
        {
            for (int day = 0; day < 40; day++)
            {
                DateTime real = new DateTime(1970, 1, 1, 6, 0, 0, DateTimeKind.Utc).AddDays(day + 20000);

                for (int i = 0; i < EventMinigameRules.KindCount; i++)
                {
                    EventKind kind = (EventKind)i;
                    TimeSpan offset = EventMinigameClock.OffsetToOpen(kind, real);

                    DateTime shifted = real + offset;
                    Assert.AreEqual(kind, EventMinigameRules.KindOf(EventCurrencyRules.DayIndex(shifted)),
                        "day " + day + " kind " + kind);
                    Assert.GreaterOrEqual(offset, TimeSpan.Zero);
                    Assert.Less(offset, TimeSpan.FromDays(EventMinigameRules.DaysPerOccurrence * EventMinigameRules.KindCount));
                }
            }
        }

        [Test]
        public void 이미_열린_이벤트는_시계를_밀지_않는다()
        {
            DateTime real = new DateTime(2026, 11, 24, 3, 0, 0, DateTimeKind.Utc);
            EventKind open = EventMinigameRules.KindOf(EventCurrencyRules.DayIndex(real));

            Assert.AreEqual(TimeSpan.Zero, EventMinigameClock.OffsetToOpen(open, real));
        }
    }
}
