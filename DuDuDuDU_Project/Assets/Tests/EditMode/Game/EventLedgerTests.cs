using NUnit.Framework;
using OJ.EventMinigame;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>이벤트 보상 장부: 묶음 계산과 낚시·래프팅 표가 "쓴 재화의 10배" 곡선과 맞는가.</summary>
    public sealed class EventLedgerTests
    {
        [Test]
        public void 재화_1개는_평균_2000데시원을_돌려준다()
        {
            Assert.AreEqual(200, EventLedger.CurrencyValue);
            Assert.AreEqual(10, EventLedger.ReturnMultiplier);
            Assert.AreEqual(2000, EventLedger.ReturnPerCurrency);
        }

        [TestCase(1L)]
        [TestCase(9L)]
        [TestCase(10L)]
        [TestCase(599L)]
        [TestCase(600L)]
        [TestCase(1234L)]
        [TestCase(100000L)]
        [TestCase(999999L)]
        public void 묶음은_예산을_넘지_않고_10데시원_넘게_버리지_않는다(long budget)
        {
            EventBundle bundle = EventBundle.Compose(budget, 300, 100, 100, 100, 100, 100, 100);

            Assert.LessOrEqual(bundle.ValueDeci, budget);
            Assert.GreaterOrEqual(bundle.ValueDeci, budget - 9);
        }

        [Test]
        public void 예산이_0_이하면_빈_묶음이다()
        {
            Assert.IsTrue(EventBundle.Compose(0, 300, 0, 0, 0, 0, 0, 0).IsEmpty);
            Assert.IsTrue(EventBundle.Compose(-500, 300, 0, 0, 0, 0, 0, 0).IsEmpty);
        }

        [Test]
        public void 큰_단위가_예산에_안_들어가면_작은_단위로_넘어간다()
        {
            // 최고급 뽑기권(18,000)은 예산 10,000 에 못 담는다 → 핀볼로 채운다.
            EventBundle bundle = EventBundle.Compose(10000, 1000, 0, 0, 0, 0, 0, 0);

            Assert.AreEqual(0, bundle.PremiumGemTickets);
            Assert.AreEqual(16, bundle.PinballShots);
        }

        [Test]
        public void 지급_목록은_핀볼에_티켓_비용을_곱하고_카피는_랜덤_표지로_적는다()
        {
            var bundle = new EventBundle(10, 4, 500, 2, 1, 1, 3, 2);

            var rewards = bundle.ToRewards(2);

            Assert.AreEqual(8, rewards.Count);
            Assert.AreEqual(20, Amount(rewards, PointType.PinballTicket));
            Assert.AreEqual(4, Amount(rewards, PointType.SynergyTicket));
            Assert.AreEqual(500, Amount(rewards, PointType.Gold));
            Assert.AreEqual(2, Amount(rewards, PointType.NormalGemTicket));
            Assert.AreEqual(1, Amount(rewards, PointType.PremiumGemTicket));
            Assert.AreEqual(1, Amount(rewards, PointType.RelicTicket));
            Assert.AreEqual(3, Amount(rewards, PointType.RandomBasicCopy));
            Assert.AreEqual(2, Amount(rewards, PointType.RandomRareKingCopy));
        }

        [Test]
        public void 지급_목록은_0인_항목을_빼고_티켓_비용_0은_1로_본다()
        {
            var bundle = new EventBundle(5, 0, 0, 0, 0, 0, 0, 0);

            var rewards = bundle.ToRewards(0);

            Assert.AreEqual(1, rewards.Count);
            Assert.AreEqual(5, Amount(rewards, PointType.PinballTicket));
            Assert.AreEqual(0, default(EventBundle).ToRewards(1).Count);
        }

        private static int Amount(System.Collections.Generic.List<PointRewardEntry> rewards, PointType type)
        {
            foreach (PointRewardEntry r in rewards)
            {
                if (r.PointType == type)
                    return r.Amount;
            }

            return 0;
        }

        // ── 낚시 표 ───────────────────────────────────────────────────

        [Test]
        public void 낚시_표는_점수_순으로_늘고_낚싯대_레벨_구간을_품는다()
        {
            int previous = 0;
            foreach (EventMilestone m in EventRewardTables.Fishing)
            {
                Assert.Greater(m.Threshold, previous);
                previous = m.Threshold;
            }

            Assert.AreEqual(500, EventRewardTables.Fishing[0].Threshold);
            Assert.AreEqual(400000, EventRewardTables.FishingLastThreshold);

            foreach (int level in FishingRules.RodLevelScore)
            {
                if (level == 0)
                    continue;

                Assert.IsTrue(System.Array.Exists(EventRewardTables.Fishing, m => m.Threshold == level), "rod level at " + level);
            }
        }

        [Test]
        public void 낚시_누적_가치는_점수_곡선과_맞다()
        {
            long cumulative = 0;
            for (int i = 0; i < EventRewardTables.Fishing.Length; i++)
            {
                EventMilestone m = EventRewardTables.Fishing[i];
                Assert.IsFalse(m.IsChoice);

                cumulative += m.Default.ValueDeci;
                long expected = (long)m.Threshold * (EventLedger.ReturnPerCurrency / FishingRules.PointsPerCurrency);

                // 줄마다 10데시원 미만을 버린다.
                Assert.LessOrEqual(cumulative, expected, "row " + i);
                Assert.GreaterOrEqual(cumulative, expected - 10L * (i + 1), "row " + i);
            }
        }

        [Test]
        public void 낚시_표에는_카피가_없다()
        {
            foreach (EventMilestone m in EventRewardTables.Fishing)
            {
                Assert.AreEqual(0, m.Default.BasicCopies);
                Assert.AreEqual(0, m.Default.RareKingCopies);
            }
        }

        [Test]
        public void 낚시_큰_덩어리_줄에는_최고급_뽑기권이_들어간다()
        {
            bool any = false;
            foreach (EventMilestone m in EventRewardTables.Fishing)
            {
                if (m.Default.PremiumGemTickets > 0)
                    any = true;
            }

            Assert.IsTrue(any);
        }

        // ── 래프팅 표 ─────────────────────────────────────────────────

        [Test]
        public void 래프팅_표는_거리_순으로_늘고_2만미터에서_끝난다()
        {
            int previous = 0;
            foreach (EventMilestone m in EventRewardTables.Rafting)
            {
                Assert.Greater(m.Threshold, previous);
                previous = m.Threshold;
            }

            Assert.AreEqual(20000, EventRewardTables.RaftingLastThreshold);
        }

        [Test]
        public void 래프팅_누적_가치는_거리_곡선과_맞다()
        {
            long cumulative = 0;
            for (int i = 0; i < EventRewardTables.Rafting.Length; i++)
            {
                EventMilestone m = EventRewardTables.Rafting[i];
                cumulative += m.Default.ValueDeci;

                long expected = (long)m.Threshold * EventLedger.ReturnPerCurrency
                                * RaftingRules.MetersPerBoatDenominator / RaftingRules.MetersPerBoatNumerator;

                // 줄마다 10데시원 미만을 버리고 예산도 정수로 내림한다.
                Assert.LessOrEqual(cumulative, expected + 1, "row " + i);
                Assert.GreaterOrEqual(cumulative, expected - 12L * (i + 1), "row " + i);
            }
        }

        [Test]
        public void 래프팅_큰_마일스톤은_가치가_같은_3택1이다()
        {
            int[] choiceRows = { 1000, 2500, 5000, 10000, 15000, 20000 };
            int choices = 0;

            foreach (EventMilestone m in EventRewardTables.Rafting)
            {
                bool expectChoice = System.Array.IndexOf(choiceRows, m.Threshold) >= 0;
                Assert.AreEqual(expectChoice, m.IsChoice, "threshold " + m.Threshold);

                if (!m.IsChoice)
                    continue;

                choices++;
                Assert.AreEqual(3, m.Options.Length);

                long min = long.MaxValue;
                long max = long.MinValue;
                foreach (EventBundle option in m.Options)
                {
                    min = System.Math.Min(min, option.ValueDeci);
                    max = System.Math.Max(max, option.ValueDeci);
                }

                Assert.LessOrEqual(max - min, 9L, "threshold " + m.Threshold);
            }

            Assert.AreEqual(choiceRows.Length, choices);
        }

        [Test]
        public void 선택하지_않은_상자는_첫_묶음이_기본이다()
        {
            foreach (EventMilestone m in EventRewardTables.Rafting)
            {
                if (!m.IsChoice)
                    continue;

                Assert.AreEqual(m.Options[0].ValueDeci, m.Default.ValueDeci);
                Assert.AreEqual(m.Options[0].PinballShots, m.Default.PinballShots);
            }
        }

        // ── 초과 ──────────────────────────────────────────────────────

        [Test]
        public void 낚시_초과_점수는_300점당_핀볼_1발이다()
        {
            Assert.AreEqual(0L, EventRewardTables.OverflowShots(EventKind.Fishing, 0));
            Assert.AreEqual(0L, EventRewardTables.OverflowShots(EventKind.Fishing, 400000));
            Assert.AreEqual(0L, EventRewardTables.OverflowShots(EventKind.Fishing, 400299));
            Assert.AreEqual(1L, EventRewardTables.OverflowShots(EventKind.Fishing, 400300));
            Assert.AreEqual(3L, EventRewardTables.OverflowShots(EventKind.Fishing, 400900));
        }

        [Test]
        public void 래프팅_초과_거리는_97분의_5씩_핀볼이다()
        {
            Assert.AreEqual(0L, EventRewardTables.OverflowShots(EventKind.Rafting, 20000));
            Assert.AreEqual(5L, EventRewardTables.OverflowShots(EventKind.Rafting, 20097));
        }

        [Test]
        public void 보물찾기는_초과_환산이_없다()
        {
            Assert.AreEqual(0L, EventRewardTables.OverflowShots(EventKind.TreasureHunt, 999999999L));
        }
    }
}
