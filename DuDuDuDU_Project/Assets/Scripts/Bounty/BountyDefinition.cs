using System;
using UnityEngine;
using OJ.Battle;
using OJ.Core;
using OJ.Utils;
using OJ.Hunting;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금 몬스터 한 등급의 정의.
    ///
    /// <b>체력을 숫자로 적지 않는다.</b> <c>referenceWaveRatio</c> 와 <c>hpMultiplier</c>
    /// 두 값만 두고 실제 체력은 그 스테이지의 웨이브 체력에서 계산한다
    /// (<see cref="OJ.Core.BountyFormula"/>). 스테이지가 30개인데 등급이 5개라
    /// 표로 적으면 150칸을 손으로 관리하게 된다.
    /// </summary>
    [Serializable]
    public sealed class BountyDefinition
    {
        [Tooltip("1..5. 0 은 '소환 X' 라 정의가 없다.")]
        [Min(1)] public int grade = 1;

        public string displayName = "현상금";

        [Header("체력")]
        [Tooltip("체력 기준 웨이브를 스테이지 길이의 몇 % 지점으로 잡을지. 0~1.")]
        [Range(0f, 1f)] public float referenceWaveRatio = 0.1f;

        [Tooltip("그 웨이브 일반 몬스터 체력의 몇 배인가.")]
        [Min(0.1f)] public float hpMultiplier = 5f;

        [Header("보상")]
        /// <summary>
        /// 줄 재화. <b>등급마다 하나만</b> 준다.
        ///
        /// 둘 다 주면 "무엇을 잡을까" 가 "센 걸 잡을수록 이득" 으로 납작해진다.
        /// 하나만 주면 SP 가 급한 판과 강화석이 급한 판의 답이 달라진다 — 그것이 이 시스템이
        /// 만들려는 유일한 선택이다.
        ///
        /// 예전에는 이 파일의 <c>BountyRewardKind</c> 였다. 전투 재화를 데이터로 지급해야 하는
        /// 곳이 현상금 말고도 생기면서 <see cref="BattlePointType"/> 으로 올렸다.
        /// <b>값(0·1)이 그대로라 <c>BountyDatabase.asset</c> 은 손댈 것이 없다.</b>
        /// </summary>
        public BattlePointType rewardKind = BattlePointType.SummonPoint;

        [Min(1)] public int rewardAmount = 60;

        [Header("연출")]
        [Tooltip("일반 몬스터 이동속도의 배수. 느리게 내려와야 때릴 시간이 생긴다.")]
        [Min(0.05f)] public float moveSpeedMultiplier = 0.3f;

        [Min(0.1f)] public float scaleMultiplier = 1.3f;

        [Tooltip("등급 색. 선택 창 카드와 배너가 같이 쓴다.")]
        public Color tint = Color.white;

        [Tooltip("선택 창에 띄울 아이콘. 비면 카드가 색 사각형으로 대신한다.")]
        public Sprite icon;

        [Tooltip("전용 프리팹. 비우면 그 스테이지 테마의 보스 프리팹을 크기·색만 바꿔 쓴다.")]
        public Monster prefabOverride;
        /// <summary>
        /// 화면에 적을 보상 한 줄. <b>UI 세 곳(배너·슬롯·콜아웃)이 같은 것을 쓴다.</b>
        ///
        /// 예전에는 셋이 각자 <c>FormatReward</c> 를 들고 <c>"SP +"</c> · <c>"강화석 +"</c> 를
        /// 글자까지 똑같이 하드코딩했다. 재화 이름을 고치려면 세 파일을 고쳐야 했고,
        /// 하나를 빠뜨려도 그 화면을 띄우기 전에는 드러나지 않았다.
        /// </summary>
        public string FormatReward()
        {
            return BattlePointUtility.GetName(rewardKind) + " +" + ShortNumberFormat.Format(rewardAmount);
        }

    }
}
