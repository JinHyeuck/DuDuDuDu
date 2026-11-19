using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.Pinball;
using OJ.Point;
using OJ.Save;

namespace OJ.Dice
{
    /// <summary>큐 한 칸의 현재 상태. 화면이 그대로 그린다.</summary>
    public readonly struct PromotionQueueView
    {
        public PromotionQueueView(DiceType diceType, int targetTier, long registeredUtcTicks)
        {
            DiceType = diceType;
            TargetTier = targetTier;
            RegisteredUtcTicks = registeredUtcTicks;
        }

        public DiceType DiceType { get; }

        /// <summary>이 큐가 올리려는 단계(1~3).</summary>
        public int TargetTier { get; }

        public long RegisteredUtcTicks { get; }
    }

    /// <summary>
    /// 다이스 승급의 실행부. <b>큐 한 칸 · 등록 뒤 쓴 분만 인정하는 진행도 · 이벤트 재화 투입과 환급</b>이
    /// 한 자리에 있다. 규칙(요구량·레벨·잠금)은 <see cref="PromotionRules"/> 가 정본이고 여기는 상태를 든다.
    ///
    /// <b>세는 곳이 여럿이라 <see cref="Notify"/> 한 곳으로 모았다.</b> 핀볼·소탕·탑·스테이지·처치가
    /// 각자 "큐가 있는지 → 이 미션이 이 단계에 있는지 → 요구량을 넘지 않게 더하고 → 다 찼는지" 를
    /// 하면 그 순서가 여섯 벌이 된다. 시너지 티켓만 <c>PointManager.OnPointSpent</c> 에서 직접 줍는다 —
    /// 소모처가 앞으로 늘어도 빠뜨리지 않으려는 것이다(시즌 패스가 고기를 세는 방식과 같다).
    ///
    /// <b>등록 전에 쓴 분은 인정하지 않는다.</b> 큐가 비었을 때의 <see cref="Notify"/> 는 아무것도 하지 않는다.
    /// 그리고 <b>세이브는 건드리지 않는다</b> — 적 처치는 한 웨이브에 수백 번 들어오므로 호출마다 저장하면
    /// 전투가 끊긴다. 값은 메모리에만 올라가고 파일은 <c>SaveService</c> 가 앱 정지·종료에서 쓴다.
    ///
    /// <b>미션 A(보유형, T3 전용)는 아직 없다.</b> 종류·수량을 테스트 뒤에 정하기로 했고,
    /// 정해지면 <see cref="AreOwnedRequirementsMet"/> 한 곳에 건다.
    /// </summary>
    [Preserve]
    public sealed class PromotionManager : ISaveStateOwner, IDisposable
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static PromotionManager Instance { get; internal set; }

        /// <summary>큐·진행도·단계가 바뀌어 화면이 다시 그려져야 할 때.</summary>
        public event Action OnChanged;

        /// <summary>승급이 끝났을 때(다이스, 새 단계).</summary>
        public event Action<DiceType, int> OnPromoted;

        private readonly PointManager points;
        private readonly DiceLevelManager levels;

        private readonly Dictionary<DiceType, int> tiers = new Dictionary<DiceType, int>();
        private readonly Dictionary<string, int> progress = new Dictionary<string, int>(StringComparer.Ordinal);

        private bool hasQueue;
        private DiceType queuedDice;
        private int queuedTargetTier;
        private long registeredUtcTicks;

        public PromotionManager(PointManager points, DiceLevelManager levels)
        {
            this.points = points;
            this.levels = levels;

            if (this.points != null)
                this.points.OnPointSpent += OnPointSpent;
        }

        public void Dispose()
        {
            if (points != null)
                points.OnPointSpent -= OnPointSpent;
        }

        // ── 조회 ───────────────────────────────────────────────────────

        /// <summary>이 다이스가 지금까지 오른 승급 단계(0~3).</summary>
        public int GetTier(DiceType diceType)
        {
            return tiers.TryGetValue(diceType, out int tier) ? tier : 0;
        }

