using System.Collections.Generic;
using UnityEngine;
using OJ.Core;
using OJ.Stage;

namespace OJ.Tower
{
    /// <summary>
    /// 무한의 탑 60구간(300층)의 정본.
    ///
    /// <b>다이스 해금 사다리는 여기 없다.</b> 가격과 세 컨텐츠의 보상처를 한 목록에 모아야
    /// 중복 환급이 성립하므로 <c>DiceUnlockDatabase</c> 로 옮겼다 — 그쪽 클래스 주석 참조.
    /// (AGENTS 확정사항 3 — 밸런스 수치는 SO 로 올린다)
    ///
    /// <b>구간 60줄은 코드가 찍고 사람이 고친다.</b> <see cref="PopulateDefaults"/> 가
    /// 기획서 6장의 규칙("학습 4구간 → 단독 소개 → 2개 조합 → 3개 조합")대로 60줄을
    /// 만들어 에셋에 굳히고, 그 뒤로는 <b>에셋이 정본</b>이다. 코드가 로드마다 다시
    /// 덮어쓰지 않는다 — <c>StageDatabase</c> 가 <c>OnEnable</c> 에서 그렇게 하다가
    /// 인스펙터 수정이 매번 사라졌던 그 함정을 반복하지 않는다.
    ///
    /// <b>층 하나의 최종 수치는 <see cref="GetPlan"/> 이 조립한다.</b>
    /// 성장 곡선(<see cref="TowerFormula"/>) × 콘셉트 배수(<see cref="TowerConceptTraits"/>)
    /// × 구간 배수 순서이고, 이 순서가 곧 "무엇을 만지면 무엇이 움직이는가" 다.
    /// </summary>
    [CreateAssetMenu(fileName = "TowerDatabase", menuName = "OJ/Tower Database")]
    public sealed class TowerDatabase : ScriptableObject
    {
        [SerializeField] private List<TowerBandDefinition> bands = new List<TowerBandDefinition>();

        [Header("공통")]
        [Tooltip("모든 층의 벽 체력. 층이 실패로 끝나는 유일한 조건이다.")]
        [Min(1)] [SerializeField] private int wallHp = 100;

        /// <summary>구간 → 계획 캐시. 층 선택 화면이 30장을 한 번에 그리므로 값어치가 있다.</summary>
        private readonly Dictionary<int, TowerFloorPlan> planCache = new Dictionary<int, TowerFloorPlan>();

        private readonly Dictionary<int, TowerBandDefinition> bandMap = new Dictionary<int, TowerBandDefinition>();

        public IReadOnlyList<TowerBandDefinition> Bands => bands;
        public int WallHp => Mathf.Max(1, wallHp);

        private void OnEnable()
        {
            if (bands == null)
                bands = new List<TowerBandDefinition>();

            // 비었을 때만 채운다. StageDatabase 와 같은 판단이고, 이유도 같다 —
            // 로드마다 덮으면 인스펙터에서 고친 밸런스가 조용히 사라진다.
            if (bands.Count == 0)
                PopulateDefaults();

            RebuildMaps();
        }

        // ── 조회 ────────────────────────────────────────────────────────

        /// <summary>
        /// 구간 정의를 찾는다. 없으면 null — <b>부르는 쪽이 시끄럽게 실패해야 한다.</b>
        /// 1구간으로 흘려보내면 배선 사고가 "300층인데 왜 쉽지" 로 바뀐다.
        /// </summary>
        public TowerBandDefinition GetBand(int band)
        {
            if (bandMap.Count != bands.Count)
                RebuildMaps();

            return bandMap.TryGetValue(band, out TowerBandDefinition definition) ? definition : null;
        }

