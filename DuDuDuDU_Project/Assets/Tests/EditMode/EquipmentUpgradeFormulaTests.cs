using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 장비 강화 규칙표(<c>EquipmentUpgradeFormula</c>)를 잠근다. (MIGRATION_BASELINE 5.2)
    ///
    /// <b>이 픽스처는 다른 픽스처들과 오라클이 다르다. 그 이유를 먼저 읽을 것.</b>
    ///
    /// 나머지 core.* 픽스처는 골든 파일을 유일한 기대값 원천으로 쓴다. 그래야 하는
    /// 이유는 <b>float</b> 때문이다 — AGENTS.md 가 말하듯 Mono 는 float 식의 중간 결과를
    /// 확장 정밀도로 들고 가다 대입 시점에 접어서, 어떤 재구현도(파이썬이든 C#이든)
    /// 기대값의 오라클이 될 수 없다. 값은 Mono 가 실제로 내놓은 것이어야 한다.
    ///
    /// <b>이 도메인에는 float 가 한 줄도 없다.</b> 규칙표 42리터럴도, 비용식도,
    /// 공격력식도, 슬롯 해금도 전부 int 다. int 산술은 Mono 든 CoreCLR 이든 IEEE754 든
    /// 정확히 같은 답을 내므로 확장 정밀도 함정 자체가 존재하지 않는다. 그래서 여기서는
    /// 기대값을 <b>파일 안에 리터럴 표로</b> 들고 있어도 규약 4번("부동소수 기대값을
    /// 계산해서 만들지 마라")을 어기지 않는다. 그 규약은 부동소수에 대한 것이다.
    ///
    /// <b>왜 골든만 쓰지 않는가 — 뜰 수가 없기 때문이다.</b> 골든 갱신은 사람이 플레이 중
    /// F7 로만 할 수 있고(규약 2번), 5.2 시점의 커밋된 기준선에는 <c>core.equipUpgrade.*</c>
    /// 키가 <b>한 줄도 없다.</b> 골든만 오라클로 쓰면 이 픽스처는 사람이 F7 을 누를 때까지
    /// 통째로 빨갛거나(키 없음 예외) 0건으로 조용히 통과한다. 둘 다 나쁘다.
    ///
    /// 그래서 <b>2중 오라클</b>이다:
    ///   1. 아래 리터럴 표 — <b>항상</b> 검사한다. 오늘의 실질적인 잠금장치다.
    ///   2. 골든 — <b>키가 있을 때만</b> 추가로 대조한다. F7 이후 자동으로 살아난다.
    /// 그리고 <see cref="GoldenSectionIsEitherAbsentOrComplete"/> 가 "반쯤 뜬 골든"을 막는다 —
    /// 키가 하나라도 있으면 전부 있어야 한다. 이 셋이 같이 있어야 조용한 초록불이 안 난다.
    ///
    /// <b>허용 오차는 쓰지 않는다.</b> 전부 int 상등 비교다.
    /// </summary>
    [TestFixture]
    public class EquipmentUpgradeFormulaTests
    {
        private const string Root = "core.equipUpgrade.";
        private const string RulePrefix = Root + "rule[";
        private const string GoldCostPrefix = Root + "goldCost[";
        private const string ScrollCostPrefix = Root + "scrollCost[";
        private const string AttackPrefix = Root + "attack[";
        private const string SlotUnlockPrefix = Root + "slotUnlock[";
        private const string MaxSlotKey = Root + "define.maxEquipmentSlot";
        private const string SlotLevelsKey = Root + "define.slotUnlockLevels";

        /// <summary>슬롯 범위 밖의 반환값. 원본은 <c>int.MaxValue</c> 다 — "어떤 레벨로도 못 연다".</summary>
        private const int Never = int.MaxValue;

        // ── 규칙표 (EquipmentManager.GetRule 715~734 에서 그대로 옮긴 42리터럴) ──────────
        //
        // 행 순서 = 아래 모든 표의 행 순서다. 인덱스 -1 과 6 은 default: 를 밟는 두 방향이고
        // 0~5 가 장비 6종이다. <b>-1 행과 6 행과 5(Necklace) 행이 전부 같은 값인 것이 사양이다.</b>
        private static readonly int[][] RuleTable =
        {
            //     index  baseGold  goldPerLevel  baseScroll  scrollPerLevel  baseAttack  attackPerLevel
            new[] {   -1,      100,           50,          2,              1,          2,             3 },
            new[] {    0,      120,           52,          3,              1,          2,             3 }, // Weapon
            new[] {    1,       95,           48,          2,              1,          2,             3 }, // Helmet
            new[] {    2,      100,           48,          2,              1,          2,             3 }, // Armor
            new[] {    3,      110,           50,          3,              1,          2,             3 }, // Ring
            new[] {    4,       90,           46,          2,              1,          2,             3 }, // Shoes
            new[] {    5,      100,           50,          2,              1,          2,             3 }, // Necklace
            new[] {    6,      100,           50,          2,              1,          2,             3 },
        };

        private static readonly string[] RuleFieldNames =
        {
            "baseGold", "goldPerLevel", "baseScroll", "scrollPerLevel", "baseAttack", "attackPerLevel",
        };

        // ── 레벨 격자 ────────────────────────────────────────────────────────────────
        //
        // -5 / 0 은 Mathf.Max(1, currentLevel) 하한을 밟는다. 1 은 그 하한과 <b>같은 값을
        // 내야 하는</b> 정상 최소 레벨이다 — 세 열이 같은 것이 이 격자의 요점이다.
        // 하한을 지우면 -5 열이 100 + (-6)*50 = -200 → Mathf.Max(0,·) 로 0 이 되어 갈린다.
        // 1 / 2 는 Attack 의 'level <= 1 → 0' 조기 반환 경계 양옆이다.
        private static readonly int[] Levels = { -5, 0, 1, 2, 10, 50 };

        // 행은 RuleTable 순서, 열은 Levels 순서.
        private static readonly int[][] GoldCostTable =
        {
            new[] { 100, 100, 100, 150, 550, 2550 },
            new[] { 120, 120, 120, 172, 588, 2668 },
            new[] {  95,  95,  95, 143, 527, 2447 },
            new[] { 100, 100, 100, 148, 532, 2452 },
            new[] { 110, 110, 110, 160, 560, 2560 },
            new[] {  90,  90,  90, 136, 504, 2344 },
            new[] { 100, 100, 100, 150, 550, 2550 },
            new[] { 100, 100, 100, 150, 550, 2550 },
        };

        private static readonly int[][] ScrollCostTable =
        {
            new[] { 2, 2, 2, 3, 11, 51 },
            new[] { 3, 3, 3, 4, 12, 52 },
            new[] { 2, 2, 2, 3, 11, 51 },
            new[] { 2, 2, 2, 3, 11, 51 },
            new[] { 3, 3, 3, 4, 12, 52 },
            new[] { 2, 2, 2, 3, 11, 51 },
            new[] { 2, 2, 2, 3, 11, 51 },
            new[] { 2, 2, 2, 3, 11, 51 },
        };

        // 8행이 전부 같다. 6종이 baseAttack=2 / attackPerLevel=3 로 동일하기 때문이고,
        // 그 사실 자체는 AllTypesShareTheSameAttackColumns 가 따로 잠근다.
        // 레벨 1 이 2 가 아니라 0 인 것이 조기 반환의 증거다.
        private static readonly int[][] AttackTable =
        {
            new[] { 0, 0, 0, 5, 29, 149 },
            new[] { 0, 0, 0, 5, 29, 149 },
            new[] { 0, 0, 0, 5, 29, 149 },
            new[] { 0, 0, 0, 5, 29, 149 },
            new[] { 0, 0, 0, 5, 29, 149 },
            new[] { 0, 0, 0, 5, 29, 149 },
            new[] { 0, 0, 0, 5, 29, 149 },
            new[] { 0, 0, 0, 5, 29, 149 },
        };

        // ── 슬롯 해금 ────────────────────────────────────────────────────────────────
        //
        // 0행이 실제 Define(MaxEquipmentSlot=5, {1,10,20,30,40})이고 나머지 넷은
        // <b>지금 도달 불가능한 폴백 분기 (slotIndex * 10) + 1 을 일부러 켜는</b> 합성이다.
        // 0행만 있으면 그 세 줄을 통째로 지워도 이 픽스처가 통과한다 — 실제로 확인했다.
        //
        // 1행(null)과 2행(빈 배열)의 기대값이 같은 것은 우연이 아니라 사양이다:
        // 원본은 null 을 "잠긴 슬롯"이 아니라 "표에 없는 슬롯"으로 다뤄 폴백으로 내려간다.
        // 4행은 MaxEquipmentSlot(8)이 표 길이(5)보다 큰 경우라 5~7 에서 폴백이 산다.
        private static readonly int[][] SlotTables =
        {
            new[] { 1, 10, 20, 30, 40 },
            null,
            new int[0],
            new[] { 1, 10 },
            new[] { 1, 10, 20, 30, 40 },
        };

        private static readonly int[] SlotMaxes = { 5, 5, 5, 5, 8 };

        private static readonly int[] SlotIndexes = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8 };

        private static readonly int[][] SlotExpected =
        {
            new[] { Never, 1, 10, 20, 30, 40, Never, Never, Never, Never },
            new[] { Never, 1, 11, 21, 31, 41, Never, Never, Never, Never },
            new[] { Never, 1, 11, 21, 31, 41, Never, Never, Never, Never },
            new[] { Never, 1, 10, 21, 31, 41, Never, Never, Never, Never },
            new[] { Never, 1, 10, 20, 30, 40,    51,    61,    71, Never },
        };

        // ── 케이스 소스 ───────────────────────────────────────────────────────────────
        //
        // 골든이 아니라 <b>위 표</b>에서 만든다. 골든에서 만들면 키가 아직 없는 지금
        // TestCaseSource 가 0건을 내고, 0건짜리 파라미터 테스트는 러너에 따라 조용히
        // 통과하거나 스위트오류가 된다. 어느 쪽도 이 픽스처가 원하는 상태가 아니다.

        public static IEnumerable<TestCaseData> RuleCases()
        {
            var cases = new List<TestCaseData>();
            for (int r = 0; r < RuleTable.Length; r++)
            {
                for (int f = 0; f < RuleFieldNames.Length; f++)
                {
                    int index = RuleTable[r][0];
                    string key = RulePrefix + index + "][" + RuleFieldNames[f] + "]";
                    cases.Add(new TestCaseData(index, RuleFieldNames[f], RuleTable[r][f + 1], key)
                        .SetName("rule#" + key));
                }
            }

            return cases;
        }

        private static IEnumerable<TestCaseData> LevelCases(string prefix, int[][] expectedTable)
        {
            var cases = new List<TestCaseData>();
            for (int r = 0; r < RuleTable.Length; r++)
            {
                for (int l = 0; l < Levels.Length; l++)
                {
                    int index = RuleTable[r][0];
                    string key = prefix + index + "][" + Levels[l] + "]";
                    cases.Add(new TestCaseData(index, Levels[l], expectedTable[r][l], key)
                        .SetName(prefix + "#" + key));
                }
            }

            return cases;
        }

        public static IEnumerable<TestCaseData> GoldCostCases() => LevelCases(GoldCostPrefix, GoldCostTable);
        public static IEnumerable<TestCaseData> ScrollCostCases() => LevelCases(ScrollCostPrefix, ScrollCostTable);
        public static IEnumerable<TestCaseData> AttackCases() => LevelCases(AttackPrefix, AttackTable);

        public static IEnumerable<TestCaseData> SlotUnlockCases()
        {
            var cases = new List<TestCaseData>();
            for (int t = 0; t < SlotTables.Length; t++)
            {
                for (int s = 0; s < SlotIndexes.Length; s++)
                {
                    string key = SlotUnlockKey(SlotIndexes[s], SlotMaxes[t], SlotTables[t]);
                    cases.Add(new TestCaseData(t, s, SlotExpected[t][s], key)
                        .SetName("slotUnlock#" + key.Replace(',', ';')));
                }
            }

            return cases;
        }

        // ── 표 대조 (+ 골든이 있으면 골든도) ──────────────────────────────────────────

        [TestCaseSource(nameof(RuleCases))]
        public void RuleMatchesTable(int index, string field, int expected, string key)
        {
            EquipmentUpgradeFormula.EquipmentUpgradeRule rule = EquipmentUpgradeFormula.Rule(index);
            AssertBoth(key, RuleField(rule, field), expected);
        }

        [TestCaseSource(nameof(GoldCostCases))]
        public void GoldCostMatchesTable(int index, int level, int expected, string key)
        {
            AssertBoth(key, EquipmentUpgradeFormula.UpgradeGoldCostOf(index, level), expected);
        }

        [TestCaseSource(nameof(ScrollCostCases))]
        public void ScrollCostMatchesTable(int index, int level, int expected, string key)
        {
            AssertBoth(key, EquipmentUpgradeFormula.UpgradeScrollCostOf(index, level), expected);
        }

        [TestCaseSource(nameof(AttackCases))]
        public void AttackMatchesTable(int index, int level, int expected, string key)
        {
            AssertBoth(key, EquipmentUpgradeFormula.AttackOf(index, level), expected);
        }

        [TestCaseSource(nameof(SlotUnlockCases))]
        public void SlotUnlockLevelMatchesTable(int tableIndex, int slotSlot, int expected, string key)
        {
            int actual = EquipmentUpgradeFormula.SlotUnlockLevel(
                SlotIndexes[slotSlot], SlotMaxes[tableIndex], SlotTables[tableIndex]);

            AssertBoth(key, actual, expected);
        }

        // ── 3인자 함수도 직접 두드린다 ────────────────────────────────────────────────
        //
        // 위 케이스는 전부 *Of 편의 오버로드를 지난다. 그것만 검사하면 표를 뽑는 부분과
        // 계산하는 부분이 한 덩어리로 묶여서, 계산식만 따로 잘못 고쳤을 때 어디가
        // 틀렸는지 안 보인다. 3인자 원형을 같은 값으로 한 번 더 밟는다.

        [Test]
        public void RawCostFunctionsAgreeWithRuleTableLookups()
        {
            for (int r = 0; r < RuleTable.Length; r++)
            {
                int index = RuleTable[r][0];
                for (int l = 0; l < Levels.Length; l++)
                {
                    Assert.That(
                        EquipmentUpgradeFormula.UpgradeGoldCost(RuleTable[r][1], RuleTable[r][2], Levels[l]),
                        Is.EqualTo(GoldCostTable[r][l]),
                        "UpgradeGoldCost index=" + index + " level=" + Levels[l]);

                    Assert.That(
                        EquipmentUpgradeFormula.UpgradeScrollCost(RuleTable[r][3], RuleTable[r][4], Levels[l]),
                        Is.EqualTo(ScrollCostTable[r][l]),
                        "UpgradeScrollCost index=" + index + " level=" + Levels[l]);

                    Assert.That(
                        EquipmentUpgradeFormula.Attack(RuleTable[r][5], RuleTable[r][6], Levels[l]),
                        Is.EqualTo(AttackTable[r][l]),
                        "Attack index=" + index + " level=" + Levels[l]);
                }
            }
        }

        // ── 구조적 사실 (값이 아니라 표의 모양을 잠근다) ──────────────────────────────

        /// <summary>
        /// 6종이 <c>baseAttack=2</c> / <c>attackPerLevel=3</c> / <c>scrollPerLevel=1</c> 로
        /// 전부 같다는 조사 결과의 박제.
        ///
        /// <b>이 테스트가 빨개지는 것은 버그가 아니라 사양 변경 신호다.</b> 장비 하나만
        /// 공격력 곡선을 다르게 하는 날 여기가 먼저 터져서, 그때 AttackTable 8행이
        /// 전부 같아도 되는지 다시 생각하게 만든다. 없으면 표만 조용히 고치고 지나간다.
        /// </summary>
        [Test]
        public void AllTypesShareTheSameAttackColumns()
        {
            int[] indexes =
            {
                EquipmentUpgradeFormula.WeaponIndex, EquipmentUpgradeFormula.HelmetIndex,
                EquipmentUpgradeFormula.ArmorIndex, EquipmentUpgradeFormula.RingIndex,
                EquipmentUpgradeFormula.ShoesIndex, EquipmentUpgradeFormula.NecklaceIndex,
            };

            for (int i = 0; i < indexes.Length; i++)
            {
                EquipmentUpgradeFormula.EquipmentUpgradeRule rule = EquipmentUpgradeFormula.Rule(indexes[i]);
                Assert.That(rule.baseAttack, Is.EqualTo(2), "baseAttack index=" + indexes[i]);
                Assert.That(rule.attackPerLevel, Is.EqualTo(3), "attackPerLevel index=" + indexes[i]);
                Assert.That(rule.scrollPerLevel, Is.EqualTo(1), "scrollPerLevel index=" + indexes[i]);
            }
        }

        /// <summary>
        /// Necklace 행과 default 행은 6필드가 전부 같다 — 즉 Necklace 케이스를 지우는 변이는
        /// <b>어떤 입력으로도 관측되지 않는다.</b> 골든이 못 잡는 등가 변이라 여기 적어 둔다.
        ///
        /// 이 테스트는 그 사실이 <b>계속 사실인지</b>만 본다. 목걸이 값을 따로 조정하는 날
        /// 여기가 터지고, 그때 "이제 두 행이 갈렸다"를 알게 된다.
        /// </summary>
        [Test]
        public void NecklaceRowIsIndistinguishableFromDefaultRow()
        {
            EquipmentUpgradeFormula.EquipmentUpgradeRule necklace =
                EquipmentUpgradeFormula.Rule(EquipmentUpgradeFormula.NecklaceIndex);
            EquipmentUpgradeFormula.EquipmentUpgradeRule unknown =
                EquipmentUpgradeFormula.Rule(EquipmentUpgradeFormula.UnknownIndex);

            Assert.That(Describe(necklace), Is.EqualTo(Describe(unknown)),
                "두 행이 갈렸다. AttackTable/GoldCostTable 의 마지막 두 행과 -1 행을 다시 볼 것.");
        }

        /// <summary>
        /// 인덱스 상수 6개가 서로 다르고 <see cref="EquipmentUpgradeFormula.UnknownIndex"/> 와도
        /// 겹치지 않는지. <c>Rule</c> 의 switch 가 이 distinctness 에 통째로 기대고 있다 —
        /// 둘이 같으면 C# 이 컴파일 에러를 내지만, 상수를 나중에 필드로 바꾸면 그 방어가 사라진다.
        /// </summary>
        [Test]
        public void RuleIndexConstantsAreDistinct()
        {
            int[] all =
            {
                EquipmentUpgradeFormula.WeaponIndex, EquipmentUpgradeFormula.HelmetIndex,
                EquipmentUpgradeFormula.ArmorIndex, EquipmentUpgradeFormula.RingIndex,
                EquipmentUpgradeFormula.ShoesIndex, EquipmentUpgradeFormula.NecklaceIndex,
                EquipmentUpgradeFormula.UnknownIndex,
            };

            var seen = new HashSet<int>();
            for (int i = 0; i < all.Length; i++)
                Assert.That(seen.Add(all[i]), Is.True, "인덱스 상수가 겹친다: " + all[i]);
        }

        /// <summary>
        /// 레벨 하한이 실제로 <b>물리는지</b>. -5 / 0 / 1 이 같은 비용을 내야 한다.
        /// 위 표가 이미 이 값을 들고 있지만, 표는 "세 값이 같다"는 <i>의도</i>를 말하지 않는다.
        /// 하한을 지우는 변이는 표에서도 잡히지만 여기서 <b>이름으로</b> 잡힌다.
        /// </summary>
        [Test]
        public void LevelIsClampedToOneBelowTheLowerBound()
        {
            for (int r = 0; r < RuleTable.Length; r++)
            {
                int index = RuleTable[r][0];
                int atOne = EquipmentUpgradeFormula.UpgradeGoldCostOf(index, 1);

                Assert.That(EquipmentUpgradeFormula.UpgradeGoldCostOf(index, 0), Is.EqualTo(atOne), "level 0, index " + index);
                Assert.That(EquipmentUpgradeFormula.UpgradeGoldCostOf(index, -5), Is.EqualTo(atOne), "level -5, index " + index);
                Assert.That(EquipmentUpgradeFormula.UpgradeGoldCostOf(index, int.MinValue), Is.EqualTo(atOne), "level MinValue, index " + index);
            }
        }

        // ── 골든 격자 밖: 오버플로에서만 드러나는 두 가지 ──────────────────────────────
        //
        // 위 격자(레벨 -5 ~ 50)로는 <b>바깥 Mathf.Max(0, ·) 를 통째로 지워도 한 건도 안
        // 걸린다.</b> 실제로 변이를 심어 확인했다 — 골드·두루마리·공격력 셋 다 0건이었다.
        // 그 하한이 물리는 자리는 곱셈항이 int 를 넘치는 지점이고, 그건 레벨 4천만 근처라
        // 골든 격자에 넣을 만한 값이 아니다(키가 흉해지고 사람이 못 읽는다). 그래서
        // <b>골든 키를 만들지 않는 별도 테스트</b>로 여기서만 밟는다 — 아래 두 테스트는
        // core.equipUpgrade.* 키를 하나도 늘리지 않으므로 244 == 244 대응이 그대로 유지된다.
        //
        // 아래 숫자는 전부 <b>Mono 러너가 실제로 내놓은 것</b>이다. 컴파일된 OJ.Core.dll 을
        // Unity 의 MonoBleedingEdge 에서 호출해 "처음으로 음수가 되는 레벨"과 그때의
        // 반환값을 뽑았다. 손으로 계산한 값이 아니다.

        /// <summary>행: index, 골드가 처음 음수가 되는 레벨, 두루마리, 공격력.</summary>
        private static readonly int[][] OverflowClampLevels =
        {
            //     index  gold        scroll        attack
            new[] {   -1, 42949672, 2147483647, 715827883 },
            new[] {    0, 41297762, 2147483646, 715827883 },
            new[] {    1, 44739242, 2147483647, 715827883 },
            new[] {    2, 44739242, 2147483647, 715827883 },
            new[] {    3, 42949672, 2147483646, 715827883 },
            new[] {    4, 46684427, 2147483647, 715827883 },
            new[] {    5, 42949672, 2147483647, 715827883 },
            new[] {    6, 42949672, 2147483647, 715827883 },
        };

        /// <summary>
        /// 곱셈항이 int 를 넘쳐 합이 음수가 되는 레벨에서 <b>바깥 <c>Mathf.Max(0, ·)</c> 가
        /// 실제로 0 으로 자르는지</b>. 이 하한을 지우는 변이는 위 격자로는 한 건도 안 잡힌다.
        ///
        /// 이 테스트가 빨개지는 경우는 둘이다: 하한을 지웠거나, 표의 <c>*PerLevel</c> 값이
        /// 바뀌어 오버플로 지점이 옮겨 갔거나. 후자면 위 표를 다시 뽑아 넣을 것 —
        /// 단 <b>Mono 에서 실행해서</b> 뽑아야 한다.
        /// </summary>
        [Test]
        public void OuterZeroClampBitesWhenTheLevelTermOverflows()
        {
            for (int r = 0; r < OverflowClampLevels.Length; r++)
            {
                int index = OverflowClampLevels[r][0];

                Assert.That(EquipmentUpgradeFormula.UpgradeGoldCostOf(index, OverflowClampLevels[r][1]),
                    Is.EqualTo(0), "골드 바깥 하한이 안 물렸다. index=" + index + " level=" + OverflowClampLevels[r][1]);
                Assert.That(EquipmentUpgradeFormula.UpgradeScrollCostOf(index, OverflowClampLevels[r][2]),
                    Is.EqualTo(0), "두루마리 바깥 하한이 안 물렸다. index=" + index + " level=" + OverflowClampLevels[r][2]);
                Assert.That(EquipmentUpgradeFormula.AttackOf(index, OverflowClampLevels[r][3]),
                    Is.EqualTo(0), "공격력 바깥 하한이 안 물렸다. index=" + index + " level=" + OverflowClampLevels[r][3]);
            }
        }

        /// <summary>행: index, 두 오버로드가 처음 갈리는 레벨, 그때 <b>int 오버로드</b>의 값.</summary>
        private static readonly int[][] IntOverloadSplit =
        {
            //     index  level      int 오버로드 값
            new[] {   -1,  671088, 33554450 },
            new[] {    0, 1290554, 67108876 },
            new[] {    1,  349525, 16777247 },
            new[] {    2, 1398101, 67108900 },
            new[] {    3,  671089, 33554510 },
            new[] {    4,  729445, 33554514 },
            new[] {    5,  671088, 33554450 },
            new[] {    6,  671088, 33554450 },
        };

        /// <summary>
        /// <c>Mathf.Max(0, ·)</c> 가 <b>int 오버로드</b>로 묶여 있는지. 0 을 <c>0f</c> 로
        /// 쓰면 float 오버로드로 넘어가고, 2^24 위에서 값이 조용히 어긋난다
        /// (예: index 1, level 349,525 → int 16,777,247 vs float 16,777,248).
        ///
        /// 파일 머리말이 "리터럴에 f 를 붙이지 마라"고 적어 두었지만, 격자가 레벨 50 까지라
        /// 그 경고를 어기는 변이가 <b>한 건도 안 잡혔다.</b> 이 테스트가 그 자리를 막는다.
        /// float 가 개입한 것이 아니라 <b>정수 결과를 float 로 왕복시키면 깨진다</b>는
        /// 사실을 잠그는 것이므로 AGENTS.md 의 부동소수 규약과 충돌하지 않는다.
        /// </summary>
        [Test]
        public void MathfMaxStaysOnTheIntOverloadAboveTwoToTheTwentyFourth()
        {
            for (int r = 0; r < IntOverloadSplit.Length; r++)
            {
                int index = IntOverloadSplit[r][0];
                int level = IntOverloadSplit[r][1];
                int expected = IntOverloadSplit[r][2];

                Assert.That(EquipmentUpgradeFormula.UpgradeGoldCostOf(index, level), Is.EqualTo(expected),
                    "index=" + index + " level=" + level + " — float 오버로드로 넘어갔을 가능성이 있다.");

                // 같은 값을 float 로 왕복시키면 달라진다는 것 자체를 박아 둔다.
                // 이 줄이 실패하면 위 표가 더 이상 경계를 밟지 못하고 있다는 뜻이다.
                Assert.That((int)(float)expected, Is.Not.EqualTo(expected),
                    "index=" + index + " level=" + level + " 이 더 이상 float 왕복 경계가 아니다. 표를 다시 뽑을 것.");
            }
        }

        // ── 골든 연동 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 골든에 <c>core.equipUpgrade.*</c> 키가 하나라도 있으면 <b>전부</b> 있어야 하고,
        /// 전부 이 픽스처가 두드리는 키여야 한다.
        ///
        /// 두 방향을 다 본다:
        ///  - 골든에 있는데 이 픽스처가 안 읽는 키 → 덤퍼가 구획을 늘렸는데 테스트가 안 따라온 것
        ///  - 이 픽스처가 기대하는데 골든에 없는 키 → 덤퍼가 구획을 줄였거나 반쯤 뜬 것
        ///
        /// <b>키가 0개면 통과한다.</b> 그것은 "아직 F7 을 안 눌렀다"는 정상 상태다 —
        /// 규약상 골든 갱신은 사람만 할 수 있고, 그때까지 실질적인 잠금은 위 리터럴 표가 한다.
        /// 0개 통과가 위험한 유일한 경우는 "표까지 같이 사라진" 때인데, 그건 러너의
        /// minTests 가 잡는다(headless.config.json).
        /// </summary>
        [Test]
        public void GoldenSectionIsEitherAbsentOrComplete()
        {
            var golden = new List<string>();
            foreach (var pair in GoldenBaseline.Section(GoldenBaseline.StableSection))
            {
                if (pair.Key.StartsWith(Root, StringComparison.Ordinal))
                    golden.Add(pair.Key);
            }

            if (golden.Count == 0)
            {
                Assert.Pass(
                    "골든에 " + Root + "* 키가 없다. 사람이 플레이 중 F7 로 기준선을 다시 뜨면 " +
                    "이 검사가 자동으로 살아난다. 그때까지 잠금은 이 파일의 리터럴 표가 한다.");
                return;
            }

            var expected = new HashSet<string>(ExpectedKeys(), StringComparer.Ordinal);
            var actual = new HashSet<string>(golden, StringComparer.Ordinal);

            Assert.That(Missing(expected, actual), Is.EqualTo(string.Empty),
                "이 픽스처가 기대하는데 골든에 없는 키다. 덤퍼 DumpEquipmentUpgrade 가 줄었거나 " +
                "Define.EquipmentSlotUnlockLevels / MaxEquipmentSlot 이 바뀌었다.");
            Assert.That(Missing(actual, expected), Is.EqualTo(string.Empty),
                "골든에 있는데 이 픽스처가 안 읽는 키다. 덤퍼가 구획을 늘렸으면 표도 늘릴 것.");
        }

        /// <summary>
        /// 골든의 <c>define.*</c> 두 줄이 <c>slotUnlock</c> 키에 박힌 표와 같은지.
        ///
        /// 두 곳은 같은 <c>Define</c> 을 읽으므로 항상 같아야 한다. 어긋나면 덤퍼가 두
        /// 값을 서로 다른 시점/출처에서 읽고 있다는 뜻이다. OJ.Core.Tests 는
        /// Assembly-CSharp 을 못 봐서 <c>Define</c> 자체를 검증할 수 없고, 이 일치성이
        /// 유일하게 확인 가능한 것이다.
        /// </summary>
        [Test]
        public void GoldenDefineSnapshotAgreesWithSlotUnlockKeys()
        {
            var section = GoldenBaseline.Section(GoldenBaseline.StableSection);

            string maxText;
            string levelsText;
            if (!section.TryGetValue(MaxSlotKey, out maxText) ||
                !section.TryGetValue(SlotLevelsKey, out levelsText))
            {
                Assert.Pass("골든에 " + Root + "define.* 이 없다. F7 이후 살아난다.");
                return;
            }

            // 0행이 실제 Define 을 쓰는 행이다.
            Assert.That(maxText, Is.EqualTo(SlotMaxes[0].ToString(CultureInfo.InvariantCulture)),
                MaxSlotKey + " 가 이 픽스처의 SlotMaxes[0] 과 다르다. Define.MaxEquipmentSlot 이 바뀌었다.");
            Assert.That(levelsText, Is.EqualTo(DescribeSlotLevels(SlotTables[0])),
                SlotLevelsKey + " 가 이 픽스처의 SlotTables[0] 과 다르다. " +
                "Define.EquipmentSlotUnlockLevels 가 바뀌었다.");
        }

        /// <summary>
        /// 접두사가 다른 픽스처를 잡아먹지 않는지. <c>StartsWith</c> 로 훑기 때문에
        /// <c>core.equip…</c> 이 <c>core.equipment…</c> 같은 이름을 삼키는 사고가 가능하다.
        /// </summary>
        [Test]
        public void PrefixDoesNotShadowOtherFixtures()
        {
            string[] others =
            {
                "core.crit.", "core.damage", "core.dmgChain", "core.incoming.",
                "core.stageBonus", "core.guaranteedGold", "core.clearGradeTier", "core.scaleAmount",
                "core.capped", "core.clearCount", "core.progress01", "core.meatSets",
                "core.secondsUntilNextMeatSet", "core.elapsedSeconds", "core.bossHp", "core.bossDefense",
                "core.bossSpawnThreshold", "core.resolvedBaseDefense", "core.monsterHp", "core.monsterDefense",
            };

            for (int i = 0; i < others.Length; i++)
            {
                Assert.That(others[i].StartsWith(Root, StringComparison.Ordinal), Is.False,
                    Root + " 가 " + others[i] + " 를 잡아먹는다.");
                Assert.That(Root.StartsWith(others[i], StringComparison.Ordinal), Is.False,
                    others[i] + " 가 " + Root + " 를 잡아먹는다.");
            }
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────────────

        /// <summary>표 기대값과 대조하고, 골든에 같은 키가 있으면 <b>추가로</b> 대조한다.</summary>
        private static void AssertBoth(string key, int actual, int expected)
        {
            Assert.That(actual, Is.EqualTo(expected), key + " (리터럴 표)");

            string raw;
            if (!GoldenBaseline.Section(GoldenBaseline.StableSection).TryGetValue(key, out raw))
                return;

            Assert.That(actual, Is.EqualTo(int.Parse(raw, CultureInfo.InvariantCulture)), key + " (골든)");
        }

        private static int RuleField(EquipmentUpgradeFormula.EquipmentUpgradeRule rule, string field)
        {
            switch (field)
            {
                case "baseGold": return rule.baseGold;
                case "goldPerLevel": return rule.goldPerLevel;
                case "baseScroll": return rule.baseScroll;
                case "scrollPerLevel": return rule.scrollPerLevel;
                case "baseAttack": return rule.baseAttack;
                case "attackPerLevel": return rule.attackPerLevel;
                default:
                    Assert.Fail("모르는 필드다: " + field);
                    return 0;
            }
        }

        private static string Describe(EquipmentUpgradeFormula.EquipmentUpgradeRule rule)
        {
            return rule.baseGold + "/" + rule.goldPerLevel + "/" + rule.baseScroll + "/" +
                   rule.scrollPerLevel + "/" + rule.baseAttack + "/" + rule.attackPerLevel;
        }

        /// <summary>덤퍼의 <c>DescribeSlotLevels</c> 와 <b>같은 규칙</b>이어야 한다. 키가 여기서 만들어진다.</summary>
        private static string DescribeSlotLevels(int[] levels)
        {
            if (levels == null)
                return "null";
            if (levels.Length == 0)
                return "empty";

            var text = new StringBuilder();
            for (int i = 0; i < levels.Length; i++)
            {
                if (i > 0)
                    text.Append(',');
                text.Append(levels[i].ToString(CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        private static string SlotUnlockKey(int slotIndex, int maxSlot, int[] levels)
        {
            return SlotUnlockPrefix + slotIndex + "][" + maxSlot + "][" + DescribeSlotLevels(levels) + "]";
        }

        /// <summary>이 픽스처가 두드리는 골든 키 전부. define 두 줄까지 포함한다.</summary>
        private static IEnumerable<string> ExpectedKeys()
        {
            var keys = new List<string> { MaxSlotKey, SlotLevelsKey };

            for (int r = 0; r < RuleTable.Length; r++)
            {
                int index = RuleTable[r][0];
                for (int f = 0; f < RuleFieldNames.Length; f++)
                    keys.Add(RulePrefix + index + "][" + RuleFieldNames[f] + "]");

                for (int l = 0; l < Levels.Length; l++)
                {
                    keys.Add(GoldCostPrefix + index + "][" + Levels[l] + "]");
                    keys.Add(ScrollCostPrefix + index + "][" + Levels[l] + "]");
                    keys.Add(AttackPrefix + index + "][" + Levels[l] + "]");
                }
            }

            for (int t = 0; t < SlotTables.Length; t++)
            {
                for (int s = 0; s < SlotIndexes.Length; s++)
                    keys.Add(SlotUnlockKey(SlotIndexes[s], SlotMaxes[t], SlotTables[t]));
            }

            return keys;
        }

        private static string Missing(IEnumerable<string> wanted, ICollection<string> have)
        {
            var missed = new List<string>();
            foreach (string key in wanted)
            {
                if (!have.Contains(key))
                    missed.Add(key);
            }

            missed.Sort(StringComparer.Ordinal);
            return string.Join(", ", missed.ToArray());
        }
    }
}
