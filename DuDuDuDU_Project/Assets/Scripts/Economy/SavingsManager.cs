using System;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;

namespace OJ.Economy
{
    /// <summary>통장의 상태. Ready 는 맡길 수 있고, Saving 은 돌려받기를 기다리는 중이다.</summary>
    public enum SavingsState
    {
        Ready = 0,
        Saving = 1,
    }

    /// <summary>
    /// 통장 — SP 를 맡기고 <see cref="SavingsRules.DurationSeconds"/> 초 뒤에 <b>원금 + 10%</b> 를 돌려받는다.
    /// <b>MonoBehaviour 가 아니다</b>(<c>BountyManager</c> 와 같은 이유 — 씬에 놓을 이유가 없다).
    ///
    /// <b>상태를 자기 필드에 들지 않는다.</b> 맡긴 금액·남은 시간·횟수는 <c>RunState</c> 가 소유하고 되돌리기 스냅샷이
    /// 같이 되돌린다. 여기 따로 들면 <c>BeginRun</c> 이 리셋하지 못하는 사본이 생긴다.
    ///
    /// 규칙(금액 단계·비율·시간)은 <see cref="SavingsRules"/> 한 곳이다. 이 클래스는 그것을 SP 에 적용하는 일만 한다.
    /// </summary>
    [Preserve]
    public sealed class SavingsManager
    {
        private readonly IBattleRefs battle;

        /// <summary>맡김·지급으로 상태가 바뀌었을 때 운다. 버튼이 듣는다.</summary>
        public event Action OnChanged;

        /// <summary>돌려받았다. 인자는 받은 총액(SP). 버튼이 돈이 소환 버튼으로 빨려 드는 연출을 낸다.</summary>
        public event Action<int> OnPaid;

        public SavingsManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        private RunState Run => battle.Game.Run;

        public SavingsState State => battle != null && battle.Game != null && Run.SavingsTimer > 0f
            ? SavingsState.Saving
            : SavingsState.Ready;

        /// <summary>다음에 맡길 금액(SP). 30 · 40 · 50 · 50 …</summary>
        public int NextDeposit => SavingsRules.DepositFor(battle != null && battle.Game != null ? Run.SavingsStep : 0);

        /// <summary>다음에 맡기면 돌려받을 총액(SP).</summary>
        public int NextPayout => SavingsRules.PayoutFor(NextDeposit);

        /// <summary>지금 맡겨 둔 금액이 돌려줄 총액(SP). 맡긴 것이 없으면 0.</summary>
        public int PendingPayout => battle != null && battle.Game != null ? SavingsRules.PayoutFor(Run.SavingsDeposit) : 0;

        /// <summary>돌려받기까지 남은 시간(초).</summary>
        public float TimeRemaining => battle != null && battle.Game != null ? Run.SavingsTimer : 0f;

        /// <summary>
        /// 지금 맡길 수 있는가. 실시간 판의 준비(카운트다운)·웨이브 중이고, 맡긴 것이 없고, SP 가 되어야 한다.
        /// 튜토리얼·탑에는 통장이 없다.
        /// </summary>
        public bool CanDeposit
        {
            get
            {
                if (battle == null || battle.Game == null || battle.BattlePoints == null)
                    return false;

                if (battle.Tutorial.IsActive || battle.Tower.IsActive || !battle.Game.CanManageBoard)
                    return false;

                if (State != SavingsState.Ready)
                    return false;

                return battle.BattlePoints.Get(BattlePointType.SummonPoint) >= NextDeposit;
            }
        }

        /// <summary>
        /// SP 를 맡긴다. <b>재화는 검증한 뒤에만 낸다</b> — <c>TrySpend</c> 가 모자라면 아무것도 깎지 않는다.
        /// </summary>
        public bool TryDeposit()
        {
            if (!CanDeposit)
                return false;

            int deposit = NextDeposit;
            if (!battle.BattlePoints.TrySpend(BattlePointType.SummonPoint, deposit))
                return false;

            Run.SavingsDeposit = deposit;
            Run.SavingsTimer = SavingsRules.DurationSeconds;

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 시간을 흘린다. <c>GameManager.Update</c> 가 웨이브 중에만 부른다 — 일시정지에서는 멈추고 배속에서는
        /// 빨리 흐른다(게임 시간). 0 이 되는 순간 원금 + 10% 를 지급하고 횟수를 올린다.
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

            int payout = SavingsRules.PayoutFor(Run.SavingsDeposit);

            Run.SavingsTimer = 0f;
            Run.SavingsDeposit = 0;
            Run.SavingsStep++;

            if (payout > 0)
                battle.Summon.AddSP(payout);

            OnChanged?.Invoke();
            OnPaid?.Invoke(payout);
        }
    }
}