        /// <summary>
        /// 층 하나의 완성된 계획. 같은 층을 두 번 물으면 같은 객체가 나온다.
        ///
        /// <b>순서가 곧 설계다.</b>
        /// <list type="number">
        /// <item>성장 곡선이 층 번호만 보고 기준 체력·방어력을 낸다.</item>
        /// <item>콘셉트 배수가 그 위에 곱해진다 — 같은 층수라도 물량과 정예가 갈린다.</item>
        /// <item>구간 배수가 마지막에 곱해진다 — 한 구간만 조정하고 싶을 때 쓰는 손잡이다.</item>
        /// </list>
        /// </summary>
        public TowerFloorPlan GetPlan(int floor)
        {
            int validFloor = TowerFormula.ClampFloor(floor);
            if (planCache.TryGetValue(validFloor, out TowerFloorPlan cached))
                return cached;

            int band = TowerFormula.BandOf(validFloor);
            TowerBandDefinition definition = GetBand(band);
            if (definition == null)
            {
                // 구간이 비었다는 것은 에셋이 60줄을 다 갖지 못했다는 뜻이다. 조용히
                // 기본 구간으로 때우면 그 사실이 영영 안 드러나므로 로그를 남기고,
                // 그래도 층은 열어 준다 — 데이터 사고로 탑 전체가 막히면 안 된다.
                Debug.LogError("[탑] " + band + "구간 정의가 없다. 임시 구간으로 " + validFloor +
                               "층을 만든다. OJ/개발/무한의 탑/데이터베이스 검사 를 돌릴 것.");
                definition = new TowerBandDefinition { band = band };
            }

            TowerFloorPlan plan = BuildPlan(validFloor, band, definition);
            planCache[validFloor] = plan;
            return plan;
        }

        private TowerFloorPlan BuildPlan(int floor, int band, TowerBandDefinition definition)
        {
            List<TowerFloorConcept> concepts = definition.ResolveConcepts();

            TowerConceptTraits traits = TowerConceptTraits.DefaultsFor(concepts[0]);
            for (int i = 1; i < concepts.Count; i++)
                traits = TowerConceptTraits.Combine(traits, TowerConceptTraits.DefaultsFor(concepts[i]));

            int rawCount = TowerFormula.MonsterCount(floor, definition.monsterCount, definition.countPerFloorStep);
            int count = Mathf.Max(1, Mathf.RoundToInt(rawCount * traits.CountMultiplier));

            int hp = TowerFormula.MonsterHp(floor, traits.HpMultiplier * definition.hpMultiplier);
            int defense = TowerFormula.MonsterDefense(floor, traits.DefenseMultiplier * definition.defenseMultiplier);

            return new TowerFloorPlan(
                floor: floor,
                band: band,
                displayName: string.IsNullOrWhiteSpace(definition.displayName)
                    ? TowerConceptText.NameOf(concepts[0])
                    : definition.displayName,
                concepts: concepts,
                theme: definition.theme,
                monsterCount: count,
                monsterHp: hp,
                monsterDefense: defense,
                moveSpeedMultiplier: traits.MoveSpeedMultiplier,
                scaleMultiplier: traits.ScaleMultiplier,
                regenPercentPerSecond: traits.RegenPercentPerSecond,
                splitChildCount: traits.SplitChildCount,
                splitChildHpRatio: traits.SplitChildHpRatio,
                shieldHitCharges: traits.ShieldHitCharges,
                mixesFastAndTough: traits.MixesFastAndTough,
                wallHp: WallHp);
        }

        // ── 검사 ────────────────────────────────────────────────────────

        /// <summary>
        /// 목록이 성한지 본다. 에디터 도구와 진단이 같이 쓴다.
        ///
        /// <b>구간 60개가 빠짐없이 한 번씩</b> 있어야 300층이 전부 열린다.
        /// 하나라도 비면 그 다섯 층이 임시 구간으로 떨어지는데, 그건 로그 한 줄로만
        /// 드러나므로 여기서 미리 잡는다.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<int>();

            for (int i = 0; i < bands.Count; i++)
            {
                TowerBandDefinition d = bands[i];
                if (d == null)
                {
                    problems.Add(i + "번 구간이 비어 있다.");
                    continue;
                }

                if (d.band < 1 || d.band > TowerFormula.BandCount)
                {
                    problems.Add(i + "번 항목의 구간 번호가 1~" + TowerFormula.BandCount + " 밖이다: " + d.band);
                    continue;
                }

                if (!seen.Add(d.band))
                    problems.Add("구간 번호가 중복이다: " + d.band);

                if (d.monsterCount < 1)
                    problems.Add(d.band + "구간의 마리 수가 0 이하다.");
            }

