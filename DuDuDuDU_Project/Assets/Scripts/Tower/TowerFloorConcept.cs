using UnityEngine;

namespace OJ.Tower
{
    /// <summary>
    /// 층 구간의 적 콘셉트. 기획서 6.2 의 표 여덟 줄이 그대로 여기 있다.
    ///
    /// <b>왜 콘셉트가 데이터가 아니라 enum 인가.</b> 각 콘셉트는 숫자 배수만이 아니라
    /// <b>동작</b>을 하나씩 켠다 — 회복은 매초 체력을 올리고, 분열은 죽을 때 새 몬스터를
    /// 만들고, 보호막은 피해 판정을 가로챈다. 그 동작은 코드에 있어야 하고, 코드가
    /// 분기해야 하는 값을 문자열이나 자유 입력으로 두면 오타가 <b>런타임 침묵</b>이 된다.
    ///
    /// 배수는 데이터다(<see cref="TowerConceptTraits"/> 기본값 → <c>TowerDatabase</c> 에셋).
    /// 동작만 여기서 갈린다.
    /// </summary>
    public enum TowerFloorConcept
    {
        [InspectorName("대규모 물량")]
        Swarm = 0,

        [InspectorName("초고속 돌진")]
        Rush = 1,

        [InspectorName("고방어 부대")]
        Armored = 2,

        [InspectorName("고체력 정예")]
        Elite = 3,

        [InspectorName("혼합 부대")]
        Mixed = 4,

        [InspectorName("회복형 적")]
        Regenerating = 5,

        [InspectorName("분열형 적")]
        Splitting = 6,

        [InspectorName("보호막 적")]
        Shielded = 7,
    }

    /// <summary>
    /// 콘셉트 하나가 층의 기준값에 먹이는 배수와, 켜는 동작.
    ///
    /// <b>여러 콘셉트가 겹치면 배수는 곱해지고 동작은 합쳐진다</b>
    /// (<see cref="TowerConceptTraits.Combine"/>). 기획서 6.2 마지막 줄의
    /// "25층 고방어 단독 → 45층 고방어 + 회복 → 70층 고방어 + 분열 + 물량" 이
    /// 그 규칙 하나로 성립한다.
    ///
    /// <b>배수를 더하지 않고 곱하는 이유.</b> 더하면 콘셉트가 셋 겹칠 때 체력 배수가
    /// 0 이나 음수로 갈 수 있다(0.45 + 0.7 - 1 …). 곱셈은 어떤 조합에서도 부호가 안 바뀐다.
    /// </summary>
    public struct TowerConceptTraits
    {
        public float HpMultiplier;
        public float CountMultiplier;
        public float DefenseMultiplier;
        public float MoveSpeedMultiplier;
        public float ScaleMultiplier;

        /// <summary>초당 최대 체력의 몇 %를 회복하는가. 0 이면 회복 없음.</summary>
        public float RegenPercentPerSecond;

        /// <summary>죽을 때 몇 마리로 갈라지는가. 0 이면 분열 없음.</summary>
        public int SplitChildCount;

        /// <summary>분열 자식이 물려받는 체력 비율.</summary>
        public float SplitChildHpRatio;

        /// <summary>
        /// 보호막이 막아 내는 <b>타격 횟수</b>. 0 이면 보호막 없음.
        ///
        /// <b>피해량이 아니라 횟수다.</b> 기획서 6.2 가 보호막의 대응으로 적어 둔 것이
        /// "다단 공격" 이라 그렇다 — 흡수량으로 만들면 한 방이 센 다이스가 그대로 뚫어
        /// 다단 공격의 자리가 사라진다. 횟수로 두면 한 방에 큰 피해를 넣는 쪽이 손해를
        /// 보고, 연쇄·다단으로 때리는 쪽이 답이 된다.
        /// </summary>
        public int ShieldHitCharges;

