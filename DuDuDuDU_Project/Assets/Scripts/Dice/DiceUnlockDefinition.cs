using System;
using UnityEngine;

namespace OJ.Dice
{
    /// <summary>
    /// 다이스 하나를 여는 방법 전부. 기본 5종을 뺀 10종이 각각 한 줄씩 갖는다.
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
    /// </summary>
    [Serializable]
    public sealed class DiceUnlockDefinition
    {
        [Tooltip("여는 대상. 기본 5종은 처음부터 보유하므로 여기 넣지 않는다.")]
        public DiceType diceType = DiceType.Tornado;

        [Tooltip("재화로 여는 값. 재화 종류는 PointManager.ToScrollType 이 정한다.")]
        [Min(1)] public int price = 100;

        [Tooltip("별의 시련 누적 별. 0 이면 이 경로로는 열리지 않는다.")]
        [Min(0)] public int starRequirement;

        [Tooltip("이 스테이지를 퍼펙트로 깨면 열린다. 0 이면 이 경로로는 열리지 않는다.")]
        [Min(0)] public int stageRequirement;

        [Tooltip("이 층을 클리어하면 열린다. 0 이면 이 경로로는 열리지 않는다.")]
        [Min(0)] public int towerFloor;

        /// <summary>컨텐츠 보상으로 나오는 자리가 하나라도 있는가. 언락 팝업이 목록을 그릴 때 쓴다.</summary>
        public bool HasContentSource => starRequirement > 0 || stageRequirement > 0 || towerFloor > 0;
    }
}
