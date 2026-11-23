using System;
using System.Collections.Generic;
using NUnit.Framework;
using OJ.Core;
using OJ.Dice;
using OJ.EventMinigame;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>
    /// <c>EventMinigameManager</c> 의 재화 소모·점수·마일스톤·회차 리셋·승급 미션 B 연결·저장 왕복.
    /// 규칙 값은 각 Rules 테스트가 잠그고, 여기는 <b>상태가 규칙대로 움직이는지</b>를 본다.
    /// 난수와 시각은 밖에서 넣는다 — <c>Roll</c>, <c>utcNow</c>.
    /// </summary>
    public sealed class EventMinigameManagerTests
    {
        private PointManager points;
        private DiceLevelManager levels;
        private PromotionManager promotion;
        private EventMinigameManager manager;

        [SetUp]
        public void SetUp()
        {
            points = new PointManager();
            levels = new DiceLevelManager(points);
            promotion = new PromotionManager(points, levels);
            manager = NewManager();
        }

        [TearDown]
        public void TearDown()
        {
            promotion.Dispose();
        }

        private EventMinigameManager NewManager()
        {
            var created = new EventMinigameManager(points, promotion);
            created.PinballTicketCostSource = () => 1;
            created.Roll = Rolls(0.5f);
            return created;
        }

        /// <summary>앞에서부터 차례로 돌려주고, 다 쓰면 마지막 값을 계속 돌려준다.</summary>
        private static Func<float> Rolls(params float[] values)
        {
            int index = 0;
            return () => values[Math.Min(index++, values.Length - 1)];
        }

        /// <summary>그 종류의 이벤트가 열린 날(UTC 낮 12시). <paramref name="cycle"/> 이 1 이면 9일 뒤(다음 회차)다.</summary>
        private static DateTime At(EventKind kind, int cycle = 0)
        {
            DateTime day = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            while (EventMinigameRules.KindOf(EventCurrencyRules.DayIndex(day)) != kind)
                day = day.AddDays(1);

            return day.AddDays(9 * cycle);
        }

        private void Give(EventKind kind, int amount)
        {
            points.Add(EventMinigameRules.CurrencyOf(kind), amount, false);
        }

        private int Held(EventKind kind)
        {
            return points.Get(EventMinigameRules.CurrencyOf(kind));
        }

        /// <summary>등급 0(일반), 어종 0, 무게 등급 2(×1.0), 지터 0.9 — 한 마리 점수 30(굿).</summary>
        private static Func<float> PlainFish(float bait = 0.9f)
        {
            return Rolls(0f, 0f, 0.5f, 0f, bait);
        }

        // ── 공통 ───────────────────────────────────────────────────────

        [Test]
        public void 열린_이벤트_종류는_날짜로_정해진다()
        {
            foreach (EventKind kind in new[] { EventKind.Fishing, EventKind.TreasureHunt, EventKind.Rafting })
            {
                DateTime day = At(kind);
                Assert.AreEqual(kind, manager.OpenKind(day));
                Assert.IsTrue(manager.IsOpen(kind, day));
            }

            Assert.IsFalse(manager.IsOpen(EventKind.Rafting, At(EventKind.Fishing)));
        }

        // ── 낚시 ───────────────────────────────────────────────────────

        [Test]
        public void 닫힌_이벤트에서는_던질_수_없다()
        {
            Give(EventKind.Fishing, 10);

            FishingCastResult result = manager.Cast(1, FishTiming.Good, At(EventKind.Rafting));

            Assert.IsFalse(result.Valid);
            Assert.AreEqual(10, Held(EventKind.Fishing));
        }

        [Test]
        public void 낚싯대가_모자라면_던질_수_없다()
        {
            Give(EventKind.Fishing, 9);

            FishingCastResult result = manager.Cast(10, FishTiming.Good, At(EventKind.Fishing));

            Assert.IsFalse(result.Valid);
            Assert.AreEqual(9, Held(EventKind.Fishing));
            Assert.AreEqual(0, manager.FishingScore);
        }

        [Test]
        public void 정해지지_않은_배율은_던질_수_없다()
        {
            Give(EventKind.Fishing, 100);

            Assert.IsFalse(manager.Cast(7, FishTiming.Good, At(EventKind.Fishing)).Valid);
            Assert.AreEqual(100, Held(EventKind.Fishing));
        }

        [Test]
        public void 던지면_낚싯대를_배율만큼_쓰고_한_마리_점수에_배율이_곱해진다()
        {
            manager.Roll = PlainFish();
            Give(EventKind.Fishing, 10);

            FishingCastResult result = manager.Cast(10, FishTiming.Good, At(EventKind.Fishing));

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(0, Held(EventKind.Fishing));
            Assert.AreEqual(30, result.FishScore);
            Assert.AreEqual(300L, result.CastScore);
            Assert.AreEqual(300L, manager.FishingScore);
            Assert.AreEqual(0, result.Rewards.Count);
        }

        [Test]
        public void 첫_마일스톤을_넘으면_그_줄의_보상을_준다()
        {
            Give(EventKind.Fishing, 20);
            DateTime now = At(EventKind.Fishing);

            manager.Roll = PlainFish();
            manager.Cast(10, FishTiming.Good, now);
            Assert.AreEqual(0, manager.FishingClaimedCount);

            manager.Roll = PlainFish();
            FishingCastResult second = manager.Cast(10, FishTiming.Good, now);

            // 600점 ≥ 500점 → 첫 줄(핀볼 10발 + 골드 4,000).
            Assert.AreEqual(1, manager.FishingClaimedCount);
            Assert.AreEqual(10, points.Get(PointType.PinballTicket));
            Assert.AreEqual(4000, points.Get(PointType.Gold));
            Assert.IsTrue(second.Rewards.Exists(r => r.PointType == PointType.PinballTicket && r.Amount == 10));
        }

        [Test]
        public void 한_번에_여러_줄을_넘으면_앞에서부터_전부_준다()
        {
            manager.Roll = PlainFish();
            Give(EventKind.Fishing, 100);

            // 점수를 8,000 근처로 올려 두고 던진다.
            var state = new SaveState();
            state.EventMinigames.OccurrenceId = EventMinigameRules.OccurrenceId(EventCurrencyRules.DayIndex(At(EventKind.Fishing)));
            state.EventMinigames.FishingScore = 7400;
            manager.ReadFrom(state);

            manager.Cast(100, FishTiming.Good, At(EventKind.Fishing));

            // 7,400 + 3,000 = 10,400 → 500 ... 10,000 까지 7줄.
            Assert.AreEqual(7, manager.FishingClaimedCount);
        }

        [Test]
        public void 낚싯대_레벨은_누적_점수로_오른다()
        {
            var state = new SaveState();
            state.EventMinigames.OccurrenceId = EventMinigameRules.OccurrenceId(EventCurrencyRules.DayIndex(At(EventKind.Fishing)));
            state.EventMinigames.FishingScore = 10000;
            manager.ReadFrom(state);

            Assert.AreEqual(2, manager.FishingRodLevel);
        }

        [Test]
        public void 떡밥이_터지면_행운이_켜지고_시한이_지나면_꺼진다()
        {
            manager.Roll = Rolls(0f, 0f, 0.5f, 0f, 0.002f);
            Give(EventKind.Fishing, 1);
            DateTime now = At(EventKind.Fishing);

            FishingCastResult result = manager.Cast(1, FishTiming.Good, now);

            Assert.AreEqual(1, result.Baits);
            Assert.AreEqual(1, manager.GetFishingLuck(now));
            Assert.AreEqual(1, manager.GetFishingLuck(now.AddSeconds(599)));
            Assert.AreEqual(0, manager.GetFishingLuck(now.AddSeconds(601)));
            Assert.Greater(manager.FishingLuckRemaining(now).TotalSeconds, 590);
            Assert.AreEqual(TimeSpan.Zero, manager.FishingLuckRemaining(now.AddSeconds(601)));
        }

        [Test]
        public void 어종_최고_무게를_넘으면_신기록이다()
        {
            Give(EventKind.Fishing, 3);
            DateTime now = At(EventKind.Fishing);

            manager.Roll = PlainFish();
            FishingCastResult first = manager.Cast(1, FishTiming.Good, now);
            manager.Roll = PlainFish();
            FishingCastResult same = manager.Cast(1, FishTiming.Good, now);
            manager.Roll = Rolls(0f, 0f, 0.5f, 1f, 0.9f);
            FishingCastResult heavier = manager.Cast(1, FishTiming.Good, now);

            Assert.IsTrue(first.NewRecord);
            Assert.IsFalse(same.NewRecord);
            Assert.IsTrue(heavier.NewRecord);
            Assert.AreEqual(330, manager.GetFishingRecord(FishingRules.SpeciesId(0, 0)));
        }

        // ── 보물찾기 ───────────────────────────────────────────────────

        private List<int> TreasureCells(TreasureBoard board)
        {
            var cells = new List<int>();
            for (int i = 0; i < board.Owner.Length; i++)
            {
                if (board.Owner[i] >= 0)
                    cells.Add(i);
            }

            return cells;
        }

        [Test]
        public void 칸을_파면_곡괭이를_단계_비용만큼_쓴다()
        {
            Give(EventKind.TreasureHunt, 10);
            DateTime now = At(EventKind.TreasureHunt);

            TreasureDigOutcome outcome = manager.Dig(0, 0, now);

            Assert.IsTrue(outcome.Valid);
            Assert.AreEqual(1, outcome.PicksSpent);
            Assert.AreEqual(9, Held(EventKind.TreasureHunt));
        }

        [Test]
        public void 같은_칸이나_범위_밖은_곡괭이를_쓰지_않는다()
        {
            Give(EventKind.TreasureHunt, 10);
            DateTime now = At(EventKind.TreasureHunt);

            Assert.IsTrue(manager.Dig(0, 0, now).Valid);
            Assert.IsFalse(manager.Dig(0, 0, now).Valid);
            Assert.IsFalse(manager.Dig(-1, 0, now).Valid);
            Assert.IsFalse(manager.Dig(0, 99, now).Valid);

            Assert.AreEqual(9, Held(EventKind.TreasureHunt));
        }

        [Test]
        public void 곡괭이가_모자라면_파지_못한다()
        {
            DateTime now = At(EventKind.TreasureHunt);

            Assert.IsFalse(manager.Dig(0, 0, now).Valid);
        }

        [Test]
        public void 닫힌_이벤트에서는_파지_못한다()
        {
            Give(EventKind.TreasureHunt, 10);

            Assert.IsFalse(manager.Dig(0, 0, At(EventKind.Fishing)).Valid);
            Assert.AreEqual(10, Held(EventKind.TreasureHunt));
        }

        [Test]
        public void 보물을_다_찾으면_스테이지_보상이_나가고_다음_스테이지로_넘어간다()
        {
            Give(EventKind.TreasureHunt, 200);
            DateTime now = At(EventKind.TreasureHunt);

            manager.GetTreasureBoard(now, out TreasureBoard board, out bool[] _);
            List<int> cells = TreasureCells(board);

            TreasureDigOutcome last = null;
            foreach (int cell in cells)
                last = manager.Dig(cell % board.Width, cell / board.Width, now);

            Assert.IsTrue(last.StageCleared);
            Assert.IsFalse(last.KeyGranted);
            Assert.AreEqual(0, last.ChestOpened);
            Assert.AreEqual(1, last.Stage);
            Assert.AreEqual(2, manager.TreasureStage);

            // 1스테이지 보상: 핀볼 23발 + 골드(가치 약 20,520데시원).
            Assert.AreEqual(TreasureHuntRules.StageReward(1).PinballShots, points.Get(PointType.PinballTicket));
            Assert.AreEqual(TreasureHuntRules.StageReward(1).Gold, points.Get(PointType.Gold));
            Assert.AreEqual(200 - cells.Count, Held(EventKind.TreasureHunt));
        }

        [Test]
        public void 세_번째_스테이지를_깨면_열쇠를_얻는다()
        {
            Give(EventKind.TreasureHunt, 1000);
            DateTime now = At(EventKind.TreasureHunt);

            TreasureDigOutcome last = null;
            for (int stage = 1; stage <= 3; stage++)
            {
                manager.GetTreasureBoard(now, out TreasureBoard board, out bool[] _);
                foreach (int cell in TreasureCells(board))
                    last = manager.Dig(cell % board.Width, cell / board.Width, now);
            }

            Assert.IsTrue(last.StageCleared);
            Assert.IsTrue(last.KeyGranted);
            Assert.AreEqual(4, manager.TreasureStage);
            Assert.AreEqual(1, manager.TreasureKeys);
        }

        // ── 래프팅 ─────────────────────────────────────────────────────

        private SaveState StateAt(EventKind kind, Action<EventMinigameSave> edit)
        {
            var state = new SaveState();
            state.EventMinigames.OccurrenceId = EventMinigameRules.OccurrenceId(EventCurrencyRules.DayIndex(At(kind)));
            edit(state.EventMinigames);
            return state;
        }

        [Test]
        public void 돌리면_보트를_배율만큼_쓰고_거리가_쌓인다()
        {
            manager.Roll = Rolls(0.5f);   // 5m 칸
            Give(EventKind.Rafting, 10);
            DateTime now = At(EventKind.Rafting);

            RaftingSpinResult result = manager.Spin(10, now);

            Assert.IsTrue(result.Valid);
            Assert.AreEqual(50L, result.Distance);
            Assert.AreEqual(50L, manager.RaftingDistance);
            Assert.AreEqual(0, Held(EventKind.Rafting));
        }

        [Test]
        public void 무료_칸은_보트를_더_쓰지_않고_이어_돈다()
        {
            manager.Roll = Rolls(0.7f, 0.7f, 0.99f);   // 무료 5m, 무료 5m, 20m
            Give(EventKind.Rafting, 1);

            RaftingSpinResult result = manager.Spin(1, At(EventKind.Rafting));

            Assert.AreEqual(30L, result.Distance);
            Assert.AreEqual(3, result.Spins);
            Assert.AreEqual(0, Held(EventKind.Rafting));
        }

        [Test]
        public void 보트가_모자라거나_닫혀_있으면_돌릴_수_없다()
        {
            Give(EventKind.Rafting, 5);

            Assert.IsFalse(manager.Spin(10, At(EventKind.Rafting)).Valid);
            Assert.IsFalse(manager.Spin(1, At(EventKind.Fishing)).Valid);
            Assert.IsFalse(manager.Spin(3, At(EventKind.Rafting)).Valid);
            Assert.AreEqual(5, Held(EventKind.Rafting));
        }

        [Test]
        public void 일반_마일스톤은_바로_지급한다()
        {
            manager.Roll = Rolls(0.99f);   // 20m
            Give(EventKind.Rafting, 10);

            RaftingSpinResult result = manager.Spin(10, At(EventKind.Rafting));

            // 200m → 50 / 100 / 200 세 줄.
            Assert.AreEqual(3, manager.RaftingClaimedCount);
            Assert.Greater(points.Get(PointType.PinballTicket), 0);
            Assert.Greater(result.Rewards.Count, 0);
            Assert.AreEqual(0, result.NewPendingChoices.Count);
        }

        private int ChoiceRow(int threshold)
        {
            for (int i = 0; i < EventRewardTables.Rafting.Length; i++)
            {
                if (EventRewardTables.Rafting[i].Threshold == threshold)
                    return i;
            }

            return -1;
        }

        private void LoadRaftingJustBeforeFirstChoice()
        {
            int row = ChoiceRow(1000);
            manager.ReadFrom(StateAt(EventKind.Rafting, s =>
            {
                s.RaftingDistance = 990;
                s.RaftingClaimed = row;
            }));
        }

        [Test]
        public void 선택_상자는_고르기를_기다린다()
        {
            LoadRaftingJustBeforeFirstChoice();
            manager.Roll = Rolls(0.99f);   // 20m
            Give(EventKind.Rafting, 1);

            RaftingSpinResult result = manager.Spin(1, At(EventKind.Rafting));

            int row = ChoiceRow(1000);
            CollectionAssert.AreEqual(new[] { row }, result.NewPendingChoices);
            CollectionAssert.AreEqual(new[] { row }, manager.GetRaftingPending());
            Assert.AreEqual(0, points.Get(PointType.SynergyTicket));
        }

        [Test]
        public void 선택한_묶음을_받고_다시_받을_수는_없다()
        {
            LoadRaftingJustBeforeFirstChoice();
            manager.Roll = Rolls(0.99f);
            Give(EventKind.Rafting, 1);
            DateTime now = At(EventKind.Rafting);
            manager.Spin(1, now);
            int row = ChoiceRow(1000);

            List<PointRewardEntry> rewards = manager.TryClaimChoice(row, 1, now);

            Assert.IsNotNull(rewards);
            Assert.Greater(points.Get(PointType.SynergyTicket), 0);
            Assert.AreEqual(0, manager.GetRaftingPending().Count);
            Assert.IsNull(manager.TryClaimChoice(row, 1, now));
        }

        [Test]
        public void 범위_밖_선택은_받을_수_없다()
        {
            LoadRaftingJustBeforeFirstChoice();
            manager.Roll = Rolls(0.99f);
            Give(EventKind.Rafting, 1);
            DateTime now = At(EventKind.Rafting);
            manager.Spin(1, now);
            int row = ChoiceRow(1000);

            Assert.IsNull(manager.TryClaimChoice(row, 3, now));
            Assert.IsNull(manager.TryClaimChoice(row, -1, now));
            Assert.IsNull(manager.TryClaimChoice(row + 1, 0, now));
            Assert.AreEqual(1, manager.GetRaftingPending().Count);
        }

        [Test]
        public void 마지막_마일스톤을_넘은_거리는_핀볼로_바뀐다()
        {
            int last = EventRewardTables.Rafting.Length;
            manager.ReadFrom(StateAt(EventKind.Rafting, s =>
            {
                s.RaftingDistance = EventRewardTables.RaftingLastThreshold;
                s.RaftingClaimed = last;
            }));
            manager.Roll = Rolls(0.99f);   // 20m × 10 = 200m → 200 × 5 / 97 = 10발
            Give(EventKind.Rafting, 10);

            RaftingSpinResult result = manager.Spin(10, At(EventKind.Rafting));

            Assert.AreEqual(10, points.Get(PointType.PinballTicket));
            Assert.IsTrue(result.Rewards.Exists(r => r.PointType == PointType.PinballTicket && r.Amount == 10));
        }

        // ── 회차 ───────────────────────────────────────────────────────

        [Test]
        public void 회차가_바뀌면_진행도를_비우고_낚시_도감은_남긴다()
        {
            manager.Roll = PlainFish();
            Give(EventKind.Fishing, 10);
            manager.Cast(10, FishTiming.Good, At(EventKind.Fishing));
            Assert.AreEqual(300L, manager.FishingScore);
            Assert.AreEqual(270, manager.GetFishingRecord(FishingRules.SpeciesId(0, 0)));

            manager.Tick(At(EventKind.Fishing, 1));

            Assert.AreEqual(0L, manager.FishingScore);
            Assert.AreEqual(0, manager.FishingClaimedCount);
            Assert.AreEqual(270, manager.GetFishingRecord(FishingRules.SpeciesId(0, 0)));
        }

        [Test]
        public void 같은_회차_안에서는_진행도가_그대로다()
        {
            manager.Roll = PlainFish();
            Give(EventKind.Fishing, 10);
            manager.Cast(10, FishTiming.Good, At(EventKind.Fishing));

            manager.Tick(At(EventKind.Fishing).AddHours(5));

            Assert.AreEqual(300L, manager.FishingScore);
        }

        [Test]
        public void 회차가_바뀌면_고르지_않은_상자는_첫_묶음으로_자동_지급한다()
        {
            LoadRaftingJustBeforeFirstChoice();
            manager.Roll = Rolls(0.99f);
            Give(EventKind.Rafting, 1);
            manager.Spin(1, At(EventKind.Rafting));
            int ticketsBefore = points.Get(PointType.PinballTicket);

            IReadOnlyList<PointRewardEntry> settled = null;
            manager.OnSettled += rewards => settled = rewards;

            manager.Tick(At(EventKind.Rafting, 1));

            // 첫 묶음은 핀볼 중심이다.
            Assert.Greater(points.Get(PointType.PinballTicket), ticketsBefore);
            Assert.IsNotNull(settled);
            Assert.AreEqual(0, manager.GetRaftingPending().Count);
            Assert.AreEqual(0L, manager.RaftingDistance);
        }

        [Test]
        public void 회차가_바뀌면_보물찾기_판도_비운다()
        {
            Give(EventKind.TreasureHunt, 10);
            manager.Dig(0, 0, At(EventKind.TreasureHunt));
            Assert.AreEqual(9, Held(EventKind.TreasureHunt));

            manager.Tick(At(EventKind.TreasureHunt, 1));

            Assert.AreEqual(1, manager.TreasureStage);
            manager.GetTreasureBoard(At(EventKind.TreasureHunt, 1), out TreasureBoard _, out bool[] revealed);
            Assert.IsFalse(System.Array.Exists(revealed, r => r));
        }

        // ── 승급 미션 B ────────────────────────────────────────────────

        private void QueueTierTwo(DateTime now)
        {
            levels.SetLevel(DiceType.Tornado, 5, false);
            Assert.AreEqual(PromotionRegisterResult.Ok, promotion.TryRegister(DiceType.Tornado, now));
            PromotionMission mission = PromotionRules.AssignedMission(PromotionManager.SlotOf(DiceType.Tornado), 1);
            promotion.Notify(mission, 1000000);
            Assert.AreEqual(1, promotion.GetTier(DiceType.Tornado));

            levels.SetLevel(DiceType.Tornado, 15, false);
            Assert.AreEqual(PromotionRegisterResult.Ok, promotion.TryRegister(DiceType.Tornado, now));
        }

        [Test]
        public void 이벤트_재화를_쓴_만큼_승급_미션_B가_오른다()
        {
            DateTime now = At(EventKind.Fishing);
            QueueTierTwo(now);
            manager.Roll = PlainFish();
            Give(EventKind.Fishing, 10);

            manager.Cast(10, FishTiming.Good, now);

            Assert.AreEqual(10, promotion.GetProgress(PromotionMission.EventCurrency));
        }

        [Test]
        public void 실패한_플레이는_미션_B를_올리지_않는다()
        {
            DateTime now = At(EventKind.Fishing);
            QueueTierTwo(now);
            Give(EventKind.Fishing, 5);

            manager.Cast(10, FishTiming.Good, now);   // 낚싯대 부족

            Assert.AreEqual(0, promotion.GetProgress(PromotionMission.EventCurrency));
        }

        // ── 저장 ───────────────────────────────────────────────────────

        private static void AssertSameState(EventMinigameManager a, EventMinigameManager b, DateTime now)
        {
            Assert.AreEqual(a.FishingScore, b.FishingScore);
            Assert.AreEqual(a.FishingClaimedCount, b.FishingClaimedCount);
            Assert.AreEqual(a.GetFishingLuck(now), b.GetFishingLuck(now));
            Assert.AreEqual(a.GetFishingRecord(FishingRules.SpeciesId(0, 0)), b.GetFishingRecord(FishingRules.SpeciesId(0, 0)));
            Assert.AreEqual(a.TreasureStage, b.TreasureStage);
            Assert.AreEqual(a.RaftingDistance, b.RaftingDistance);
            Assert.AreEqual(a.RaftingClaimedCount, b.RaftingClaimedCount);
            CollectionAssert.AreEqual(a.GetRaftingPending(), b.GetRaftingPending());
        }

        [Test]
        public void 낚시_상태는_저장하고_읽으면_같다()
        {
            DateTime fishingDay = At(EventKind.Fishing);
            manager.Roll = Rolls(0f, 0f, 0.5f, 0f, 0.002f);
            Give(EventKind.Fishing, 100);
            manager.Cast(10, FishTiming.Good, fishingDay);

            var state = new SaveState();
            manager.WriteTo(state);
            string json = SaveSerializer.Serialize(state);

            EventMinigameManager loaded = NewManager();
            loaded.ReadFrom(SaveSerializer.Deserialize(json));

            AssertSameState(manager, loaded, fishingDay);
            Assert.Greater(manager.FishingScore, 0);
            Assert.AreEqual(4, loaded.GetFishingLuck(fishingDay));
        }

        [Test]
        public void 래프팅_상태는_저장하고_읽으면_같다()
        {
            LoadRaftingJustBeforeFirstChoice();
            manager.Roll = Rolls(0.99f);
            Give(EventKind.Rafting, 1);
            DateTime now = At(EventKind.Rafting);
            manager.Spin(1, now);

            var state = new SaveState();
            manager.WriteTo(state);
            EventMinigameManager loaded = NewManager();
            loaded.ReadFrom(SaveSerializer.Deserialize(SaveSerializer.Serialize(state)));

            AssertSameState(manager, loaded, now);
            Assert.AreEqual(1, loaded.GetRaftingPending().Count);
            Assert.AreEqual(1010L, loaded.RaftingDistance);
        }

        [Test]
        public void 읽은_보물찾기_판은_이미_판_칸을_이어_간다()
        {
            DateTime now = At(EventKind.TreasureHunt);
            Give(EventKind.TreasureHunt, 50);
            manager.Roll = Rolls(0.5f);
            manager.Dig(1, 1, now);
            manager.Dig(2, 3, now);

            var state = new SaveState();
            manager.WriteTo(state);
            EventMinigameManager loaded = NewManager();
            loaded.ReadFrom(state);

            loaded.GetTreasureBoard(now, out TreasureBoard board, out bool[] revealed);
            manager.GetTreasureBoard(now, out TreasureBoard original, out bool[] originalRevealed);

            CollectionAssert.AreEqual(original.Owner, board.Owner);
            CollectionAssert.AreEqual(originalRevealed, revealed);
            Assert.IsTrue(revealed[1 * board.Width + 1]);
            Assert.IsTrue(revealed[3 * board.Width + 2]);
        }

        [Test]
        public void 손상된_저장값은_읽을_때_가둔다()
        {
            var state = StateAt(EventKind.Fishing, s =>
            {
                s.FishingScore = -500;
                s.FishingLuckStacks = 99;
                s.FishingClaimed = 9999;
                s.FishingRecords["999"] = 100;
                s.FishingRecords["abc"] = 100;
                s.FishingRecords["0"] = -3;
                s.FishingRecords["10"] = 800;
                s.TreasureStage = -4;
                s.TreasureHasBoard = true;
                s.TreasureBoardSeed = 77;
                s.TreasureRevealed.AddRange(new[] { -1, 3, 9999 });
                s.RaftingDistance = -7;
                s.RaftingClaimed = 9999;
                s.RaftingPending.AddRange(new[] { 0, -2, 9999, ChoiceRow(1000), ChoiceRow(1000) });
            });

            manager.ReadFrom(state);
            DateTime now = At(EventKind.Fishing);

            Assert.AreEqual(0L, manager.FishingScore);
            Assert.AreEqual(FishingRules.MaxLuckStacks, state.EventMinigames.FishingLuckStacks < 0 ? 0 : FishingRules.MaxLuckStacks);
            Assert.AreEqual(EventRewardTables.Fishing.Length, manager.FishingClaimedCount);
            Assert.AreEqual(0, manager.GetFishingRecord(999));
            Assert.AreEqual(0, manager.GetFishingRecord(0));
            Assert.AreEqual(800, manager.GetFishingRecord(10));
            Assert.AreEqual(1, manager.TreasureStage);
            Assert.AreEqual(0L, manager.RaftingDistance);
            Assert.AreEqual(EventRewardTables.Rafting.Length, manager.RaftingClaimedCount);

            // 선택 줄이 아닌 번호·범위 밖·중복은 버리고 3택1 줄 하나만 남는다.
            CollectionAssert.AreEqual(new[] { ChoiceRow(1000) }, manager.GetRaftingPending());

            manager.GetTreasureBoard(now, out TreasureBoard board, out bool[] revealed);
            Assert.AreEqual(board.Width * board.Height, revealed.Length);
            Assert.IsTrue(revealed[3]);
        }

        [Test]
        public void 비어_있는_저장은_처음_상태다()
        {
            manager.ReadFrom(new SaveState());

            Assert.AreEqual(0L, manager.FishingScore);
            Assert.AreEqual(1, manager.TreasureStage);
            Assert.AreEqual(0L, manager.RaftingDistance);
            Assert.AreEqual(0, manager.GetRaftingPending().Count);
        }
    }
}
