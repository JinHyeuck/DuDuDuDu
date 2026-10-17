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
    /// 통장 — SP 를 맡기고 정한 시간이 지나면 <b>원금보다 더 많이</b> 돌려받는다. 맡길 때 선택지 셋 중 하나를 고른다.
    /// <b>MonoBehaviour 가 아니다</b>(<c>BountyManager</c> 와 같은 이유 — 씬에 놓을 이유가 없다).
    ///
    /// <b>상태를 자기 필드에 들지 않는다.</b> 맡긴 금액·남은 시간·횟수는 <c>RunState</c> 가 소유하고 되돌리기 스냅샷이
    /// 같이 되돌린다. 여기 따로 들면 <c>BeginRun</c> 이 리셋하지 못하는 사본이 생긴다.
    ///
    /// 규칙(선택지의 금액·비율·시간)은 <see cref="SavingsRules"/> 한 곳이다. 이 클래스는 그것을 SP 에 적용하는 일만 한다.
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

        /// <summary>지금 맡겨 둔 금액이 돌려줄 총액(SP). 맡긴 것이 없으면 0.</summary>
        public int PendingPayout => State == SavingsState.Saving ? SavingsRules.Payout(Run.SavingsOption) : 0;

        /// <summary>지금 맡겨 둔 선택지의 전체 시간(초). 맡긴 것이 없으면 0.</summary>
        public float PendingDuration => State == SavingsState.Saving ? SavingsRules.Duration(Run.SavingsOption) : 0f;

        /// <summary>돌려받기까지 남은 시간(초).</summary>
        public float TimeRemaining => battle != null && battle.Game != null ? Run.SavingsTimer : 0f;

        /// <summary>
        /// 통장 창을 열 수 있는가. 실시간 판의 준비·웨이브 중이고 맡긴 것이 없어야 한다. 탑에는 통장이 없다.
        /// <b>SP 는 보지 않는다</b> — 모자라도 창은 열려서 "얼마가 필요한지" 를 보여 준다(<see cref="CanDeposit"/> 이 따로 본다).
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

        /// <summary>이 선택지를 지금 맡길 수 있는가(창을 열 수 있고 SP 가 되어야 한다).</summary>
        public bool CanDeposit(int option)
        {
            return CanOpen && battle.BattlePoints.Get(BattlePointType.SummonPoint) >= SavingsRules.Deposit(option);
        }

        /// <summary>가장 싼 선택지라도 맡길 수 있는가. 버튼이 "눌러 볼 만한가" 를 그리는 기준이다.</summary>
        public bool CanDepositAny => CanOpen
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

        /// <summary>
        /// 시간을 흘린다. <c>GameManager.Update</c> 가 웨이브 중에만 부른다 — 일시정지에서는 멈추고 배속에서는
        /// 빨리 흐른다(게임 시간). 0 이 되는 순간 정한 금액을 지급한다.
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

            int payout = SavingsRules.Payout(Run.SavingsOption);

            Run.SavingsTimer = 0f;
            Run.SavingsDeposit = 0;

            if (payout > 0)
                battle.Summon.AddSP(payout);

            OnChanged?.Invoke();
            OnPaid?.Invoke(payout);
        }
    }
}
