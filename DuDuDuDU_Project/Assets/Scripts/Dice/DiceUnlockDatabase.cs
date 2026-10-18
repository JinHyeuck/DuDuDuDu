using System.Collections.Generic;
using UnityEngine;
using OJ.Core;

namespace OJ.Dice
{
    /// <summary>
    /// 다이스 언락의 <b>유일한 정본</b>. 가격 10줄과 세 컨텐츠의 보상처가 여기 다 있다.
    /// (AGENTS 확정사항 3 — 밸런스 수치는 SO 로 올린다)
    ///
    /// <b>무한의 탑 해금 사다리를 여기로 흡수했다.</b> 예전에는 <c>TowerDatabase.diceUnlocks</c> 가
    /// "N층 → 다이스" 를 들고 있었는데, 그대로 두고 가격표를 따로 만들면 정본이 둘이 된다.
    /// 그리고 환급 규칙("이미 보유한 다이스를 컨텐츠가 주면 가격만큼 재화로 준다")이
    /// <b>목록 하나를 강제한다</b> — 둘이면 가격 없는 컨텐츠 해금을 만들 수 있고, 그때
    /// 환급할 금액을 알 방법이 없다. 탑에 위임 껍데기를 남기지 않은 것도 같은 이유다.
    ///
    /// <b>검사와 기본값이 <c>static</c> 인 이유.</b> 헤드리스 EditMode 러너 안에서는
    /// <c>ScriptableObject.CreateInstance</c> 가 돌지 않는다(엔진 네이티브가 없어
    /// <c>MissingMethodException</c> 이 난다 — <c>AssemblyPilotTests</c> 주석 참조).
    /// 규칙을 SO 인스턴스에 매달면 테스트가 에디터를 열어야만 가능해지므로, 목록을 받는
    /// <c>static</c> 에 규칙을 두고 인스턴스 메서드는 자기 필드를 넘기기만 한다.
    /// 그래서 에디터를 열어야 확인되는 것은 "에셋에 값이 실려 있는가" 하나로 줄어든다.
    /// </summary>
    [CreateAssetMenu(fileName = "DiceUnlockDatabase", menuName = "OJ/Dice Unlock Database")]
    public sealed class DiceUnlockDatabase : ScriptableObject
    {
        [Tooltip("킹 5종. 가격은 필수, 컨텐츠 보상처는 0 이면 없음.")]
        [SerializeField] private List<DiceUnlockDefinition> definitions = new List<DiceUnlockDefinition>();

        private readonly Dictionary<DiceType, DiceUnlockDefinition> map =
            new Dictionary<DiceType, DiceUnlockDefinition>();

        public IReadOnlyList<DiceUnlockDefinition> Definitions => definitions;

        private void OnEnable()
        {
            if (definitions == null)
                definitions = new List<DiceUnlockDefinition>();

            // 비었을 때만 채운다. TowerDatabase·StageDatabase 와 같은 판단이고 이유도 같다 —
            // 로드마다 덮으면 인스펙터에서 고친 수치가 조용히 사라진다.
            if (definitions.Count == 0)
                definitions.AddRange(BuildDefaults());

            RebuildMap();
        }

        private void RebuildMap()
        {
            map.Clear();
            for (int i = 0; i < definitions.Count; i++)
            {
                DiceUnlockDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                map[definition.diceType] = definition;
            }
        }

        // ── 조회 ────────────────────────────────────────────────────────

        /// <summary>이 다이스의 언락 정의. 기본 다이스이거나 목록에 없으면 null.</summary>
        public DiceUnlockDefinition Get(DiceType diceType)
        {
            if (map.Count != definitions.Count)
                RebuildMap();

            return map.TryGetValue(diceType, out DiceUnlockDefinition definition) ? definition : null;
        }