        /// <summary>
        /// 전투 코드가 부르는 단계 조회. 매니저가 없으면(헤드리스·로비 밖) 0 이다 —
        /// 승급 효과는 단계가 열렸을 때만 켜지므로 0 이면 원래 동작 그대로다.
        /// </summary>
        public static int TierOf(DiceType diceType)
        {
            return Instance != null ? Instance.GetTier(diceType) : 0;
        }

        /// <summary>기본 5종인가. 기본 5종은 T1 까지만 오른다.</summary>
        public static bool IsBasic(DiceType diceType)
        {
            return diceType >= DiceType.Normal && diceType <= DiceType.Poison;
        }

        /// <summary>승급 대상인가. 기본·레어·킹 15종이 전부 대상이고 그 밖의 값은 아니다.</summary>
        public static bool IsPromotable(DiceType diceType)
        {
            return (diceType >= DiceType.Normal && diceType <= DiceType.Poison)
                   || (diceType >= DiceType.Tornado && diceType <= DiceType.Time)
                   || (diceType >= DiceType.KingNormal && diceType <= DiceType.KingPoison);
        }

        public bool HasQueue => hasQueue;

        /// <summary>큐 한 칸. 비어 있으면 false.</summary>
        public bool TryGetQueue(out PromotionQueueView view)
        {
            view = hasQueue ? new PromotionQueueView(queuedDice, queuedTargetTier, registeredUtcTicks) : default;
            return hasQueue;
        }

        /// <summary>큐에 올라 있는 다이스의 미션 진행도(요구량으로 자른 값). 큐가 비었거나 제외된 미션이면 0.</summary>
        public int GetProgress(PromotionMission mission)
        {
            return hasQueue ? PromotionRules.Progress(progress, mission, queuedTargetTier) : 0;
        }

        /// <summary>교체 잠금이 남은 시간. 큐가 비었거나 잠기지 않았으면 0.</summary>
        public TimeSpan ReplaceLockRemaining(DateTime utcNow)
        {
            return hasQueue
                ? PromotionRules.ReplaceLockRemaining(registeredUtcTicks, utcNow.Ticks)
                : TimeSpan.Zero;
        }

        // ── 등록 ───────────────────────────────────────────────────────

        public PromotionRegisterResult CanRegister(DiceType diceType, DateTime utcNow)
        {
            if (!IsPromotable(diceType))
                return PromotionRegisterResult.MaxTier;

            int level = levels != null ? levels.GetLevel(diceType) : 1;
            bool same = hasQueue && queuedDice == diceType;
            bool other = hasQueue && queuedDice != diceType;

            return PromotionRules.CanRegister(
                GetTier(diceType), IsBasic(diceType), level, same, other, registeredUtcTicks, utcNow.Ticks);
        }

        /// <summary>
        /// 큐에 올린다. <b>다른 다이스로 교체하면 진행도가 초기화된다</b>(등록 뒤 24시간은 교체할 수 없다).
        /// 승급이 끝난 직후처럼 큐가 비어 있으면 잠금 없이 곧바로 올릴 수 있다.
        /// </summary>
        public PromotionRegisterResult TryRegister(DiceType diceType, DateTime utcNow)
        {
            PromotionRegisterResult result = CanRegister(diceType, utcNow);
            if (result != PromotionRegisterResult.Ok)
                return result;

            hasQueue = true;
            queuedDice = diceType;
            queuedTargetTier = GetTier(diceType) + 1;
            registeredUtcTicks = utcNow.Ticks;
            progress.Clear();

            OnChanged?.Invoke();
            return PromotionRegisterResult.Ok;
        }

        // ── 세기 ───────────────────────────────────────────────────────

        /// <summary>
        /// 행동 하나를 센다. <b>절대 던지지 않는다</b> — 전투·핀볼 한가운데에 끼어 있어서
        /// 여기서 예외가 나면 그 플레이가 실패한 것처럼 보인다. 큐가 비었거나 이 단계에서 제외된
        /// 미션이면 아무것도 하지 않는다.
        /// </summary>
        public void Notify(PromotionMission mission, int amount = 1)
        {
            if (!hasQueue || mission == PromotionMission.EventCurrency)
                return;

            int added = PromotionRules.Add(progress, mission, queuedTargetTier, amount);
            if (added <= 0)
                return;

            if (!TryComplete())
                OnChanged?.Invoke();
        }

