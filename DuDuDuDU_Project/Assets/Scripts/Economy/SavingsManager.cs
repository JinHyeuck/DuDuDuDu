using System;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;
using OJ.Stage;

namespace OJ.Economy
{
    /// <summary>저금통의 상태. Ready 는 고를 수 있고, Saving 은 다음 선택(또는 지급)을 기다리는 중이다.</summary>
    public enum SavingsState
    {
        Ready = 0,
        Saving = 1,
    }

    /// <summary>
    /// 저금통 방식. <b>판이 시작될 때 한 번 정해지고 그 판 동안 바뀌지 않는다</b> — 도중에 바뀌면 타이머·대기 금액의 뜻이 갈린다.
    /// </summary>
    public enum SavingsMode
    {
        /// <summary>기본. "지금 바로 N SP" / "45초 뒤 M SP" 중 하나를 고른다. 비용이 없다.</summary>
        Choice = 0,

        /// <summary>이전 방식. SP 를 맡기는 선택지 셋(30→45 · 40→65 · 50→90) 중 하나를 고른다.</summary>
        Tiered = 1,
    }

    /// <summary>
    /// 저금통. <b>MonoBehaviour 가 아니다</b>(<c>BountyManager</c> 와 같은 이유 — 씬에 놓을 이유가 없다).
    ///
    /// <b>상태를 자기 필드에 들지 않는다.</b> 떠 있는 오퍼·대기 금액·남은 시간은 <c>RunState</c> 가 소유하고 되돌리기 스냅샷이
    /// 같이 되돌린다. 여기 따로 들면 <c>BeginRun</c> 이 리셋하지 못하는 사본이 생긴다. 들고 있는 것은 방식(<see cref="Mode"/>) 뿐이다.
    ///
    /// 규칙(금액·비율·시간)은 <see cref="SavingsRules"/> 한 곳이다. 이 클래스는 그것을 SP 에 적용하는 일만 한다.
    /// </summary>
    [Preserve]
    public sealed class SavingsManager
    {
        private readonly IBattleRefs battle;

        /// <summary>이번 판의 방식. DEV 치트(<c>DevCheatFlags.SavingsTiered</c>)가 켜져 있으면 3단, 아니면(릴리스 포함) 선택이다.</summary>
        public SavingsMode Mode { get; }

        /// <summary>선택·맡김·지급으로 상태가 바뀌었을 때 운다. 버튼이 듣는다.</summary>
        public event Action OnChanged;

        /// <summary>SP 를 받았다. 인자는 받은 총액(SP). 버튼이 돈이 소환 버튼으로 빨려 드는 연출을 낸다.</summary>
        public event Action<int> OnPaid;

        public SavingsManager(IBattleRefs battle)
        {
            this.battle = battle;

#if UNITY_EDITOR || DEV_DEFINE
            Mode = OJ.Dev.DevCheatFlags.SavingsTiered ? SavingsMode.Tiered : SavingsMode.Choice;
#else
            Mode = SavingsMode.Choice;
#endif
        }

        private RunState Run => battle.Game.Run;

        public SavingsState State => battle != null && battle.Game != null && Run.SavingsTimer > 0f
            ? SavingsState.Saving
            : SavingsState.Ready;

        /// <summary>다음 선택(또는 지급)까지 남은 시간(초).</summary>
        public float TimeRemaining => battle != null && battle.Game != null ? Run.SavingsTimer : 0f;

        /// <summary>지금 받기로 해 둔 총액(SP). 받을 것이 없으면 0. 두 방식 모두 "곧 들어올 SP" 를 뜻한다.</summary>
        public int PendingPayout
        {
            get
            {
                if (State != SavingsState.Saving)
                    return 0;

                return Mode == SavingsMode.Tiered ? SavingsRules.Payout(Run.SavingsOption) : Run.SavingsPending;
            }
        }

        /// <summary>지금 맡겨 둔 선택지의 전체 시간(초, 3단 방식). 맡긴 것이 없으면 0.</summary>
        public float PendingDuration => State == SavingsState.Saving && Mode == SavingsMode.Tiered
            ? SavingsRules.Duration(Run.SavingsOption)
            : 0f;

        /// <summary>
        /// 창을 열 수 있는가. 실시간 판의 준비·웨이브 중이고 지금 대기 중이 아니어야 한다. 탑에는 저금통이 없다.
        /// <b>SP 는 보지 않는다</b> — 3단 방식에서 모자라도 창은 열려 "얼마가 필요한지" 를 보여 준다(<see cref="CanDeposit"/> 이 따로 본다).
        /// </summary>
        public bool CanOpen
        {
            get
            {
                if (battle == null || battle.Game == null || battle.BattlePoints == null)
                    return false;

                if (battle.Tower.IsActive || !battle.Game.CanManageBoard)
                    return false;

                return State == SavingsState.Ready;
            }
        }

        /// <summary>
        /// 버튼이 "눌러 볼 만한가" 를 그리는 기준. 선택 방식은 비용이 없어 열 수 있으면 된다.
        /// 3단 방식은 가장 싼 선택지를 낼 SP 도 있어야 한다.
        /// </summary>
        public bool CanStart => Mode == SavingsMode.Tiered ? CanDepositAny : CanOpen;