        /// <summary>
        /// 이 다이스의 재화 가격. 없으면 0 이다.
        ///
        /// 0 은 <b>"살 수 없다"</b>는 뜻이고 <b>"공짜"</b>가 아니다 — 호출부는 0 을 만나면
        /// 구매를 막아야 한다. 기본 다이스는 애초에 보유 상태라 여기까지 오지 않는다.
        /// </summary>
        public int GetPrice(DiceType diceType)
        {
            DiceUnlockDefinition definition = Get(diceType);
            return definition != null ? Mathf.Max(0, definition.price) : 0;
        }

        /// <summary>
        /// 이 누적 별 수가 열어 주는 다이스. 없으면 <c>DiceType.Max</c>.
        ///
        /// <b>보상 인덱스가 아니라 별 수로 찾는다.</b> 인덱스는 스테이지가 늘면 뜻이 변하지만
        /// "누적 36별" 은 변하지 않는다.
        /// </summary>
        public DiceType GetStarUnlock(int starCount)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                DiceUnlockDefinition definition = definitions[i];
                if (definition != null && definition.starRequirement > 0 && definition.starRequirement == starCount)
                    return definition.diceType;
            }

            return DiceType.Max;
        }

        /// <summary>이 스테이지를 퍼펙트로 깨면 열리는 다이스. 없으면 <c>DiceType.Max</c>.</summary>
        public DiceType GetStageUnlock(int stageIndex)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                DiceUnlockDefinition definition = definitions[i];
                if (definition != null && definition.stageRequirement > 0 && definition.stageRequirement == stageIndex)
                    return definition.diceType;
            }

            return DiceType.Max;
        }

        /// <summary>
        /// 이 층을 클리어하면 열리는 다이스. 없으면 <c>DiceType.Max</c> 다.
        /// <see cref="DiceType.Max"/> 를 "없음" 으로 쓰는 것은 이 프로젝트의 기존 관례다.
        /// </summary>
        public DiceType GetUnlockAtFloor(int floor)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                DiceUnlockDefinition definition = definitions[i];
                if (definition != null && definition.towerFloor > 0 && definition.towerFloor == floor)
                    return definition.diceType;
            }

            return DiceType.Max;
        }

        /// <summary>
        /// 지금 진행도에서 <b>다음에 열릴</b> 탑 해금. 층 선택 화면의 목표 배너와
        /// 결과 화면의 해금 진행도가 쓴다(기획서 5.2 · 5.6). 전부 열었으면 null 이다.
        /// </summary>
        public DiceUnlockDefinition GetNextTowerUnlock(int highestClearedFloor)
        {
            DiceUnlockDefinition best = null;
            for (int i = 0; i < definitions.Count; i++)
            {
                DiceUnlockDefinition definition = definitions[i];
                if (definition == null || definition.towerFloor <= 0 || definition.towerFloor <= highestClearedFloor)
                    continue;

                if (best == null || definition.towerFloor < best.towerFloor)
                    best = definition;
            }

            return best;
        }

        /// <summary>이 다이스를 열어 주는 층. 없으면 0 이다.</summary>
        public int GetUnlockFloorOf(DiceType diceType)
        {
            DiceUnlockDefinition definition = Get(diceType);
            return definition != null ? definition.towerFloor : 0;
        }

        // ── 검사 ────────────────────────────────────────────────────────

        /// <summary>목록이 성한지 본다. 에디터 도구와 진단이 같이 쓴다.</summary>
        public List<string> Validate(int stageCount, int maxStarCount, int starsPerReward)
        {
            return Validate(definitions, stageCount, maxStarCount, starsPerReward);
        }

        /// <summary>
        /// 규칙 본체. <b>SO 없이 목록만으로 돈다</b> — 위 클래스 주석의 <c>static</c> 이유 참조.
        ///
        /// 바깥 수치 셋(스테이지 수·최대 별 수·보상 간격)을 인자로 받는 것은 그것들이
        /// 다른 SO 와 상수에서 오기 때문이다. 숨기면 이 검사가 다시 엔진에 묶인다.
        /// 0 을 넘기면 그 검사는 건너뛴다.
        /// </summary>
        public static List<string> Validate(
            IReadOnlyList<DiceUnlockDefinition> definitions,
            int stageCount,
            int maxStarCount,
            int starsPerReward)
        {
            var problems = new List<string>();

            if (definitions == null)
            {
                problems.Add("목록이 null 이다.");
                return problems;
            }

            var seenDice = new HashSet<DiceType>();
            var seenFloors = new HashSet<int>();
            var seenStars = new HashSet<int>();
            var seenStages = new HashSet<int>();

            for (int i = 0; i < definitions.Count; i++)
            {
                DiceUnlockDefinition definition = definitions[i];
                if (definition == null)
                {
                    problems.Add(i + "번 항목이 비어 있다.");
                    continue;
                }

                DiceType diceType = definition.diceType;

                // 기본 5종과 특수(레어) 5종은 규칙으로 보유한다. 목록에 들어오면
                // 가격·보상처가 생기는데 보유 판정은 그것을 안 보므로, 유저가 이미 가진
                // 것을 파는 화면이 만들어진다. (DiceEvolution.IsOwnedFromStart 참조)
                if (DiceEvolution.IsOwnedFromStart(diceType))
                    problems.Add("이 다이스는 처음부터 보유라 언락 대상이 아니다: " + diceType);

                if (!seenDice.Add(diceType))
                    problems.Add("같은 다이스가 두 번 있다: " + diceType);

                // 가격 없는 줄은 만들 수 없다. 환급액이 곧 가격이라, 없으면 컨텐츠가
                // 중복으로 줬을 때 무엇을 돌려줄지 알 수 없다.
                if (definition.price < 1)
                    problems.Add(diceType + " 의 가격이 없다. 환급액이 곧 가격이라 1 이상이어야 한다.");

                if (definition.towerFloor > 0)
                {
                    if (definition.towerFloor > TowerFormula.TotalFloors)
                        problems.Add(diceType + " 의 해금 층이 1~" + TowerFormula.TotalFloors + " 밖이다: " + definition.towerFloor);

                    // 같은 층이 둘을 열면 결과 화면이 하나만 보여 준다.
                    if (!seenFloors.Add(definition.towerFloor))
                        problems.Add("해금 층이 중복이다: " + definition.towerFloor + "층");
                }

                if (definition.starRequirement > 0)
                {
                    if (maxStarCount > 0 && definition.starRequirement > maxStarCount)
                        problems.Add(diceType + " 의 요구 별이 최대 " + maxStarCount + "개를 넘는다: " + definition.starRequirement);

                    // 별 보상은 starsPerReward 간격으로만 존재한다. 어긋난 값을 적으면
                    // 그 다이스는 영영 이 경로로 안 열린다 — 조용히 죽는 종류라 여기서 잡는다.
                    if (starsPerReward > 0 && definition.starRequirement % starsPerReward != 0)
                        problems.Add(diceType + " 의 요구 별이 " + starsPerReward + " 의 배수가 아니다: " + definition.starRequirement);

                    if (!seenStars.Add(definition.starRequirement))
                        problems.Add("요구 별이 중복이다: " + definition.starRequirement + "개");
                }

                if (definition.stageRequirement > 0)
                {
                    if (stageCount > 0 && definition.stageRequirement > stageCount)
                        problems.Add(diceType + " 의 요구 스테이지가 " + stageCount + " 를 넘는다: " + definition.stageRequirement);

                    if (!seenStages.Add(definition.stageRequirement))
                        problems.Add("요구 스테이지가 중복이다: " + definition.stageRequirement);
                }
            }

            // 킹 5종이 빠짐없이 있어야 한다. 빠진 다이스는 영영 못 얻는다 —
            // 가격도 보상처도 없는 상태가 되기 때문이다.
            // (특수 5종은 처음부터 보유라 여기서 요구하지 않는다.)
            AppendMissing(problems, seenDice, DiceEvolution.KingTypes);

            return problems;
        }

        private static void AppendMissing(List<string> problems, HashSet<DiceType> seen, IReadOnlyList<DiceType> required)
        {
            for (int i = 0; i < required.Count; i++)
            {
                if (!seen.Contains(required[i]))
                    problems.Add(required[i] + " 의 언락 정의가 없다. 그러면 그 다이스는 영영 못 얻는다.");
            }
        }

        // ── 기본값 ──────────────────────────────────────────────────────

        /// <summary>
        /// 코드가 아는 기본 표. 에셋이 비었을 때 한 번 찍히고, 그 뒤로는 에셋이 정본이다.
        ///
        /// <b>킹 5종만 있다(2026-10-14).</b> 특수(레어) 5종이 여기 있었는데, 그것들은
        /// <b>판 안에서 만드는 전술 선택</b>이 되었으므로 처음부터 보유로 바꿨다 —
        /// 근거는 <c>DiceEvolution.IsOwnedFromStart</c> 에 적어 두었다. 그래서 별 15/36,
        /// 스테이지 8/22, 튜토리얼에 걸려 있던 네 자리가 비었고, <b>그 자리를 다시 채우지
        /// 않았다</b>. 거기 킹을 옮겨 붙이면 탑의 해금 사다리와 정본이 겹친다.
        ///
        /// <b>가격의 근거.</b> <c>MythicScroll</c> 은 Half 15 + Perfect 10 으로
        /// <b>스테이지당 1회성</b>이라 30스테이지를 전부 퍼펙트해도 상한이 750 이다.
        /// 킹 5종 합계 1850 은 그보다 크므로 전부 재화로 사는 것은 구조적으로 불가능하다.
        /// 가장 싼 KingFire(200)만 8스테이지쯤 전 웨이브를 깨면 닿아서
        /// "탑 30층 전에 하나 당겨오기" 가 성립한다.
        ///
        /// <b>KingFire 30층은 고정이다.</b> 기획서에 박혀 있는 값이다(5.2 목표 배너,
        /// 5.6 해금 진행도). 옮기면 그 두 화면의 문구가 같이 틀어진다.
        ///
        /// <b>요구 별은 3의 배수여야 한다.</b> 별 보상이 3별 간격으로만 존재하기 때문이고,
        /// 어긋나면 그 다이스는 그 경로로 영영 안 열린다. <see cref="Validate"/> 가 잡는다.
        ///
        /// <b>스테이지 14 는 KingThunder 다.</b> 진화 재료가 Time 인데 그 Time 은 이제
        /// 처음부터 보유라, "4성 썬더 → 타임 → 킹썬더" 가 첫 판부터 열려 있는 길이 된다.
        /// </summary>
        public static List<DiceUnlockDefinition> BuildDefaults()
        {
            return new List<DiceUnlockDefinition>
            {
                Make(DiceType.KingFire, price: 200, floor: 30),
                Make(DiceType.KingIce, price: 280, floor: 100),
                Make(DiceType.KingThunder, price: 360, stage: 14),
                Make(DiceType.KingNormal, price: 450, floor: 190),
                Make(DiceType.KingPoison, price: 560, floor: 250),
            };
        }

        private static DiceUnlockDefinition Make(
            DiceType diceType, int price, int stars = 0, int stage = 0, int floor = 0)
        {
            return new DiceUnlockDefinition
            {
                diceType = diceType,
                price = price,
                starRequirement = stars,
                stageRequirement = stage,
                towerFloor = floor,
            };
        }

        /// <summary>에디터 도구가 에셋을 처음 만들 때 쓴다.</summary>
        public void PopulateDefaults()
        {
            definitions.Clear();
            definitions.AddRange(BuildDefaults());
            RebuildMap();
        }
    }
}
