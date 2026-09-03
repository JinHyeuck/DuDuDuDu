using System.Collections.Generic;
using UnityEngine;
using OJ.Core;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금 5등급의 정본. (AGENTS 확정사항 3 — 밸런스 수치는 SO 로 올린다)
    ///
    /// <b>수급 규모를 코드에서 실측해 잡은 값이다.</b> 강화석 소비처는 둘뿐이고
    /// <c>DiceEvolution</c> 에 상수 넷으로 모여 있다 — 기본→특수 10, 특수→킹 20,
    /// 교환 5·10 — 그리고 속성강화가 레벨당 1·2·3… 을 먹는다. 즉 <b>킹 하나 = 30</b>.
    /// 기본 수급은 웨이브 클리어당 1 이라 15웨이브 스테이지 예산이 15 안팎이고,
    /// 그것만으로는 특수 다이스 하나에서 멈춘다.
    ///
    /// 그래서 <b>현상금이 킹으로 가는 값을 대는 자리</b>가 된다 — 순서대로 다섯을 다
    /// 잡으면 강화석 68 이 들어와 킹 두 개가 열린다. 무시해도 판은 굴러가되(특수 다이스
    /// 한 줄기) 상위 단계는 현상금을 통과해야 닿는다. 그것이 "곁가지지만 전략적"의 뜻이다.
    ///
    /// <b>SP 와 강화석을 번갈아 배치한 것도 의도다.</b> 한 종류로 몰면 등급이 곧 이득
    /// 순서가 되어 고를 것이 없어진다.
    /// </summary>
    [CreateAssetMenu(fileName = "BountyDatabase", menuName = "OJ/Bounty Database")]
    public sealed class BountyDatabase : ScriptableObject
    {
        [SerializeField] private List<BountyDefinition> definitions = new List<BountyDefinition>();

        public IReadOnlyList<BountyDefinition> Definitions => definitions;

        /// <summary>
        /// 등급으로 정의를 찾는다. 없으면 null — <b>부르는 쪽이 시끄럽게 실패해야 한다.</b>
        /// 여기서 1등급으로 흘려보내면 배선 사고가 "약한 놈이 나온다"로 바뀐다.
        /// </summary>
        public BountyDefinition Get(int grade)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].grade == grade)
                    return definitions[i];
            }

            return null;
        }

        /// <summary>
        /// 목록이 성한지 본다. 에디터 도구와 폴백 생성이 같이 쓴다.
        /// 등급 1..5 가 <b>빠짐없이 한 번씩</b> 있어야 선택 창의 여섯 칸이 채워진다.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<int>();

            for (int i = 0; i < definitions.Count; i++)
            {
                BountyDefinition d = definitions[i];
                if (d == null)
                {
                    problems.Add(i + "번 항목이 비어 있다.");
                    continue;
                }

                if (d.grade < 1 || d.grade > BountyFormula.GradeCount)
                {
                    problems.Add(i + "번 항목의 등급이 1~" + BountyFormula.GradeCount + " 밖이다: " + d.grade);
                    continue;
                }

                if (!seen.Add(d.grade))
                    problems.Add("등급이 중복이다: " + d.grade);
            }

            for (int grade = 1; grade <= BountyFormula.GradeCount; grade++)
            {
                if (!seen.Contains(grade))
                    problems.Add("등급 " + grade + " 이 없다.");
            }

            return problems;
        }

        /// <summary>
        /// 코드 기본값. 에셋이 없을 때 <see cref="BountyDatabaseProvider"/> 가 쓰고,
        /// 에디터 도구가 에셋을 처음 만들 때도 이것을 찍어 넣는다 —
        /// <b>기본값이 두 벌이 되지 않게</b> 한 곳에서만 적는다.
        /// </summary>
        public void PopulateDefaults()
        {
            definitions = new List<BountyDefinition>
            {
                new BountyDefinition
                {
                    grade = 1,
                    displayName = "좀도둑",
                    referenceWaveRatio = 0.10f,
                    hpMultiplier = 5f,
                    rewardKind = BountyRewardKind.SummonPoint,
                    rewardAmount = 60,
                    moveSpeedMultiplier = 0.3f,
                    scaleMultiplier = 1.15f,
                    tint = new Color(0.72f, 0.78f, 0.86f, 1f),
                },
                new BountyDefinition
                {
                    grade = 2,
                    displayName = "악덕상인",
                    referenceWaveRatio = 0.30f,
                    hpMultiplier = 6f,
                    rewardKind = BountyRewardKind.EnhanceStone,
                    rewardAmount = 8,
                    moveSpeedMultiplier = 0.3f,
                    scaleMultiplier = 1.25f,
                    tint = new Color(0.55f, 0.85f, 0.62f, 1f),
                },
                new BountyDefinition
                {
                    grade = 3,
                    displayName = "해적왕",
                    referenceWaveRatio = 0.50f,
                    hpMultiplier = 8f,
                    rewardKind = BountyRewardKind.SummonPoint,
                    rewardAmount = 150,
                    moveSpeedMultiplier = 0.3f,
                    scaleMultiplier = 1.35f,
                    tint = new Color(0.45f, 0.70f, 0.98f, 1f),
                },
                new BountyDefinition
                {
                    grade = 4,
                    displayName = "기사단장",
                    referenceWaveRatio = 0.75f,
                    hpMultiplier = 10f,
                    rewardKind = BountyRewardKind.EnhanceStone,
                    rewardAmount = 20,
                    moveSpeedMultiplier = 0.3f,
                    scaleMultiplier = 1.45f,
                    tint = new Color(0.80f, 0.55f, 0.95f, 1f),
                },
                new BountyDefinition
                {
                    grade = 5,
                    displayName = "대마법사",
                    referenceWaveRatio = 1.00f,
                    hpMultiplier = 14f,
                    rewardKind = BountyRewardKind.EnhanceStone,
                    rewardAmount = 40,
                    moveSpeedMultiplier = 0.3f,
                    scaleMultiplier = 1.6f,
                    tint = new Color(1f, 0.72f, 0.32f, 1f),
                },
            };
        }
    }
}
