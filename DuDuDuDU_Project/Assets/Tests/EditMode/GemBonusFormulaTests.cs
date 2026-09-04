using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 보석 보너스 합산·매칭을 골든으로 잠근다. (MIGRATION_BASELINE 5.2)
    ///
    /// 기대값을 여기 적지 않는다 — <b>키를 골든에서 읽는 것이 아니라 격자에서 만들고</b>,
    /// 계산 인자는 그 키에서 파싱하고, 기대값만 골든에서 읽는다. 방향이 이래야 덤퍼가
    /// 격자를 조용히 좁혔을 때(케이스가 사라졌을 때)도 여기서 터진다. 골든 키 목록에서
    /// 케이스를 만들면 그 사고가 "검사할 것이 줄었다"로 흡수돼 초록불이 난다.
    ///
    /// 효과 집합은 <b>키에 통째로 인코딩돼 있다</b>. 그래서 덤퍼에서 옮겨 적은 것은
    /// 집합 목록 하나뿐이고 percent/flat/intParam 을 손으로 베낀 자리가 없다.
    /// 집합 목록이 덤퍼와 갈라지면 키가 달라져 <see cref="EveryGemBonusKeyIsConsumed"/> 가 잡는다.
    ///
    /// <b>허용 오차를 두지 않는다.</b> int/bool 은 그대로, float 은 덤퍼와 같은 라운드트립
    /// 표기("R")로 만들어 <b>문자열이 한 글자라도 다르면 실패</b>다. 그 비교가 비트 동일 비교와
    /// 같은 강도라는 것은 <see cref="RoundTripTextIsBitTight"/> 가 따로 증명한다.
    ///
    /// <b>골든이 아직 없을 때(F7 전):</b> 이 픽스처의 골든 대조 케이스는 전부 Ignore 로 떨어지고
    /// 러너 요약에 "건너뜀 N" 으로 크게 남는다. 값을 지어내서 채우지 않는다 — 이 프로젝트의
    /// float 기대값은 <b>Mono 가 실제로 내놓은 것</b>이어야 하고, 그것을 뜨는 것은 사람의 F7 이다.
    /// 골든이 없어도 도는 검사(<see cref="RoundTripTextIsBitTight"/> 와 아래 구조 카나리아 4종)는
    /// 그 사이에도 실제로 값을 검증한다.
    /// </summary>
    [TestFixture]
    public class GemBonusFormulaTests
    {
        // --- 골든 키 이름 ---------------------------------------------------------------

        private const string SectionRoot = "core.gemBonus.";

        private const string BaseDiceName = SectionRoot + "baseDice";
        private const string ElementName = SectionRoot + "element";
        private const string MatchName = SectionRoot + "match";
        private const string SumPercentName = SectionRoot + "sumPercent";
        private const string SumFlatName = SectionRoot + "sumFlat";
        private const string CooldownName = SectionRoot + "cooldown";
        private const string FirstNWavesName = SectionRoot + "firstNWaves";
        private const string CooldownCapName = SectionRoot + "cooldownCap";

        // 접두사에 '[' 를 붙인다. "core.gemBonus.cooldown" 만 쓰면 cooldownCap 을 조용히
        // 삼켜서 인자 파싱이 엉뚱한 데서 터진다 — 실제로 앞부분이 겹치는 이름이 둘 있다.
        private const string BaseDicePrefix = BaseDiceName + "[";
        private const string ElementPrefix = ElementName + "[";
        private const string MatchPrefix = MatchName + "[";
        private const string SumPercentPrefix = SumPercentName + "[";
        private const string SumFlatPrefix = SumFlatName + "[";
        private const string CooldownPrefix = CooldownName + "[";
        private const string FirstNWavesPrefix = FirstNWavesName + "[";

        private const string PendingMessage =
            "골든에 " + SectionRoot + "* 구획이 아직 없다. 플레이 중 F7 로 기준선을 다시 뜨면 " +
            "이 케이스들이 값 비교로 살아난다. 기준선을 손으로 채우지 말 것 — float 기대값은 " +
            "Mono 가 실제로 내놓은 것이어야 한다.";

        // --- 덤퍼에서 옮겨 적은 입력 격자 -------------------------------------------------
        // 출처: Assets/Scripts/SceneFlow/GoldenBaselineDumper.cs 의 DumpGemBonus.
        // 계산 인자는 키에서 파싱하므로 이 격자는 오직 "골든에 있어야 할 키 목록"을 만드는 데만 쓴다.
        // 각 값의 선정 근거는 덤퍼 쪽 주석에 적혀 있다.

        private static readonly int[] DiceCodes =
        {
            -1, 0, 1, 2, 3, 4, 5, 7, 99, 100, 101, 102, 103, 104,
            199, 200, 201, 202, 203, 204, 205, 206, 1000,
        };

        private static readonly int[] MatchTargetDice = { 0, 1, 3, 100, 200, 205 };
        private static readonly int[] MatchTargetElements = { 0, 1, 2, 4, 5 };
        private static readonly int[] MatchDice = { 0, 1, 3, 4, 100, 103, 200, 203, 205 };

        private static readonly int[] SumDice = { 0, 1, 100, 205 };
        private static readonly int[] SumPercentStats = { 0, 2 };
        private static readonly int[] SumFlatStats = { 1, 5 };
        private static readonly int[] CooldownDice = { 0, 1, 3, 100, 200, 205 };
        private static readonly int[] WaveDice = { 0, 205 };
        private static readonly int[] WaveIndices = { -1, 0, 1, 2, 3, 4, 10 };

        private static readonly GemEffectInput[][] EffectSets =
        {
            new GemEffectInput[0],

            new[] { Gem(0, 205, 5, 0.25f, 0, 0) },
            new[] { Gem(0, 100, 5, 0.25f, 0, 0) },

            new[] { Gem(0, 205, 5, 0.1f, 0, 0), Gem(0, 205, 5, 0.2f, 0, 0), Gem(0, 205, 5, 0.3f, 0, 0) },
            new[] { Gem(0, 205, 5, 0.3f, 0, 0), Gem(0, 205, 5, 0.2f, 0, 0), Gem(0, 205, 5, 0.1f, 0, 0) },

            new[] { Gem(0, 205, 5, -0.5f, 0, 0), Gem(0, 205, 5, 0.25f, 0, 0) },

            new[] { Gem(2, 205, 5, 0.7999999f, 0, 0) },
            new[] { Gem(2, 205, 5, 0.8f, 0, 0) },
            new[] { Gem(2, 205, 5, 0.8000001f, 0, 0) },
            new[] { Gem(2, 205, 5, 0.5f, 0, 0), Gem(2, 205, 5, 0.3f, 0, 0) },
            new[] { Gem(2, 205, 5, 0.5f, 0, 0), Gem(2, 1, 5, 0.4f, 0, 0) },

            new[] { Gem(1, 205, 5, 0f, 7, 0), Gem(1, 205, 5, 0f, -3, 0), Gem(1, 205, 5, 0f, 5, 0) },
            new[] { Gem(1, 205, 5, 0f, int.MaxValue, 0), Gem(1, 205, 5, 0f, 1, 0) },

            new[] { Gem(3, 205, 5, 0f, 10, 3), Gem(3, 205, 5, 0f, 100, 1) },
            new[] { Gem(3, 205, 5, 0f, 10, 0), Gem(3, 205, 5, 0f, 20, -2) },

            new[] { Gem(5, 100, 5, 0f, 3, 0), Gem(9, 100, 5, 0f, 4, 0) },

            new[] { Gem(0, 1, 1, 0.5f, 0, 0), Gem(0, 205, 2, 0.25f, 0, 0), Gem(1, 2, 5, 0f, 6, 0) },

            new[] { Gem(6, 205, 5, 0.15f, 0, 0), Gem(0, 205, 5, 0.15f, 0, 0) },

            // 18/19: 순서에 실제로 민감한 삼중항과 그 역순. 3/4번 쌍은 모양만 뒤집혀 있고
            // (0.1, 0.2, 0.3) 은 어느 순서로 더해도 0.6 이라 순서 변경을 검출하지 못한다.
            new[] { Gem(0, 205, 5, 0.1f, 0, 0), Gem(0, 205, 5, 0.1f, 0, 0), Gem(0, 205, 5, 0.5f, 0, 0) },
            new[] { Gem(0, 205, 5, 0.5f, 0, 0), Gem(0, 205, 5, 0.1f, 0, 0), Gem(0, 205, 5, 0.1f, 0, 0) },
        };

        private static GemEffectInput Gem(int stat, int dice, int element, float percent, int flat, int intParam)
        {
            return new GemEffectInput(stat, dice, element, percent, flat, intParam);
        }

        // --- 케이스 소스 (격자에서 키를 만든다) ------------------------------------------

        public static IEnumerable<TestCaseData> BaseDiceKeys()
        {
            foreach (string key in BuildKeys(BaseDiceName))
                yield return new TestCaseData(key);
        }

        public static IEnumerable<TestCaseData> ElementKeys()
        {
            foreach (string key in BuildKeys(ElementName))
                yield return new TestCaseData(key);
        }

        public static IEnumerable<TestCaseData> MatchKeys()
        {
            foreach (string key in BuildKeys(MatchName))
                yield return new TestCaseData(key);
        }

        public static IEnumerable<TestCaseData> SumPercentKeys()
        {
            foreach (string key in BuildKeys(SumPercentName))
                yield return new TestCaseData(key);
        }

        public static IEnumerable<TestCaseData> SumFlatKeys()
        {
            foreach (string key in BuildKeys(SumFlatName))
                yield return new TestCaseData(key);
        }

        public static IEnumerable<TestCaseData> CooldownKeys()
        {
            foreach (string key in BuildKeys(CooldownName))
                yield return new TestCaseData(key);
        }

        public static IEnumerable<TestCaseData> FirstNWavesKeys()
        {
            foreach (string key in BuildKeys(FirstNWavesName))
                yield return new TestCaseData(key);
        }

        /// <summary>격자가 만들어야 할 키 전부. 이름별로 걸러 돌려준다.</summary>
        private static List<string> BuildKeys(string name)
        {
            var keys = new List<string>();

            if (name == BaseDiceName || name == ElementName)
            {
                for (int i = 0; i < DiceCodes.Length; i++)
                    keys.Add(name + "[" + DiceCodes[i] + "]");

                return keys;
            }

            if (name == MatchName)
            {
                for (int t = 0; t < MatchTargetDice.Length; t++)
                {
                    for (int e = 0; e < MatchTargetElements.Length; e++)
                    {
                        for (int d = 0; d < MatchDice.Length; d++)
                        {
                            keys.Add(MatchName + "[" + MatchTargetDice[t] + "][" +
                                     MatchTargetElements[e] + "][" + MatchDice[d] + "]");
                        }
                    }
                }

                return keys;
            }

            for (int s = 0; s < EffectSets.Length; s++)
            {
                string set = Describe(EffectSets[s]);

                if (name == SumPercentName)
                {
                    for (int i = 0; i < SumPercentStats.Length; i++)
                    {
                        for (int d = 0; d < SumDice.Length; d++)
                            keys.Add(name + "[" + set + "][" + SumPercentStats[i] + "][" + SumDice[d] + "]");
                    }
                }
                else if (name == SumFlatName)
                {
                    for (int i = 0; i < SumFlatStats.Length; i++)
                    {
                        for (int d = 0; d < SumDice.Length; d++)
                            keys.Add(name + "[" + set + "][" + SumFlatStats[i] + "][" + SumDice[d] + "]");
                    }
                }
                else if (name == CooldownName)
                {
                    for (int d = 0; d < CooldownDice.Length; d++)
                        keys.Add(name + "[" + set + "][" + CooldownDice[d] + "]");
                }
                else if (name == FirstNWavesName)
                {
                    for (int d = 0; d < WaveDice.Length; d++)
                    {
                        for (int w = 0; w < WaveIndices.Length; w++)
                            keys.Add(name + "[" + set + "][" + WaveDice[d] + "][" + WaveIndices[w] + "]");
                    }
                }
                else
                {
                    throw new ArgumentException("모르는 구획 이름이다: " + name, "name");
                }
            }

            return keys;
        }

        // --- 골든 대조 -----------------------------------------------------------------

        [TestCaseSource(nameof(BaseDiceKeys))]
        public void BaseDiceTypeMatchesGolden(string key)
        {
            string[] args = ParseArguments(key, 1);
            AssertGolden(key, GemBonusFormula.BaseDiceType(ParseInt(args[0], key)).ToString(CultureInfo.InvariantCulture));
        }

        [TestCaseSource(nameof(ElementKeys))]
        public void ElementTypeOfMatchesGolden(string key)
        {
            string[] args = ParseArguments(key, 1);
            AssertGolden(key, GemBonusFormula.ElementTypeOf(ParseInt(args[0], key)).ToString(CultureInfo.InvariantCulture));
        }

        [TestCaseSource(nameof(MatchKeys))]
        public void IsTargetMatchedMatchesGolden(string key)
        {
            string[] args = ParseArguments(key, 3);
            bool matched = GemBonusFormula.IsTargetMatched(
                ParseInt(args[0], key), ParseInt(args[1], key), ParseInt(args[2], key));

            AssertGolden(key, matched.ToString());
        }

        [TestCaseSource(nameof(SumPercentKeys))]
        public void SumPercentMatchesGolden(string key)
        {
            string[] args = ParseArguments(key, 3);
            float value = GemBonusFormula.SumPercent(
                ParseEffects(args[0], key), ParseInt(args[1], key), ParseInt(args[2], key));

            AssertGolden(key, Format(value));
        }

        [TestCaseSource(nameof(SumFlatKeys))]
        public void SumFlatMatchesGolden(string key)
        {
            string[] args = ParseArguments(key, 3);
            int value = GemBonusFormula.SumFlat(
                ParseEffects(args[0], key), ParseInt(args[1], key), ParseInt(args[2], key));

            AssertGolden(key, value.ToString(CultureInfo.InvariantCulture));
        }

        [TestCaseSource(nameof(CooldownKeys))]
        public void CooldownReductionPercentMatchesGolden(string key)
        {
            string[] args = ParseArguments(key, 2);
            float value = GemBonusFormula.CooldownReductionPercent(
                ParseEffects(args[0], key), ParseInt(args[1], key));

            AssertGolden(key, Format(value));
        }

        [TestCaseSource(nameof(FirstNWavesKeys))]
        public void FirstNWavesDamageFlatBonusMatchesGolden(string key)
        {
            string[] args = ParseArguments(key, 3);
            int value = GemBonusFormula.FirstNWavesDamageFlatBonus(
                ParseEffects(args[0], key), ParseInt(args[1], key), ParseInt(args[2], key));

            AssertGolden(key, value.ToString(CultureInfo.InvariantCulture));
        }

        [Test]
        public void CooldownCapMatchesGolden()
        {
            AssertGolden(CooldownCapName, Format(GemBonusFormula.CooldownReductionCap));
        }

        // --- 누락 방지 -----------------------------------------------------------------

        /// <summary>
        /// 골든의 <c>core.gemBonus.*</c> 키를 <b>하나도 남기지 않고</b> 소비하는지 센다.
        /// 덤퍼가 하위 구획을 늘렸는데 픽스처를 안 붙이면 여기서 터진다.
        /// 반대 방향(격자가 만든 키가 골든에 없는 것)은 각 케이스가 골든을 읽다가 터져서 잡힌다.
        /// </summary>
        [Test]
        public void EveryGemBonusKeyIsConsumed()
        {
            if (!SectionDumped)
                Assert.Ignore(PendingMessage);

            var expected = new HashSet<string>(StringComparer.Ordinal);
            string[] names =
            {
                BaseDiceName, ElementName, MatchName,
                SumPercentName, SumFlatName, CooldownName, FirstNWavesName,
            };

            for (int i = 0; i < names.Length; i++)
            {
                foreach (string key in BuildKeys(names[i]))
                    expected.Add(key);
            }

            expected.Add(CooldownCapName);

            var orphans = new List<string>();
            foreach (var pair in GoldenBaseline.Section(GoldenBaseline.StableSection))
            {
                if (!pair.Key.StartsWith(SectionRoot, StringComparison.Ordinal))
                    continue;
                if (!expected.Contains(pair.Key))
                    orphans.Add(pair.Key);
            }

            orphans.Sort(StringComparer.Ordinal);
            Assert.That(string.Join(", ", orphans.ToArray()), Is.EqualTo(string.Empty),
                SectionRoot + "* 키인데 이 픽스처의 격자가 만들지 않는다. 덤퍼가 격자를 " +
                "넓혔으면 여기 격자도 같이 넓혀야 한다 — 안 그러면 새 축이 검사 없이 지나간다.");
        }

        /// <summary>
        /// 접두사가 다른 픽스처를 잡아먹지 않는지. StartsWith 로 훑기 때문에
        /// core.gemBonus 가 core.gemBonusX 를 삼키는 사고가 가능하다.
        /// </summary>
        [Test]
        public void PrefixesDoNotShadowOtherFixtures()
        {
            string[] mine =
            {
                BaseDicePrefix, ElementPrefix, MatchPrefix,
                SumPercentPrefix, SumFlatPrefix, CooldownPrefix, FirstNWavesPrefix, CooldownCapName,
            };

            string[] others =
            {
                "core.crit.", "core.damage", "core.dmgChain", "core.incoming.",
                "core.stageBonus", "core.equipUpgrade.",
            };

            for (int i = 0; i < mine.Length; i++)
            {
                for (int j = 0; j < others.Length; j++)
                {
                    Assert.That(others[j].StartsWith(mine[i], StringComparison.Ordinal), Is.False,
                        mine[i] + " 가 " + others[j] + " 를 잡아먹는다.");
                    Assert.That(mine[i].StartsWith(others[j], StringComparison.Ordinal), Is.False,
                        others[j] + " 가 " + mine[i] + " 를 잡아먹는다.");
                }
            }

            // cooldownCap 이 cooldown[ 에 삼켜지지 않는지. 이름이 겹치는 실제 쌍이라 이론이 아니다.
            Assert.That(CooldownCapName.StartsWith(CooldownPrefix, StringComparison.Ordinal), Is.False);
        }

        // --- 골든이 없어도 도는 검사 -----------------------------------------------------

        /// <summary>
        /// float 반환값을 <b>문자열로</b> 비교하는 것이 비트 동일 비교와 같은 강도인지 증명한다.
        /// 표기 → 파싱이 비트를 보존하지 않으면 서로 다른 두 float 이 같은 문자열이 되어
        /// 위 SumPercent / Cooldown 대조가 조용히 약해진다.
        ///
        /// 키에 박히는 <b>인자쪽</b> percent 도 같이 검사한다 — 그쪽이 깨지면 키는 맞는데
        /// 다른 값으로 함수를 부르게 된다(방향이 반대라 놓치기 쉽다).
        /// 골든 파일을 읽지 않으므로 기준선이 없어도 돈다.
        /// </summary>
        [Test]
        public void RoundTripTextIsBitTight()
        {
            var broken = new List<string>();

            for (int s = 0; s < EffectSets.Length; s++)
            {
                for (int e = 0; e < EffectSets[s].Length; e++)
                    CheckRoundTrip(EffectSets[s][e].PercentValue, "set[" + s + "].effect[" + e + "].percent", broken);

                for (int i = 0; i < SumPercentStats.Length; i++)
                {
                    for (int d = 0; d < SumDice.Length; d++)
                    {
                        CheckRoundTrip(
                            GemBonusFormula.SumPercent(EffectSets[s], SumPercentStats[i], SumDice[d]),
                            "sumPercent[" + s + "][" + SumPercentStats[i] + "][" + SumDice[d] + "]", broken);
                    }
                }

                for (int d = 0; d < CooldownDice.Length; d++)
                {
                    CheckRoundTrip(
                        GemBonusFormula.CooldownReductionPercent(EffectSets[s], CooldownDice[d]),
                        "cooldown[" + s + "][" + CooldownDice[d] + "]", broken);
                }
            }

            CheckRoundTrip(GemBonusFormula.CooldownReductionCap, "cooldownCap", broken);

            Assert.That(string.Join(", ", broken.ToArray()), Is.EqualTo(string.Empty),
                "라운드트립 표기가 비트를 보존하지 못한다. 그러면 이 픽스처의 float 비교가 " +
                "비트 동일 비교보다 약해진다 — 표기 방식을 바꾸거나 비교를 비트로 내려야 한다.");
        }

        /// <summary>
        /// 격자 안에 <b>서로 다른 집합인데 같은 글자로 인코딩되는 쌍</b>이 없는지.
        /// 있으면 키가 겹쳐서 한 집합의 검사가 조용히 사라진다(골든 리더는 값이 같은 중복을
        /// 허용하므로 터지지도 않는다). 순서만 뒤집은 3번/4번 집합이 실제로 아슬아슬한 쌍이다.
        /// </summary>
        [Test]
        public void EffectSetEncodingsAreDistinct()
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int s = 0; s < EffectSets.Length; s++)
            {
                string text = Describe(EffectSets[s]);
                Assert.That(seen.ContainsKey(text), Is.False,
                    "집합 " + s + " 이 집합 " + (seen.ContainsKey(text) ? seen[text] : -1) +
                    " 과 같은 글자로 인코딩된다: " + text);

                seen[text] = s;
            }

            Assert.That(seen.Count, Is.EqualTo(EffectSets.Length));
        }

        /// <summary>
        /// <b>사고 카나리아.</b> <c>targetDiceType == 100</c>(Tornado)인 효과는
        /// <c>diceType == 205</c>(Max)로 물을 때 <b>말고는</b> 절대 매칭되지 않는다.
        ///
        /// 이것이 보석 효과 100개 중 71개를 죽였던 그 성질이다(f0cccdb / 3a6f5bd).
        /// <c>BaseDiceType</c> 의 치역이 {0,1,2,3,4} 라서 100 은 나올 수 없고,
        /// 살아남은 19개(WellHpOnKill / GoldOnKill)는 게터가 Max 로 물어서 조기 반환에 걸린 것뿐이다.
        /// 접기 표를 손대는 변경이 이 성질을 뒤집으면 여기서 먼저 터진다.
        ///
        /// 값을 지어내지 않는다 — 참/거짓 구조만 본다.
        /// </summary>
        [Test]
        public void TornadoTargetCodeNeverMatchesExceptMaxQuery()
        {
            int[] realDice =
            {
                0, 1, 2, 3, 4,
                100, 101, 102, 103, 104,
                200, 201, 202, 203, 204,
            };

            for (int i = 0; i < realDice.Length; i++)
            {
                Assert.That(GemBonusFormula.IsTargetMatched(100, 5, realDice[i]), Is.False,
                    "targetDiceType=100 이 다이스 " + realDice[i] + " 에 매칭됐다. " +
                    "BaseDiceType 의 치역이 바뀌었다면 에셋 리맵도 같이 봐야 한다.");
            }

            Assert.That(GemBonusFormula.IsTargetMatched(100, 5, GemBonusFormula.DiceTypeMax), Is.True,
                "Max 로 물으면 조기 반환으로 매칭돼야 한다 — WellHpOnKill / GoldOnKill 19개가 " +
                "살아 있는 유일한 이유가 이 줄이다.");
        }

        /// <summary>
        /// 쿨감이 <b>0.8 을 넘지 못한다</b>. 호출부(DiceMetaDataProvider.GetCooldown :481-482)의
        /// <c>Mathf.Max(0.05f, 1f - r)</c> 는 감소율 0.95 까지 허용하지만, 여기 캡이 먼저 물어서
        /// 그 하한은 <b>한 번도 도달하지 않는 죽은 가지</b>다. 실효 상한은 0.8 이다.
        ///
        /// 값을 지어내지 않는다 — 상한/하한 관계만 본다.
        /// </summary>
        [Test]
        public void CooldownReductionNeverExceedsCap()
        {
            for (int s = 0; s < EffectSets.Length; s++)
            {
                for (int d = 0; d < CooldownDice.Length; d++)
                {
                    float value = GemBonusFormula.CooldownReductionPercent(EffectSets[s], CooldownDice[d]);

                    Assert.That(value, Is.LessThanOrEqualTo(GemBonusFormula.CooldownReductionCap),
                        "집합 " + s + " / 다이스 " + CooldownDice[d] + " 에서 캡을 넘었다.");
                    Assert.That(value, Is.GreaterThanOrEqualTo(0f),
                        "집합 " + s + " / 다이스 " + CooldownDice[d] + " 에서 음수가 나왔다.");

                    // 캡이 실제로 무는 자리가 격자 안에 있어야 검사가 의미를 갖는다.
                    // 아래 단언이 이 격자에 캡을 넘기는 표본이 있음을 보장한다.
                    Assert.That(1f - value, Is.GreaterThan(0.05f),
                        "호출부 하한 0.05f 가 살아났다 — 캡 0.8 이 풀렸다는 뜻이다.");
                }
            }

            // 캡을 실제로 밟는 집합이 하나도 없으면 위 단언은 공허하다. 8번 집합(0.8000001)이
            // 그 자리를 맡는다 — 잘리지 않았다면 캡이 사라진 것이다.
            float overCap = GemBonusFormula.CooldownReductionPercent(EffectSets[8], 0);
            Assert.That(overCap, Is.EqualTo(GemBonusFormula.CooldownReductionCap),
                "캡을 넘기는 표본이 잘리지 않았다. 격자가 경계를 안 밟으면 위 검사는 공허하다.");
        }

        /// <summary>
        /// FirstNWaves 의 한계가 <b><c>waveIndex &lt;= intParam</c> 포함</b>이다.
        /// 13번 집합은 limit 3(flat 10)과 limit 1(flat 100)을 들고 있다.
        ///
        /// 값을 지어내지 않는다 — "웨이브 3 에서는 붙고 4 에서는 안 붙는다"는 <b>구조</b>만 본다.
        /// 부등호를 <c>&lt;</c> 로 바꾸면 웨이브 3 이 0 이 되어 여기서 터진다.
        /// </summary>
        [Test]
        public void WaveLimitIsInclusive()
        {
            GemEffectInput[] set = EffectSets[13];

            int atLimit = GemBonusFormula.FirstNWavesDamageFlatBonus(set, 0, 3);
            int pastLimit = GemBonusFormula.FirstNWavesDamageFlatBonus(set, 0, 4);
            int atSmallLimit = GemBonusFormula.FirstNWavesDamageFlatBonus(set, 0, 1);
            int pastSmallLimit = GemBonusFormula.FirstNWavesDamageFlatBonus(set, 0, 2);

            Assert.That(atLimit, Is.GreaterThan(0), "웨이브 == intParam 은 포함이어야 한다.");
            Assert.That(pastLimit, Is.EqualTo(0), "웨이브 > intParam 은 붙으면 안 된다.");
            Assert.That(atSmallLimit, Is.GreaterThan(atLimit),
                "limit 1 짜리(flat 100)가 웨이브 1 에서 같이 붙어야 한다.");
            Assert.That(pastSmallLimit, Is.EqualTo(atLimit),
                "웨이브 2 에서는 limit 1 짜리만 떨어져야 한다.");

            // 조기 반환 축. waveIndex <= 0 이면 효과가 무엇이든 0 이다.
            Assert.That(GemBonusFormula.FirstNWavesDamageFlatBonus(set, 0, 0), Is.EqualTo(0));
            Assert.That(GemBonusFormula.FirstNWavesDamageFlatBonus(set, 0, -1), Is.EqualTo(0));

            // limit <= 0 축. 14번 집합은 intParam 이 0 과 -2 라 어느 웨이브에서도 안 붙는다.
            for (int w = 1; w <= 10; w++)
            {
                Assert.That(GemBonusFormula.FirstNWavesDamageFlatBonus(EffectSets[14], 0, w), Is.EqualTo(0),
                    "intParam 이 0 이하인 효과가 웨이브 " + w + " 에서 붙었다.");
            }
        }

        /// <summary>
        /// <b>누적 순서가 값에 들어간다</b>는 것을 값으로 증명한다. <c>SumPercent</c> 는 float
        /// 덧셈이고 float 덧셈은 결합적이 아니므로, 효과 배열의 순서를 바꾸면 답이 갈릴 수 있다.
        ///
        /// 이 검사가 따로 필요한 이유: 3/4번 집합은 "같은 원소를 순서만 뒤집은 쌍"이라고
        /// 적혀 있지만 (0.1, 0.2, 0.3) 은 <b>어느 순서로 더해도 정확히 0.6</b> 이다. 그래서
        /// 그 쌍만으로는 누적 순서를 뒤집는 변경이 골든을 통과해 버린다 — 실제로 SumPercent 의
        /// 순회를 뒤집는 변이를 심었을 때 972개가 전부 초록이었다. 18/19번 집합이 그 구멍을 메운다.
        ///
        /// 값을 지어내지 않는다 — "두 순서의 답이 서로 다르다"는 관계만 본다.
        /// 골든 파일을 읽지 않으므로 기준선이 없어도 돈다.
        /// </summary>
        [Test]
        public void SumPercentIsOrderSensitive()
        {
            GemEffectInput[] forward = EffectSets[18];
            GemEffectInput[] reversed = EffectSets[19];

            Assert.That(forward.Length, Is.EqualTo(reversed.Length),
                "18/19번은 같은 원소를 순서만 뒤집은 쌍이어야 한다.");
            for (int i = 0; i < forward.Length; i++)
            {
                Assert.That(SameBits(forward[i].PercentValue, reversed[forward.Length - 1 - i].PercentValue), Is.True,
                    "18/19번이 서로의 역순이 아니다. 한쪽만 고쳤다면 둘 다 고쳐야 한다.");
            }

            float a = GemBonusFormula.SumPercent(forward, 0, 0);
            float b = GemBonusFormula.SumPercent(reversed, 0, 0);

            Assert.That(SameBits(a, b), Is.False, string.Format(
                "18/19번 집합의 합이 같아졌다({0}). 이 쌍은 '누적 순서를 바꾸면 값이 바뀐다'를 " +
                "지키는 유일한 표본이다 — 값이 같아지는 순간 순서를 뒤집는 변경이 골든을 " +
                "통과한다. 표본을 고쳤다면 Mono 에서 두 순서가 실제로 갈리는지 먼저 확인할 것.",
                Format(a)));
        }

        // --- 헬퍼 ---------------------------------------------------------------------

        private static bool? sectionDumped;

        /// <summary>
        /// 골든에 이 구획이 이미 떠 있는가. 없으면 케이스를 <b>실패가 아니라 Ignore</b> 로 떨어뜨린다 —
        /// 덤퍼에 구획을 넣은 커밋과 사람이 F7 로 기준선을 뜨는 시점이 갈리기 때문이다.
        /// 한 키라도 있으면 "떴다"로 보고 그때부터는 엄격하게 대조한다(빠진 키는 실패다).
        /// </summary>
        private static bool SectionDumped
        {
            get
            {
                if (sectionDumped.HasValue)
                    return sectionDumped.Value;

                bool found = false;
                foreach (var pair in GoldenBaseline.Section(GoldenBaseline.StableSection))
                {
                    if (!pair.Key.StartsWith(SectionRoot, StringComparison.Ordinal))
                        continue;

                    found = true;
                    break;
                }

                sectionDumped = found;
                return found;
            }
        }

        private static void AssertGolden(string key, string actual)
        {
            if (!SectionDumped)
                Assert.Ignore(PendingMessage);

            string expected = GoldenBaseline.Raw(GoldenBaseline.StableSection, key);

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

        /// <summary>덤퍼의 DescribeGemEffects 와 <b>같은 인코딩</b>이어야 한다.</summary>
        private static string Describe(GemEffectInput[] effects)
        {
            if (effects == null || effects.Length == 0)
                return "-";

            var parts = new string[effects.Length];
            for (int i = 0; i < effects.Length; i++)
            {
                parts[i] = string.Concat(
                    effects[i].StatType.ToString(CultureInfo.InvariantCulture), ":",
                    effects[i].TargetDiceType.ToString(CultureInfo.InvariantCulture), ":",
                    effects[i].TargetElementType.ToString(CultureInfo.InvariantCulture), ":",
                    Format(effects[i].PercentValue), ":",
                    effects[i].FlatValue.ToString(CultureInfo.InvariantCulture), ":",
                    effects[i].IntParam.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join("|", parts);
        }

        /// <summary>키에 박힌 효과 집합을 되돌린다. 여기서 만든 배열로 함수를 부른다.</summary>
        private static GemEffectInput[] ParseEffects(string text, string key)
        {
            if (text == "-")
                return new GemEffectInput[0];

            string[] items = text.Split('|');
            var effects = new GemEffectInput[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                string[] fields = items[i].Split(':');
                if (fields.Length != 6)
                {
                    throw new FormatException(string.Format(
                        "골든 키 {0} 의 효과 {1} 이 6칸이 아니다: '{2}'", key, i, items[i]));
                }

                effects[i] = new GemEffectInput(
                    ParseInt(fields[0], key),
                    ParseInt(fields[1], key),
                    ParseInt(fields[2], key),
                    ParseFloat(fields[3], key),
                    ParseInt(fields[4], key),
                    ParseInt(fields[5], key));
            }

            return effects;
        }

        /// <summary>키의 대괄호 인자를 순서대로 뽑는다.</summary>
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

            // 인자 개수가 다르면 격자가 키 모양을 바꾼 것이다. 앞 인자만 읽고 통과시키면
            // 엉뚱한 함수 호출을 골든과 비교하게 되므로 여기서 끊는다.
            if (arguments.Count != expectedCount)
            {
                throw new FormatException(string.Format(
                    "골든 키 {0} 의 인자가 {1}개다(기대 {2}개).", key, arguments.Count, expectedCount));
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
        /// 키에 적힌 실수 인자를 float 으로 되돌린다. 골든 표기는 라운드트립("R")이라
        /// 되읽으면 덤퍼가 넘긴 값과 비트가 같다 — <see cref="RoundTripTextIsBitTight"/> 가 그것을 본다.
        /// double 로 받아 넘기면 승격 시점이 달라져 경계에서 값이 갈릴 수 있다.
        /// </summary>
        private static float ParseFloat(string text, string key)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                throw new FormatException(string.Format("골든 키 {0} 의 실수 인자를 못 읽었다: '{1}'", key, text));

            return value;
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

            if (!SameBits(value, parsed))
                broken.Add(label + " 표기 '" + text + "' 가 비트를 보존하지 못했다");
        }

        private static bool SameBits(float left, float right)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(left), 0)
                   == BitConverter.ToInt32(BitConverter.GetBytes(right), 0);
        }
    }
}
