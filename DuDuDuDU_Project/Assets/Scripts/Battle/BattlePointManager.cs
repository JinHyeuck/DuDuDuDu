using System;
using UnityEngine;
using OJ.Core;

namespace OJ.Battle
{
    /// <summary>
    /// 전투 재화 창구. 값은 <see cref="RunState"/> 가 들고, 이 클래스는 <b>읽고 쓰는 길과
    /// 바뀌었다는 신호</b>만 제공한다.
    ///
    /// <b>왜 값을 직접 안 들고 있는가.</b> <see cref="RunState"/> 에 두면 <c>BeginRun</c> 이
    /// 리셋을 한 곳에서 맡고 <c>WaveSnapshot</c> 이 되돌리기를 공짜로 덮는다. 게다가
    /// <c>RunStateSnapshotTests</c> 가 리플렉션으로 전 필드를 훑어, 새 재화를 넣고 그 셋 중
    /// 하나라도 빠뜨리면 <b>필드 이름을 대고 실패한다.</b> 여기에 필드를 따로 두면 그 그물
    /// 바깥으로 나간다 — 실제로 강화석이 그래서 <c>WaveRewindManager</c> 에 자기만을 위한
    /// 복원 코드를 따로 갖고 있었다.
    ///
    /// <b>그럼 왜 이 클래스가 필요한가.</b> UI 세 곳이 "재화가 바뀌었다"를 구독해야 하는데
    /// <see cref="RunState"/> 에는 이벤트를 <b>넣을 수 없다</b> — <c>RunStateSnapshotTests</c>
    /// 의 채우기 루프가 <c>int</c>·<c>bool</c> 이 아닌 인스턴스 필드를 만나면 실패한다.
    /// 그 제약은 스냅샷이 "모든 필드"를 담는다는 보장을 지키는 것이라 풀 이유가 없다.
    /// 그래서 <see cref="RunState"/> 는 멍청하게 두고 이벤트는 이쪽이 갖는다.
    ///
    /// <b>저장하지 않는다.</b> 판이 끝나면 사라지는 값이라 세이브 경로가 아예 없다 —
    /// <c>PointManager</c> 와 갈라지는 지점이 그것이다.
    /// </summary>
    public sealed class BattlePointManager
    {
        private readonly RunState run;

        /// <summary>재화가 바뀌었다. 인자는 (종류, 바뀐 뒤의 보유량).</summary>
        public event Action<BattlePointType, int> OnBattlePointChanged;

        public BattlePointManager(RunState run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public int Get(BattlePointType battlePointType)
        {
            switch (battlePointType)
            {
                case BattlePointType.SummonPoint: return run.SummonPoint;
                case BattlePointType.EnhanceStone: return run.EnhanceStone;
                default: return 0;
            }
        }

        /// <summary>
        /// 보유량을 정한다. <c>PointManager.Set</c> 과 같은 규칙으로 <b>음수를 0 으로 깎는다</b> —
        /// "보유량은 음수가 아니다" 가 나머지 코드의 전제이고, 그 전제가 깨지면 이후에 얻은
        /// 재화가 빚을 메우는 데 먼저 쓰이고 조용히 사라진다.
        /// </summary>
        public void Set(BattlePointType battlePointType, int value)
        {
            if (battlePointType == BattlePointType.Max)
                return;

            int clamped = Mathf.Max(0, value);

            switch (battlePointType)
            {
                case BattlePointType.SummonPoint:
                    run.SummonPoint = clamped;
                    break;
                case BattlePointType.EnhanceStone:
                    run.EnhanceStone = clamped;
                    break;
                default:
                    return;
            }

            OnBattlePointChanged?.Invoke(battlePointType, clamped);
        }

        public void Add(BattlePointType battlePointType, int amount)
        {
            if (amount <= 0)
                return;

            Set(battlePointType, Get(battlePointType) + amount);
        }

        public bool TrySpend(BattlePointType battlePointType, int amount)
        {
            if (amount < 0)
                return false;

            if (Get(battlePointType) < amount)
                return false;

            Set(battlePointType, Get(battlePointType) - amount);
            return true;
        }

        /// <summary>
        /// <c>BeginRun</c> 이나 되돌리기가 <see cref="RunState"/> 를 <b>직접</b> 고친 뒤에 부른다.
        /// 이 창구를 거치지 않은 변경은 이벤트를 내지 않아서, 안 부르면 열려 있던 UI 가
        /// 옛 숫자를 든 채로 남는다.
        /// </summary>
        public void NotifyAllChanged()
        {
            foreach (BattlePointType battlePointType in Enum.GetValues(typeof(BattlePointType)))
            {
                if (battlePointType == BattlePointType.Max)
                    continue;

                OnBattlePointChanged?.Invoke(battlePointType, Get(battlePointType));
            }
        }
    }
}
