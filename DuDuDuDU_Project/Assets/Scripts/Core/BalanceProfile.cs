namespace OJ.Core
{
    /// <summary>
    /// 지금 판이 "새 밸런스"(2026-10-25 시험 모드)로 도는가. 엔진을 쓰지 않는다.
    ///
    /// <b>1스테이지에만 적용해 맛을 본다</b>(<c>StageData.newBalance</c>). 켜지면 한꺼번에 바뀌는 것:
    /// 소환 클릭 수가 초반 2회 → 후반 4회가 되도록 SP 곡선을 다시 짜고(<see cref="WaveSpCurve"/>), 성급 데미지가 3배씩 오르고
    /// (<see cref="DamageFormula.StarMultiplier"/>), 레어·킹 다이스가 더 세지고(<see cref="TierMultiplier"/>),
    /// 자리 값과 확률업 값이 그 경제에 맞게 내려가고, 자리를 사면 무료 소환권이 한 장 나온다.
    /// 목표는 "많이 뽑아 1성으로 채우기" 가 아니라 "적게 뽑아 잘 키운 다이스로 칭찬받기" 다.
    ///
    /// <b>정적 스위치인 이유.</b> 세 규칙(SP 곡선·성급·비용)이 전부 값만 받는 순수 함수라, 스테이지 데이터를 인자로 끌고 다니면
    /// 호출부 수십 곳이 바뀐다. 판이 시작될 때 <c>GameManager</c> 가 한 번 세우고(튜토리얼·탑·다른 스테이지는 끈다) 로비는 항상 꺼진 채다.
    /// 꺼져 있으면 모든 규칙이 예전 값 그대로라 골든 기준선이 한 자리도 안 변한다.
    /// </summary>
    public static class BalanceProfile
    {
        /// <summary>새 밸런스가 켜졌는가. <c>GameManager.InitializeStage</c> 가 판마다 정한다.</summary>
        public static bool Active { get; set; }

        /// <summary>레어 다이스 데미지 배수(켜졌을 때).</summary>
        public const float RareMultiplier = 3f;

        /// <summary>킹 다이스 데미지 배수(켜졌을 때). 킹의 기존 <see cref="DamageFormula.KingDiceDamageMultiplier"/> 에 더해 곱해진다.</summary>
        public const float KingMultiplier = 2.55f;

        /// <summary>
        /// 다이스 쿨타임 배수(켜졌을 때). 웨이브당 소환이 2회뿐인 경제에서 1웨이브에 다이스 둘로도 버겁지 않게 쏘는 속도를 올린다
        /// (2026-10-27, 사용자 지시). 쿨타임이 짧을수록 높은 성급(×1.2씩 느려진다)·레어도 같이 빨라진다.
        /// </summary>
        public const float CooldownMultiplier = 0.6f;

        /// <summary>새 밸런스에서 소환 비용이 두 번 소환마다 오르는 양.</summary>
        public const int SummonCostStepOn = 2;

        /// <summary>비용이 이 값(시작 비용) 이하인 동안만 소환 <c>basePerIncrease</c> 번마다 오른다. 그 뒤로는 매 소환마다 오른다.</summary>
        public const int SummonCostStartPhase = 10;

        /// <summary>
        /// 다음 비용 상승까지 필요한 소환 횟수. 꺼져 있으면 <paramref name="basePerIncrease"/> 그대로.
        /// 켜지면 처음 두 번(비용 10)만 같은 값이고, 그 뒤로는 소환할 때마다 오른다 — 10 · 10 · 12 · 14 · 16 …
        /// </summary>
        public static int SummonsPerCostIncrease(int currentCost, int basePerIncrease)
        {
            int baseCount = basePerIncrease < 1 ? 1 : basePerIncrease;
            return Active && currentCost > SummonCostStartPhase ? 1 : baseCount;
        }

        /// <summary>
        /// 소환 비용이 오를 때 더하는 값(2026-10-29, 사용자 지시 "처음 10 10 만 유지, 이후 1번마다 +2").
        /// 꺼져 있으면 예전처럼 항상 1. 켜지면 늘 <see cref="SummonCostStepOn"/> 이다 — 10 · 10 · 12 · 14 · 16 …
        /// SP 수급 곡선(<see cref="WaveSpCurve"/>)은 건드리지 않았다 — 후반에 소환 횟수가 줄고 한 번이 무거워진다.
        /// </summary>
        public static int SummonCostStep(int currentCost)
        {
            return Active ? SummonCostStepOn : 1;
        }

        /// <summary>다이스 등급(0 일반 · 1 레어 · 2 킹)별 배수. 꺼져 있으면 1.</summary>
        public static float TierMultiplier(int tier)
        {
            if (!Active)
                return 1f;

            return tier == 1 ? RareMultiplier : tier == 2 ? KingMultiplier : 1f;
        }
    }
}
