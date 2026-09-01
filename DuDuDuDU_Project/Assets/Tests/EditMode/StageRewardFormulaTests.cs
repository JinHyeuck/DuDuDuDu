using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>
    /// StageRewardFormula 골든 특성화 테스트. (MIGRATION_BASELINE 3.3)
    ///
    /// 목적은 계산을 "개선"하는 것이 아니라 <b>현행 숫자를 못 박는 것</b>이다.
    /// Tests/Golden/formula_baseline.txt 의 값이 정본이고, 계산이 달라지면 여기가 먼저 터진다.
    /// 그러므로 실패했을 때 고칠 곳은 골든이 아니라 (혹은 테스트가 아니라) 계산식 쪽이다.
    ///
    /// 기대값을 상수로 베껴 오지 않고 매번 골든에서 읽는 이유: 베껴 두면 골든과 테스트가
    /// 갈라져도 아무도 모른다. 인자 역시 키 문자열에서 파싱한다 —
    /// GoldenBaselineDumper.DumpCoreFormulas 가 키에 인자를 통째로 적어 두기 때문에
    /// 키 하나가 곧 "입력 + 기대 출력" 한 쌍이다.
    ///
    /// 부동소수 비교에 허용 오차를 두지 않는다. 이 식들은 마지막에 FloorToInt 로 잘리므로
    /// 정밀도가 1ULP만 흔들려도 결과가 통째로 1 어긋난다. 그 어긋남을 잡는 게 이 테스트다.
    ///
    /// 소비하는 구획은 core.* 만이 아니다. reward.guaranteedGold / reward.accumulatedGold 는
    /// StageRewardCalculator 를 한 겹 거치지만 그 메서드가 StageRewardFormula 로 그대로
    /// 넘기는 pass-through 이고 인자가 전부 기본형이라, 값은 사실 이 클래스의 출력이다.
    /// 특히 AccumulatedGuaranteedGold 는 core.* 에 아예 없어서 저 구획을 빼면 검증이 0 이다.
    ///
    /// 여전히 못 잡는 것이 남아 있고, 전부 <b>덤퍼 입력을 늘려야</b> 닿는 곳이다.
    /// (덤퍼 수정 + 기준선 재생성이라 여기서 단독으로 못 한다.)
    ///   - StageBonus 의 Mathf.Max(1, ...) 하한: stageIndex &lt;= 0 인 입력이 골든에 없다.
    ///   - ClearGradeTier 의 Clamp01: 음수 체력 입력이 없어 하한이 한 번도 안 밟힌다.
    ///   - ScaleAmount 의 float 유지: 지금 배율 7 종으로는 double 승격과 결과가 갈리지 않는다.
    /// </summary>
    [TestFixture]
    public class StageRewardFormulaTests
    {
        // 담당 접두사. 대괄호를 붙이지 않은 '맨' 접두사를 쓴다 — 덤퍼가 나중에 같은 계열의
        // 키를 더 뱉으면 그것까지 함께 걸려들어 파서에서 터지고, "새 골든 줄이 아무도 안 보는
        // 사이에 늘어나는" 상황이 드러난다. 대괄호까지 붙이면 조용히 빠져나갈 여지가 생긴다.
        private const string StageBonusPrefix = "core.stageBonus";
        private const string GuaranteedGoldPrefix = "core.guaranteedGold";
        private const string ClearGradeTierPrefix = "core.clearGradeTier";
        private const string ScaleAmountPrefix = "core.scaleAmount";

        // core.* 만 보면 AccumulatedGuaranteedGold 가 <b>통째로</b> 검증에서 빠진다.
        // StageRewardCalculator.GetAccumulatedGuaranteedGold / GetGuaranteedNormalGold 는
        // StageRewardFormula 로 그대로 넘기는 pass-through 라, reward.* 구획의 값은 사실
        // 이 클래스의 출력이고 인자도 전부 기본형이라 여기서 그대로 재현된다.
        //
        // 이 두 줄이 없을 때 무엇을 놓치는지는 변이로 확인했다 — ratio 를 double 로 승격,
        // clearedWaves/totalWaves 인자 뒤집기, FloorToInt -> RoundToInt 가 <b>전부</b>
        // 초록불로 지나갔다. 셋 다 계산식 주석이 "하면 안 된다"고 명시한 바로 그 변경이다.
        private const string AccumulatedGoldPrefix = "reward.accumulatedGold";
        private const string RewardGuaranteedGoldPrefix = "reward.guaranteedGold";

        // GoldenBaselineDumper.DumpCoreFormulas 가 넣는 입력 조합의 개수다.
        //
        // 이 상수가 필요한 이유가 있다. TestCaseSource 가 골든에서 키를 뽑아 오는 구조라
        // 골든에서 줄이 통째로 사라지면 테스트 케이스도 같이 사라져 <b>조용히 통과</b>한다.
        // (GoldenBaseline.Keys 는 0건일 때만 예외를 던지므로 8건이 3건으로 준 것은 못 잡는다.)
        // 그래서 덤퍼가 만들 수 있는 조합 수를 여기에 못 박아 개수 감소를 실패로 만든다.
        private const int StageBonusCaseCount = 8;      // rewardStages = { 1, 9, 10, 11, 20, 21, 30, 100 }
        private const int GuaranteedGoldCaseCount = 8;  // 위와 같은 배열을 돈다
        private const int ClearGradeTierCaseCount = 10; // wallPairs 10 쌍
        private const int ScaleAmountCaseCount = 42;    // amounts 6 종 × multipliers 7 종

        // DumpStageRewards 쪽 입력 조합. (덤퍼 라인 123~139)
        private const int RewardGuaranteedGoldCaseCount = 30;  // s = 1..30
        private const int AccumulatedGoldCaseCount = 126;      // DumpStages 7 × totalSamples 3 × clearedSamples 6

        // ---- core.stageBonus[stageIndex] ----

        [TestCaseSource(nameof(StageBonusKeys))]
        public void StageBonus_MatchesGolden(string key)
        {
            int stageIndex = ParseInt(key, Arguments(key, StageBonusPrefix, 1)[0]);
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);

            Assert.That(StageRewardFormula.StageBonus(stageIndex), Is.EqualTo(expected), Broken(key));
        }

        // ---- core.guaranteedGold[stageIndex] ----

        [TestCaseSource(nameof(GuaranteedGoldKeys))]
        public void GuaranteedNormalGold_MatchesGolden(string key)
        {
            int stageIndex = ParseInt(key, Arguments(key, GuaranteedGoldPrefix, 1)[0]);
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);

            Assert.That(StageRewardFormula.GuaranteedNormalGold(stageIndex), Is.EqualTo(expected), Broken(key));
        }

        // ---- core.clearGradeTier[currentWallHp/totalWallHp] ----

        [TestCaseSource(nameof(ClearGradeTierKeys))]
        public void ClearGradeTier_MatchesGolden(string key)
        {
            // 이 구획만 인자 구분자가 '/' 다. 덤퍼가 "10/0" 처럼 체력 비율로 읽히게 적어 뒀다.
            // 앞이 현재 벽 체력, 뒤가 최대 벽 체력 — 순서를 뒤집으면 0.5 경계가 통째로 뒤집힌다.
            string[] pair = Split(key, Arguments(key, ClearGradeTierPrefix, 1)[0], '/', 2);
            int currentWallHp = ParseInt(key, pair[0]);
            int totalWallHp = ParseInt(key, pair[1]);
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);

            Assert.That(StageRewardFormula.ClearGradeTier(currentWallHp, totalWallHp), Is.EqualTo(expected), Broken(key));
        }

        // ---- core.scaleAmount[amount][multiplier] ----

        [TestCaseSource(nameof(ScaleAmountKeys))]
        public void ScaleAmount_MatchesGolden(string key)
        {
            string[] args = Arguments(key, ScaleAmountPrefix, 2);
            int amount = ParseInt(key, args[0]);
            float multiplier = ParseFloat(key, args[1]);
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);

            Assert.That(StageRewardFormula.ScaleAmount(amount, multiplier), Is.EqualTo(expected), Broken(key));
        }

        // ---- reward.guaranteedGold[stageIndex] ----

        // core.guaranteedGold 와 같은 함수를 두드리지만 인자 집합이 다르다 — 1~30 을 빠짐없이
        // 도므로 10 스테이지 계단 경계(10/11, 20/21)를 전부 밟는다.
        [TestCaseSource(nameof(RewardGuaranteedGoldKeys))]
        public void RewardGuaranteedGold_MatchesGolden(string key)
        {
            int stageIndex = ParseInt(key, Arguments(key, RewardGuaranteedGoldPrefix, 1)[0]);
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);

            Assert.That(StageRewardFormula.GuaranteedNormalGold(stageIndex), Is.EqualTo(expected), Broken(key));
        }

        // ---- reward.accumulatedGold[stageIndex][clearedWaves/totalWaves] ----

        [TestCaseSource(nameof(AccumulatedGoldKeys))]
        public void AccumulatedGuaranteedGold_MatchesGolden(string key)
        {
            string[] args = Arguments(key, AccumulatedGoldPrefix, 2);
            int stageIndex = ParseInt(key, args[0]);

            // 두 번째 인자만 '/' 로 또 쪼갠다. 앞이 클리어한 웨이브, 뒤가 총 웨이브다 —
            // 순서를 뒤집으면 ratio 가 통째로 뒤집힌다. 골든에 cleared > total 인 쌍(12/8)이
            // 들어 있어서 Clamp 상한이 실제로 밟힌다.
            string[] pair = Split(key, args[1], '/', 2);
            int clearedWaves = ParseInt(key, pair[0]);
            int totalWaves = ParseInt(key, pair[1]);
            int expected = GoldenBaseline.Int(GoldenBaseline.StableSection, key);

            Assert.That(
                StageRewardFormula.AccumulatedGuaranteedGold(stageIndex, clearedWaves, totalWaves),
                Is.EqualTo(expected), Broken(key));
        }

        // ---- 누락 방지 ----

        /// <summary>
        /// 골든에 있는 담당 키 개수가 덤퍼의 입력 조합 수와 같은지 본다.
        /// 위 네 테스트는 "골든에 있는 키"만 검사하므로, 골든에서 줄이 사라진 경우를 못 잡는다.
        /// 그 구멍을 막는 게 이 테스트다.
        /// </summary>
        [TestCase(StageBonusPrefix, StageBonusCaseCount)]
        [TestCase(GuaranteedGoldPrefix, GuaranteedGoldCaseCount)]
        [TestCase(ClearGradeTierPrefix, ClearGradeTierCaseCount)]
        [TestCase(ScaleAmountPrefix, ScaleAmountCaseCount)]
        [TestCase(RewardGuaranteedGoldPrefix, RewardGuaranteedGoldCaseCount)]
        [TestCase(AccumulatedGoldPrefix, AccumulatedGoldCaseCount)]
        public void GoldenKeyCount_MatchesDumperInputCombinations(string prefix, int expectedCount)
        {
            var keys = SortedKeys(prefix);

            Assert.That(keys.Count, Is.EqualTo(expectedCount), string.Format(
                "골든 접두사 '{0}' 의 키가 {1}개인데 덤퍼 입력 조합은 {2}개다. " +
                "골든에서 줄이 사라졌거나 덤퍼 입력 배열이 바뀌었다. 파일: {3}",
                prefix, keys.Count, expectedCount, GoldenBaseline.FilePath));
        }

        /// <summary>
        /// 담당 접두사의 키를 한 개도 남기지 않고 테스트가 소비했는지 본다.
        /// TestCaseSource 와 같은 목록을 다시 세는 것이라 동어반복처럼 보이지만,
        /// 접두사 오타나 소스 메서드 누락처럼 "케이스가 통째로 안 만들어진" 사고를 잡는다.
        /// </summary>
        [Test]
        public void AllAssignedGoldenKeys_AreConsumed()
        {
            int consumed = CountOf(StageBonusKeys()) + CountOf(GuaranteedGoldKeys())
                           + CountOf(ClearGradeTierKeys()) + CountOf(ScaleAmountKeys())
                           + CountOf(RewardGuaranteedGoldKeys()) + CountOf(AccumulatedGoldKeys());
            const int total = StageBonusCaseCount + GuaranteedGoldCaseCount
                              + ClearGradeTierCaseCount + ScaleAmountCaseCount
                              + RewardGuaranteedGoldCaseCount + AccumulatedGoldCaseCount;

            Assert.That(consumed, Is.EqualTo(total), string.Format(
                "담당 골든 키 {0}개 중 {1}개만 테스트 케이스가 됐다. 파일: {2}",
                total, consumed, GoldenBaseline.FilePath));
        }

        // ---- 케이스 소스 ----

        // 네 소스 메서드는 public 이다. NUnit 은 private 소스도 찾아내지만, 접근성 때문에
        // 케이스가 0건이 되는 사고는 "테스트가 통째로 사라진 채 초록불"로 나타난다.
        // 그 실패 모드가 너무 조용해서 공개 쪽으로 기운다.
        public static IEnumerable<string> StageBonusKeys()
        {
            return SortedKeys(StageBonusPrefix);
        }

        public static IEnumerable<string> GuaranteedGoldKeys()
        {
            return SortedKeys(GuaranteedGoldPrefix);
        }

        public static IEnumerable<string> ClearGradeTierKeys()
        {
            return SortedKeys(ClearGradeTierPrefix);
        }

        public static IEnumerable<string> ScaleAmountKeys()
        {
            return SortedKeys(ScaleAmountPrefix);
        }

        public static IEnumerable<string> RewardGuaranteedGoldKeys()
        {
            return SortedKeys(RewardGuaranteedGoldPrefix);
        }

        public static IEnumerable<string> AccumulatedGoldKeys()
        {
            return SortedKeys(AccumulatedGoldPrefix);
        }

        // 사전 순회 순서는 보장이 없다. 실패가 늘 같은 순서로 재현되도록 정렬해 둔다.
        private static List<string> SortedKeys(string prefix)
        {
            var keys = new List<string>(GoldenBaseline.Keys(GoldenBaseline.StableSection, prefix).Keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }

        private static int CountOf(IEnumerable<string> keys)
        {
            int count = 0;
            foreach (string unused in keys)
                count++;

            return count;
        }

        // ---- 키 파싱 ----

        /// <summary>
        /// "prefix[a][b]" 에서 대괄호 안을 잘라 준다.
        /// 형식이 조금이라도 다르면 건너뛰지 않고 즉시 터뜨린다 — 파싱 실패를 "검사 안 함"으로
        /// 흡수하면 골든이 바뀌어도 초록불이 켜지고, 그게 이 테스트가 막으려는 바로 그 상황이다.
        /// </summary>
        private static string[] Arguments(string key, string prefix, int expectedCount)
        {
            if (!key.StartsWith(prefix + "[", StringComparison.Ordinal) || !key.EndsWith("]", StringComparison.Ordinal))
            {
                throw new FormatException(string.Format(
                    "골든 키 '{0}' 가 예상 형식('{1}[...]')이 아니다. 덤퍼가 키 형식을 바꿨는지 볼 것.",
                    key, prefix));
            }

            string body = key.Substring(prefix.Length + 1, key.Length - prefix.Length - 2);
            string[] parts = body.Split(new[] { "][" }, StringSplitOptions.None);
            if (parts.Length != expectedCount)
            {
                throw new FormatException(string.Format(
                    "골든 키 '{0}' 의 인자가 {1}개인데 {2}개를 기대했다.", key, parts.Length, expectedCount));
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].IndexOf('[') >= 0 || parts[i].IndexOf(']') >= 0)
                {
                    throw new FormatException(string.Format(
                        "골든 키 '{0}' 의 {1}번째 인자('{2}')를 읽을 수 없다.", key, i, parts[i]));
                }
            }

            return parts;
        }

        private static string[] Split(string key, string text, char separator, int expectedCount)
        {
            string[] parts = text.Split(separator);
            if (parts.Length != expectedCount)
            {
                throw new FormatException(string.Format(
                    "골든 키 '{0}' 의 인자 '{1}' 를 '{2}' 로 {3}조각으로 나눌 수 없다.",
                    key, text, separator, expectedCount));
            }

            return parts;
        }

        private static int ParseInt(string key, string text)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                throw new FormatException(string.Format(
                    "골든 키 '{0}' 의 정수 인자 '{1}' 를 읽을 수 없다.", key, text));
            }

            return value;
        }

        // 배율은 반드시 float 로 되읽는다. 덤퍼가 float 를 "R"(라운드트립)로 적었으므로
        // float.Parse 로 읽으면 원래 비트가 그대로 돌아온다. double 로 읽고 (float) 캐스팅하면
        // 반올림이 두 번 일어나 0.999 같은 경계에서 다른 비트가 나올 수 있고,
        // ScaleAmount 는 FloorToInt 라 그 한 비트가 결과 1 차이로 증폭된다.
        private static float ParseFloat(string key, string text)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new FormatException(string.Format(
                    "골든 키 '{0}' 의 실수 인자 '{1}' 를 읽을 수 없다.", key, text));
            }

            return value;
        }

        // 실패 메시지에 키 이름을 반드시 넣는다. 어느 골든 줄이 깨졌는지 바로 보여야 한다.
        private static string Broken(string key)
        {
            return string.Format(
                "골든 '{0}' 이(가) 깨졌다. 값을 테스트에 맞추지 말 것 — 계산이 왜 달라졌는지가 답이다. 파일: {1}",
                key, GoldenBaseline.FilePath);
        }
    }
}
