using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>
    /// DamageFormula.Calculate 의 골든 특성화 테스트. (MIGRATION_BASELINE 3.3)
    ///
    /// 이 테스트의 목적은 "식이 옳은지"가 아니라 "식이 어제와 같은지"다.
    /// Tests/Golden/formula_baseline.txt 의 core.damage 줄이 정본이고, 여기서는
    /// 그 숫자를 절대 고치지 않는다. 값이 갈리면 그건 테스트 버그가 아니라 발견이다.
    ///
    /// 왜 인자를 손으로 옮겨 적는가:
    /// stage/idle 쪽 골든 키는 core.scaleAmount[20][0.5] 처럼 인자가 키에 박혀 있어 파싱만 하면
    /// 되지만, core.damage 는 키에 케이스 이름만 있다(core.damage[king]). 인자가 14개짜리
    /// 구조체라 키에 넣을 수 없었기 때문이다. 그래서 입력은 덤퍼
    /// Assets/Scripts/SceneFlow/GoldenBaselineDumper.cs 의 DumpDamageCase 호출부를
    /// 옮겨 적는 것 말고는 복원할 방법이 없다.
    ///
    /// 옮겨 적은 값이 덤퍼와 갈리는 순간 이 테스트는 "다른 입력의 결과를 옛 기대값과 비교하는"
    /// 무의미한 테스트가 된다. 그래서 아래 Case(...) 는 시그니처(인자 순서·이름·기본값)를
    /// DumpDamageCase 와 똑같이 맞춰 두었다. 호출 7줄은 덤퍼의 호출 7줄에서 맨 앞 sb 인자만
    /// 뺀 <b>글자 그대로의 사본</b>이어야 한다. 덤퍼를 고치면 이 파일도 같이 고쳐야 한다.
    ///
    /// 허용 오차를 두지 않는 이유: 반환값이 int 이고 마지막 단계가 Mathf.RoundToInt 다.
    /// 중간 float 가 1 ULP 만 흔들려도 0.5 경계에서 정수가 1 바뀌고, 그 1 은 플레이어에게
    /// 그대로 보이는 회귀다.
    ///
    /// 주의 — 이름과 달리 halfBoundary 케이스는 .5 경계가 아니다. 반올림 전 값이 8.25 라
    /// 은행가 반올림·올림·내림이 전부 8 을 낸다. 골든 7개 중 .5 에 정확히 걸리는 것은
    /// neutral 과 clampedInputs 둘뿐이고 둘 다 7.5 인데, 7.5 는 정수부가 홀수라
    /// 은행가 반올림도 8, (int)(x + 0.5f) 도 8 이다. 두 규칙이 같은 답을 내므로
    /// <b>골든 7개만으로는 반올림 규칙이 바뀐 것을 못 잡는다</b> — Mathf.RoundToInt 를
    /// (int)(scaled + 0.5f) 나 Math.Round(..., MidpointRounding.AwayFromZero) 로 바꿔도
    /// 7개가 전부 통과한다(변이 검사로 확인함). 그 구멍은 아래
    /// RoundingIsHalfToEven_NotHalfUp 이 막는다. 케이스 이름 자체는 골든 키라 못 고친다.
    /// </summary>
    [TestFixture]
    public class DamageFormulaTests
    {
        /// <summary>내 담당 접두사. 이 접두사로 시작하는 골든 키는 한 줄도 남기지 않고 소비한다.</summary>
        private const string KeyPrefix = "core.damage";

        [TestCaseSource(nameof(DamageCases))]
        public void Calculate_MatchesGoldenBaseline(string caseName, DamageInputs inputs)
        {
            string key = GoldenKey(caseName);
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);
            int actual = DamageFormula.Calculate(inputs);

            // 실패 메시지에 키를 박아 두는 이유: 케이스 이름만 보면 어느 골든 줄이 깨졌는지
            // 파일을 뒤져야 한다. 깨진 줄을 바로 열 수 있게 키와 파일 경로를 같이 준다.
            string message =
                "골든이 깨졌다: " + key + Environment.NewLine +
                "기준선 " + expected + " → 지금 " + actual + Environment.NewLine +
                "기준선 파일: " + GoldenBaseline.FilePath + Environment.NewLine +
                "기준선을 지금 값으로 고치지 말 것. 계산식(또는 덤퍼 인자)이 왜 바뀌었는지가 먼저다.";

            Assert.That(actual, Is.EqualTo(expected), message);
        }

        /// <summary>
        /// 케이스 수와 골든 키 수가 같은지 센다.
        /// 케이스를 지우거나 덤퍼에 케이스가 늘었는데 여기 안 옮겨 적으면, 나머지 케이스가
        /// 전부 통과해서 "초록불인데 사실 절반만 본" 상태가 된다. 그 조용한 구멍을 막는 테스트다.
        /// </summary>
        [Test]
        public void CaseCountMatchesGoldenKeyCount()
        {
            var golden = GoldenBaseline.Keys(GoldenBaseline.StableSection, KeyPrefix);
            var covered = CoveredKeys();

            string message =
                "'" + KeyPrefix + "' 골든 키는 " + golden.Count + "개인데 테스트 케이스는 " +
                covered.Count + "개다." + Environment.NewLine +
                "골든 키: " + string.Join(", ", SortedKeys(golden)) + Environment.NewLine +
                "테스트 키: " + string.Join(", ", covered) + Environment.NewLine +
                "덤퍼 GoldenBaselineDumper.DumpCoreFormulas 의 DumpDamageCase 호출부와 " +
                "이 파일의 Cases() 를 다시 맞출 것.";

            Assert.That(covered.Count, Is.EqualTo(golden.Count), message);
        }

        /// <summary>
        /// 개수만 같고 이름이 엇갈리는 경우(케이스 하나를 지우고 하나를 새로 넣는 등)를 잡는다.
        /// 개수 검사만으로는 통과해 버리므로 양방향으로 대조한다.
        /// </summary>
        [Test]
        public void EveryGoldenKeyIsConsumed()
        {
            var golden = GoldenBaseline.Keys(GoldenBaseline.StableSection, KeyPrefix);
            var covered = CoveredKeys();
            var coveredSet = new HashSet<string>(covered, StringComparer.Ordinal);

            var missing = new List<string>();
            foreach (string key in SortedKeys(golden))
            {
                if (!coveredSet.Contains(key))
                    missing.Add(key);
            }

            var unknown = new List<string>();
            foreach (string key in covered)
            {
                if (!golden.ContainsKey(key))
                    unknown.Add(key);
            }

            Assert.That(missing, Is.Empty,
                "골든에는 있는데 아무 테스트도 두드리지 않는 키: " + string.Join(", ", missing) +
                Environment.NewLine + "덤퍼의 DumpDamageCase 호출을 Cases() 에 옮겨 적을 것.");

            Assert.That(unknown, Is.Empty,
                "테스트만 알고 골든에는 없는 키: " + string.Join(", ", unknown) +
                Environment.NewLine + "케이스 이름 오타이거나, 덤퍼에서 빠진 케이스다. " +
                "기준선은 손으로 고치지 말고 덤퍼를 다시 돌려 뜰 것.");
        }

        /// <summary>
        /// 마지막 반올림이 은행가 반올림(half-to-even)인지 직접 못 박는다.
        ///
        /// 왜 골든 케이스로는 안 되는가: 위 7개 중 .5 에 걸리는 것은 7.5 짜리 둘뿐이고,
        /// 7.5 는 정수부가 홀수라 은행가 반올림과 반올림-올림이 둘 다 8 을 낸다.
        /// 두 규칙이 갈리려면 정수부가 <b>짝수</b>인 .5(8.5 / 10.5 / 16.5)가 필요하다.
        /// 그런 입력이 골든에 없어서 규칙 교체가 무사통과하던 구멍을 여기서 막는다.
        ///
        /// 기대값은 새로 지어낸 것이 아니라 DamageFormula 주석이 명시한 계약이다.
        /// 특히 16.5 → 16 은 기준선의 damage[Normal][pip=1][lv=6] 에 박혀 있는 바로 그
        /// 경계다. 이 픽스처는 Assembly-CSharp 을 못 봐서 그 줄을 직접 읽을 수 없으므로,
        /// 같은 규칙을 순수 함수 쪽에서 대신 지킨다.
        ///
        /// 골든 키를 쓰지 않으므로 CoveredKeys() 에는 넣지 않는다 — 넣는 순간
        /// EveryGoldenKeyIsConsumed 가 "골든에 없는 키"라고 터진다.
        ///
        /// 입력은 BaseAttack 만 남기고 전부 중립이라(pip=1, lv=1, 증가치 0)
        /// 반올림 전 값이 정확히 BaseAttack * 0.5f 다. 0.5f 는 2의 거듭제곱이라
        /// 이 곱셈에는 오차가 없고, 뒤따르는 *1f / +0 은 비트를 보존한다.
        /// </summary>
        [Test]
        public void RoundingIsHalfToEven_NotHalfUp()
        {
            // { BaseAttack, 기대 반환값 } — 반올림 전 값은 BaseAttack / 2.0 다.
            int[][] midpoints =
            {
                new[] { 15, 8 },   //  7.5 → 8 : 내림(7)만 잡는다. 올림도 8 이다.
                new[] { 17, 8 },   //  8.5 → 8 : 올림이면 9. 규칙 교체를 잡는 핵심.
                new[] { 19, 10 },  //  9.5 → 10: 내림이면 9.
                new[] { 21, 10 },  // 10.5 → 10: 올림이면 11.
                new[] { 33, 16 },  // 16.5 → 16: 기준선 damage[Normal][pip=1][lv=6] 과 같은 경계.
            };

            var mismatches = new List<string>();
            foreach (int[] row in midpoints)
            {
                int baseAttack = row[0];
                int expected = row[1];
                int actual = DamageFormula.Calculate(Case("midpoint", baseAttack, 0, 1, 1).Value);

                if (actual != expected)
                {
                    mismatches.Add(
                        "반올림 전 " + ScaledText(baseAttack) +
                        " → 은행가 반올림이면 " + expected + " 인데 지금 " + actual);
                }
            }

            string message =
                "반올림 규칙이 바뀌었다(은행가 반올림 → 다른 규칙):" + Environment.NewLine +
                string.Join(Environment.NewLine, mismatches) + Environment.NewLine +
                "DamageFormula 마지막 줄이 Mathf.RoundToInt 인지 확인할 것. " +
                "(int)(scaled + 0.5f) 나 System.Math.Round(AwayFromZero) 로 바뀌면 " +
                ".5 가 전부 위로 올라가 기준선이 깨진다.";

            Assert.That(mismatches, Is.Empty, message);
        }

        /// <summary>
        /// 반올림 전 값을 문자열로 만든다. float 를 ToString 하면 로케일에 따라
        /// "8,5" 가 나올 수 있어 정수 연산만으로 찍는다.
        /// </summary>
        private static string ScaledText(int baseAttack)
        {
            return (baseAttack / 2) + (((baseAttack % 2) == 0) ? ".0" : ".5");
        }

        private static string GoldenKey(string caseName)
        {
            return KeyPrefix + "[" + caseName + "]";
        }

        /// <summary>이 픽스처가 실제로 검증하는 골든 키 목록. 이름이 겹치면 한 케이스가
        /// 다른 케이스를 가려 개수 검사를 통과해 버리므로 그 자리에서 터뜨린다.</summary>
        private static List<string> CoveredKeys()
        {
            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pair in Cases())
            {
                string key = GoldenKey(pair.Key);
                if (!seen.Add(key))
                {
                    throw new InvalidOperationException(
                        "테스트 케이스 이름이 중복이다: " + key + " — Cases() 를 확인할 것.");
                }

                keys.Add(key);
            }

            return keys;
        }

        private static List<string> SortedKeys(IReadOnlyDictionary<string, string> section)
        {
            var keys = new List<string>();
            foreach (var pair in section)
                keys.Add(pair.Key);

            // 사전 순회 순서는 보장되지 않는다. 실패 메시지를 diff 하려면 순서가 고정돼야 한다.
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        private static IEnumerable<TestCaseData> DamageCases()
        {
            foreach (var pair in Cases())
            {
                // 러너 목록에 구조체 덤프 대신 골든 키가 그대로 뜨게 한다.
                yield return new TestCaseData(pair.Key, pair.Value)
                    .SetName("Calculate_MatchesGoldenBaseline(" + GoldenKey(pair.Key) + ")");
            }
        }

        /// <summary>
        /// ↓ 여기부터 7줄은 GoldenBaselineDumper.DumpCoreFormulas 의 DumpDamageCase 호출부
        /// 사본이다. sb 인자만 빠졌고 나머지는 글자 그대로 같아야 한다. 숫자 하나라도
        /// 손대는 순간 이 파일은 골든과 다른 식을 검사하게 된다.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, DamageInputs>> Cases()
        {
            yield return Case("neutral", 12, 3, 1, 1);
            yield return Case("pip6lv12", 12, 3, 6, 12);
            yield return Case("king", 104, 20, 3, 9, isKing: true, levelMul: 1.3f, kingSynergy: 1.2f);
            yield return Case("equipped", 10, 4, 4, 6,
                equipmentAttack: 37, attackPercent: 0.25f, attackFlat: 12,
                earlyWaveFlat: 5, finalPercent: 0.1f, elementMul: 1.4f, relicMul: 1.15f);
            yield return Case("halfBoundary", 15, 0, 1, 6, levelMul: 1.1f);
            yield return Case("zeroAttack", 0, 0, 1, 1);
            yield return Case("clampedInputs", 12, 3, -5, -5);
        }

        /// <summary>
        /// DumpDamageCase 와 인자 순서·이름·기본값을 똑같이 맞춘 사본이다(sb 만 없다).
        /// 시그니처를 맞춰 둬야 위의 호출 7줄을 덤퍼에서 그대로 복사해 붙일 수 있고,
        /// 이름 있는 인자가 엉뚱한 필드로 흘러드는 사고도 안 난다. 필드 대입 순서까지
        /// 덤퍼와 같게 두었다 — 덤퍼와 이 파일을 나란히 놓고 눈으로 대조하기 위해서다.
        /// </summary>
        private static KeyValuePair<string, DamageInputs> Case(
            string name,
            int baseAttack,
            int levelUpAttackIncrease,
            int dicePip,
            int bulletLevel,
            int equipmentAttack = 0,
            float levelMul = 1f,
            float kingSynergy = 1f,
            bool isKing = false,
            float attackPercent = 0f,
            int attackFlat = 0,
            int earlyWaveFlat = 0,
            float finalPercent = 0f,
            float elementMul = 1f,
            float relicMul = 1f)
        {
            var inputs = new DamageInputs
            {
                BaseAttack = baseAttack,
                LevelUpAttackIncrease = levelUpAttackIncrease,
                DicePip = dicePip,
                BulletLevel = bulletLevel,
                EquipmentAttackTotal = equipmentAttack,
                LevelDamageMultiplier = levelMul,
                KingSynergyMultiplier = kingSynergy,
                IsKingDice = isKing,
                AttackPercentBonus = attackPercent,
                AttackFlatBonus = attackFlat,
                EarlyWaveFlatBonus = earlyWaveFlat,
                FinalDamagePercentBonus = finalPercent,
                ElementUpgradeMultiplier = elementMul,
                RelicDamageMultiplier = relicMul,
            };

            return new KeyValuePair<string, DamageInputs>(name, inputs);
        }
    }

    /// <summary>
    /// <c>core.dmgChain</c> 324키를 소비한다. (MIGRATION_BASELINE 5.1-b)
    ///
    /// 왜 이 픽스처가 생겼는가 — 5.0-b 가 골든에 <c>core.dmgChain</c> 324줄을 넣었지만
    /// <b>그것을 읽는 테스트가 하나도 없었다.</b> 값은 파일에 있는데 아무도 검사하지 않으니
    /// 지금 <c>DamageFormula.Calculate</c> 의 곱셈 연쇄를 고쳐도 러너는 초록이고, 사람이
    /// 기준선을 diff 로 볼 때만 드러난다. 그 시차를 없애는 것이 이 픽스처의 전부다.
    ///
    /// 위 <see cref="DamageFormulaTests"/> 와 <b>같은 파일에 있지만 담당 접두사가 다르다.</b>
    /// 저쪽은 <c>"core.damage"</c> 를 StartsWith 로 훑고, 이쪽은 <c>"core.dmgChain["</c> 다.
    /// 둘은 <c>core.d</c> 까지만 같고 그 다음 글자가 <c>a</c> / <c>m</c> 로 갈린다 —
    /// 어느 쪽도 상대의 접두사로 시작하지 않으므로 서로의 '전부 소비' 검사를 깨지 않는다.
    /// 눈대중이 아니라 <see cref="KeyPrefixDoesNotShadowOtherFixtures"/> 가 문자열로 못 박는다.
    ///
    /// 인자를 손으로 옮겨 적지 않는다 — <b>키에 다섯 인자가 전부 박혀 있다.</b>
    /// 키에서 파싱해 <c>Calculate</c> 를 부르고 같은 줄의 값과 비교한다. 옮겨 적은 것은
    /// "골든에 있어야 할 키 목록"을 만드는 격자뿐이고, 격자가 덤퍼와 갈라지면
    /// <see cref="GoldenKeySetMatchesDumperGrid"/> 가 양방향으로 잡는다.
    ///
    /// 키에 없는 필드는 <b>전부 중립</b>이다. 출처는 덤퍼 <c>DumpDamageMultiplierChain</c> 이고
    /// 아래 <see cref="CalculateMatchesGolden"/> 의 초기화는 그 호출부와 <b>필드 순서까지</b>
    /// 같게 두었다 — 나란히 놓고 눈으로 대조하기 위해서다.
    ///
    /// 허용 오차는 없다. 반환이 int 이고 마지막이 <c>Mathf.RoundToInt</c> 라, 중간 float 가
    /// 1 ULP 만 흔들려도 .5 경계에서 정수가 1 바뀐다. 그 1 이 플레이어에게 그대로 보인다.
    ///
    /// <b>이 격자가 실제로 무엇을 잡는지 Mono 로 재어 뒀다</b>(324키 중 몇 개가 빨개지는가).
    /// 재는 방법은 리포 밖에 DamageFormula 사본을 만들어 한 군데씩 바꾸고, Unity 번들 mono 로
    /// 진짜 OJ.Core.Tests.dll 을 돌린 것이다 — 재구현이 아니라 실행 결과다.
    ///
    ///   0.5f → 1f                                324개
    ///   원소 / 레벨 / 유물 배수 삭제              210 / 208 / 199개
    ///   RoundToInt → CeilToInt / FloorToInt       167 / 151개
    ///   RoundToInt → (int)(scaled + 0.5f)          12개
    ///   <b>원소 * 유물 접기</b>                     <b>0개</b>
    ///   레벨 * 왕시너지 접기 / 0.5f 결합 순서 변경 / double 승격 / 하한 제거   각 0개
    ///
    /// <b>주의 — 덤퍼 주석과 갈리는 부분이 하나 있다.</b> 덤퍼
    /// <c>DumpDamageMultiplierChain</c> 은 "baseAttack=125 가 <c>scaled *= em; scaled *= rm;</c> 를
    /// <c>scaled *= (em * rm)</c> 로 합치는 변이를 409 → 410 으로 잡는다"고 적고, 그것을
    /// "이 한 줄이 이 구획의 존재 이유"라고 말한다. <b>Mono 에서는 잡히지 않는다.</b>
    /// 접기 전후로 반올림 <i>전</i> float 비트가 갈리는 키는 324개 중 47개인데,
    /// 그 중 정수 경계를 넘는 것이 <b>0개</b>다(문제의 키도 접기 후 그대로 409 다).
    /// 409 → 410 은 엄격 float32 재구현에서 나온 수치이고, AGENTS.md 가 못 박은 대로
    /// Mono 는 중간 결과를 매 연산마다 접지 않는다 — 그래서 순차 곱 자체가 이미 접힌 것처럼
    /// 굴고 접기 변이가 무연산에 가까워진다. 덤퍼가 "못 잡는다"고 인정한 KingSynergy 쪽과
    /// 같은 성질이며, <b>여기 격자를 넓혀서 고칠 수 있는 종류가 아니다.</b>
    /// (이 픽스처는 그 사실을 바꾸지 못한다. 다만 위 표대로 나머지 변이는 확실히 잡는다.)
    /// </summary>
    [TestFixture]
    public class DamageMultiplierChainTests
    {
        /// <summary>내 담당 접두사. '[' 까지 붙인다 — 나중에 core.dmgChainFoo 같은 키가 생겨도
        /// 조용히 딸려 들어와 5인자 파싱이 엉뚱한 데서 터지는 일이 없게 한다.</summary>
        private const string KeyName = "core.dmgChain";

        private const string KeyPrefix = KeyName + "[";

        // --- 덤퍼에서 옮겨 적은 입력 격자 -------------------------------------------------
        // 출처: Assets/Scripts/SceneFlow/GoldenBaselineDumper.cs 의 DumpDamageMultiplierChain.
        // 계산 인자는 키에서 파싱하므로 이 격자는 오직 "골든에 있어야 할 키 목록"을 만드는 데만 쓴다.
        //
        // 125 는 덤퍼가 접기 변이를 잡으라고 따로 넣은 값이다. 그 주장은 Mono 에서 성립하지
        // 않지만(클래스 주석 참고) 값 자체는 남길 이유가 있다 — 반올림 규칙 교체를 잡는
        // 12키 중 <b>6키가 baseAttack=125</b> 다. 125 를 빼면 이 구획에 남은 유일한 미세
        // 검출력이 절반으로 준다. BaseAttack125IsStillInTheGrid 가 그것을 막는다.
        //
        // 홀수만 .5 경계에 걸린다는 것도 실측으로 확인됐다 — 반올림 전 값이
        // baseAttack * 0.5f 에서 출발하기 때문이다. 12키의 baseAttack 은 전부
        // {7, 15, 33, 125, 255} 이고 짝수인 104 는 <b>한 키도 기여하지 않는다.</b>
        // 격자를 줄일 일이 있으면 104 부터 뺄 것.
        private static readonly int[] BaseAttacks = { 7, 15, 33, 104, 125, 255 };
        private static readonly int[] Pips = { 1, 3 };
        private static readonly float[] LevelMuls = { 1f, 1.1f, 1.3f };
        private static readonly float[] ElementMuls = { 1f, 1.15f, 1.4f };
        private static readonly float[] RelicMuls = { 1f, 1.05f, 1.2f };

        /// <summary>5.1-b 기준선 시점의 담당 키 개수. 6 * 2 * 3 * 3 * 3 = 324.</summary>
        private const int HandledKeyCount = 324;

        /// <summary>
        /// 덤퍼가 "이 한 줄이 이 구획의 존재 이유"라고 지목한 키.
        /// <b>그 주장은 Mono 에서 성립하지 않는다</b> — 접기 변이를 넣어도 이 키는 409 그대로다.
        /// (근거는 클래스 주석의 실측표에 있다.) 그래도 격자에서 사라지는 것은 막는다.
        /// </summary>
        private const string FoldClaimKey = "core.dmgChain[125][3][1.3][1.4][1.2]";

        // --- 케이스 소스 -----------------------------------------------------------------

        /// <summary>
        /// 골든에서 담당 키를 뽑아 케이스로 만든다. 키 자체를 인자로 넘겨 실패 목록에 키가
        /// 그대로 뜬다. GoldenBaseline.Keys 는 0건이면 예외라, 파일이 비거나 접두사가 어긋나면
        /// "케이스 0개로 초록불"이 아니라 수집 단계에서 터진다.
        /// </summary>
        public static IEnumerable<TestCaseData> ChainKeys()
        {
            var keys = new List<string>(
                GoldenBaseline.Keys(GoldenBaseline.StableSection, KeyPrefix).Keys);

            // 사전 순회 순서에 기대지 않는다. 실행 순서가 매번 같아야 실패 목록을 diff 로 본다.
            keys.Sort(StringComparer.Ordinal);

            var cases = new List<TestCaseData>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
                cases.Add(new TestCaseData(keys[i]).SetName("CalculateMatchesGolden(" + keys[i] + ")"));

            return cases;
        }

        // --- 값 검증 ---------------------------------------------------------------------

        /// <summary>
        /// ↓ DamageInputs 초기화는 덤퍼 DumpDamageMultiplierChain 의 것과 필드 순서까지 같다.
        /// 키에서 파싱하는 다섯을 뺀 나머지 아홉은 전부 중립값이고, 그 목록이 덤퍼와 갈라지면
        /// 이 테스트는 "다른 입력의 결과를 옛 기대값과 비교하는" 무의미한 테스트가 된다.
        /// </summary>
        [TestCaseSource(nameof(ChainKeys))]
        public void CalculateMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 5);

            var inputs = new DamageInputs
            {
                BaseAttack = ParseInt(arguments[0], key),
                LevelUpAttackIncrease = 0,
                DicePip = ParseInt(arguments[1], key),
                BulletLevel = 1,
                EquipmentAttackTotal = 0,
                LevelDamageMultiplier = ParseFloat(arguments[2], key),
                KingSynergyMultiplier = 1f,
                IsKingDice = false,
                AttackPercentBonus = 0f,
                AttackFlatBonus = 0,
                EarlyWaveFlatBonus = 0,
                FinalDamagePercentBonus = 0f,
                ElementUpgradeMultiplier = ParseFloat(arguments[3], key),
                RelicDamageMultiplier = ParseFloat(arguments[4], key),
            };

            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);
            int actual = DamageFormula.Calculate(inputs);

            string message =
                "골든이 깨졌다: " + key + Environment.NewLine +
                "기준선 " + expected + " → 지금 " + actual + Environment.NewLine +
                "기준선 파일: " + GoldenBaseline.FilePath + Environment.NewLine +
                "기준선을 지금 값으로 고치지 말 것. 계산식(또는 덤퍼 격자)이 왜 바뀌었는지가 먼저다. " +
                "이 구획이 실제로 잡는 것: 배수 삭제 / 반올림 규칙 교체 / 0.5f 상수 변경.";

            Assert.That(actual, Is.EqualTo(expected), message);
        }

        // --- 누락 방지 -------------------------------------------------------------------

        /// <summary>
        /// 담당 접두사의 골든 키 개수와 케이스 개수가 5.1-b 기준선과 같은지 본다.
        /// 케이스 소스가 골든에서 키를 뽑아 오므로 두 수는 원래 같지만, 기대 개수를 상수로
        /// 박아 두면 <b>덤퍼가 격자를 줄여</b> "케이스 3개로 전부 통과" 같은 상태가 됐을 때
        /// 여기서 먼저 터진다. 일부만 검사하면서 통과하는 테스트가 가장 위험하다.
        /// </summary>
        [Test]
        public void GoldenKeyCountMatchesTestCaseCount()
        {
            int goldenCount = GoldenBaseline.Keys(GoldenBaseline.StableSection, KeyPrefix).Count;
            int caseCount = 0;
            foreach (var unused in ChainKeys())
                caseCount++;

            Assert.That(goldenCount, Is.EqualTo(HandledKeyCount),
                "골든의 '" + KeyPrefix + "' 키 개수가 " + goldenCount + "개다(기대 " +
                HandledKeyCount + "개). 덤퍼 격자가 바뀌었는지 확인할 것.");
            Assert.That(caseCount, Is.EqualTo(HandledKeyCount),
                "'" + KeyPrefix + "' 로 만들어진 테스트 케이스가 " + caseCount + "개다(기대 " +
                HandledKeyCount + "개).");
        }

        /// <summary>
        /// 골든에 있는 담당 키 집합이 덤퍼 격자와 <b>정확히</b> 같은지 본다.
        /// 사라진 키(검사가 조용히 줄어든 경우)와 새 키(테스트가 안 두드리는 인자가 생긴 경우)를
        /// 양방향으로 잡는다. 개수만 세면 하나 지우고 하나 넣는 변경이 통과해 버린다.
        /// </summary>
        [Test]
        public void GoldenKeySetMatchesDumperGrid()
        {
            var expected = new SortedSet<string>(ExpectedKeys(), StringComparer.Ordinal);
            Assert.That(expected.Count, Is.EqualTo(HandledKeyCount),
                "덤퍼 격자에서 만들어진 키 개수가 5.1-b 기준선과 다르다. 격자 상수를 잘못 옮겼거나 " +
                "키가 겹쳐서 SortedSet 에서 합쳐졌다.");

            var actual = new SortedSet<string>(
                GoldenBaseline.Keys(GoldenBaseline.StableSection, KeyPrefix).Keys, StringComparer.Ordinal);

            var missing = new SortedSet<string>(expected, StringComparer.Ordinal);
            missing.ExceptWith(actual);
            var unexpected = new SortedSet<string>(actual, StringComparer.Ordinal);
            unexpected.ExceptWith(expected);

            Assert.That(string.Join(", ", new List<string>(missing).ToArray()), Is.EqualTo(string.Empty),
                "덤퍼 격자에 있는 키가 골든에 없다. 기준선이 낡았거나 덤퍼가 케이스를 지웠다.");
            Assert.That(string.Join(", ", new List<string>(unexpected).ToArray()), Is.EqualTo(string.Empty),
                "골든에 격자 밖의 키가 있다. 덤퍼가 케이스를 늘렸으면 위 격자 상수도 같이 늘릴 것.");
        }

        /// <summary>
        /// <c>core.dmgChain</c> 으로 시작하는 골든 키가 <b>한 줄도 남김없이</b> 담당 접두사에
        /// 걸리는지 본다.
        ///
        /// 위 두 테스트는 <c>KeyPrefix</c>('[' 포함)에 걸리는 키만 본다. 덤퍼가
        /// <c>core.dmgChainV2[...]</c> 같은 이름을 새로 뱉으면 그 키는 아무도 안 두드리는데
        /// 전부 초록이다 — 5.0-b 가 이 구획 전체를 그 상태로 두었던 바로 그 실패다.
        /// </summary>
        [Test]
        public void EveryDmgChainKeyIsConsumed()
        {
            var orphans = new List<string>();
            foreach (var pair in GoldenBaseline.Keys(GoldenBaseline.StableSection, KeyName))
            {
                if (!pair.Key.StartsWith(KeyPrefix, StringComparison.Ordinal))
                    orphans.Add(pair.Key);
            }

            orphans.Sort(StringComparer.Ordinal);

            Assert.That(string.Join(", ", orphans.ToArray()), Is.EqualTo(string.Empty),
                "'" + KeyName + "' 로 시작하는데 아무 테스트도 두드리지 않는 키다. " +
                "덤퍼에 하위 구획을 늘렸으면 이 픽스처의 접두사와 격자도 같이 늘릴 것.");
        }

        /// <summary>
        /// baseAttack = 125 축이 아직 살아 있는지 못 박는다.
        ///
        /// 위 개수·집합 검사는 <b>격자를 정본으로 삼는다.</b> 그래서 누가 격자 배열과 개수
        /// 상수를 <b>같이</b> 줄이면 둘 다 통과한다 — 검사가 조용히 약해지는 유일한 경로다.
        /// 125 는 그때 제일 먼저 지워질 값이다(덤퍼가 그 값을 넣은 이유였던 접기 변이가
        /// Mono 에서는 안 잡히므로, 근거 없는 값처럼 보인다).
        ///
        /// 하지만 <b>지우면 실제로 잃는 것이 있다</b>: 이 구획이 잡는다고 실측된 유일한
        /// 미세 변이(Mathf.RoundToInt → (int)(scaled + 0.5f))의 12키 중 6키가 125 에서 나온다.
        /// 그래서 접기 근거가 무너진 뒤에도 이 축은 남긴다.
        /// </summary>
        [Test]
        public void BaseAttack125IsStillInTheGrid()
        {
            var grid = new SortedSet<string>(ExpectedKeys(), StringComparer.Ordinal);
            Assert.That(grid.Contains(FoldClaimKey), Is.True,
                "이 픽스처의 격자에서 " + FoldClaimKey + " 가 사라졌다. BaseAttacks 에 125 를 되돌릴 것 — " +
                "반올림 규칙 교체를 잡는 12키 중 6키가 그 축에서 나온다.");

            var golden = GoldenBaseline.Keys(GoldenBaseline.StableSection, KeyPrefix);
            Assert.That(golden.ContainsKey(FoldClaimKey), Is.True,
                "골든에서 " + FoldClaimKey + " 가 사라졌다." + Environment.NewLine +
                "덤퍼 DumpDamageMultiplierChain 의 baseAttacks 에서 125 를 뺀 것이다.");

            int keysWith125 = 0;
            foreach (string key in grid)
            {
                if (key.StartsWith(KeyName + "[125][", StringComparison.Ordinal))
                    keysWith125++;
            }

            // 2(pip) * 3 * 3 * 3 = 54. 축 하나가 통째로 살아 있어야 6키가 나온다.
            Assert.That(keysWith125, Is.EqualTo(54),
                "baseAttack=125 축의 키가 " + keysWith125 + "개다(기대 54개). 축 일부만 지워졌다.");
        }

        /// <summary>
        /// 담당 접두사가 다른 픽스처의 담당 구역과 겹치지 않는지 <b>문자열로 직접</b> 확인한다.
        ///
        /// 같은 파일의 <see cref="DamageFormulaTests"/> 는 <c>"core.damage"</c> 를 StartsWith 로
        /// 훑고 "안 두드리는 키가 있으면 터진다"는 검사를 갖고 있다. 내 키가 그쪽 접두사로
        /// 시작하면 그 픽스처가 내 키까지 가져가 <b>사람이 F7 로 기준선을 다시 뜬 뒤에야</b>
        /// 터진다 — 덤퍼를 고친 사람이 아니라 기준선을 뜬 사람이 맞는 실패다.
        /// 그래서 골든 파일 없이 상수만으로 미리 확인한다.
        ///
        /// 두 이름은 <c>core.d</c>(6글자)까지 같고 그 다음 index 6 에서 <c>a</c> / <c>m</c> 로
        /// 갈린다. 그 갈림을 눈대중으로 두지 않고 여기서 못 박는다.
        /// </summary>
        [Test]
        public void KeyPrefixDoesNotShadowOtherFixtures()
        {
            const string DamagePrefix = "core.damage";

            // 6글자까지는 같아야 한다 — 여기가 달라졌다면 둘 중 하나의 이름이 통째로 바뀐 것이다.
            Assert.That(KeyName.Substring(0, 6), Is.EqualTo(DamagePrefix.Substring(0, 6)),
                "두 접두사의 공통부가 'core.d' 가 아니다. 이름이 바뀌었으면 이 검사도 다시 볼 것.");

            // index 6 에서 갈린다. 이 한 글자가 두 픽스처를 갈라 놓는 전부다.
            Assert.That(KeyName[6], Is.EqualTo('m'),
                "core.dmgChain 의 7번째 글자가 'm' 이 아니다.");
            Assert.That(DamagePrefix[6], Is.EqualTo('a'),
                "core.damage 의 7번째 글자가 'a' 가 아니다.");

            // 다른 픽스처가 StartsWith 로 소비하는 접두사 전부.
            // (DamageFormulaTests / StageGrowthFormulaTests / StageRewardFormulaTests /
            //  IdleRewardFormulaTests / IncomingDamageFormulaTests)
            string[] foreign =
            {
                DamagePrefix,
                "core.incoming.",
                "core.bossSpawnThreshold[", "core.resolvedBaseDefense[", "core.monsterHp[",
                "core.monsterDefense[", "core.bossHp[", "core.bossDefense[",
                "core.stageBonus", "core.guaranteedGold", "core.clearGradeTier", "core.scaleAmount",
                "core.elapsedSeconds[", "core.capped[", "core.clearCount[", "core.progress01[",
                "core.meatSets[", "core.secondsUntilNextMeatSet[",
                "reward.accumulatedGold", "reward.guaranteedGold",
            };

            var collisions = new List<string>();
            for (int i = 0; i < foreign.Length; i++)
            {
                // 내 키가 남의 접두사로 시작하면 남이 내 키를 가져간다.
                if (KeyPrefix.StartsWith(foreign[i], StringComparison.Ordinal))
                    collisions.Add(KeyPrefix + " → " + foreign[i] + " 가 가져간다");

                // 남의 접두사가 내 접두사로 시작하면 내가 남의 키를 가져간다.
                if (foreign[i].StartsWith(KeyName, StringComparison.Ordinal))
                    collisions.Add(foreign[i] + " → " + KeyName + " 가 가져간다");
            }

            Assert.That(string.Join(", ", collisions.ToArray()), Is.EqualTo(string.Empty),
                "접두사가 다른 픽스처와 겹친다. 겹치면 어느 한쪽의 '전부 소비' 검사가 터지는데, " +
                "그 실패는 덤퍼를 고친 사람이 아니라 기준선을 뜬 사람에게 간다.");
        }

        /// <summary>
        /// 배수 축의 <b>표기 → 파싱이 비트를 보존</b>하는지 증명한다.
        ///
        /// 이 픽스처는 키에 적힌 "1.15" 같은 글자를 float 으로 되돌려 <c>Calculate</c> 에
        /// 넘긴다. 되돌린 값이 덤퍼가 넣은 리터럴과 1 ULP 라도 다르면, 키는 맞는데
        /// <b>다른 입력의 결과를 골든과 비교하게 된다</b> — 그러면 통과해도 아무것도 안 지킨다.
        /// (여기가 어긋나면 접기 변이를 잡는 409/410 경계가 제일 먼저 무너진다.)
        ///
        /// 골든 파일을 읽지 않으므로 기준선이 없어도 도는 테스트다.
        /// </summary>
        [Test]
        public void AxisTextRoundTripsBitExact()
        {
            var broken = new List<string>();

            CheckRoundTrip(LevelMuls, "levelMul", broken);
            CheckRoundTrip(ElementMuls, "elementMul", broken);
            CheckRoundTrip(RelicMuls, "relicMul", broken);

            Assert.That(string.Join(", ", broken.ToArray()), Is.EqualTo(string.Empty),
                "라운드트립 표기가 비트를 보존하지 못한다. 키에서 파싱한 배수가 덤퍼가 넣은 " +
                "리터럴과 다른 float 이 되므로, 이 픽스처는 다른 입력의 결과를 골든과 비교하게 된다.");
        }

        private static void CheckRoundTrip(float[] axis, string label, List<string> broken)
        {
            for (int i = 0; i < axis.Length; i++)
            {
                string text = Format(axis[i]);

                float parsed;
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    broken.Add(label + "[" + i + "] 표기 '" + text + "' 를 되읽지 못했다");
                    continue;
                }

                if (!SameBits(parsed, axis[i]))
                    broken.Add(label + "[" + i + "] 표기 '" + text + "' 가 비트를 잃었다");
            }
        }

        // --- 보조 ------------------------------------------------------------------------

        /// <summary>덤퍼 격자로 만들어 낸, 골든에 있어야 할 키 전부.</summary>
        private static IEnumerable<string> ExpectedKeys()
        {
            for (int b = 0; b < BaseAttacks.Length; b++)
            {
                for (int p = 0; p < Pips.Length; p++)
                {
                    for (int l = 0; l < LevelMuls.Length; l++)
                    {
                        for (int e = 0; e < ElementMuls.Length; e++)
                        {
                            for (int r = 0; r < RelicMuls.Length; r++)
                            {
                                yield return
                                    KeyName + "[" + BaseAttacks[b] + "][" + Pips[p] + "][" + Format(LevelMuls[l]) +
                                    "][" + Format(ElementMuls[e]) + "][" + Format(RelicMuls[r]) + "]";
                            }
                        }
                    }
                }
            }
        }

        /// <summary>덤퍼의 Num(float) 과 <b>같은 표기</b>여야 한다. 갈라지면 키가 어긋난다.</summary>
        private static string Format(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool SameBits(float left, float right)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(left), 0)
                   == BitConverter.ToInt32(BitConverter.GetBytes(right), 0);
        }

        /// <summary>키의 대괄호 인자를 순서대로 뽑는다. core.dmgChain[125][3][1.3][1.4][1.2] → 다섯 개.</summary>
        private static string[] ParseArguments(string key, int expectedCount)
        {
            var arguments = new List<string>();
            int cursor = 0;
            while (cursor < key.Length)
            {
                int open = key.IndexOf('[', cursor);
                if (open < 0)
                    break;

                int close = key.IndexOf(']', open + 1);
                if (close < 0)
                    throw new FormatException("골든 키의 대괄호가 안 닫혔다: " + key);

                arguments.Add(key.Substring(open + 1, close - open - 1));
                cursor = close + 1;
            }

            // 인자 개수가 다르면 덤퍼가 키 모양을 바꾼 것이다. 앞 인자만 읽고 통과시키면
            // 엉뚱한 입력의 결과를 골든과 비교하게 되므로 여기서 끊는다.
            if (arguments.Count != expectedCount)
            {
                throw new FormatException(
                    "골든 키 " + key + " 의 인자가 " + arguments.Count + "개다(기대 " + expectedCount +
                    "개). 덤퍼가 키 모양을 바꿨다면 테스트도 따라가야 한다.");
            }

            return arguments.ToArray();
        }

        private static int ParseInt(string text, string key)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new FormatException("골든 키 " + key + " 의 정수 인자를 못 읽었다: '" + text + "'");

            return value;
        }

        /// <summary>
        /// 키에 적힌 배수를 float 으로 되돌린다.
        /// 골든 표기는 라운드트립("R")이라 되읽으면 덤퍼가 넘긴 값과 비트가 같다 —
        /// 그것을 AxisTextRoundTripsBitExact 가 따로 증명한다.
        /// double 로 받아 넘기면 승격 시점이 달라져 반올림 경계에서 값이 갈릴 수 있다.
        /// </summary>
        private static float ParseFloat(string text, string key)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException("골든 키 " + key + " 의 실수 인자를 못 읽었다: '" + text + "'");

            return value;
        }
    }
}