        /// <summary>
        /// 두 마리 중 한 마리를 <b>빠른 개체</b>로 만드는가. "혼합 부대" 전용이다.
        /// 기획서 6.2 "빠른 적과 단단한 적이 함께 등장 → 역할이 다른 다이스 조합".
        /// </summary>
        public bool MixesFastAndTough;

        public static TowerConceptTraits Neutral => new TowerConceptTraits
        {
            HpMultiplier = 1f,
            CountMultiplier = 1f,
            DefenseMultiplier = 1f,
            MoveSpeedMultiplier = 1f,
            ScaleMultiplier = 1f,
            RegenPercentPerSecond = 0f,
            SplitChildCount = 0,
            SplitChildHpRatio = 0f,
            ShieldHitCharges = 0,
            MixesFastAndTough = false,
        };

        /// <summary>
        /// 이 콘셉트의 기본 배수. <b>코드 기본값이고 정본이 아니다</b> —
        /// <c>TowerDatabase</c> 에셋이 이 값을 담아 두고, 밸런스는 거기서 만진다
        /// (AGENTS 확정사항 3). 에셋이 없을 때만 이 표가 쓰인다.
        /// </summary>
        public static TowerConceptTraits DefaultsFor(TowerFloorConcept concept)
        {
            TowerConceptTraits traits = Neutral;

            switch (concept)
            {
                case TowerFloorConcept.Swarm:
                    // 약한 적이 아주 많이. 폭발·연쇄·범위 공격이 답이다.
                    traits.HpMultiplier = 0.45f;
                    traits.CountMultiplier = 2.2f;
                    traits.DefenseMultiplier = 0.6f;
                    traits.ScaleMultiplier = 0.85f;
                    break;

                case TowerFloorConcept.Rush:
                    // 짧은 시간 안에 경로를 통과. 빙결·기절·넉백이 답이다.
                    // 속도 2.2 배는 "손도 못 대고 벽까지 간다" 가 아니라 "둔화가 없으면
                    // 화력이 모자란다" 가 되도록 잡은 값이다.
                    traits.HpMultiplier = 0.7f;
                    traits.CountMultiplier = 0.6f;
                    traits.DefenseMultiplier = 0.7f;
                    traits.MoveSpeedMultiplier = 2.2f;
                    traits.ScaleMultiplier = 0.9f;
                    break;

                case TowerFloorConcept.Armored:
                    // 받는 피해를 크게 감소. 방어 감소·강한 단일 피해가 답이다.
                    traits.HpMultiplier = 2.2f;
                    traits.CountMultiplier = 0.25f;
                    traits.DefenseMultiplier = 3f;
                    traits.MoveSpeedMultiplier = 0.8f;
                    traits.ScaleMultiplier = 1.25f;
                    break;

                case TowerFloorConcept.Elite:
                    // 소수의 적이 긴 시간 생존. 중독·지속 피해·집중 공격이 답이다.
                    traits.HpMultiplier = 5f;
                    traits.CountMultiplier = 0.18f;
                    traits.DefenseMultiplier = 1.2f;
                    traits.MoveSpeedMultiplier = 0.75f;
                    traits.ScaleMultiplier = 1.4f;
                    break;

                case TowerFloorConcept.Mixed:
                    // 배수는 건드리지 않는다. 이 콘셉트의 알맹이는 개체마다 다르게
                    // 태어난다는 것 하나이고, 그것을 배수로는 표현할 수 없다.
                    traits.MixesFastAndTough = true;
                    break;

                case TowerFloorConcept.Regenerating:
                    // 일정 시간마다 체력 회복. 순간 화력이 답이다.
                    // 초당 4% 는 25초면 한 바퀴를 채우는 속도라, 화력이 그 선을 못 넘으면
                    // 영영 못 잡는다 — 그 선이 이 구간의 검사 지점이다.
                    traits.HpMultiplier = 2.2f;
                    traits.CountMultiplier = 0.4f;
                    traits.MoveSpeedMultiplier = 0.9f;
                    traits.ScaleMultiplier = 1.15f;
                    traits.RegenPercentPerSecond = 4f;
                    break;

                case TowerFloorConcept.Splitting:
                    // 처치 시 작은 적으로 분열. 단일 공격과 범위 공격의 균형이 답이다.
                    traits.HpMultiplier = 1.2f;
                    traits.CountMultiplier = 0.6f;
                    traits.DefenseMultiplier = 0.8f;
                    traits.ScaleMultiplier = 1.1f;
                    traits.SplitChildCount = 2;
                    traits.SplitChildHpRatio = 0.35f;
                    break;

                case TowerFloorConcept.Shielded:
                    // 일정 피해를 막는 보호막. 다단 공격이 답이다.
                    traits.HpMultiplier = 1.4f;
                    traits.CountMultiplier = 0.5f;
                    traits.DefenseMultiplier = 0.9f;
                    traits.MoveSpeedMultiplier = 0.95f;
                    traits.ScaleMultiplier = 1.15f;
                    traits.ShieldHitCharges = 3;
                    break;
            }

            return traits;
        }

