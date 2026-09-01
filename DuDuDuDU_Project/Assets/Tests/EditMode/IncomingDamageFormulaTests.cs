using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>
    /// IncomingDamageFormula 골든 특성화 테스트. (MIGRATION_BASELINE 5.1-a2)
    ///
    /// 왜 이 파일이 생겼는가 — 5.1-a 가 받는쪽 산술을 OJ.Core 로 내렸지만 <b>아무도 그것을
    /// 지키지 않았다.</b> 골든 2581키 중 이 경로를 밟는 키 0개, EditMode 496개 중 0개.
    /// 감쇄식과 상태 피해증가를 통째로 지우고 <c>return dmg;</c> 로 바꿔도 전부 초록이었다.
    ///
    /// 목적은 개선이 아니라 고정이다. 기대값을 여기 적지 않는다 — 키를 골든에서 읽고,
    /// 키에서 인자를 파싱해 함수를 부르고, 기대값도 같은 줄에서 읽는다. 인자가 전부 키에
    /// 박혀 있어서 <b>덤퍼에서 손으로 옮겨 적을 것이 없다</b>(core.damage 7줄과 다른 점이다).
    /// 옮겨 적은 것은 "골든에 있어야 할 키 목록"을 만드는 격자뿐이고, 그 격자가 덤퍼와
    /// 갈라지면 GoldenKeySetMatchesDumperGrid 가 양방향으로 잡는다.
    ///
    /// <b>허용 오차를 두지 않는다.</b> int 반환은 그대로 비교하고, float 반환은 덤퍼와 똑같은
    /// 라운드트립 표기("R")로 만들어 <b>문자열이 한 글자라도 다르면 실패</b>다. 그 표기가
    /// 비트 동일성과 같은 강도라는 것은 RoundTripTextIsBitTight 가 따로 증명한다 —
    /// 오차를 두면 CeilToInt 경계가 1 밀리는 회귀(이 식에서 가장 흔한 회귀다)를 통째로 놓친다.
    ///
    /// 이 어셈블리는 Assembly-CSharp 을 참조할 수 없지만 IncomingDamageFormula 는 순수 함수 +
    /// 기본형 인자라 아무 제약이 없다. 받는쪽 전체가 이 픽스처 하나로 재현된다.
    /// </summary>
    [TestFixture]
    public class IncomingDamageFormulaTests
    {
        // --- 골든 키 이름 ---------------------------------------------------------------
        //
        // 이 구획 전체의 뿌리. 아래 EveryIncomingKeyIsConsumed 가 "이걸로 시작하는데 아무
        // 접두사에도 안 걸리는 키"를 잡는다 — 덤퍼에 core.incoming.* 하위 구획이 새로 생겼는데
        // 테스트를 안 붙이면 거기서 터진다.
        private const string SectionRoot = "core.incoming.";

        // 접두사에 '[' 를 붙인다. "core.incoming.stateBonus" 만 쓰면 나중에
        // core.incoming.stateBonusClamped 같은 키가 생겼을 때 조용히 딸려 들어와
        // 인자 파싱이 엉뚱한 데서 터진다. (stateBonus / stateActive 처럼 앞부분이 겹치는
        // 이름이 실제로 둘 있으므로 이건 이론이 아니다.)
        private const string DefMulName = SectionRoot + "defMul";
        private const string StateBonusName = SectionRoot + "stateBonus";
        private const string TotalBonusName = SectionRoot + "totalBonus";
        private const string AppliedName = SectionRoot + "applied";
        private const string StateActiveName = SectionRoot + "stateActive";
        private const string BonusExpiredName = SectionRoot + "bonusExpired";
        private const string WallHpName = SectionRoot + "wallHp";
        private const string WallRatioOnDamageName = SectionRoot + "wallRatioOnDamage";
        private const string WallRatioClampedName = SectionRoot + "wallRatioClamped";

        private const string DefMulPrefix = DefMulName + "[";
        private const string StateBonusPrefix = StateBonusName + "[";
        private const string TotalBonusPrefix = TotalBonusName + "[";
        private const string AppliedPrefix = AppliedName + "[";
        private const string StateActivePrefix = StateActiveName + "[";
        private const string BonusExpiredPrefix = BonusExpiredName + "[";
        private const string WallHpPrefix = WallHpName + "[";
        private const string WallRatioOnDamagePrefix = WallRatioOnDamageName + "[";
        private const string WallRatioClampedPrefix = WallRatioClampedName + "[";

        private static readonly string[] HandledPrefixes =
        {
            DefMulPrefix,
            StateBonusPrefix,
            TotalBonusPrefix,
            AppliedPrefix,
            StateActivePrefix,
            BonusExpiredPrefix,
            WallHpPrefix,
            WallRatioOnDamagePrefix,
            WallRatioClampedPrefix,
        };

        // --- 덤퍼에서 옮겨 적은 입력 격자 -------------------------------------------------
        // 출처: Assets/Scripts/SceneFlow/GoldenBaselineDumper.cs 의 DumpIncomingDamage.
        // 계산 인자는 키에서 파싱하므로 이 격자는 오직 "골든에 있어야 할 키 목록"을 만드는 데만 쓴다.
        // 각 값의 선정 근거(어떤 변이를 잡는가)는 덤퍼 쪽 주석에 실측치와 함께 적혀 있다.
        // 덤퍼가 격자를 좁히면(케이스가 조용히 사라지면) GoldenKeySetMatchesDumperGrid 가 잡는다.
        private static readonly int[] Armors =
        {
            -100000, -300, -200, -101, -100, -99, -50, -1, 0, 1, 50, 99, 100, 101, 200, 300, 100000,
        };

        private static readonly bool[] Flags = { false, true };
        private static readonly int[] SlowRelicPercents = { -100, -30, -15, -1, 0, 1, 7, 15, 40 };

        private static readonly int[][] TotalBonusRows =
        {
            new[] { 0, 0, 0, 0, 0, 0, 0 },
            new[] { 1, 2, 4, 8, 16, 32, 64 },
            new[] { 64, 32, 16, 8, 4, 2, 1 },
            new[] { 10, 20, 10, 15, 10, 0, 30 },
            new[] { -100, 0, 0, 0, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0, -150 },
            new[] { 10, 20, 10, 15, 10, 25, -30 },
        };

        private static readonly int[] Dmgs = { 0, 1, 2, 3, 7, 9, 100, 1000000, 16777217 };
        private static readonly int[] Defenses = { -300, -100, -50, -1, 0, 1, 50, 100, 300 };
        private static readonly int[] Bonuses = { -200, -100, -50, -1, 0, 15, 50, 100 };

        // 시간 축은 float 이라 키에 들어가는 문자열도 덤퍼와 같은 표기여야 한다.
        // 그래서 리터럴을 그대로 옮겨 적고 같은 Format() 으로 키를 만든다 — 표기를 손으로
        // 적으면 1ULP 값(0.99999994 / 1.00000012)에서 어긋난다.
        private static readonly float[][] TimePairs =
        {
            new[] { 0f, -1f }, new[] { -1f, -1f }, new[] { 0f, 0f }, new[] { 0f, 1f },
            new[] { 1f, 1f }, new[] { 1.00000012f, 1f }, new[] { 0.99999994f, 1f },
            new[] { 100f, 100f }, new[] { 100f, 99.99999f },
            new[] { float.NaN, 1f }, new[] { 1f, float.NaN },
            new[] { float.PositiveInfinity, 1f },
        };

        private static readonly int[][] WallHpPairs =
        {
            new[] { 0, 0 }, new[] { 10, 0 }, new[] { 10, 9 }, new[] { 10, 10 },
            new[] { 10, 11 }, new[] { 10, 1000 }, new[] { 0, 5 }, new[] { 10, -5 },
            new[] { -3, 0 }, new[] { -3, 2 }, new[] { 2147483647, 1 }, new[] { 1000000, 999999 },
        };

        private static readonly int[][] WallRatioPairs =
        {
            new[] { 0, 0 }, new[] { 1, 0 }, new[] { -1, 0 },
            new[] { 0, 100 }, new[] { 1, 3 }, new[] { 50, 100 }, new[] { 99, 100 },
            new[] { 100, 100 }, new[] { 101, 100 }, new[] { 150, 100 },
            new[] { -20, 100 }, new[] { 1, 1000000 },
            new[] { -20, -100 }, new[] { -1, -1 },
        };

        // 5.1-a2 기준선 시점의 담당 키 개수.
        // 17 + 36 + 7 + 648 + 12 + 12 + 12 + 14 + 14 = 772.
        private const int HandledKeyCount = 772;

        private const int DefMulKeyCount = 17;          // Armors
        private const int StateBonusKeyCount = 36;      // 2 * 2 * 9
        private const int TotalBonusKeyCount = 7;       // TotalBonusRows
        private const int AppliedKeyCount = 648;        // 9 * 9 * 8
        private const int TimeKeyCount = 12;            // TimePairs (stateActive / bonusExpired 각각)
        private const int WallHpKeyCount = 12;          // WallHpPairs
        private const int WallRatioKeyCount = 14;       // WallRatioPairs (두 함수 각각)

        // --- 케이스 소스 -----------------------------------------------------------------

        public static IEnumerable<TestCaseData> DefMulKeys()
        {
            return KeyCases(DefMulPrefix);
        }

        public static IEnumerable<TestCaseData> StateBonusKeys()
        {
            return KeyCases(StateBonusPrefix);
        }

        public static IEnumerable<TestCaseData> TotalBonusKeys()
        {
            return KeyCases(TotalBonusPrefix);
        }

        public static IEnumerable<TestCaseData> AppliedKeys()
        {
            return KeyCases(AppliedPrefix);
        }

        public static IEnumerable<TestCaseData> StateActiveKeys()
        {
            return KeyCases(StateActivePrefix);
        }

        public static IEnumerable<TestCaseData> BonusExpiredKeys()
        {
            return KeyCases(BonusExpiredPrefix);
        }

        public static IEnumerable<TestCaseData> WallHpKeys()
        {
            return KeyCases(WallHpPrefix);
        }

        public static IEnumerable<TestCaseData> WallRatioOnDamageKeys()
        {
            return KeyCases(WallRatioOnDamagePrefix);
        }

        public static IEnumerable<TestCaseData> WallRatioClampedKeys()
        {
            return KeyCases(WallRatioClampedPrefix);
        }

        // --- 값 검증 ---------------------------------------------------------------------

        [TestCaseSource(nameof(DefMulKeys))]
        public void DefenseMultiplierMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 1);
            int defense = ParseInt(arguments[0], key);

            AssertGolden(key, Format(IncomingDamageFormula.DefenseMultiplier(defense)));
        }

        [TestCaseSource(nameof(StateBonusKeys))]
        public void StateBonusPercentMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 3);
            bool kingIce = ParseBool(arguments[0], key);
            bool kingPoison = ParseBool(arguments[1], key);
            int slowRelicPercent = ParseInt(arguments[2], key);

            int actual = IncomingDamageFormula.StateBonusPercent(kingIce, kingPoison, slowRelicPercent);

            AssertGolden(key, actual.ToString(CultureInfo.InvariantCulture));
        }

        [TestCaseSource(nameof(TotalBonusKeys))]
        public void TotalBonusPercentMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 7);

            int actual = IncomingDamageFormula.TotalBonusPercent(
                ParseInt(arguments[0], key),
                ParseInt(arguments[1], key),
                ParseInt(arguments[2], key),
                ParseInt(arguments[3], key),
                ParseInt(arguments[4], key),
                ParseInt(arguments[5], key),
                ParseInt(arguments[6], key));

            AssertGolden(key, actual.ToString(CultureInfo.InvariantCulture));
        }

        [TestCaseSource(nameof(AppliedKeys))]
        public void AppliedDamageMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 3);
            int dmg = ParseInt(arguments[0], key);
            int defense = ParseInt(arguments[1], key);
            int totalBonusPercent = ParseInt(arguments[2], key);

            int actual = IncomingDamageFormula.AppliedDamage(dmg, defense, totalBonusPercent);

            AssertGolden(key, actual.ToString(CultureInfo.InvariantCulture));
        }

        [TestCaseSource(nameof(StateActiveKeys))]
        public void IsStateActiveMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 2);
            float now = ParseFloat(arguments[0], key);
            float untilTime = ParseFloat(arguments[1], key);

            AssertGolden(key, IncomingDamageFormula.IsStateActive(now, untilTime).ToString());
        }

        [TestCaseSource(nameof(BonusExpiredKeys))]
        public void IsBonusExpiredMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 2);
            float now = ParseFloat(arguments[0], key);
            float untilTime = ParseFloat(arguments[1], key);

            AssertGolden(key, IncomingDamageFormula.IsBonusExpired(now, untilTime).ToString());
        }

        [TestCaseSource(nameof(WallHpKeys))]
        public void WallHpAfterDamageMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 2);
            int currentHp = ParseInt(arguments[0], key);
            int dmg = ParseInt(arguments[1], key);

            int actual = IncomingDamageFormula.WallHpAfterDamage(currentHp, dmg);

            AssertGolden(key, actual.ToString(CultureInfo.InvariantCulture));
        }

        [TestCaseSource(nameof(WallRatioOnDamageKeys))]
        public void WallHpBarRatioOnDamageMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 2);
            int currentHp = ParseInt(arguments[0], key);
            int totalHp = ParseInt(arguments[1], key);

            AssertGolden(key, Format(IncomingDamageFormula.WallHpBarRatioOnDamage(currentHp, totalHp)));
        }

        [TestCaseSource(nameof(WallRatioClampedKeys))]
        public void WallHpBarRatioClampedMatchesGolden(string key)
        {
            string[] arguments = ParseArguments(key, 2);
            int currentHp = ParseInt(arguments[0], key);
            int totalHp = ParseInt(arguments[1], key);

            AssertGolden(key, Format(IncomingDamageFormula.WallHpBarRatioClamped(currentHp, totalHp)));
        }

        // --- 누락 방지 -------------------------------------------------------------------

        /// <summary>
        /// 담당 접두사의 골든 키 개수와 케이스 개수가 5.1-a2 기준선과 같은지 본다.
        /// 케이스 소스가 골든에서 키를 뽑아 오므로 두 수는 원래 같지만, 기대 개수를 상수로
        /// 박아 두면 <b>덤퍼가 격자를 줄여</b> "케이스 3개로 전부 통과" 같은 상태가 됐을 때
        /// 여기서 먼저 터진다. 일부만 검사하면서 통과하는 테스트가 가장 위험하다.
        /// </summary>
        [TestCase(DefMulPrefix, DefMulKeyCount)]
        [TestCase(StateBonusPrefix, StateBonusKeyCount)]
        [TestCase(TotalBonusPrefix, TotalBonusKeyCount)]
        [TestCase(AppliedPrefix, AppliedKeyCount)]
        [TestCase(StateActivePrefix, TimeKeyCount)]
        [TestCase(BonusExpiredPrefix, TimeKeyCount)]
        [TestCase(WallHpPrefix, WallHpKeyCount)]
        [TestCase(WallRatioOnDamagePrefix, WallRatioKeyCount)]
        [TestCase(WallRatioClampedPrefix, WallRatioKeyCount)]
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
        /// 양방향으로 잡는다. 개수만 세면 하나 지우고 하나 넣는 변경이 통과해 버린다.
        /// </summary>
        [Test]
        public void GoldenKeySetMatchesDumperGrid()
        {
            var expected = new SortedSet<string>(ExpectedKeys(), StringComparer.Ordinal);
            Assert.That(expected.Count, Is.EqualTo(HandledKeyCount),
                "덤퍼 격자에서 만들어진 키 개수가 5.1-a2 기준선과 다르다. 격자 상수를 잘못 옮겼거나 " +
                "키가 겹쳐서 SortedSet 에서 합쳐졌다.");

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

        /// <summary>
        /// <c>core.incoming.</c> 으로 시작하는 골든 키가 <b>한 줄도 남김없이</b> 담당 접두사에
        /// 걸리는지 본다.
        ///
        /// 위 두 테스트는 "내가 아는 접두사"만 본다. 덤퍼에 core.incoming.foo 라는 하위 구획이
        /// 새로 생기고 여기 접두사를 안 늘리면, 그 키는 아무도 안 두드리는데 전부 초록이다.
        /// 그 조용한 구멍을 막는다. (core.dmgChain 324줄이 지금 정확히 그 상태다 — 소비하는
        /// 픽스처가 없다. 같은 일을 여기서 반복하지 않으려는 것이다.)
        ///
        /// 접두사가 정확히 하나만 걸리는지도 같이 본다. 두 개가 걸리면 한 키를 두 픽스처가
        /// 서로 다른 인자 개수로 파싱하게 된다.
        /// </summary>
        [Test]
        public void EveryIncomingKeyIsConsumed()
        {
            var orphans = new List<string>();
            var ambiguous = new List<string>();

            foreach (var pair in GoldenBaseline.Keys(GoldenBaseline.StableSection, SectionRoot))
            {
                int matches = 0;
                for (int i = 0; i < HandledPrefixes.Length; i++)
                {
                    if (pair.Key.StartsWith(HandledPrefixes[i], StringComparison.Ordinal))
                        matches++;
                }

                if (matches == 0)
                    orphans.Add(pair.Key);
                else if (matches > 1)
                    ambiguous.Add(pair.Key);
            }

            orphans.Sort(StringComparer.Ordinal);
            ambiguous.Sort(StringComparer.Ordinal);

            Assert.That(string.Join(", ", orphans), Is.EqualTo(string.Empty),
                "'" + SectionRoot + "' 로 시작하는데 아무 테스트도 두드리지 않는 키다. " +
                "덤퍼에 하위 구획을 늘렸으면 HandledPrefixes 와 ExpectedKeys 도 같이 늘릴 것.");
            Assert.That(string.Join(", ", ambiguous), Is.EqualTo(string.Empty),
                "두 접두사에 동시에 걸리는 키다. 접두사 이름이 서로를 가린다 — 이름을 갈라야 한다.");
        }

        /// <summary>
        /// 접두사끼리 서로를 가리지 않는지 <b>문자열로 직접</b> 확인한다.
        ///
        /// 위 EveryIncomingKeyIsConsumed 는 골든 파일이 있어야 돌지만, 이 검사는 상수만 보므로
        /// 기준선이 아직 없어도 돈다. stateBonus / stateActive 처럼 앞부분이 겹치는 이름이
        /// 실제로 있어서 이건 이론이 아니다 — '[' 를 안 붙였으면 지금 터졌을 것이다.
        /// </summary>
        [Test]
        public void HandledPrefixesDoNotShadowEachOther()
        {
            var shadowed = new List<string>();
            for (int i = 0; i < HandledPrefixes.Length; i++)
            {
                for (int j = 0; j < HandledPrefixes.Length; j++)
                {
                    if (i == j)
                        continue;

                    if (HandledPrefixes[i].StartsWith(HandledPrefixes[j], StringComparison.Ordinal))
                        shadowed.Add(HandledPrefixes[i] + " 가 " + HandledPrefixes[j] + " 로 시작한다");
                }
            }

            Assert.That(string.Join(", ", shadowed), Is.EqualTo(string.Empty),
                "한 접두사가 다른 접두사로 시작한다. 넓은 쪽이 좁은 쪽 키까지 가져가 " +
                "인자 파싱이 엉뚱한 데서 터진다.");
        }

        /// <summary>
        /// 담당 접두사가 <b>다른 픽스처의 담당 구역을 침범하지 않는지</b> 본다.
        ///
        /// DamageFormulaTests 의 KeyPrefix 는 <c>"core.damage"</c> 이고 StartsWith 로 훑는다.
        /// core.damage 로 시작하는 키를 새로 만들면 그 픽스처의 EveryGoldenKeyIsConsumed 가
        /// "테스트가 안 두드리는 키"라며 터지는데, 그것도 <b>사람이 F7 로 기준선을 다시 뜬
        /// 뒤에야</b> 터진다 — 덤퍼를 고친 사람이 아니라 기준선을 뜬 사람이 맞는다.
        /// 그 시차를 없애려고 여기서 상수만으로 미리 확인한다.
        /// </summary>
        [Test]
        public void HandledPrefixesDoNotCollideWithOtherFixtures()
        {
            // 다른 픽스처가 StartsWith 로 소비하는 접두사 전부.
            // (DamageFormulaTests / StageGrowthFormulaTests / StageRewardFormulaTests / IdleRewardFormulaTests)
            string[] foreign =
            {
                "core.damage",
                "core.bossSpawnThreshold[", "core.resolvedBaseDefense[", "core.monsterHp[",
                "core.monsterDefense[", "core.bossHp[", "core.bossDefense[",
                "core.stageBonus", "core.guaranteedGold", "core.clearGradeTier", "core.scaleAmount",
                "core.elapsedSeconds[", "core.capped[", "core.clearCount[", "core.progress01[",
                "core.meatSets[", "core.secondsUntilNextMeatSet[",
                "reward.accumulatedGold", "reward.guaranteedGold",
            };

            var collisions = new List<string>();
            for (int i = 0; i < HandledPrefixes.Length; i++)
            {
                for (int j = 0; j < foreign.Length; j++)
                {
                    // 내 키가 남의 접두사로 시작하면 남이 내 키를 가져간다.
                    if (HandledPrefixes[i].StartsWith(foreign[j], StringComparison.Ordinal))
                        collisions.Add(HandledPrefixes[i] + " → " + foreign[j] + " 가 가져간다");

                    // 남의 접두사가 내 접두사로 시작하면 내가 남의 키를 가져간다.
                    if (foreign[j].StartsWith(HandledPrefixes[i], StringComparison.Ordinal))
                        collisions.Add(foreign[j] + " → " + HandledPrefixes[i] + " 가 가져간다");
                }
            }

            Assert.That(string.Join(", ", collisions), Is.EqualTo(string.Empty),
                "접두사가 다른 픽스처와 겹친다. 겹치면 어느 한쪽의 '전부 소비' 검사가 터지는데, " +
                "그 실패는 덤퍼를 고친 사람이 아니라 기준선을 뜬 사람에게 간다.");
        }

        /// <summary>
        /// float 반환값을 <b>문자열로</b> 비교하는 것이 비트 동일 비교와 같은 강도인지 증명한다.
        ///
        /// 이 픽스처는 DefenseMultiplier / WallHpBarRatio* 를 라운드트립 표기("R")로 만들어
        /// 골든 문자열과 대조한다. 그 비교가 타당하려면 <b>표기 → 파싱이 비트를 보존</b>해야
        /// 한다. 보존되지 않으면 서로 다른 두 float 이 같은 문자열이 되어 검사가 조용히 약해진다.
        ///
        /// 골든 파일을 읽지 않으므로 <b>기준선이 없어도 도는 테스트</b>다.
        /// (NaN 도 여기 포함된다 — 0f/0f 가 만드는 NaN 과 float.NaN 과 파싱된 "NaN" 이
        /// 전부 같은 비트라는 것을 Mono 실측으로 확인했고, 그것이 여기서 다시 검증된다.)
        /// </summary>
        [Test]
        public void RoundTripTextIsBitTight()
        {
            var broken = new List<string>();

            for (int i = 0; i < Armors.Length; i++)
                CheckRoundTrip(IncomingDamageFormula.DefenseMultiplier(Armors[i]), "defMul[" + Armors[i] + "]", broken);

            for (int i = 0; i < WallRatioPairs.Length; i++)
            {
                int hp = WallRatioPairs[i][0];
                int total = WallRatioPairs[i][1];
                CheckRoundTrip(IncomingDamageFormula.WallHpBarRatioOnDamage(hp, total),
                    "wallRatioOnDamage[" + hp + "][" + total + "]", broken);
                CheckRoundTrip(IncomingDamageFormula.WallHpBarRatioClamped(hp, total),
                    "wallRatioClamped[" + hp + "][" + total + "]", broken);
            }

            // 시간 축은 키 <b>인자</b>가 float 이라 방향이 반대다 — 덤퍼가 찍은 표기를
            // 테스트가 파싱해 도로 float 으로 만든다. 여기가 어긋나면 키는 맞는데 인자가
            // 다른 함수를 부르게 된다.
            for (int i = 0; i < TimePairs.Length; i++)
            {
                CheckRoundTrip(TimePairs[i][0], "timePairs[" + i + "].now", broken);
                CheckRoundTrip(TimePairs[i][1], "timePairs[" + i + "].until", broken);
            }

            Assert.That(string.Join(", ", broken), Is.EqualTo(string.Empty),
                "라운드트립 표기가 비트를 보존하지 못한다. 그러면 이 픽스처의 float 비교가 " +
                "비트 동일 비교보다 약해진다 — 표기 방식을 바꾸거나 비교를 비트로 내려야 한다.");
        }

        private static void CheckRoundTrip(float value, string label, List<string> broken)
        {
            string text = Format(value);

            float parsed;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                broken.Add(label + " 표기 '" + text + "' 를 되읽지 못했다");
                return;
            }

            if (!SameBits(parsed, value))
                broken.Add(label + " 표기 '" + text + "' 가 비트를 잃었다");
        }

        // --- 보조 ------------------------------------------------------------------------

        /// <summary>덤퍼 격자로 만들어 낸, 골든에 있어야 할 키 전부.</summary>
        private static IEnumerable<string> ExpectedKeys()
        {
            for (int i = 0; i < Armors.Length; i++)
                yield return DefMulName + "[" + Armors[i] + "]";

            for (int i = 0; i < Flags.Length; i++)
            {
                for (int j = 0; j < Flags.Length; j++)
                {
                    for (int k = 0; k < SlowRelicPercents.Length; k++)
                    {
                        yield return StateBonusName + "[" + Flags[i] + "][" + Flags[j] + "][" +
                                     SlowRelicPercents[k] + "]";
                    }
                }
            }

            for (int i = 0; i < TotalBonusRows.Length; i++)
            {
                int[] row = TotalBonusRows[i];
                yield return TotalBonusName + "[" + row[0] + "][" + row[1] + "][" + row[2] + "][" +
                             row[3] + "][" + row[4] + "][" + row[5] + "][" + row[6] + "]";
            }

            for (int d = 0; d < Dmgs.Length; d++)
            {
                for (int a = 0; a < Defenses.Length; a++)
                {
                    for (int b = 0; b < Bonuses.Length; b++)
                        yield return AppliedName + "[" + Dmgs[d] + "][" + Defenses[a] + "][" + Bonuses[b] + "]";
                }
            }

            for (int i = 0; i < TimePairs.Length; i++)
            {
                string arguments = "[" + Format(TimePairs[i][0]) + "][" + Format(TimePairs[i][1]) + "]";
                yield return StateActiveName + arguments;
                yield return BonusExpiredName + arguments;
            }

            for (int i = 0; i < WallHpPairs.Length; i++)
                yield return WallHpName + "[" + WallHpPairs[i][0] + "][" + WallHpPairs[i][1] + "]";

            for (int i = 0; i < WallRatioPairs.Length; i++)
            {
                string arguments = "[" + WallRatioPairs[i][0] + "][" + WallRatioPairs[i][1] + "]";
                yield return WallRatioOnDamageName + arguments;
                yield return WallRatioClampedName + arguments;
            }
        }

        /// <summary>
        /// 접두사에 걸리는 골든 키를 케이스로 만든다. 키 자체를 인자로 넘겨서 실패 목록에 키가
        /// 그대로 뜬다. GoldenBaseline.Keys 는 0건이면 예외라, 파일이 비거나 접두사가 어긋나면
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

        /// <summary>
        /// 골든 값과 <b>문자열로</b> 대조한다. int / bool / float 을 한 경로로 다루려는 것이고,
        /// 무엇보다 허용 오차가 끼어들 자리를 없애려는 것이다. float 쪽에서 이 비교가
        /// 비트 동일 비교와 같은 강도라는 근거는 RoundTripTextIsBitTight 에 있다.
        /// </summary>
        private static void AssertGolden(string key, string actual)
        {
            string expected = GoldenBaseline.Raw(GoldenBaseline.StableSection, key);

            // 실패 메시지에 키를 박는다 — 어느 골든 줄이 깨졌는지가 첫 줄에 보여야 한다.
            Assert.That(actual, Is.EqualTo(expected), string.Format(
                "골든이 깨졌다: {0}{1}기준선 {2} → 지금 {3}{4}기준선 파일: {5}{6}" +
                "기준선을 지금 값으로 고치지 말 것. 계산식(또는 덤퍼 격자)이 왜 바뀌었는지가 먼저다.",
                key, Environment.NewLine, expected, actual, Environment.NewLine,
                GoldenBaseline.FilePath, Environment.NewLine));
        }

        /// <summary>덤퍼의 Num(float) 과 <b>같은 표기</b>여야 한다. 갈라지면 키도 값도 어긋난다.</summary>
        private static string Format(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool SameBits(float left, float right)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(left), 0)
                   == BitConverter.ToInt32(BitConverter.GetBytes(right), 0);
        }

        /// <summary>키의 대괄호 인자를 순서대로 뽑는다. core.incoming.applied[7][-50][50] → 7, -50, 50.</summary>
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
        /// 키의 bool 인자. 덤퍼는 C# 기본 표기("True"/"False")로 찍으므로 그대로 되읽는다.
        /// bool.TryParse 는 대소문자를 가리지 않지만, 표기가 바뀐 것 자체가 신호라
        /// 키 모양이 달라지면 위 ExpectedKeys 대조에서 먼저 잡힌다.
        /// </summary>
        private static bool ParseBool(string text, string key)
        {
            bool value;
            if (!bool.TryParse(text, out value))
                throw new FormatException(string.Format("골든 키 {0} 의 bool 인자를 못 읽었다: '{1}'", key, text));

            return value;
        }

        /// <summary>
        /// 키에 적힌 실수 인자를 float 으로 되돌린다.
        /// 골든 표기는 라운드트립("R")이라 되읽으면 덤퍼가 넘긴 값과 비트가 같다 —
        /// NaN / Infinity 도 포함해서 RoundTripTextIsBitTight 가 그것을 검증한다.
        /// double 로 받아 넘기면 승격 시점이 달라져 비교 경계에서 값이 갈릴 수 있다.
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
