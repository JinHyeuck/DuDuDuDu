using System.Collections.Generic;
using System.Text;
using OJ.Core;
using OJ.Stage;

namespace OJ.Tower
{
    /// <summary>
    /// 한 층을 도는 데 필요한 값 전부. <see cref="TowerDatabase.GetPlan"/> 이 만든다.
    ///
    /// <b>왜 계산 결과를 객체로 굳히나.</b> 이 값들을 읽는 곳이 넷이다 —
    /// 층 선택 카드(체력 미리보기), 편성 화면 상단(적 정보), 스포너(실제 소환),
    /// 결과 화면(남은 체력 비율). 넷이 각자 계산하면 <b>화면에 뜬 숫자와 실제로 나온
    /// 몬스터가 갈릴 수 있다</b>. 한 번 만들어 돌려쓰면 그 갈림이 원천적으로 없다.
    ///
    /// <b>불변이다.</b> 만든 뒤에 아무도 못 고친다 — 전투 중에 층 계획이 바뀌는 일은
    /// 없어야 하고, 바뀔 수 있게 두면 "왜 체력이 다르지"를 전투 코드 전체에서 찾게 된다.
    /// </summary>
    public sealed class TowerFloorPlan
    {
        public int Floor { get; }

        /// <summary>이 층이 속한 구간 번호(1부터).</summary>
        public int Band { get; }

        /// <summary>구간 이름. 층 카드와 편성 화면 상단에 그대로 나간다.</summary>
        public string DisplayName { get; }

        /// <summary>이 층에 겹쳐진 콘셉트. 최소 1개다.</summary>
        public IReadOnlyList<TowerFloorConcept> Concepts { get; }

        /// <summary>배경·몬스터 프리팹을 고르는 테마. 본편 스테이지 테마를 그대로 빌린다.</summary>
        public StageTheme Theme { get; }

        public int MonsterCount { get; }
        public int MonsterHp { get; }
        public int MonsterDefense { get; }
        public float MoveSpeedMultiplier { get; }
        public float ScaleMultiplier { get; }
        public float SpawnInterval { get; }

        public float RegenPercentPerSecond { get; }
        public int SplitChildCount { get; }
        public float SplitChildHpRatio { get; }
        public int ShieldHitCharges { get; }
        public bool MixesFastAndTough { get; }

        /// <summary>
        /// 잡아야 하는 총 마리 수. 분열 자식까지 포함한다.
        /// <see cref="TowerFormula.WaveKillTarget"/> 참조 — 웨이브 종료 판정이 여기 걸린다.
        /// </summary>
        public int KillTarget { get; }

        /// <summary>
        /// 이 층 전체의 체력 합. 결과 화면의 "남은 적 체력 %" 분모다.
        /// <b>분열 자식도 센다</b> — 안 세면 자식만 남았을 때 잔여율이 음수가 된다.
        /// </summary>
        public int TotalHpPool { get; }

        public int WallHp { get; }

        public TowerFloorPlan(
            int floor,
            int band,
            string displayName,
            IReadOnlyList<TowerFloorConcept> concepts,
            StageTheme theme,
            int monsterCount,
            int monsterHp,
            int monsterDefense,
            float moveSpeedMultiplier,
            float scaleMultiplier,
            float regenPercentPerSecond,
            int splitChildCount,
            float splitChildHpRatio,
            int shieldHitCharges,
            bool mixesFastAndTough,
            int wallHp)
        {
            Floor = floor;
            Band = band;
            DisplayName = displayName;
            Concepts = concepts;
            Theme = theme;
            MonsterCount = monsterCount;
            MonsterHp = monsterHp;
            MonsterDefense = monsterDefense;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            ScaleMultiplier = scaleMultiplier;
            RegenPercentPerSecond = regenPercentPerSecond;
            SplitChildCount = splitChildCount;
            SplitChildHpRatio = splitChildHpRatio;
            ShieldHitCharges = shieldHitCharges;
            MixesFastAndTough = mixesFastAndTough;
            WallHp = wallHp;

            SpawnInterval = TowerFormula.SpawnInterval(monsterCount);
            KillTarget = TowerFormula.WaveKillTarget(monsterCount, splitChildCount);

            int childHp = SplitChildHp;
            TotalHpPool = monsterCount * monsterHp + monsterCount * splitChildCount * childHp;
        }

        /// <summary>분열 자식 한 마리의 체력. 분열이 없으면 0 이다.</summary>
        public int SplitChildHp
        {
            get
            {
                if (SplitChildCount <= 0)
                    return 0;

                return OJMath.Max(1, OJMath.RoundToInt(MonsterHp * SplitChildHpRatio));
            }
        }

        /// <summary>
        /// 적 구성 한 줄 요약. 기획서 5.3 의 "고방어 몬스터 ×1 · 받는 피해 대폭 감소" 자리다.
        /// </summary>
        public string BuildEnemySummary()
        {
            var sb = new StringBuilder();
            sb.Append(TowerConceptText.NameOf(Concepts[0]));
            sb.Append(" ×");
            sb.Append(MonsterCount);

            for (int i = 0; i < Concepts.Count; i++)
            {
                sb.Append(" · ");
                sb.Append(TowerConceptText.FeatureOf(Concepts[i]));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 권장 대응 태그 목록. 편성 화면 상단의 칩이 된다(기획서 5.3 의 "방어력 감소 권장").
        /// 겹친 콘셉트가 같은 키워드를 내면 하나만 남긴다 — 같은 칩이 두 번 뜨면
        /// 무엇을 말하려는지 흐려진다.
        /// </summary>
        public List<string> BuildCounterTags()
        {
            var tags = new List<string>();
            for (int i = 0; i < Concepts.Count; i++)
            {
                string tag = TowerConceptText.CounterOf(Concepts[i]);
                if (string.IsNullOrEmpty(tag) || tags.Contains(tag))
                    continue;

                tags.Add(tag);
            }

            return tags;
        }
    }
}
