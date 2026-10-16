using System;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;

namespace OJ.Economy
{
    /// <summary>
    /// 행운 주사위 — SP 를 내고 굴려 <b>마석 1~10개</b>를 얻는다(<see cref="LuckyDiceRules"/>).
    /// <b>MonoBehaviour 가 아니다</b>. 상태가 없다 — 굴림은 한 번에 끝나고 쿨타임도 횟수 제한도 없다
    /// (마석이 급할 때 SP 를 내는 출구다).
    /// </summary>
    [Preserve]
    public sealed class LuckyDiceManager
    {
        private readonly IBattleRefs battle;

        /// <summary>굴렸다. 인자는 얻은 마석 개수. 버튼이 굴림 연출의 결과로 쓴다.</summary>
        public event Action<int> OnRolled;

        public LuckyDiceManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        /// <summary>지금 굴릴 수 있는가. 실시간 판의 준비·웨이브 중이고 SP 가 되어야 한다. 튜토리얼·탑에는 없다.</summary>
        public bool CanRoll
        {
            get
            {
                if (battle == null || battle.Game == null || battle.BattlePoints == null)
                    return false;

                if (battle.Tutorial.IsActive || battle.Tower.IsActive || !battle.Game.CanManageBoard)
                    return false;

                return battle.BattlePoints.Get(BattlePointType.SummonPoint) >= LuckyDiceRules.CostSp;
            }
        }

        /// <summary>
        /// 굴린다. 성공하면 얻은 마석 개수(1~10)를, 못 굴렸으면 0 을 돌려준다.
        /// <b>검증 → 차감 → 지급</b> 순서다 — 내고 나서 못 받는 경로는 없다.
        /// </summary>
        public int TryRoll()
        {
            if (!CanRoll)
                return 0;

            if (!battle.BattlePoints.TrySpend(BattlePointType.SummonPoint, LuckyDiceRules.CostSp))
                return 0;

            int stones = LuckyDiceRules.Roll(UnityEngine.Random.Range(0, 100));
            battle.BattlePoints.Add(BattlePointType.EnhanceStone, stones);

            OnRolled?.Invoke(stones);
            return stones;
        }
    }
}