        private void OnPointSpent(PointType pointType, int amount)
        {
            if (pointType == PointType.SynergyTicket)
                Notify(PromotionMission.SynergyTickets, amount);
        }

        /// <summary>
        /// 열린 이벤트의 재화를 미션 B 에 넣는다. 돌려주는 값은 <b>실제로 쓴 개수</b>다.
        /// 부른 양·가진 양·남은 요구량 중 가장 작은 만큼만 쓴다.
        /// </summary>
        public int DonateEventCurrency(int offered, DateTime utcNow, bool saveNow = true)
        {
            if (!hasQueue || points == null)
                return 0;

            int need = PromotionRules.Requirement(PromotionMission.EventCurrency, queuedTargetTier);
            if (need <= 0)
                return 0;

            PointType open = EventCurrencyRules.Resolve(EventCurrencyRules.DayIndex(utcNow));
            int amount = PromotionRules.AcceptableEventCurrency(
                GetProgress(PromotionMission.EventCurrency), need, points.Get(open), offered);

            if (amount <= 0 || !points.TrySpend(open, amount, saveNow))
                return 0;

            PromotionRules.Add(progress, PromotionMission.EventCurrency, queuedTargetTier, amount);

            if (!TryComplete())
                OnChanged?.Invoke();

            return amount;
        }

        /// <summary>
        /// 미션 A(보유형). <b>아직 정해진 것이 없다</b> — 종류·수량은 테스트 뒤에 정한다.
        /// 승급 단계와 무관한 것만 걸 것(순환 의존 금지: 승급하려면 다른 다이스의 승급이 필요하면 안 된다).
        /// </summary>
        private static bool AreOwnedRequirementsMet(DiceType diceType, int targetTier)
        {
            return true;
        }

        private bool TryComplete()
        {
            if (!hasQueue
                || !PromotionRules.AllMissionsMet(progress, queuedTargetTier)
                || !AreOwnedRequirementsMet(queuedDice, queuedTargetTier))
            {
                return false;
            }

            DiceType promoted = queuedDice;
            int tier = queuedTargetTier;

            tiers[promoted] = tier;
            hasQueue = false;
            queuedTargetTier = 0;
            registeredUtcTicks = 0;
            progress.Clear();

            OnPromoted?.Invoke(promoted, tier);
            OnChanged?.Invoke();
            return true;
        }

        // ── 이벤트 재화 환급 ─────────────────────────────────────────────

        /// <summary>
        /// 끝난 이벤트에 남은 재화를 핀볼로 돌려준다. <b>3개 = 1발, 소수점은 버린다.</b>
        /// 열려 있는 이벤트의 재화는 건드리지 않는다. 돌려준 발수를 돌려준다.
        ///
        /// 앱 시작과 <b>플레이 중 이벤트가 바뀌는 순간</b>(<see cref="Tick(DateTime)"/>)에 돈다.
        /// </summary>
        public int RefundClosedEventCurrency(DateTime utcNow, bool saveNow = true)
        {
            if (points == null)
                return 0;

            PointType open = EventCurrencyRules.Resolve(EventCurrencyRules.DayIndex(utcNow));
            PointType[] all = { PointType.EventCurrency1, PointType.EventCurrency2, PointType.EventCurrency3 };

            int shots = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == open)
                    continue;

                int held = points.Get(all[i]);
                if (held <= 0)
                    continue;