        // 겹침의 상·하한. <b>이것이 없으면 3개 조합 구간이 곧바로 벽이 된다.</b>
        //
        // 실제로 계산해 보면 그렇다: 정예(5) × 고방어(2.2) × 회복(2.2) 이면 체력 배수가
        // 24 배이고 방어력도 3.6 배라, 200층에서 체력 35,000 에 피해 감쇄 0.06 인 개체가
        // 초당 4% 씩 회복한다 — 어떤 편성으로도 못 잡는다. 겹침은 <b>다른 대응을 동시에
        // 요구하는 것</b>이지 같은 수치를 곱으로 밀어 올리는 것이 아니다.
        //
        // 상한을 콘셉트 하나의 최댓값(정예 5배) 근처로 잡아 두면, 겹쳐도 "정예보다 조금
        // 더한 정도" 에서 멈추고 차이는 <b>켜진 동작의 가짓수</b>로만 벌어진다.
        private const float MinHpMultiplier = 0.2f;
        private const float MaxHpMultiplier = 6f;
        private const float MinCountMultiplier = 0.1f;
        private const float MaxCountMultiplier = 2.6f;
        private const float MinDefenseMultiplier = 0.3f;
        private const float MaxDefenseMultiplier = 3.4f;
        private const float MinSpeedMultiplier = 0.5f;
        private const float MaxSpeedMultiplier = 2.4f;
        private const float MinScaleMultiplier = 0.7f;
        private const float MaxScaleMultiplier = 1.7f;

        /// <summary>
        /// 두 콘셉트를 겹친다. 배수는 곱한 뒤 <b>잘라 내고</b>, 동작은 센 쪽이 남는다.
        ///
        /// 동작을 더하지 않는 것도 같은 이유다. 회복이 둘 겹쳐 초당 8% 가 되면 그 구간은
        /// 조합이 아니라 벽이 된다 — 겹침의 재미는 "다른 대응을 동시에 요구하는 것" 이지
        /// "같은 대응을 두 배로 요구하는 것" 이 아니다.
        /// </summary>
        public static TowerConceptTraits Combine(TowerConceptTraits a, TowerConceptTraits b)
        {
            return new TowerConceptTraits
            {
                HpMultiplier = Mathf.Clamp(a.HpMultiplier * b.HpMultiplier, MinHpMultiplier, MaxHpMultiplier),
                CountMultiplier = Mathf.Clamp(a.CountMultiplier * b.CountMultiplier, MinCountMultiplier, MaxCountMultiplier),
                DefenseMultiplier = Mathf.Clamp(a.DefenseMultiplier * b.DefenseMultiplier, MinDefenseMultiplier, MaxDefenseMultiplier),
                MoveSpeedMultiplier = Mathf.Clamp(a.MoveSpeedMultiplier * b.MoveSpeedMultiplier, MinSpeedMultiplier, MaxSpeedMultiplier),
                ScaleMultiplier = Mathf.Clamp(a.ScaleMultiplier * b.ScaleMultiplier, MinScaleMultiplier, MaxScaleMultiplier),
                RegenPercentPerSecond = Mathf.Max(a.RegenPercentPerSecond, b.RegenPercentPerSecond),
                SplitChildCount = Mathf.Max(a.SplitChildCount, b.SplitChildCount),
                SplitChildHpRatio = Mathf.Max(a.SplitChildHpRatio, b.SplitChildHpRatio),
                ShieldHitCharges = Mathf.Max(a.ShieldHitCharges, b.ShieldHitCharges),
                MixesFastAndTough = a.MixesFastAndTough || b.MixesFastAndTough,
            };
        }
    }

