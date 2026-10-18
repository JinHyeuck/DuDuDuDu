using System;
using UnityEngine;

namespace OJ.Dice
{
    /// <summary>
    /// 다이스 하나를 여는 방법 전부. <b>킹 5종만</b> 각각 한 줄씩 갖는다 —
    /// 기본 5종과 특수(레어) 5종은 처음부터 보유하므로 여는 방법이 없다
    /// (<c>DiceEvolution.IsOwnedFromStart</c>).
    ///
    /// <b>왜 가격과 컨텐츠 보상처가 한 줄에 있나.</b> 기획의 "이미 보유한 다이스를 컨텐츠가
    /// 주면 그 다이스의 가격만큼 재화로 환급한다"가 이 모양을 강제한다 — 컨텐츠로 나오는
    /// 다이스는 <b>반드시</b> 가격을 갖고 있어야 환급액을 알 수 있다. 목록을 둘로 나누면
    /// "사다리엔 있는데 가격표엔 없는 다이스" 를 만들 수 있게 되고, 그때 환급할 금액이 없다.
    ///
    /// <b>재화 <i>종류</i>는 여기 적지 않는다.</b> <c>PointManager.ToScrollType</c> 이
    /// 킹→<c>MythicScroll</c>, 특수→<c>SpecialDiceCore</c> 를 이미 완전히 매핑한다.
    /// 여기 또 적으면 "강화에 쓰는 재화" 와 "언락에 쓰는 재화" 가 갈라진다.
    ///
    /// <b>컨텐츠 셋을 <c>enum + int</c> 한 쌍으로 접지 않은 이유.</b> 그러면 한 다이스가
    /// 두 컨텐츠에서 나오는 구성을 아예 못 쓰고, 인스펙터에서 "이 다이스는 어디서 나오나"를
    /// 한눈에 볼 수 없다. 0 은 "그 경로 없음" 이다.
    ///
    /// <b>튜토리얼 보상 칸이 없어졌다(2026-10-14).</b> 그 칸은 타임 다이스 하나를 위한
    /// 것이었는데, 특수 5종이 처음부터 보유로 바뀌면서 줄 것이 사라졌다. 튜토리얼은
    /// 여전히 진화를 가르치지만 <b>다이스를 지급하지는 않는다</b>.
    /// </summary>
    [Serializable]
    public sealed class DiceUnlockDefinition
    {
        [Tooltip("여는 대상. 기본·특수 10종은 처음부터 보유하므로 여기 넣지 않는다.")]
        public DiceType diceType = DiceType.KingFire;

        [Tooltip("재화로 여는 값. 재화 종류는 PointManager.ToScrollType 이 정한다.")]
        [Min(1)] public int price = 100;

        [Tooltip("별의 시련 누적 별. 0 이면 이 경로로는 열리지 않는다.")]
        [Min(0)] public int starRequirement;

        [Tooltip("이 스테이지의 마지막 마일스톤(전 웨이브 클리어)을 수령하면 열린다. 0 이면 이 경로로는 열리지 않는다.")]
        [Min(0)] public int stageRequirement;

        [Tooltip("이 층을 클리어하면 열린다. 0 이면 이 경로로는 열리지 않는다.")]
        [Min(0)] public int towerFloor;

        /// <summary>컨텐츠 보상으로 나오는 자리가 하나라도 있는가. 언락 팝업이 목록을 그릴 때 쓴다.</summary>
        public bool HasContentSource => HasRevisitableSource;

        /// <summary>
        /// <b>다시 찾아갈 수 있는</b> 획득처가 있는가. "획득 미션" 의 진행 버튼
        /// (<c>DiceUnlockShortcut</c>)이 이것을 본다 — 갈 곳이 없는데 버튼을 띄우면
        /// 눌러도 아무 일이 없다.
        ///
        /// 지금은 세 경로가 전부 다시 찾아갈 수 있어 <see cref="HasContentSource"/> 와
        /// 같은 값이다. 두 이름을 남겨 둔 이유는 한 번만 지나가는 경로(튜토리얼 같은 것)가
        /// 다시 생겼을 때 <b>어느 쪽을 봐야 하는지</b>가 호출부에 이미 적혀 있게 하려는 것이다.
        /// </summary>
        public bool HasRevisitableSource => starRequirement > 0 || stageRequirement > 0 || towerFloor > 0;
    }
}