                // 1~2개 남은 것도 비운다 — 닫힌 이벤트의 재화는 어디에도 못 쓴다.
                if (points.TrySpend(all[i], held, false))
                    shots += PromotionRules.RefundShots(held);
            }

            if (shots > 0)
            {
                int ticketCost = PinballManager.Instance != null ? PinballManager.Instance.TicketCost : 1;
                points.Add(PointType.PinballTicket, shots * ticketCost, saveNow);
            }

            return shots;
        }

        /// <summary>앱 시작 뒤(로드 다음)에 부른다.</summary>
        public void NotifyAppStart()
        {
            Tick(DateTime.UtcNow);
        }

        /// <summary>환급을 했을 때(돌려준 발수). 알림 화면이 구독한다.</summary>
        public event Action<int> OnEventCurrencyRefunded;

        private int lastCheckedDay = int.MinValue;

        /// <summary>
        /// 날짜가 바뀌었으면 환급을 돌린다. <b>플레이 중에 이벤트가 바뀌는 순간을 잡는 자리다</b> —
        /// 이벤트는 날(UTC 자정) 단위로 도니 날짜가 바뀐 때만 보면 충분하고, 같은 날의 호출은 정수 비교로 끝난다.
        /// </summary>
        public void Tick(DateTime utcNow, bool saveNow = true)
        {
            int day = EventCurrencyRules.DayIndex(utcNow);
            if (day == lastCheckedDay)
                return;

            lastCheckedDay = day;

            int shots = RefundClosedEventCurrency(utcNow, saveNow);
            if (shots > 0)
                OnEventCurrencyRefunded?.Invoke(shots);
        }

        // ── 저장 ───────────────────────────────────────────────────────

        public void WriteTo(SaveState saveState)
        {
            PromotionSave target = saveState.Promotion;

            target.Tiers.Clear();
            foreach (KeyValuePair<DiceType, int> pair in tiers)
            {
                if (pair.Value > 0)
                    target.Tiers[pair.Key.ToString()] = pair.Value;
            }

            target.QueuedDice = hasQueue ? queuedDice.ToString() : string.Empty;
            target.QueuedTargetTier = hasQueue ? queuedTargetTier : 0;
            target.RegisteredUtcTicks = hasQueue ? registeredUtcTicks : 0;

            target.Progress.Clear();
            if (hasQueue)
            {
                foreach (KeyValuePair<string, int> pair in progress)
                    target.Progress[pair.Key] = pair.Value;
            }
        }

        public void ReadFrom(SaveState saveState)
        {
            PromotionSave source = saveState.Promotion;

            tiers.Clear();
            foreach (KeyValuePair<string, int> pair in source.Tiers)
            {
                // 모르는 다이스 이름·범위 밖 단계는 손상이다 — 그대로 들이면 다음 저장에서 굳는다.
                if (!Enum.TryParse(pair.Key, out DiceType diceType) || !IsPromotable(diceType))
                    continue;

                int max = PromotionRules.MaxTier(IsBasic(diceType));
                int tier = pair.Value < 0 ? 0 : (pair.Value > max ? max : pair.Value);
                if (tier > 0)
                    tiers[diceType] = tier;
            }

            hasQueue = false;
            queuedTargetTier = 0;
            registeredUtcTicks = 0;
            progress.Clear();

            if (string.IsNullOrEmpty(source.QueuedDice)
                || !Enum.TryParse(source.QueuedDice, out DiceType queued)
                || !IsPromotable(queued))
            {
                return;
            }

            // 목표 단계는 저장값을 믿지 않고 다시 구한다 — 이미 오른 단계 위로만 갈 수 있다.
            int target = GetTier(queued) + 1;
            if (target > PromotionRules.MaxTier(IsBasic(queued)))
                return;

            hasQueue = true;
            queuedDice = queued;
            queuedTargetTier = target;
            registeredUtcTicks = source.RegisteredUtcTicks;

            // 저장된 목표 단계가 지금 계산과 다르면 진행도를 이어 붙이지 않는다(다른 단계의 요구량이다).
            if (source.QueuedTargetTier != target)
                return;

            for (int i = 0; i < PromotionRules.MissionCount; i++)
            {
                var mission = (PromotionMission)i;
                string key = PromotionRules.Key(mission);
                if (!source.Progress.TryGetValue(key, out int value))
                    continue;

                int clamped = PromotionRules.Clamp(value, PromotionRules.Requirement(mission, target));
                if (clamped > 0)
                    progress[key] = clamped;
            }
        }
    }
}