    /// <summary>콘셉트의 한글 이름과 대응 키워드. 화면 세 곳이 같이 쓴다.</summary>
    public static class TowerConceptText
    {
        /// <summary>층 카드·편성 화면 상단에 뜨는 이름.</summary>
        public static string NameOf(TowerFloorConcept concept)
        {
            switch (concept)
            {
                case TowerFloorConcept.Swarm: return "대규모 물량";
                case TowerFloorConcept.Rush: return "초고속 돌진";
                case TowerFloorConcept.Armored: return "고방어 부대";
                case TowerFloorConcept.Elite: return "고체력 정예";
                case TowerFloorConcept.Mixed: return "혼합 부대";
                case TowerFloorConcept.Regenerating: return "회복형 적";
                case TowerFloorConcept.Splitting: return "분열형 적";
                case TowerFloorConcept.Shielded: return "보호막 적";
                default: return "알 수 없는 적";
            }
        }

        /// <summary>전투 특징 한 줄. 기획서 6.2 의 가운데 칸이다.</summary>
        public static string FeatureOf(TowerFloorConcept concept)
        {
            switch (concept)
            {
                case TowerFloorConcept.Swarm: return "약한 적이 매우 많이 등장";
                case TowerFloorConcept.Rush: return "짧은 시간 안에 경로를 통과";
                case TowerFloorConcept.Armored: return "받는 피해를 크게 감소";
                case TowerFloorConcept.Elite: return "소수의 적이 긴 시간 생존";
                case TowerFloorConcept.Mixed: return "빠른 적과 단단한 적이 함께 등장";
                case TowerFloorConcept.Regenerating: return "일정 시간마다 체력 회복";
                case TowerFloorConcept.Splitting: return "처치 시 작은 적으로 분열";
                case TowerFloorConcept.Shielded: return "일정 타격을 막는 보호막 보유";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// 권장 대응 키워드. 기획서 6.2 의 오른쪽 칸이고, 편성 화면 상단의 태그가 된다.
        ///
        /// <b>정답을 알려 주는 것이 아니다.</b> 5.3 이 말하는 "적을 보고 → 답을 고른다"
        /// 에서 <i>적을 보는</i> 쪽을 대신해 줄 뿐이고, 어떤 다이스가 그 키워드를 갖는지는
        /// 여전히 유저가 안다.
        /// </summary>
        public static string CounterOf(TowerFloorConcept concept)
        {
            switch (concept)
            {
                case TowerFloorConcept.Swarm: return "폭발 · 연쇄 · 범위";
                case TowerFloorConcept.Rush: return "빙결 · 기절 · 넉백";
                case TowerFloorConcept.Armored: return "방어 감소 · 단일 고화력";
                case TowerFloorConcept.Elite: return "중독 · 지속 피해";
                case TowerFloorConcept.Mixed: return "역할이 다른 조합";
                case TowerFloorConcept.Regenerating: return "순간 화력";
                case TowerFloorConcept.Splitting: return "단일 + 범위 균형";
                case TowerFloorConcept.Shielded: return "다단 공격";
                default: return string.Empty;
            }
        }
    }
}