        // ── 선택 방식 ─────────────────────────────────────────────

        /// <summary>떠 있는 "지금 바로" 값(SP). <see cref="EnsureOffer"/> 뒤에 읽는다.</summary>
        public int OfferInstant => battle != null && battle.Game != null ? Run.SavingsInstant : 0;

        /// <summary>떠 있는 "45초 뒤" 값(SP).</summary>
        public int OfferDelayed => battle != null && battle.Game != null ? Run.SavingsDelayed : 0;

        /// <summary>
        /// 오퍼가 없으면 굴려서 저장한다. <b>이미 있으면 다시 굴리지 않는다</b> — 창을 닫았다 열어 좋은 값을 노리는 일을 막는다.
        /// 값은 고를 때까지 <c>RunState</c> 에 고정되고, 고르면 비워진다(다음 Ready 에서 다시 굴린다).
        /// </summary>
        public void EnsureOffer()
        {
            if (Mode != SavingsMode.Choice || !CanOpen || Run.SavingsInstant > 0)
                return;

            StageData stage = battle.Game.CurrentStageData;
            int total = stage != null
                ? SavingsRules.StageSpTotal(stage.initialSP, stage.waveClearSP, stage.totalWaves)
                : 0;

            SavingsRules.RollOffer(total, UnityEngine.Random.Range(0, 1000), UnityEngine.Random.Range(0, 1000),
                out int instant, out int delayed);

            Run.SavingsInstant = instant;
            Run.SavingsDelayed = delayed;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 둘 중 하나를 고른다. 즉시면 SP 가 그 자리에서 들어오고, 나중이면 45초 뒤에 들어온다.
        /// 어느 쪽이든 <b>45초 뒤에 다시 고른다</b>. 오퍼가 없거나 고를 수 없으면 거절한다.
        /// </summary>
        public bool TryChoose(bool instant)
        {
            if (Mode != SavingsMode.Choice || !CanOpen || Run.SavingsInstant <= 0)
                return false;

            int amount = instant ? Run.SavingsInstant : Run.SavingsDelayed;

            Run.SavingsInstant = 0;
            Run.SavingsDelayed = 0;
            Run.SavingsPending = instant ? 0 : amount;
            Run.SavingsTimer = SavingsRules.ChoiceCooldownSeconds;

            if (instant)
                battle.Summon.AddSP(amount);

            OnChanged?.Invoke();

            if (instant)
                OnPaid?.Invoke(amount);

            return true;
        }

        // ── 3단 방식 ──────────────────────────────────────────────

        /// <summary>이 선택지를 지금 맡길 수 있는가(창을 열 수 있고 SP 가 되어야 한다).</summary>
        public bool CanDeposit(int option)
        {
            return Mode == SavingsMode.Tiered && CanOpen
                && battle.BattlePoints.Get(BattlePointType.SummonPoint) >= SavingsRules.Deposit(option);
        }

        /// <summary>가장 싼 선택지라도 맡길 수 있는가.</summary>
        public bool CanDepositAny => Mode == SavingsMode.Tiered && CanOpen
            && battle.BattlePoints.Get(BattlePointType.SummonPoint) >= SavingsRules.CheapestDeposit;

        /// <summary>
        /// 선택지를 골라 SP 를 맡긴다. <b>재화는 검증한 뒤에만 낸다</b> — <c>TrySpend</c> 가 모자라면 아무것도 깎지 않는다.
        /// </summary>
        public bool TryDeposit(int option)
        {
            if (!CanDeposit(option))
                return false;

            option = SavingsRules.ClampOption(option);
            if (!battle.BattlePoints.TrySpend(BattlePointType.SummonPoint, SavingsRules.Deposit(option)))
                return false;

            Run.SavingsOption = option;
            Run.SavingsDeposit = SavingsRules.Deposit(option);
            Run.SavingsTimer = SavingsRules.Duration(option);

            OnChanged?.Invoke();
            return true;
        }

        // ── 시간 ──────────────────────────────────────────────────

        /// <summary>
        /// 시간을 흘린다. <c>GameManager.Update</c> 가 웨이브 중에만 부른다 — 일시정지에서는 멈추고 배속에서는
        /// 빨리 흐른다(게임 시간). 0 이 되는 순간 받기로 한 금액이 있으면 지급한다(선택 방식에서 즉시를 골랐다면 없다).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (battle == null || battle.Game == null)
                return;

            float before = Run.SavingsTimer;
            if (before <= 0f || deltaTime <= 0f)
                return;

            float after = before - deltaTime;
            if (after > 0f)
            {
                Run.SavingsTimer = after;
                return;
            }

            int payout = Mode == SavingsMode.Tiered ? SavingsRules.Payout(Run.SavingsOption) : Run.SavingsPending;

            Run.SavingsTimer = 0f;
            Run.SavingsDeposit = 0;
            Run.SavingsPending = 0;

            // 선택 방식에서 "지금 바로" 를 골랐다면 받을 것이 없다 — 코인 연출을 빈손으로 내지 않는다.
            if (payout > 0)
                battle.Summon.AddSP(payout);

            OnChanged?.Invoke();

            if (payout > 0)
                OnPaid?.Invoke(payout);
        }
    }
}
