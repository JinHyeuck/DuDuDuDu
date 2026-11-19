using System;
using NUnit.Framework;
using OJ.Core;
using OJ.Dice;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>
    /// <c>PromotionManager</c> 의 큐·진행도·투입·환급·저장 왕복.
    /// 규칙 값(요구량 표 등)은 <c>PromotionRulesTests</c> 가 잠그고, 여기는 <b>상태가 규칙대로 움직이는지</b>를 본다.
    /// <c>PointManager</c>·<c>DiceLevelManager</c> 는 생성자가 순수해서 실물을 그대로 쓴다.
    /// </summary>
    public sealed class PromotionManagerTests
    {
        private PointManager points;
        private DiceLevelManager levels;
        private PromotionManager manager;

        private static readonly DateTime T0 = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            points = new PointManager();
            levels = new DiceLevelManager(points);
            manager = new PromotionManager(points, levels);
        }

        [TearDown]
        public void TearDown()
        {
            manager.Dispose();
        }

        private void FillTierOneMissions()
        {
            manager.Notify(PromotionMission.PinballShots, 100000);
            manager.Notify(PromotionMission.SynergyTickets, 100000);
            manager.Notify(PromotionMission.Sweeps, 100000);
            manager.Notify(PromotionMission.TowerChallenges, 100000);
            manager.Notify(PromotionMission.StageChallenges, 100000);
            manager.Notify(PromotionMission.MonsterKills, 100000);
            manager.Notify(PromotionMission.BossKills, 100000);
        }

        private void PromoteToTierOne(DiceType dice)
        {
            levels.SetLevel(dice, 5, false);
            Assert.AreEqual(PromotionRegisterResult.Ok, manager.TryRegister(dice, T0));
            FillTierOneMissions();
            Assert.AreEqual(1, manager.GetTier(dice));
        }

        // ── 등록 ───────────────────────────────────────────────────────

        [Test]
        public void 레벨이_모자라면_등록할_수_없다()
        {
            levels.SetLevel(DiceType.Tornado, 4, false);

            Assert.AreEqual(PromotionRegisterResult.LevelTooLow, manager.TryRegister(DiceType.Tornado, T0));
            Assert.IsFalse(manager.HasQueue);
        }

        [Test]
        public void 레벨이_차면_T1_큐에_오른다()
        {
            levels.SetLevel(DiceType.Tornado, 5, false);

            Assert.AreEqual(PromotionRegisterResult.Ok, manager.TryRegister(DiceType.Tornado, T0));

            Assert.IsTrue(manager.TryGetQueue(out PromotionQueueView view));
            Assert.AreEqual(DiceType.Tornado, view.DiceType);
            Assert.AreEqual(1, view.TargetTier);
        }

        [Test]
        public void 같은_다이스를_두_번_올릴_수_없다()
        {
            levels.SetLevel(DiceType.Tornado, 5, false);
            manager.TryRegister(DiceType.Tornado, T0);

            Assert.AreEqual(PromotionRegisterResult.AlreadyQueued, manager.TryRegister(DiceType.Tornado, T0));
        }

        [Test]
        public void 승급_대상이_아닌_값은_거절한다()
        {
            Assert.AreEqual(PromotionRegisterResult.MaxTier, manager.TryRegister(DiceType.Max, T0));
        }

        // ── 교체 ───────────────────────────────────────────────────────

        [Test]
        public void 교체는_24시간_잠기고_풀리면_진행도가_초기화된다()
        {
            levels.SetLevel(DiceType.Tornado, 5, false);
            levels.SetLevel(DiceType.Stun, 5, false);
            manager.TryRegister(DiceType.Tornado, T0);
            manager.Notify(PromotionMission.Sweeps, 4);

            Assert.AreEqual(PromotionRegisterResult.ReplaceLocked,
                manager.TryRegister(DiceType.Stun, T0.AddHours(23)));
            Assert.AreEqual(4, manager.GetProgress(PromotionMission.Sweeps));

            Assert.AreEqual(PromotionRegisterResult.Ok, manager.TryRegister(DiceType.Stun, T0.AddHours(24)));
            Assert.IsTrue(manager.TryGetQueue(out PromotionQueueView view));
            Assert.AreEqual(DiceType.Stun, view.DiceType);
            Assert.AreEqual(0, manager.GetProgress(PromotionMission.Sweeps));
        }

        // ── 진행도 ─────────────────────────────────────────────────────

        [Test]
        public void 큐가_비었으면_센_것이_인정되지_않는다()
        {
            manager.Notify(PromotionMission.Sweeps, 10);

            levels.SetLevel(DiceType.Tornado, 5, false);
            manager.TryRegister(DiceType.Tornado, T0);

            Assert.AreEqual(0, manager.GetProgress(PromotionMission.Sweeps));
        }

        [Test]
        public void 진행도는_요구량에서_멈춘다()
        {
            levels.SetLevel(DiceType.Tornado, 5, false);
            manager.TryRegister(DiceType.Tornado, T0);

            manager.Notify(PromotionMission.Sweeps, 9999);

            Assert.AreEqual(10, manager.GetProgress(PromotionMission.Sweeps));
        }

        [Test]
        public void 시너지_티켓을_쓰면_자동으로_센다()
        {
            levels.SetLevel(DiceType.Tornado, 5, false);
            manager.TryRegister(DiceType.Tornado, T0);
            points.Add(PointType.SynergyTicket, 80, false);

            Assert.IsTrue(points.TrySpend(PointType.SynergyTicket, 30, false));

            Assert.AreEqual(30, manager.GetProgress(PromotionMission.SynergyTickets));
        }

        [Test]
        public void 시너지_티켓_외의_소모는_세지_않는다()
        {
            levels.SetLevel(DiceType.Tornado, 5, false);
            manager.TryRegister(DiceType.Tornado, T0);
            points.Add(PointType.Gold, 500, false);

            points.TrySpend(PointType.Gold, 100, false);

            Assert.AreEqual(0, manager.GetProgress(PromotionMission.SynergyTickets));
        }

        // ── 완료 ───────────────────────────────────────────────────────

        [Test]
        public void 미션이_다_차면_승급하고_큐가_비며_바로_다음을_올릴_수_있다()
        {
            int promotedTier = 0;
            DiceType promotedDice = DiceType.Max;
            manager.OnPromoted += (dice, tier) => { promotedDice = dice; promotedTier = tier; };

            PromoteToTierOne(DiceType.Tornado);

            Assert.AreEqual(DiceType.Tornado, promotedDice);
            Assert.AreEqual(1, promotedTier);
            Assert.IsFalse(manager.HasQueue);

            // 큐가 비었으니 24시간을 기다리지 않고 다음 다이스를 올릴 수 있다.
            levels.SetLevel(DiceType.Stun, 5, false);
            Assert.AreEqual(PromotionRegisterResult.Ok, manager.TryRegister(DiceType.Stun, T0));
        }

        [Test]
        public void 한_미션이라도_비면_승급하지_않는다()
        {
            levels.SetLevel(DiceType.Tornado, 5, false);
            manager.TryRegister(DiceType.Tornado, T0);

            manager.Notify(PromotionMission.PinballShots, 100000);
            manager.Notify(PromotionMission.SynergyTickets, 100000);
            manager.Notify(PromotionMission.Sweeps, 100000);
            manager.Notify(PromotionMission.TowerChallenges, 100000);
            manager.Notify(PromotionMission.StageChallenges, 100000);
            manager.Notify(PromotionMission.MonsterKills, 100000);

            Assert.AreEqual(0, manager.GetTier(DiceType.Tornado));
            Assert.IsTrue(manager.HasQueue);
        }

        [Test]
        public void 기본_다이스는_T1이_끝이다()
        {
            PromoteToTierOne(DiceType.Normal);

            levels.SetLevel(DiceType.Normal, 30, false);
            Assert.AreEqual(PromotionRegisterResult.MaxTier, manager.TryRegister(DiceType.Normal, T0));
        }

        [Test]
        public void T2는_Lv15가_되어야_올릴_수_있다()
        {
            PromoteToTierOne(DiceType.Tornado);

            levels.SetLevel(DiceType.Tornado, 14, false);
            Assert.AreEqual(PromotionRegisterResult.LevelTooLow, manager.TryRegister(DiceType.Tornado, T0));

            levels.SetLevel(DiceType.Tornado, 15, false);
            Assert.AreEqual(PromotionRegisterResult.Ok, manager.TryRegister(DiceType.Tornado, T0));
            manager.TryGetQueue(out PromotionQueueView view);
            Assert.AreEqual(2, view.TargetTier);
        }

        // ── 이벤트 재화 투입 (미션 B) ───────────────────────────────────

        private PointType OpenCurrency(DateTime utc)
        {
            return EventCurrencyRules.Resolve(EventCurrencyRules.DayIndex(utc));
        }

        private void QueueTierTwo(DiceType dice)
        {
            PromoteToTierOne(dice);
            levels.SetLevel(dice, 15, false);
            Assert.AreEqual(PromotionRegisterResult.Ok, manager.TryRegister(dice, T0));
        }

        [Test]
        public void 열린_이벤트_재화를_요구량까지만_넣는다()
        {
            QueueTierTwo(DiceType.Tornado);
            PointType open = OpenCurrency(T0);
            points.Add(open, 1500, false);

            int used = manager.DonateEventCurrency(5000, T0, false);

            Assert.AreEqual(1000, used);
            Assert.AreEqual(500, points.Get(open));
            Assert.AreEqual(1000, manager.GetProgress(PromotionMission.EventCurrency));
        }

        [Test]
        public void 가진_만큼만_넣는다()
        {
            QueueTierTwo(DiceType.Tornado);
            PointType open = OpenCurrency(T0);
            points.Add(open, 40, false);

            Assert.AreEqual(40, manager.DonateEventCurrency(5000, T0, false));
            Assert.AreEqual(0, points.Get(open));
        }

        [Test]
        public void 닫힌_이벤트의_재화는_넣을_수_없다()
        {
            QueueTierTwo(DiceType.Tornado);
            PointType open = OpenCurrency(T0);
            PointType closed = open == PointType.EventCurrency1 ? PointType.EventCurrency2 : PointType.EventCurrency1;
            points.Add(closed, 500, false);

            Assert.AreEqual(0, manager.DonateEventCurrency(500, T0, false));
            Assert.AreEqual(500, points.Get(closed));
        }

        [Test]
        public void T1에는_이벤트_재화_미션이_없다()
        {
            levels.SetLevel(DiceType.Tornado, 5, false);
            manager.TryRegister(DiceType.Tornado, T0);
            PointType open = OpenCurrency(T0);
            points.Add(open, 100, false);

            Assert.AreEqual(0, manager.DonateEventCurrency(100, T0, false));
            Assert.AreEqual(100, points.Get(open));
        }

        [Test]
        public void T2는_이벤트_재화와_참여_미션을_다_채워야_끝난다()
        {
            QueueTierTwo(DiceType.Tornado);
            PointType open = OpenCurrency(T0);
            points.Add(open, 1000, false);

            manager.Notify(PromotionMission.PinballShots, 3000);
            manager.Notify(PromotionMission.SynergyTickets, 500);
            manager.Notify(PromotionMission.Sweeps, 100);
            manager.Notify(PromotionMission.StageChallenges, 30);
            Assert.AreEqual(1, manager.GetTier(DiceType.Tornado));

            manager.DonateEventCurrency(1000, T0, false);
            Assert.AreEqual(2, manager.GetTier(DiceType.Tornado));
        }

        // ── 환급 ───────────────────────────────────────────────────────

        [Test]
        public void 끝난_이벤트의_남은_재화는_3개당_1발로_환급된다()
        {
            PointType open = OpenCurrency(T0);
            PointType[] all = { PointType.EventCurrency1, PointType.EventCurrency2, PointType.EventCurrency3 };
            PointType closedA = open == all[0] ? all[1] : all[0];
            PointType closedB = open == all[2] ? all[1] : all[2];

            points.Add(open, 50, false);
            points.Add(closedA, 8, false);   // 2발, 2개는 버려진다
            points.Add(closedB, 2, false);   // 0발, 그래도 비워진다

            int shots = manager.RefundClosedEventCurrency(T0, false);

            Assert.AreEqual(2, shots);
            Assert.AreEqual(2, points.Get(PointType.PinballTicket));
            Assert.AreEqual(50, points.Get(open));
            Assert.AreEqual(0, points.Get(closedA));
            Assert.AreEqual(0, points.Get(closedB));
        }

        [Test]
        public void 플레이_중_이벤트가_바뀌면_Tick이_환급한다()
        {
            int refunded = 0;
            manager.OnEventCurrencyRefunded += shots => refunded += shots;

            manager.Tick(T0, false);
            PointType before = OpenCurrency(T0);
            points.Add(before, 30, false);

            // 같은 날의 Tick 은 아무 일도 하지 않는다.
            manager.Tick(T0.AddHours(3), false);
            Assert.AreEqual(30, points.Get(before));

            // 이벤트는 3일마다 바뀐다. 3일 뒤에는 방금까지 열려 있던 재화가 닫힌다.
            DateTime later = T0.AddDays(EventCurrencyRules.DaysPerEvent);
            Assert.AreNotEqual(before, OpenCurrency(later));

            manager.Tick(later, false);

            Assert.AreEqual(0, points.Get(before));
            Assert.AreEqual(10, points.Get(PointType.PinballTicket));
            Assert.AreEqual(10, refunded);
        }

        [Test]
        public void 같은_이벤트_안에서_날이_바뀌어도_환급하지_않는다()
        {
            // 이벤트의 첫날로 맞춘다 — 그날과 다음 날은 같은 이벤트다.
            DateTime first = T0.Date;
            while (EventCurrencyRules.DayIndex(first) % EventCurrencyRules.DaysPerEvent != 0)
                first = first.AddDays(1);

            manager.Tick(first, false);
            PointType open = OpenCurrency(first);
            points.Add(open, 30, false);

            DateTime nextDay = first.AddDays(1);
            Assert.AreEqual(open, OpenCurrency(nextDay));

            manager.Tick(nextDay, false);

            Assert.AreEqual(30, points.Get(open));
            Assert.AreEqual(0, points.Get(PointType.PinballTicket));
        }

        // ── 저장 왕복 ──────────────────────────────────────────────────

        private PromotionManager Reload(SaveState saved)
        {
            var reloaded = new PromotionManager(points, levels);
            reloaded.ReadFrom(saved);
            return reloaded;
        }

        [Test]
        public void 단계와_큐와_진행도가_저장_왕복된다()
        {
            PromoteToTierOne(DiceType.Tornado);
            levels.SetLevel(DiceType.Stun, 5, false);
            manager.TryRegister(DiceType.Stun, T0.AddHours(1));
            manager.Notify(PromotionMission.Sweeps, 7);
            manager.Notify(PromotionMission.MonsterKills, 20);

            var saved = new SaveState();
            manager.WriteTo(saved);
            PromotionManager reloaded = Reload(saved);

            Assert.AreEqual(1, reloaded.GetTier(DiceType.Tornado));
            Assert.IsTrue(reloaded.TryGetQueue(out PromotionQueueView view));
            Assert.AreEqual(DiceType.Stun, view.DiceType);
            Assert.AreEqual(1, view.TargetTier);
            Assert.AreEqual(T0.AddHours(1).Ticks, view.RegisteredUtcTicks);
            Assert.AreEqual(7, reloaded.GetProgress(PromotionMission.Sweeps));
            Assert.AreEqual(20, reloaded.GetProgress(PromotionMission.MonsterKills));

            var again = new SaveState();
            reloaded.WriteTo(again);
            Assert.AreEqual(saved.Promotion.QueuedDice, again.Promotion.QueuedDice);
            CollectionAssert.AreEqual(saved.Promotion.Tiers, again.Promotion.Tiers);
            CollectionAssert.AreEqual(saved.Promotion.Progress, again.Promotion.Progress);

            reloaded.Dispose();
        }

        [Test]
        public void 손상된_세이브는_버린다()
        {
            var saved = new SaveState();
            saved.Promotion.Tiers["NoSuchDice"] = 2;
            saved.Promotion.Tiers["Normal"] = 3;          // 기본 5종은 T1 이 끝이다 → 1 로 자른다
            saved.Promotion.Tiers["Tornado"] = -4;        // 음수는 없는 것이다
            saved.Promotion.QueuedDice = "Stun";
            saved.Promotion.QueuedTargetTier = 1;
            saved.Promotion.Progress["Sweeps"] = 99999;   // 요구량으로 자른다
            saved.Promotion.Progress["Nonsense"] = 5;

            PromotionManager reloaded = Reload(saved);

            Assert.AreEqual(1, reloaded.GetTier(DiceType.Normal));
            Assert.AreEqual(0, reloaded.GetTier(DiceType.Tornado));
            Assert.AreEqual(10, reloaded.GetProgress(PromotionMission.Sweeps));

            var again = new SaveState();
            reloaded.WriteTo(again);
            Assert.IsFalse(again.Promotion.Progress.ContainsKey("Nonsense"));
            Assert.IsFalse(again.Promotion.Tiers.ContainsKey("NoSuchDice"));

            reloaded.Dispose();
        }

        [Test]
        public void 모르는_다이스가_큐에_있으면_큐를_비운다()
        {
            var saved = new SaveState();
            saved.Promotion.QueuedDice = "Gone";
            saved.Promotion.QueuedTargetTier = 1;

            PromotionManager reloaded = Reload(saved);

            Assert.IsFalse(reloaded.HasQueue);
            reloaded.Dispose();
        }

        [Test]
        public void 저장된_목표_단계가_다르면_진행도를_이어붙이지_않는다()
        {
            var saved = new SaveState();
            saved.Promotion.QueuedDice = "Stun";
            saved.Promotion.QueuedTargetTier = 2;         // 이 다이스는 아직 T0 → 계산한 목표는 1
            saved.Promotion.Progress["Sweeps"] = 10;

            PromotionManager reloaded = Reload(saved);

            Assert.IsTrue(reloaded.HasQueue);
            Assert.AreEqual(0, reloaded.GetProgress(PromotionMission.Sweeps));
            reloaded.Dispose();
        }
    }
}