            for (int band = 1; band <= TowerFormula.BandCount; band++)
            {
                if (!seen.Contains(band))
                    problems.Add("구간 " + band + " 이 없다.");
            }

            return problems;
        }

        // ── 기본값 ──────────────────────────────────────────────────────

        /// <summary>
        /// 코드 기본값. 에셋이 없을 때 <see cref="TowerDatabaseProvider"/> 가 쓰고,
        /// 에디터 도구가 에셋을 처음 만들 때도 이것을 찍어 넣는다 —
        /// <b>기본값이 두 벌이 되지 않게</b> 한 곳에서만 적는다.
        /// </summary>
        public void PopulateDefaults()
        {
            bands = BuildDefaultBands();
            planCache.Clear();
            RebuildMaps();
        }

        /// <summary>
        /// 60구간을 만든다.
        ///
        /// <b>1~4구간(1~20층)은 기획서 6.1 을 글자 그대로 옮긴다.</b>
        /// 물량 → 단일 → 속도 → 방어. 마리 수까지 표에 적힌 값이다
        /// ("약 30마리부터 시작해 층마다 수 증가", "2~5마리", "약 10마리", "1마리").
        ///
        /// <b>5구간(21층)부터는 6.2 의 규칙으로 만든다.</b> "새로운 기믹은 한 구간에
        /// 한 가지씩 먼저 소개하고, 이후 구간에서 기존 기믹과 조합한다."
        /// 그래서 세 단계로 나눈다.
        /// <list type="bullet">
        /// <item><b>소개(21~60층)</b> — 아직 안 나온 콘셉트 넷(혼합·회복·분열·보호막)을
        ///       단독으로 한 구간씩, 그리고 앞의 넷도 한 번씩 다시.</item>
        /// <item><b>2개 조합(61~180층)</b> — 서로 다른 둘을 짝짓는다.</item>
        /// <item><b>3개 조합(181~300층)</b> — 셋을 겹친다.</item>
        /// </list>
        ///
        /// <b>조합을 난수로 뽑지 않는다.</b> 돌릴 때마다 다른 표가 나오면 밸런스를
        /// 잡을 수 없고, 무엇보다 "어제 45층이 회복이었는데 오늘은 아니다" 가 된다.
        /// 층 번호에서 결정적으로 계산한다.
        /// </summary>
        private static List<TowerBandDefinition> BuildDefaultBands()
        {
            var list = new List<TowerBandDefinition>();

            // 학습 구간 (기획서 6.1).
            //
            // <b>여기 적힌 수는 화면에 나오는 수가 아니다.</b> 콘셉트 배수가 곱해진 뒤가
            // 실제 마리 수라, 기획서의 값에서 배수를 나눠 역산한 값을 적는다.
            // 결과는 기획서 표와 정확히 맞는다:
            //   1구간 ×2.2  → 31 · 35 · 40 · 44 · 48   ("약 30마리부터 시작해 층마다 증가")
            //   2구간 ×0.18 →  2 ·  3 ·  3 ·  4 ·  5   ("2~5마리")
            //   3구간 ×0.6  → 10 마리 고정              ("약 10마리")
            //   4구간 ×0.25 →  1 마리 고정              ("1마리")
            list.Add(MakeBand(1, "첫 무리", TowerFloorConcept.Swarm, 14, 2));
            list.Add(MakeBand(2, "무거운 발걸음", TowerFloorConcept.Elite, 11, 4));
            list.Add(MakeBand(3, "달려오는 것들", TowerFloorConcept.Rush, 17, 0));
            list.Add(MakeBand(4, "굳은 껍질", TowerFloorConcept.Armored, 4, 0));

            // 21층 이후. 콘셉트 여덟 개를 순서대로 돌린다.
            TowerFloorConcept[] cycle =
            {
                TowerFloorConcept.Mixed,
                TowerFloorConcept.Regenerating,
                TowerFloorConcept.Splitting,
                TowerFloorConcept.Shielded,
                TowerFloorConcept.Swarm,
                TowerFloorConcept.Rush,
                TowerFloorConcept.Armored,
                TowerFloorConcept.Elite,
            };

            for (int band = 5; band <= TowerFormula.BandCount; band++)
            {
                int step = band - 5;
                TowerFloorConcept primary = cycle[step % cycle.Length];

                var definition = MakeBand(band, null, primary, BaseCountFor(primary), StepFor(primary));

                // 소개 구간(5~12)은 단독. 그 뒤로는 둘, 마지막 넷은 셋을 겹친다.
                // 겹치는 상대는 주 콘셉트에서 일정 간격 떨어진 것을 고른다 —
                // 인접한 것끼리만 붙으면 "물량+속도" 같은 조합이 계속 반복된다.
                if (band >= 13)
                    definition.extraConcepts.Add(cycle[(step + 3) % cycle.Length]);

                if (band >= 37)
                    definition.extraConcepts.Add(cycle[(step + 5) % cycle.Length]);

                // 구간이 오를수록 체력을 아주 조금씩 더 준다. 성장 곡선만으로는
                // 후반 구간의 콘셉트 차이가 뭉개져서, 겹침이 늘어난 만큼을 여기서 눌러 준다.
                // 1.0 에서 시작해 60구간에서 1.15 가 된다.
                definition.hpMultiplier = 1f + (band - 5) * 0.0027f;

                definition.displayName = BuildBandName(definition);
                list.Add(definition);
            }

            return list;
        }

