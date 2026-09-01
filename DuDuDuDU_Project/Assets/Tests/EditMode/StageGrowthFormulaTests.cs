using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>
    /// StageGrowthFormula 골든 특성화 테스트. (MIGRATION_BASELINE 3.3)
    ///
    /// 목적은 개선이 아니라 고정이다. Tests/Golden/formula_baseline.txt 의 [stable] 구획에 있는
    /// core.bossSpawnThreshold / core.resolvedBaseDefense / core.monsterHp / core.monsterDefense /
    /// core.bossHp / core.bossDefense 135줄이 "현행 동작"의 정본이고, 그 숫자가 하나라도
    /// 달라지면 즉시 실패해야 한다.
    ///
    /// 기대값을 테스트에 다시 적지 않는다 — 키를 골든에서 읽고, 키에서 인자를 파싱해 함수를 부르고,
    /// 기대값도 같은 줄에서 읽는다. 값을 옮겨 적으면 나중에 "테스트를 값에 맞추는" 유혹이 생긴다.
    ///
    /// 예외는 맨 아래 "격자 밖 경계값 고정" 구획뿐이다. 덤퍼 격자가 아예 뜨지 않는 입력이라
    /// 골든에서 읽어올 값이 없다. 이유와 근거는 그 구획 주석에 적었다.
    ///
    /// 결과가 전부 int 라 비교에 허용 오차가 없다. 애초에 오차를 두면 Mathf.RoundToInt 의
    /// .5 경계가 밀리는 회귀(가장 흔한 회귀다)를 통째로 놓친다.
    ///
    /// 이 어셈블리는 Assembly-CSharp 을 참조할 수 없어서 StageData 를 못 쓴다. 그래서 검증 대상은
    /// 순수 함수를 기본형 인자로 두드린 core.* 구획뿐이고, 에셋에서 나온 stage.* 는 사람이 diff 로 본다.
    /// </summary>
    [TestFixture]
    public class StageGrowthFormulaTests
    {
        // --- 골든 키 이름 ---------------------------------------------------------------
        // 접두사에 '[' 를 붙인다. "core.bossHp" 만 쓰면 나중에 core.bossHpRatio 같은 키가 생겼을 때
        // 조용히 딸려 들어와 인자 파싱이 엉뚱한 데서 터진다.
        private const string BossSpawnThresholdName = "core.bossSpawnThreshold";
        private const string ResolvedBaseDefenseName = "core.resolvedBaseDefense";
        private const string MonsterHpName = "core.monsterHp";
        private const string MonsterDefenseName = "core.monsterDefense";
        private const string BossHpName = "core.bossHp";
        private const string BossDefenseName = "core.bossDefense";

        private const string BossSpawnThresholdPrefix = BossSpawnThresholdName + "[";
        private const string ResolvedBaseDefensePrefix = ResolvedBaseDefenseName + "[";
        private const string MonsterHpPrefix = MonsterHpName + "[";
        private const string MonsterDefensePrefix = MonsterDefenseName + "[";
        private const string BossHpPrefix = BossHpName + "[";
        private const string BossDefensePrefix = BossDefenseName + "[";

        private static readonly string[] HandledPrefixes =
        {
            BossSpawnThresholdPrefix,
            ResolvedBaseDefensePrefix,
            MonsterHpPrefix,
            MonsterDefensePrefix,
            BossHpPrefix,
            BossDefensePrefix,
        };

        // --- 덤퍼에서 옮겨 적은 입력 격자 -------------------------------------------------
        // 출처: Assets/Scripts/SceneFlow/GoldenBaselineDumper.cs 의 DumpCoreFormulas.
        // 계산 인자는 키에서 파싱하므로 이 격자는 오직 "골든에 있어야 할 키 목록"을 만드는 데만 쓴다.
        // 덤퍼가 격자를 좁히면(케이스가 조용히 사라지면) 아래 GoldenKeySetMatchesDumperGrid 가 잡는다.
        private static readonly int[] MonsterCounts = { 1, 2, 3, 7, 19, 20, 21, 40, 41 };
        private static readonly int[] BaseDefenses = { 0, 1, 7 };
        private static readonly int[] StageIndexes = { 1, 2, 3, 7, 10, 17, 30, 100 };
        private static readonly int[] TotalWavesSet = { 8, 10, 15, 20 };
        private static readonly int[] Waves = { 1, 2, 3, 7, 12, 20 };

        // 덤퍼는 이 인자들을 키에 "문자 그대로" 박는다 — 숫자 포맷 함수를 거치지 않는다.
        // 그래서 여기서도 float 를 포맷해 만들지 않고 문자열을 그대로 옮긴다. 포맷을 거치면
        // 0.145 가 0.144999996 같은 표기로 나와 키가 어긋날 수 있다.
        private static readonly string[] MonsterHpArgumentHeads = { "[20][0.16][0.02]", "[7][0.145][0.018]" };
        private const string MonsterDefenseArgumentHead = "[4][0.12][0.015]";

        // core.bossHp / core.bossDefense 키에는 웨이브 번호만 들어 있고 실제 인자는 덤퍼가 만든다:
        //     BossHp(waves[w] * 13, 6.4f)
        //     BossDefense(waves[w] * 3, 2.5f)
        // 키에서 복원할 수 없는 유일한 부분이라 여기에 옮겨 적는다. 덤퍼를 고치면 여기도 같이 고쳐야
        // 이 테스트가 의미를 유지한다 — 갈라지면 그냥 다른 값을 검사하는 테스트가 된다.
        private const int BossHpMonsterHpPerWave = 13;
        private const float BossHpMultiplier = 6.4f;
        private const int BossDefenseMonsterDefensePerWave = 3;
        private const float BossDefenseMultiplier = 2.5f;

        // 3.3 기준선 시점의 담당 키 개수. 합계는 9 + 96 + 12 + 6 + 6 + 6 이다.
        private const int HandledKeyCount = 135;

        // --- 케이스 소스 -----------------------------------------------------------------

        public static IEnumerable<TestCaseData> BossSpawnThresholdKeys()
        {
            return KeyCases(BossSpawnThresholdPrefix);
        }

        public static IEnumerable<TestCaseData> ResolvedBaseDefenseKeys()
        {
            return KeyCases(ResolvedBaseDefensePrefix);
        }

        public static IEnumerable<TestCaseData> MonsterHpKeys()
        {
            return KeyCases(MonsterHpPrefix);
        }

        public static IEnumerable<TestCaseData> MonsterDefenseKeys()
        {
            return KeyCases(MonsterDefensePrefix);
        }

        public static IEnumerable<TestCaseData> BossHpKeys()
        {
            return KeyCases(BossHpPrefix);
        }

        public static IEnumerable<TestCaseData> BossDefenseKeys()
        {
            return KeyCases(BossDefensePrefix);
        }

        // --- 값 검증 ---------------------------------------------------------------------

        [TestCaseSource(nameof(BossSpawnThresholdKeys))]
        public void BossSpawnThresholdMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 1);
            int monstersPerWave = ParseInt(arguments[0], key);

            int actual = StageGrowthFormula.BossSpawnThreshold(monstersPerWave);

            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(ResolvedBaseDefenseKeys))]
        public void ResolvedBaseDefenseMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 3);
            int baseMonsterDefense = ParseInt(arguments[0], key);
            int stageIndex = ParseInt(arguments[1], key);
            int totalWaves = ParseInt(arguments[2], key);

            int actual = StageGrowthFormula.ResolvedBaseDefense(baseMonsterDefense, stageIndex, totalWaves);

            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(MonsterHpKeys))]
        public void MonsterHpMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 4);
            int baseMonsterHp = ParseInt(arguments[0], key);
            float linearFactor = ParseFloat(arguments[1], key);
            float quadraticFactor = ParseFloat(arguments[2], key);
            int waveIndex = ParseInt(arguments[3], key);

            int actual = StageGrowthFormula.MonsterHp(baseMonsterHp, linearFactor, quadraticFactor, waveIndex);

            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(MonsterDefenseKeys))]
        public void MonsterDefenseMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 4);
            int resolvedBaseDefense = ParseInt(arguments[0], key);
            float linearFactor = ParseFloat(arguments[1], key);
            float quadraticFactor = ParseFloat(arguments[2], key);
            int waveIndex = ParseInt(arguments[3], key);

            int actual = StageGrowthFormula.MonsterDefense(resolvedBaseDefense, linearFactor, quadraticFactor, waveIndex);

            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(BossHpKeys))]
        public void BossHpMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 1);
            int waveIndex = ParseInt(arguments[0], key);

            // 덤퍼와 같은 방식으로 "이미 반올림된 정수 몬스터 체력"을 만들어 넣는다.
            // BossHp 는 이중 반올림이 원본 동작이라 float 체력을 넘기면 값이 달라진다.
            int actual = StageGrowthFormula.BossHp(waveIndex * BossHpMonsterHpPerWave, BossHpMultiplier);

            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(BossDefenseKeys))]
        public void BossDefenseMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 1);
            int waveIndex = ParseInt(arguments[0], key);

            int actual = StageGrowthFormula.BossDefense(waveIndex * BossDefenseMonsterDefensePerWave, BossDefenseMultiplier);

            AssertGolden(key, actual);
        }

        // --- 누락 방지 -------------------------------------------------------------------

        /// <summary>
        /// 담당 접두사의 골든 키 개수와 케이스 개수가 3.3 기준선과 같은지 본다.
        /// 케이스 소스가 골든에서 키를 뽑아 오므로 두 수는 원래 같지만, 기대 개수를 상수로 박아 두면
        /// 덤퍼가 격자를 줄여 "케이스 3개로 전부 통과" 같은 상태가 됐을 때 여기서 먼저 터진다.
        /// </summary>
        [TestCase(BossSpawnThresholdPrefix, 9)]
        [TestCase(ResolvedBaseDefensePrefix, 96)]
        [TestCase(MonsterHpPrefix, 12)]
        [TestCase(MonsterDefensePrefix, 6)]
        [TestCase(BossHpPrefix, 6)]
        [TestCase(BossDefensePrefix, 6)]
        public void GoldenKeyCountMatchesTestCaseCount(string prefix, int expectedCount)
        {
            int goldenCount = GoldenBaseline.Keys(GoldenBaseline.StableSection, prefix).Count;
            int caseCount = KeyCases(prefix).Count;

            Assert.That(goldenCount, Is.EqualTo(expectedCount), string.Format(
                "골든의 '{0}' 키 개수가 달라졌다. 덤퍼 격자가 바뀌었는지 확인할 것.", prefix));
            Assert.That(caseCount, Is.EqualTo(expectedCount), string.Format(
                "'{0}' 로 만들어진 테스트 케이스 개수가 골든과 안 맞는다.", prefix));
        }

        /// <summary>
        /// 골든에 있는 담당 키 집합이 덤퍼 격자와 정확히 같은지 본다.
        /// 사라진 키(검사가 조용히 줄어든 경우)와 새 키(테스트가 안 두드리는 인자가 생긴 경우)를
        /// 양방향으로 잡는다.
        /// </summary>
        [Test]
        public void GoldenKeySetMatchesDumperGrid()
        {
            var expected = new SortedSet<string>(ExpectedKeys(), StringComparer.Ordinal);
            Assert.That(expected.Count, Is.EqualTo(HandledKeyCount),
                "덤퍼 격자에서 만들어진 키 개수가 3.3 기준선과 다르다. 격자 상수를 잘못 옮겼다.");

            var actual = new SortedSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < HandledPrefixes.Length; i++)
            {
                foreach (var pair in GoldenBaseline.Keys(GoldenBaseline.StableSection, HandledPrefixes[i]))
                    actual.Add(pair.Key);
            }

            var missing = new SortedSet<string>(expected, StringComparer.Ordinal);
            missing.ExceptWith(actual);
            var unexpected = new SortedSet<string>(actual, StringComparer.Ordinal);
            unexpected.ExceptWith(expected);

            Assert.That(string.Join(", ", missing), Is.EqualTo(string.Empty),
                "덤퍼 격자에 있는 키가 골든에 없다. 기준선이 낡았거나 덤퍼가 케이스를 지웠다.");
            Assert.That(string.Join(", ", unexpected), Is.EqualTo(string.Empty),
                "골든에 격자 밖의 키가 있다. 덤퍼가 케이스를 늘렸으면 위 격자 상수도 같이 늘릴 것.");
        }

        // --- 격자 밖 경계값 고정 -----------------------------------------------------------
        //
        // 여기서부터는 기대값을 테스트에 적는다. 위 구획과 원칙이 다른 이유는 하나뿐이다:
        // 덤퍼 격자가 이 입력들을 아예 뜨지 않아서 골든에 읽어올 값이 없다.
        //   - 웨이브가 1부터라 waveIndex <= 0 방어 분기를 안 지난다.
        //   - 몬스터 수가 1부터라 BossSpawnThreshold 의 Clamp 하한을 안 건드린다.
        //   - totalWaves 가 {8,10,15,20} 이라 '== 20' 을 '>= 20' 으로 바꿔도 안 걸린다.
        //   - baseMonsterHp 가 {20,7}, 보스 체력 배수가 6.4f 하나뿐이라 곱셈 결과가 .5 로
        //     딱 떨어지는 입력이 없다. 그래서 반올림 규칙 자체가 드러나지 않는다.
        //
        // 추측이 아니라 변이 검사로 확인한 구멍이다. 위 골든 135개만으로는
        // Mathf.RoundToInt 를 (int)(x + 0.5f) 로 바꿔도, Mathf.Max(1, waveIndex) 의 1 을 0 으로
        // 바꿔도, Mathf.Clamp 를 통째로 지워도 전부 초록불이었다.
        //
        // 아래 숫자는 손계산이 아니라 현행 구현을 실제로 돌려서 뽑았다. 골든과 목적이 같고
        // 저장 위치만 다르다 — "지금 이렇게 동작한다"의 박제다. 값이 달라지면 골든을 고치지
        // 않듯 이 숫자도 고치지 말고 계산식 변경을 되돌려야 한다.
        // 덤퍼 격자가 넓어져 골든이 이 입력들을 덮게 되면 이 구획은 지워도 된다.

        /// <summary>
        /// Mathf.RoundToInt 는 .5 에서 짝수 쪽으로 붙는다(은행가 반올림).
        /// (int)(x + 0.5f) 나 Math.Floor(x + 0.5) 같은 "같아 보이는" 치환은 바로 여기서만 갈린다.
        /// 내림/올림 양방향을 다 박아야 한쪽만 우연히 맞는 치환도 걸린다.
        /// </summary>
        [TestCase(1, 0.16f, 0.015f, 7, 2)]    // 2.5  -> 2  (짝수로 내림)
        [TestCase(1, 0.12f, 0.02f, 20, 10)]   // 10.5 -> 10 (짝수로 내림)
        [TestCase(1, 0.16f, 0.02f, 18, 10)]   // 9.5  -> 10 (짝수로 올림)
        [TestCase(1, 0.16f, 0.005f, 19, 6)]   // 5.5  -> 6  (짝수로 올림)
        public void MonsterHpRoundsHalfToEven(
            int baseMonsterHp, float linearFactor, float quadraticFactor, int waveIndex, int expected)
        {
            Assert.That(
                StageGrowthFormula.MonsterHp(baseMonsterHp, linearFactor, quadraticFactor, waveIndex),
                Is.EqualTo(expected));
        }

        /// <summary>MonsterDefense 도 같은 반올림 규칙이다. 두 식은 별개라 따로 박는다.</summary>
        [TestCase(1, 0.16f, 0.015f, 7, 2)]    // 2.5  -> 2
        [TestCase(1, 0.12f, 0.02f, 20, 10)]   // 10.5 -> 10
        [TestCase(1, 0.16f, 0.02f, 18, 10)]   // 9.5  -> 10
        public void MonsterDefenseRoundsHalfToEven(
            int resolvedBaseDefense, float linearFactor, float quadraticFactor, int waveIndex, int expected)
        {
            Assert.That(
                StageGrowthFormula.MonsterDefense(resolvedBaseDefense, linearFactor, quadraticFactor, waveIndex),
                Is.EqualTo(expected));
        }

        /// <summary>
        /// 보스 쪽 반올림. 골든이 쓰는 배수(6.4f)로는 곱셈 결과가 .5 에 걸리는 웨이브가 없어서
        /// 배수를 2.5f 로 준다. 배수는 함수 인자라 이렇게 두드리는 것이 정당하다.
        /// </summary>
        [TestCase(1, 2)]    // 2.5  -> 2
        [TestCase(3, 8)]    // 7.5  -> 8
        [TestCase(5, 12)]   // 12.5 -> 12
        [TestCase(7, 18)]   // 17.5 -> 18
        public void BossHpRoundsHalfToEven(int monsterHpForWave, int expected)
        {
            Assert.That(StageGrowthFormula.BossHp(monsterHpForWave, 2.5f), Is.EqualTo(expected));
        }

        [TestCase(1, 2)]
        [TestCase(3, 8)]
        [TestCase(5, 12)]
        [TestCase(7, 18)]
        public void BossDefenseRoundsHalfToEven(int monsterDefenseForWave, int expected)
        {
            Assert.That(StageGrowthFormula.BossDefense(monsterDefenseForWave, 2.5f), Is.EqualTo(expected));
        }

        /// <summary>
        /// <b>이 테스트는 결합순서를 고정하지 못한다.</b> 원래 그 목적으로 쓰였고 기대값을 5 로
        /// 박았지만 둘 다 틀렸다. 지우지 않고 남기는 이유는 아래 사실을 기록으로 남기기 위해서다.
        ///
        /// Unity 는 EditMode 테스트를 <b>Mono</b> 에서 돌린다. Mono 의 JIT 는 float 식의 중간
        /// 결과를 매 연산마다 float 로 접지 않고 더 높은 정밀도로 들고 가다 대입 시점에 접는다
        /// (C# 명세가 허용하는 동작이다). CoreCLR 은 연산마다 접는다. 같은 IL 인데 답이 갈린다:
        ///
        ///   1f + (7f * 0.145f) + (7f * 7f * 0.015f)
        ///     Mono    -> 2.75f       -> RoundToInt(2 * 2.75f)      = 6   ← Unity 의 실제 동작
        ///     CoreCLR -> 2.7499998f  -> RoundToInt(2 * 2.7499998f) = 5
        ///
        /// 기대값 5 는 엄격 float32 로 재구현해서 뽑은 값이었다. <b>재구현은 오라클이 아니다</b> —
        /// 값은 게임이 실제로 내놓은 것이어야 한다. 그것이 골든 기준선을 두는 이유다.
        ///
        /// 그리고 Mono 에서는 재괄호화가 실제로 무효과라(변이 검사로 확인) 이 입력으로는
        /// 결합순서를 잡을 수 없다. 결합순서 고정이 필요하면 Mono 에서 갈리는 입력을 새로 찾아야
        /// 하고, 그런 입력이 존재하는지부터 확인해야 한다.
        /// </summary>
        [Test]
        public void MonsterHpMatchesMonoFloatBehaviour()
        {
            Assert.That(StageGrowthFormula.MonsterHp(2, 0.145f, 0.015f, 8), Is.EqualTo(6),
                "Mono 의 확장 정밀도로 multiplier 가 정확히 2.75f 가 되어 5.5 -> 6 이다. " +
                "5 가 나왔다면 CoreCLR 의미론으로 돌고 있다는 뜻이다 — 러너가 Unity 를 재현하지 못한다.");
        }

        /// <summary>waveIndex <= 0 은 웨이브 1 로 취급한다. Mathf.Max(1, waveIndex) 를 지우면 깨진다.</summary>
        [TestCase(0)]
        [TestCase(-3)]
        public void MonsterHpClampsNonPositiveWaveToFirstWave(int waveIndex)
        {
            Assert.That(StageGrowthFormula.MonsterHp(20, 0.16f, 0.02f, waveIndex),
                Is.EqualTo(StageGrowthFormula.MonsterHp(20, 0.16f, 0.02f, 1)));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void MonsterDefenseClampsNonPositiveWaveToFirstWave(int waveIndex)
        {
            Assert.That(StageGrowthFormula.MonsterDefense(4, 0.12f, 0.015f, waveIndex),
                Is.EqualTo(StageGrowthFormula.MonsterDefense(4, 0.12f, 0.015f, 1)));
        }

        /// <summary>
        /// 체력 하한은 1, 방어력 하한은 0 이다. 두 값이 다르다는 것이 요점이라 한 곳에 붙여 둔다 —
        /// 공통 함수로 합치고 싶어질 때 여기가 막는다.
        /// </summary>
        [Test]
        public void HpFloorIsOneAndDefenseFloorIsZero()
        {
            Assert.That(StageGrowthFormula.MonsterHp(0, 0.16f, 0.02f, 5), Is.EqualTo(1), "몬스터 체력 하한은 1 이다.");
            Assert.That(StageGrowthFormula.MonsterDefense(0, 0.12f, 0.015f, 5), Is.EqualTo(0), "몬스터 방어력 하한은 0 이다.");
            Assert.That(StageGrowthFormula.BossHp(0, 6.4f), Is.EqualTo(1), "보스 체력 하한은 1 이다.");
            Assert.That(StageGrowthFormula.BossDefense(0, 2.5f), Is.EqualTo(0), "보스 방어력 하한은 0 이다.");
        }

        /// <summary>
        /// Clamp 하한. 격자가 몬스터 1마리부터라 이 분기는 골든으로 검증되지 않는다 —
        /// Mathf.Clamp 를 통째로 지워도 골든 9개는 전부 통과한다.
        /// </summary>
        [TestCase(0)]
        [TestCase(-5)]
        public void BossSpawnThresholdIsAtLeastOne(int monstersPerWave)
        {
            Assert.That(StageGrowthFormula.BossSpawnThreshold(monstersPerWave), Is.EqualTo(1));
        }

        /// <summary>
        /// 웨이브 길이 보정은 10 과 20 "정확히 일치"할 때만 걸린다.
        /// 격자가 20 에서 끝나 '== 20' 을 '>= 20' 으로 바꿔도 안 걸리므로 21 이상을 직접 둔다.
        /// </summary>
        [TestCase(21)]
        [TestCase(25)]
        [TestCase(30)]
        public void ResolvedBaseDefenseAppliesNoCorrectionBeyondTwentyWaves(int totalWaves)
        {
            // 15웨이브(보정 없음)와 같아야 한다. 15 는 골든에 있으므로 기준으로 쓸 수 있다.
            int uncorrected = GoldenBaseline.Int(GoldenBaseline.StableSection, "core.resolvedBaseDefense[0][100][15]");

            Assert.That(StageGrowthFormula.ResolvedBaseDefense(0, 100, totalWaves), Is.EqualTo(uncorrected),
                "totalWaves 보정은 10 과 20 에만 걸려야 한다. 조건을 범위로 일반화하면 여기서 터진다.");
        }

        /// <summary>
        /// ResolvedBaseDefense 의 반올림 규칙. 스테이지 인덱스가 이렇게 큰 것은 일부러다 —
        /// stageIndex 1700 아래에서는 역산식 결과가 float 에서 정확히 .5 로 떨어지는 지점이
        /// 하나도 없다(그 구간은 float 간격이 촘촘해 .5 를 스치고 지나간다).
        /// 즉 이 두 줄이 아니면 이 함수의 반올림 규칙은 어떤 입력으로도 고정되지 않는다.
        /// </summary>
        [TestCase(1796, 5294)]   // 5294.5 -> 5294 (짝수로 내림)
        [TestCase(2294, 6872)]   // 6871.5 -> 6872 (짝수로 올림)
        public void ResolvedBaseDefenseRoundsHalfToEven(int stageIndex, int expected)
        {
            Assert.That(StageGrowthFormula.ResolvedBaseDefense(0, stageIndex, 8), Is.EqualTo(expected));
        }

        /// <summary>stageIndex <= 1 은 stageOffset 0 으로 눌린다. Mathf.Max(0, stageIndex - 1) 방어.</summary>
        [TestCase(0)]
        [TestCase(-5)]
        public void ResolvedBaseDefenseClampsStageOffsetAtZero(int stageIndex)
        {
            Assert.That(StageGrowthFormula.ResolvedBaseDefense(0, stageIndex, 8),
                Is.EqualTo(StageGrowthFormula.ResolvedBaseDefense(0, 1, 8)));
        }

        /// <summary>baseMonsterDefense 가 0 보다 크면 역산식을 타지 않고 그 값을 그대로 돌려준다.</summary>
        [Test]
        public void ResolvedBaseDefenseShortCircuitsOnPositiveInput()
        {
            Assert.That(StageGrowthFormula.ResolvedBaseDefense(9, 100, 10), Is.EqualTo(9),
                "0 보다 큰 baseMonsterDefense 는 그대로 나와야 한다. '> 0' 을 '>= 0' 으로 바꾸면 깨진다.");
        }

        // --- 보조 ------------------------------------------------------------------------

        /// <summary>덤퍼 격자로 만들어 낸, 골든에 있어야 할 키 전부.</summary>
        private static IEnumerable<string> ExpectedKeys()
        {
            for (int i = 0; i < MonsterCounts.Length; i++)
            {
                yield return string.Format(CultureInfo.InvariantCulture, "{0}[{1}]",
                    BossSpawnThresholdName, MonsterCounts[i]);
            }

            for (int b = 0; b < BaseDefenses.Length; b++)
            {
                for (int s = 0; s < StageIndexes.Length; s++)
                {
                    for (int w = 0; w < TotalWavesSet.Length; w++)
                    {
                        yield return string.Format(CultureInfo.InvariantCulture, "{0}[{1}][{2}][{3}]",
                            ResolvedBaseDefenseName, BaseDefenses[b], StageIndexes[s], TotalWavesSet[w]);
                    }
                }
            }

            for (int w = 0; w < Waves.Length; w++)
            {
                for (int h = 0; h < MonsterHpArgumentHeads.Length; h++)
                {
                    yield return string.Format(CultureInfo.InvariantCulture, "{0}{1}[{2}]",
                        MonsterHpName, MonsterHpArgumentHeads[h], Waves[w]);
                }

                yield return string.Format(CultureInfo.InvariantCulture, "{0}{1}[{2}]",
                    MonsterDefenseName, MonsterDefenseArgumentHead, Waves[w]);
                yield return string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", BossHpName, Waves[w]);
                yield return string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", BossDefenseName, Waves[w]);
            }
        }

        /// <summary>
        /// 접두사에 걸리는 골든 키를 케이스로 만든다. 키 자체를 인자로 넘겨서 실패 목록에 키가 그대로 뜬다.
        /// GoldenBaseline.Keys 는 0건이면 예외라, 파일이 비거나 접두사가 어긋나면
        /// "케이스 0개로 초록불"이 아니라 수집 단계에서 터진다.
        /// </summary>
        private static List<TestCaseData> KeyCases(string prefix)
        {
            var keys = new List<string>(GoldenBaseline.Keys(GoldenBaseline.StableSection, prefix).Keys);
            // 사전 순회 순서에 기대지 않는다. 실행 순서가 매번 같아야 실패 목록을 diff 로 볼 수 있다.
            keys.Sort(StringComparer.Ordinal);

            var cases = new List<TestCaseData>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
                cases.Add(new TestCaseData(keys[i]));

            return cases;
        }

        private static void AssertGolden(string key, int actual)
        {
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);

            // 실패 메시지에 키를 박는다 — 어느 골든 줄이 깨졌는지가 첫 줄에 보여야 한다.
            Assert.That(actual, Is.EqualTo(expected), string.Format(
                "골든이 깨졌다: {0} (골든 {1} / 현재 {2}). 골든을 고치지 말고 계산식 변경을 되돌릴 것.",
                key, expected, actual));
        }

        /// <summary>키의 대괄호 인자를 순서대로 뽑는다. core.monsterHp[20][0.16][0.02][1] → 20, 0.16, 0.02, 1.</summary>
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
                    throw new FormatException(string.Format("골든 키의 대괄호가 안 닫혔다: {0}", key));

                arguments.Add(key.Substring(open + 1, close - open - 1));
                cursor = close + 1;
            }

            // 인자 개수가 다르면 덤퍼가 키 모양을 바꾼 것이다. 앞 인자만 읽고 통과시키면
            // 엉뚱한 함수 호출을 골든과 비교하게 되므로 여기서 끊는다.
            if (arguments.Count != expectedCount)
            {
                throw new FormatException(string.Format(
                    "골든 키 {0} 의 인자가 {1}개다(기대 {2}개). 덤퍼가 키 모양을 바꿨다면 테스트도 따라가야 한다.",
                    key, arguments.Count, expectedCount));
            }

            return arguments.ToArray();
        }

        private static int ParseInt(string text, string key)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new FormatException(string.Format("골든 키 {0} 의 정수 인자를 못 읽었다: '{1}'", key, text));

            return value;
        }

        /// <summary>
        /// 키에 적힌 실수 인자를 float 으로 되돌린다.
        /// 골든 표기는 라운드트립("R")이라 "0.145" → float 은 덤퍼가 쓴 리터럴 0.145f 와 비트가 같다.
        /// double 로 받아 넘기면 승격 시점이 달라져 Mathf.RoundToInt 경계에서 값이 갈릴 수 있다.
        /// </summary>
        private static float ParseFloat(string text, string key)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException(string.Format("골든 키 {0} 의 실수 인자를 못 읽었다: '{1}'", key, text));

            return value;
        }
    }
}
