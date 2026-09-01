using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>
    /// IdleRewardFormula 특성화 테스트. (MIGRATION_BASELINE 3.3)
    /// </summary>
    /// <remarks>
    /// 이 테스트는 계산식이 "옳은지"를 묻지 않는다. 골든 기준선에 박힌 숫자가
    /// <b>바뀌었는지</b>만 묻는다. 그래서 기대값은 전부 파일에서 읽고, 테스트 안에는
    /// 어떤 기대 숫자도 다시 쓰지 않는다 — 여기에 손으로 적는 순간 계산식과 함께
    /// 고쳐질 수 있고, 그러면 회귀를 못 잡는다.
    ///
    /// 부동소수 비교에 <b>허용 오차를 두지 않는다.</b> 반올림이 한 비트 달라지는 것이
    /// 정확히 이 테스트가 잡아야 할 회귀라서, 오차를 허용하면 목적이 사라진다.
    /// 골든 값은 "R"(라운드트립) 표기라 파싱하면 원래 비트가 그대로 돌아온다.
    ///
    /// 케이스는 골든 키에서 인자를 파싱해 만든다. 키 목록을 테스트에 복사해 두면
    /// 덤퍼가 케이스를 늘렸을 때 조용히 빠지므로, 파일이 케이스 목록의 정본이다.
    /// 대신 "줄어드는" 쪽은 파일만 보고는 못 잡으므로 개수를 따로 박아 둔다
    /// (OwnedKeyCounts 참고).
    /// </remarks>
    [TestFixture]
    public sealed class IdleRewardFormulaTests
    {
        // 접두사에 여는 대괄호까지 포함시킨다. 나중에 core.cappedRaw 같은 키가 생겼을 때
        // 남의 키를 이 픽스처가 조용히 삼키는 것을 막으려는 것이다.
        private const string ElapsedSecondsPrefix = "core.elapsedSeconds[";
        private const string CappedPrefix = "core.capped[";
        private const string ClearCountPrefix = "core.clearCount[";
        private const string Progress01Prefix = "core.progress01[";
        private const string MeatSetsPrefix = "core.meatSets[";
        private const string SecondsUntilNextMeatSetPrefix = "core.secondsUntilNextMeatSet[";

        // 덤퍼가 넘긴 상수를 그대로 옮겨 적은 것이다. 원본은 IdleRewardManager 의
        // AutoBattleMaxSeconds / SecondsPerAutoBattleClear / MeatSetIntervalSeconds /
        // MaxMeatSetCount 이고, 이 테스트 어셈블리는 Assembly-CSharp 을 참조할 수 없어
        // 참조로 가져올 방법이 없다.
        //
        // 골든에서 역산하는 방법도 있지만 일부러 안 한다. 역산하면 상수가 바뀌고 기준선을
        // 다시 뜬 경우에도 테스트가 통과해 버린다. 손으로 박아 두면 그 상황에서 실패하고,
        // 상수 변경은 실제로 사람이 봐야 하는 변경이다.
        // 곱셈 형태까지 원본과 똑같이 유지한다 — 상수 접기 결과는 같지만 diff 로 1:1 대조하려는 것이다.
        private const double AutoBattleMaxSeconds = 8d * 60d * 60d;
        private const double SecondsPerAutoBattleClear = 20d * 60d;
        private const double MeatSetIntervalSeconds = 6d * 60d * 60d;
        private const int MaxMeatSetCount = 30;

        /// <summary>담당 접두사별로 골든에 있어야 하는 키 개수.</summary>
        /// <remarks>
        /// GoldenBaselineDumper.DumpCoreFormulas 의 입력 배열에서 나오는 수다.
        /// startTicks 4개 × nowOffsets 6개 = 24, 나머지 5종은 elapsed 배열 11개씩.
        ///
        /// 개수를 골든에서만 세면 테스트 케이스도 골든에서 나오므로 항상 일치한다 —
        /// 즉 덤퍼가 케이스를 빠뜨려 골든이 짧아져도 조용히 통과한다. 그 구멍을 막는
        /// 유일한 방법이 바깥에 적힌 기대 개수라서 여기에 박아 둔다.
        /// </remarks>
        private static readonly Dictionary<string, int> OwnedKeyCounts = new Dictionary<string, int>
        {
            { ElapsedSecondsPrefix, 24 },
            { CappedPrefix, 11 },
            { ClearCountPrefix, 11 },
            { Progress01Prefix, 11 },
            { MeatSetsPrefix, 11 },
            { SecondsUntilNextMeatSetPrefix, 11 },
        };

        [TestCaseSource(nameof(ElapsedSecondsCases))]
        public void ElapsedSeconds_MatchesGolden(string goldenKey, long startUtcTicks, long nowUtcTicks)
        {
            double expected = GoldenBaseline.Double(GoldenBaseline.StableSection, goldenKey);
            double actual = IdleRewardFormula.ElapsedSeconds(startUtcTicks, nowUtcTicks);

            Assert.That(actual, Is.EqualTo(expected), FailureMessage(goldenKey, Num(expected), Num(actual)));
        }

        [TestCaseSource(nameof(CappedCases))]
        public void CappedElapsedSeconds_MatchesGolden(string goldenKey, double elapsedSeconds)
        {
            double expected = GoldenBaseline.Double(GoldenBaseline.StableSection, goldenKey);
            double actual = IdleRewardFormula.CappedElapsedSeconds(elapsedSeconds, AutoBattleMaxSeconds);

            Assert.That(actual, Is.EqualTo(expected), FailureMessage(goldenKey, Num(expected), Num(actual)));
        }

        [TestCaseSource(nameof(ClearCountCases))]
        public void AutoBattleClearCount_MatchesGolden(string goldenKey, double elapsedSeconds)
        {
            // 덤퍼는 raw elapsed 가 아니라 상한을 먹인 값을 넘긴다. 순서를 바꾸면
            // 1e9 같은 입력에서 값이 완전히 달라지므로 호출 순서까지 그대로 재현한다.
            double capped = IdleRewardFormula.CappedElapsedSeconds(elapsedSeconds, AutoBattleMaxSeconds);

            double expected = GoldenBaseline.Double(GoldenBaseline.StableSection, goldenKey);
            double actual = IdleRewardFormula.AutoBattleClearCount(capped, SecondsPerAutoBattleClear);

            Assert.That(actual, Is.EqualTo(expected), FailureMessage(goldenKey, Num(expected), Num(actual)));
        }

        [TestCaseSource(nameof(Progress01Cases))]
        public void Progress01_MatchesGolden(string goldenKey, double elapsedSeconds)
        {
            // 여기도 상한을 먼저 먹인 값이 인자다. 덤퍼와 같은 capped 변수를 쓴다.
            double capped = IdleRewardFormula.CappedElapsedSeconds(elapsedSeconds, AutoBattleMaxSeconds);

            // float 로 읽는다. double 로 읽어 비교하면 골든의 9자리 표기가 double 로
            // 승격되면서 float 결과와 절대 같아지지 않는다.
            float expected = GoldenBaseline.Float(GoldenBaseline.StableSection, goldenKey);
            float actual = IdleRewardFormula.Progress01(capped, AutoBattleMaxSeconds);

            Assert.That(actual, Is.EqualTo(expected), FailureMessage(goldenKey, Num(expected), Num(actual)));
        }

        /// <summary>상한을 넘긴 경과 초를 그대로 넣어도 Clamp01 이 같은 값으로 눌러 주는지 본다.</summary>
        /// <remarks>
        /// 위 Progress01_MatchesGolden 은 덤퍼와 똑같이 <b>상한을 먼저 먹인</b> 값을 넣는다.
        /// 그래서 비율이 항상 [0,1] 안이고 Mathf.Clamp01 이 한 번도 일을 하지 않는다 —
        /// 실제로 Clamp01 을 통째로 지워도 골든 케이스 전부가 초록으로 통과한다(변이 검사로 확인).
        /// 계산식 문서가 "Mathf.Clamp01 을 System.Math.Clamp 로 바꾸지 마라"고 못 박은 그 지점이
        /// 정작 아무 테스트에도 걸리지 않는 상태였다.
        ///
        /// 그래서 여기서는 상한을 안 먹인 원본 초를 그대로 넣는다. 기대값은 여전히 골든의
        /// 같은 키다 — 상한을 넘긴 입력의 진행도는 상한을 먹인 것과 같은 값이어야 하고
        /// 그 값이 이미 골든에 있다. 새 숫자를 손으로 적지 않는다는 규칙을 그대로 지킨다.
        /// </remarks>
        [Test]
        public void Progress01_SaturatesInputsAboveTheCap()
        {
            int checkedCount = 0;

            foreach (var pair in GoldenBaseline.Keys(GoldenBaseline.StableSection, Progress01Prefix))
            {
                string[] arguments = ParseArguments(pair.Key, 1);
                double elapsedSeconds = ParseSeconds(pair.Key, arguments[0]);
                if (elapsedSeconds <= AutoBattleMaxSeconds)
                    continue;

                float expected = GoldenBaseline.Float(GoldenBaseline.StableSection, pair.Key);
                float actual = IdleRewardFormula.Progress01(elapsedSeconds, AutoBattleMaxSeconds);

                Assert.That(actual, Is.EqualTo(expected), FailureMessage(pair.Key, Num(expected), Num(actual)));
                checkedCount++;
            }

            // 상한을 넘긴 입력이 골든에서 사라지면 이 테스트는 0건을 돌고 조용히 통과한다.
            // 그 순간 Clamp01 은 다시 무방비가 되므로 여기서 막는다.
            Assert.That(checkedCount, Is.GreaterThan(0), string.Format(
                CultureInfo.InvariantCulture,
                "골든에 상한({0}초)을 넘긴 progress01 입력이 하나도 없다. Clamp01 이 검사되지 않는다. 파일: {1}",
                Num(AutoBattleMaxSeconds), GoldenBaseline.FilePath));
        }

        [TestCaseSource(nameof(MeatSetsCases))]
        public void StoredMeatSetCount_MatchesGolden(string goldenKey, double elapsedSeconds)
        {
            // 고기는 8시간 상한이 아니라 세트 수 상한으로 막힌다 — 그래서 raw elapsed 를 넘긴다.
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, goldenKey);
            int actual = IdleRewardFormula.StoredMeatSetCount(elapsedSeconds, MeatSetIntervalSeconds, MaxMeatSetCount);

            Assert.That(actual, Is.EqualTo(expected), FailureMessage(goldenKey, Num(expected), Num(actual)));
        }

        [TestCaseSource(nameof(SecondsUntilNextMeatSetCases))]
        public void SecondsUntilNextMeatSet_MatchesGolden(string goldenKey, double elapsedSeconds)
        {
            double expected = GoldenBaseline.Double(GoldenBaseline.StableSection, goldenKey);
            double actual = IdleRewardFormula.SecondsUntilNextMeatSet(elapsedSeconds, MeatSetIntervalSeconds);

            Assert.That(actual, Is.EqualTo(expected), FailureMessage(goldenKey, Num(expected), Num(actual)));
        }

        /// <summary>골든에 담당 키가 예상한 개수만큼 남아 있는지 본다.</summary>
        /// <remarks>
        /// 케이스가 골든에서 생성되므로 "골든이 짧아지는" 회귀는 케이스 수로는 안 잡힌다.
        /// 이 테스트가 그 하나를 담당한다.
        /// </remarks>
        [Test]
        public void OwnedGoldenKeyCounts_AreUnchanged()
        {
            foreach (var pair in OwnedKeyCounts)
            {
                int actual = GoldenBaseline.Keys(GoldenBaseline.StableSection, pair.Key).Count;
                Assert.That(actual, Is.EqualTo(pair.Value), string.Format(
                    CultureInfo.InvariantCulture,
                    "골든의 '{0}...]' 키가 {1}개다(기대 {2}개). 덤퍼 입력이 바뀌었거나 줄이 사라졌다. " +
                    "기준선을 다시 뜬 것이 의도한 변경이면 이 표의 숫자를 같이 고칠 것. 파일: {3}",
                    pair.Key, actual, pair.Value, GoldenBaseline.FilePath));
            }
        }

        /// <summary>담당 골든 키를 하나도 남기지 않고 케이스로 소비했는지 본다.</summary>
        /// <remarks>파싱이 조용히 키를 흘리면 테스트 수만 줄고 초록으로 통과한다 — 그것을 막는다.</remarks>
        [Test]
        public void EveryOwnedGoldenKey_IsConsumedByACase()
        {
            var consumed = new Dictionary<string, int>
            {
                { ElapsedSecondsPrefix, CountCases(ElapsedSecondsCases()) },
                { CappedPrefix, CountCases(CappedCases()) },
                { ClearCountPrefix, CountCases(ClearCountCases()) },
                { Progress01Prefix, CountCases(Progress01Cases()) },
                { MeatSetsPrefix, CountCases(MeatSetsCases()) },
                { SecondsUntilNextMeatSetPrefix, CountCases(SecondsUntilNextMeatSetCases()) },
            };

            Assert.That(consumed.Count, Is.EqualTo(OwnedKeyCounts.Count),
                "담당 접두사 하나가 케이스 소스 없이 남았다. OwnedKeyCounts 와 이 목록이 갈라졌다.");

            foreach (var pair in OwnedKeyCounts)
            {
                Assert.That(consumed[pair.Key], Is.EqualTo(pair.Value), string.Format(
                    CultureInfo.InvariantCulture,
                    "'{0}...]' 골든 키 {1}개 중 케이스로 소비한 것이 {2}개다. 검사되지 않는 키가 남았다.",
                    pair.Key, pair.Value, consumed[pair.Key]));
            }
        }

        private static IEnumerable<TestCaseData> ElapsedSecondsCases()
        {
            foreach (var pair in GoldenBaseline.Keys(GoldenBaseline.StableSection, ElapsedSecondsPrefix))
            {
                string[] arguments = ParseArguments(pair.Key, 2);
                yield return new TestCaseData(
                    pair.Key,
                    ParseTicks(pair.Key, arguments[0]),
                    ParseTicks(pair.Key, arguments[1]));
            }
        }

        private static IEnumerable<TestCaseData> CappedCases()
        {
            return ElapsedArgumentCases(CappedPrefix);
        }

        private static IEnumerable<TestCaseData> ClearCountCases()
        {
            return ElapsedArgumentCases(ClearCountPrefix);
        }

        private static IEnumerable<TestCaseData> Progress01Cases()
        {
            return ElapsedArgumentCases(Progress01Prefix);
        }

        private static IEnumerable<TestCaseData> MeatSetsCases()
        {
            return ElapsedArgumentCases(MeatSetsPrefix);
        }

        private static IEnumerable<TestCaseData> SecondsUntilNextMeatSetCases()
        {
            return ElapsedArgumentCases(SecondsUntilNextMeatSetPrefix);
        }

        /// <summary>인자가 "경과 초" 하나뿐인 다섯 접두사가 공유하는 케이스 생성.</summary>
        private static IEnumerable<TestCaseData> ElapsedArgumentCases(string prefix)
        {
            foreach (var pair in GoldenBaseline.Keys(GoldenBaseline.StableSection, prefix))
            {
                string[] arguments = ParseArguments(pair.Key, 1);
                yield return new TestCaseData(pair.Key, ParseSeconds(pair.Key, arguments[0]));
            }
        }

        /// <summary>키의 대괄호 안을 순서대로 잘라 낸다.</summary>
        /// <remarks>
        /// 개수가 안 맞으면 던진다. 인자를 하나 흘린 채로 기본값이 들어가면 다른 계산을
        /// 검사하면서 통과해 버리는데, 그게 이 테스트에서 가장 나쁜 실패다.
        /// </remarks>
        private static string[] ParseArguments(string goldenKey, int expectedCount)
        {
            var arguments = new List<string>(expectedCount);

            int open = goldenKey.IndexOf('[');
            while (open >= 0)
            {
                int close = goldenKey.IndexOf(']', open + 1);
                if (close < 0)
                {
                    throw new InvalidOperationException(
                        string.Format(CultureInfo.InvariantCulture, "골든 키의 대괄호가 안 닫혔다: {0}", goldenKey));
                }

                arguments.Add(goldenKey.Substring(open + 1, close - open - 1));
                open = goldenKey.IndexOf('[', close + 1);
            }

            if (arguments.Count != expectedCount)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "골든 키 {0} 의 인자가 {1}개다(기대 {2}개). 덤퍼의 키 형식이 바뀌었다.",
                    goldenKey, arguments.Count, expectedCount));
            }

            return arguments.ToArray();
        }

        private static long ParseTicks(string goldenKey, string text)
        {
            long value;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture, "골든 키 {0} 의 ticks 인자 '{1}' 를 못 읽었다.", goldenKey, text));
            }

            return value;
        }

        private static double ParseSeconds(string goldenKey, string text)
        {
            // 골든 값과 키의 인자 모두 "R" 표기라 파싱하면 덤퍼가 넣은 비트가 그대로 돌아온다.
            // 그래서 0.0000004 같은 값도 반올림 없이 재현된다.
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture, "골든 키 {0} 의 초 인자 '{1}' 를 못 읽었다.", goldenKey, text));
            }

            return value;
        }

        private static int CountCases(IEnumerable<TestCaseData> cases)
        {
            int count = 0;
            foreach (var unused in cases)
                count++;

            return count;
        }

        /// <summary>실패 메시지. 어느 골든 줄이 깨졌는지 키 이름으로 바로 찾게 한다.</summary>
        private static string FailureMessage(string goldenKey, string expectedText, string actualText)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "골든 키 {0} 가 어긋났다 — 기준선 {1} / 지금 {2}. " +
                "기준선은 현행 동작의 정본이다. 값을 고치지 말고 계산식 변경을 되돌릴 것. 파일: {3}",
                goldenKey, expectedText, actualText, GoldenBaseline.FilePath);
        }

        // 메시지에 찍히는 표기를 덤퍼(GoldenBaselineDumper.Num)와 맞춘다. 실패했을 때
        // 골든 파일을 그대로 grep 할 수 있어야 한다. int 는 "R" 서식이 없으므로 따로 둔다.
        private static string Num(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Num(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Num(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