        private static TowerBandDefinition MakeBand(
            int band, string displayName, TowerFloorConcept primary, int count, int step)
        {
            return new TowerBandDefinition
            {
                band = band,
                displayName = displayName,
                primaryConcept = primary,
                extraConcepts = new List<TowerFloorConcept>(),
                // 다섯 테마를 구간마다 돌린다. 300층 내내 같은 배경을 보지 않게 하는 것이
                // 전부이고, 테마가 전투에 주는 영향은 몬스터 프리팹 외양뿐이다.
                theme = (StageTheme)((band - 1) % 5),
                monsterCount = count,
                countPerFloorStep = step,
                hpMultiplier = 1f,
                defenseMultiplier = 1f,
            };
        }

        /// <summary>
        /// 콘셉트별 기준 마리 수. <b>콘셉트 배수를 곱하기 전 값</b>이라
        /// 화면에 나오는 수와 다르다 — 예를 들어 물량은 여기 20 이어도 배수 2.2 로
        /// 44마리가 된다.
        /// </summary>
        private static int BaseCountFor(TowerFloorConcept concept)
        {
            switch (concept)
            {
                case TowerFloorConcept.Swarm: return 20;
                case TowerFloorConcept.Rush: return 18;
                case TowerFloorConcept.Armored: return 16;
                case TowerFloorConcept.Elite: return 22;
                case TowerFloorConcept.Mixed: return 18;
                case TowerFloorConcept.Regenerating: return 15;
                case TowerFloorConcept.Splitting: return 14;
                case TowerFloorConcept.Shielded: return 16;
                default: return 18;
            }
        }

        private static int StepFor(TowerFloorConcept concept)
        {
            // 물량 계열만 구간 안에서 늘어난다. 나머지는 다섯 층이 같은 수이고
            // 체력·방어력만 오른다 — 정예가 층마다 늘어나면 "소수" 라는 콘셉트가 깨진다.
            return concept == TowerFloorConcept.Swarm ? 2 : 0;
        }

        private static string BuildBandName(TowerBandDefinition definition)
        {
            List<TowerFloorConcept> concepts = definition.ResolveConcepts();
            if (concepts.Count == 1)
                return TowerConceptText.NameOf(concepts[0]);

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < concepts.Count; i++)
            {
                if (i > 0)
                    sb.Append(" + ");

                sb.Append(TowerConceptText.NameOf(concepts[i]));
            }

            return sb.ToString();
        }

        private void RebuildMaps()
        {
            bandMap.Clear();
            planCache.Clear();

            for (int i = 0; i < bands.Count; i++)
            {
                TowerBandDefinition definition = bands[i];
                if (definition == null)
                    continue;

                bandMap[definition.band] = definition;
            }
        }
    }
}
