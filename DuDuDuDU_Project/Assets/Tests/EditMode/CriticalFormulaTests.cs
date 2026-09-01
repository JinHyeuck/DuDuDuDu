using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 배수 3단(크리 → 일반 lv12 더블 → 유물)을 골든으로 잠근다. (MIGRATION_BASELINE 5.1-b)
    ///
    /// 지키는 것은 <b>세 단이 각각 따로 정수로 접힌다</b>는 사실이다. 1·2단을 하나의
    /// 곱으로 합치면 damage 2/3/7/8/12 에서 값이 갈리고, 3단의 하한 1 을 빼면
    /// damage 0 에서 갈린다. 격자가 그 점들을 일부러 밟는다.
    ///
    /// <b>허용 오차를 쓰지 않는다.</b> 전부 int 비교이고, 실수 인자는 키에 적힌 문자열을
    /// 그대로 파싱해 넘긴다 — 덤퍼가 "R" 라운드트립 표기로 적으므로 비트가 보존된다.
    /// </summary>
    [TestFixture]
    public class CriticalFormulaTests
    {
        private const string CriticalDamagePrefix = "core.crit.criticalDamage[";
        private const string DoubleHitDamagePrefix = "core.crit.doubleHitDamage[";
        private const string RelicDamagePrefix = "core.crit.relicDamage[";
        private const string AppliedPrefix = "core.crit.applied[";
        private const string ChanceActivePrefix = "core.crit.chanceActive[";
        private const string RollCriticalPrefix = "core.crit.rollHitsCritical[";
        private const string RollDoubleHitPrefix = "core.crit.rollHitsDoubleHit[";
        private const string DoubleHitLevelPrefix = "core.crit.doubleHitLevel[";

        private static readonly string[] HandledPrefixes =
        {
            CriticalDamagePrefix, DoubleHitDamagePrefix, RelicDamagePrefix, AppliedPrefix,
            ChanceActivePrefix, RollCriticalPrefix, RollDoubleHitPrefix, DoubleHitLevelPrefix,
        };

        // --- 케이스 소스 ---------------------------------------------------------------

        private static IEnumerable<TestCaseData> KeyCases(string prefix)
        {
            var keys = new List<string>(
                GoldenBaseline.Keys(GoldenBaseline.StableSection, prefix).Keys);
            keys.Sort(StringComparer.Ordinal);

            var cases = new List<TestCaseData>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
                cases.Add(new TestCaseData(keys[i]).SetName(prefix + "#" + keys[i]));

            return cases;
        }

        public static IEnumerable<TestCaseData> CriticalDamageKeys() => KeyCases(CriticalDamagePrefix);
        public static IEnumerable<TestCaseData> DoubleHitDamageKeys() => KeyCases(DoubleHitDamagePrefix);
        public static IEnumerable<TestCaseData> RelicDamageKeys() => KeyCases(RelicDamagePrefix);
        public static IEnumerable<TestCaseData> AppliedKeys() => KeyCases(AppliedPrefix);
        public static IEnumerable<TestCaseData> ChanceActiveKeys() => KeyCases(ChanceActivePrefix);
        public static IEnumerable<TestCaseData> RollCriticalKeys() => KeyCases(RollCriticalPrefix);
        public static IEnumerable<TestCaseData> RollDoubleHitKeys() => KeyCases(RollDoubleHitPrefix);
        public static IEnumerable<TestCaseData> DoubleHitLevelKeys() => KeyCases(DoubleHitLevelPrefix);

        // --- 골든 대조 -----------------------------------------------------------------

        [TestCaseSource(nameof(CriticalDamageKeys))]
        public void CriticalDamageMatchesGolden(string key)
        {
            string[] args = Arguments(key, 2);
            int actual = CriticalFormula.CriticalDamage(Int(args[0], key), Float(args[1], key));
            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(DoubleHitDamageKeys))]
        public void DoubleHitDamageMatchesGolden(string key)
        {
            string[] args = Arguments(key, 1);
            AssertGolden(key, CriticalFormula.DoubleHitDamage(Int(args[0], key)));
        }

        [TestCaseSource(nameof(RelicDamageKeys))]
        public void RelicDamageMatchesGolden(string key)
        {
            string[] args = Arguments(key, 2);
            int actual = CriticalFormula.RelicDamage(Int(args[0], key), Float(args[1], key));
            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(AppliedKeys))]
        public void ApplyCriticalMatchesGolden(string key)
        {
            string[] args = Arguments(key, 4);
            int actual = CriticalFormula.ApplyCritical(
                Int(args[0], key), Bool(args[1], key), 2.2f, Bool(args[2], key), Bool(args[3], key), 1.15f);
            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(ChanceActiveKeys))]
        public void IsCriticalChanceActiveMatchesGolden(string key)
        {
            string[] args = Arguments(key, 1);
            AssertGolden(key, CriticalFormula.IsCriticalChanceActive(Float(args[0], key)));
        }

        [TestCaseSource(nameof(RollCriticalKeys))]
        public void RollHitsCriticalMatchesGolden(string key)
        {
            string[] args = Arguments(key, 2);
            bool actual = CriticalFormula.RollHitsCritical(Float(args[0], key), Float(args[1], key));
            AssertGolden(key, actual);
        }

        [TestCaseSource(nameof(RollDoubleHitKeys))]
        public void RollHitsDoubleHitMatchesGolden(string key)
        {
            string[] args = Arguments(key, 1);
            AssertGolden(key, CriticalFormula.RollHitsDoubleHit(Float(args[0], key)));
        }

        [TestCaseSource(nameof(DoubleHitLevelKeys))]
        public void IsDoubleHitLevelMatchesGolden(string key)
        {
            string[] args = Arguments(key, 1);
            AssertGolden(key, CriticalFormula.IsDoubleHitLevel(Int(args[0], key)));
        }

        // --- 누락 방지 -----------------------------------------------------------------

        /// <summary>
        /// 담당 접두사의 골든 키를 <b>전부</b> 소비하는지 센다. 일부만 검사하면서
        /// 통과하는 테스트가 가장 위험하다 — 이 프로젝트에서 실제로 두 번 나왔다.
        /// </summary>
        [Test]
        public void EveryCriticalKeyIsConsumed()
        {
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < HandledPrefixes.Length; i++)
            {
                foreach (var pair in GoldenBaseline.Keys(GoldenBaseline.StableSection, HandledPrefixes[i]))
                    consumed.Add(pair.Key);
            }

            var all = new List<string>();
            foreach (var pair in GoldenBaseline.Section(GoldenBaseline.StableSection))
            {
                if (pair.Key.StartsWith("core.crit.", StringComparison.Ordinal))
                    all.Add(pair.Key);
            }

            all.Sort(StringComparer.Ordinal);
            var missed = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                if (!consumed.Contains(all[i]))
                    missed.Add(all[i]);
            }

            Assert.That(string.Join(", ", missed), Is.EqualTo(string.Empty),
                "core.crit.* 키인데 어느 픽스처도 읽지 않는다. 덤퍼가 구획을 늘렸으면 " +
                "HandledPrefixes 에도 추가할 것.");
            Assert.That(consumed.Count, Is.EqualTo(all.Count));
        }

        /// <summary>
        /// 다른 픽스처의 접두사를 잡아먹지 않는지. StartsWith 로 훑기 때문에
        /// core.crit 이 core.critX 를 삼키는 사고가 가능하다.
        /// </summary>
        [Test]
        public void PrefixesDoNotShadowOtherFixtures()
        {
            string[] others = { "core.damage", "core.dmgChain", "core.incoming.", "core.stageBonus" };
            for (int i = 0; i < HandledPrefixes.Length; i++)
            {
                for (int j = 0; j < others.Length; j++)
                {
                    Assert.That(others[j].StartsWith(HandledPrefixes[i], StringComparison.Ordinal), Is.False,
                        HandledPrefixes[i] + " 가 " + others[j] + " 를 잡아먹는다.");
                    Assert.That(HandledPrefixes[i].StartsWith(others[j], StringComparison.Ordinal), Is.False,
                        others[j] + " 가 " + HandledPrefixes[i] + " 를 잡아먹는다.");
                }
            }
        }

        // --- 헬퍼 ---------------------------------------------------------------------

        private static string[] Arguments(string key, int expected)
        {
            int open = key.IndexOf('[');
            Assert.That(open, Is.GreaterThan(-1), "키에 대괄호가 없다: " + key);

            string tail = key.Substring(open);
            var args = new List<string>();
            int cursor = 0;
            while (cursor < tail.Length && tail[cursor] == '[')
            {
                int close = tail.IndexOf(']', cursor);
                Assert.That(close, Is.GreaterThan(cursor), "닫는 대괄호가 없다: " + key);
                args.Add(tail.Substring(cursor + 1, close - cursor - 1));
                cursor = close + 1;
            }

            Assert.That(args.Count, Is.EqualTo(expected), "인자 개수가 다르다: " + key);
            return args.ToArray();
        }

        private static int Int(string text, string key)
        {
            int value;
            Assert.That(int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value),
                Is.True, "정수가 아니다: " + text + " (" + key + ")");
            return value;
        }

        private static float Float(string text, string key)
        {
            float value;
            Assert.That(float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value),
                Is.True, "실수가 아니다: " + text + " (" + key + ")");
            return value;
        }

        private static bool Bool(string text, string key)
        {
            bool value;
            Assert.That(bool.TryParse(text, out value), Is.True, "불리언이 아니다: " + text + " (" + key + ")");
            return value;
        }

        private static void AssertGolden(string key, int actual)
        {
            Assert.That(actual, Is.EqualTo(GoldenBaseline.Int(GoldenBaseline.StableSection, key)), key);
        }

        private static void AssertGolden(string key, bool actual)
        {
            Assert.That(actual.ToString(), Is.EqualTo(GoldenBaseline.Raw(GoldenBaseline.StableSection, key)), key);
        }
    }
}
