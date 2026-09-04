using System;
using UnityEngine;
using OJ.Hunting;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금 보상으로 줄 수 있는 재화. <b>등급마다 하나만</b> 준다.
    ///
    /// 둘 다 주면 "무엇을 잡을까" 가 "센 걸 잡을수록 이득" 으로 납작해진다.
    /// 하나만 주면 SP 가 급한 판과 강화석이 급한 판의 답이 달라진다 — 그것이 이 시스템이
    /// 만들려는 유일한 선택이다.
    /// </summary>
    public enum BountyRewardKind
    {
        [InspectorName("SP (소환 포인트)")]
        SummonPoint = 0,

        [InspectorName("전투 강화석")]
        EnhanceStone = 1,
    }

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
        public BountyRewardKind rewardKind = BountyRewardKind.SummonPoint;

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
    }
}
