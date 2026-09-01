using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 커밋된 골든 기준선을 읽는다. (MIGRATION_BASELINE 3.3)
    ///
    /// 기준선 파일은 <b>Assets 바깥</b>(DuDuDuDU_Project/Tests/Golden/)에 있다.
    /// 빌드에 섞이지 않고 .meta 도 생기지 않게 하려는 것이라, AssetDatabase 나
    /// Resources 로는 못 읽고 파일 경로로 읽어야 한다.
    ///
    /// 이 테스트 어셈블리는 OJ.Core 만 참조한다 — <b>Assembly-CSharp 은 참조할 수 없다.</b>
    /// 그래서 에셋에서 나온 값(stage.* / dice.* 등)은 여기서 재현할 수 없고,
    /// 순수 함수를 기본형 인자로 두드린 <c>core.*</c> 구획만 검증 대상이다.
    /// 나머지 구획은 사람이 diff 로 보는 기록이다.
    /// </summary>
    internal static class GoldenBaseline
    {
        public const string StableSection = "[stable]";
        public const string EnvironmentSection = "[environment]";

        private const string RelativePath = "../Tests/Golden/formula_baseline.txt";

        /// <summary>
        /// 기준선 파일 경로를 밖에서 지정하는 환경변수. 헤드리스 러너(Tools/headless)가 쓴다.
        /// </summary>
        private const string PathOverrideVariable = "OJ_GOLDEN_BASELINE";

        private static Dictionary<string, Dictionary<string, string>> cache;

        /// <summary>
        /// 기준선 파일의 절대 경로.
        ///
        /// 기본은 예전과 같다 — <c>Application.dataPath</c> 기준 상대 경로. Unity 에디터/플레이어
        /// 안에서의 동작은 바뀌지 않는다.
        ///
        /// 다만 이 테스트 어셈블리는 Tools/headless 의 헤드리스 러너가 <b>에디터 밖 프로세스</b>에서도
        /// 그대로 실행한다(Unity 를 띄우지 않고 EditMode 테스트를 돌리기 위한 것이다).
        /// 거기서는 <c>Application.dataPath</c> 가 빈 문자열이거나 접근 자체가 예외를 던져
        /// 기준선을 찾을 방법이 없다. 그래서 환경변수 <c>OJ_GOLDEN_BASELINE</c> 가 설정돼 있으면
        /// 그 경로를 먼저 쓴다. Unity 안에서는 이 변수가 없으므로 이 분기는 타지 않는다.
        /// </summary>
        public static string FilePath
        {
            get
            {
                string overridePath = ReadPathOverride();
                if (!string.IsNullOrEmpty(overridePath))
                    return Path.GetFullPath(overridePath);

                string dataPath;
                try
                {
                    dataPath = Application.dataPath;
                }
                catch (Exception error)
                {
                    // 에디터/플레이어 밖에서 Application 을 건드리면 여기로 온다.
                    // 원본 예외만 던지면 "왜 못 읽는지"가 안 보이므로 해결책을 문구에 붙인다.
                    throw new InvalidOperationException(string.Format(
                        "Application.dataPath 를 쓸 수 없는 환경이다({0}). 골든 기준선 경로를 " +
                        "환경변수 {1} 로 넘길 것.", error.GetType().Name, PathOverrideVariable), error);
                }

                if (string.IsNullOrEmpty(dataPath))
                {
                    throw new InvalidOperationException(string.Format(
                        "Application.dataPath 가 비어 있다. 골든 기준선 경로를 환경변수 {0} 로 넘길 것.",
                        PathOverrideVariable));
                }

                return Path.GetFullPath(Path.Combine(dataPath, RelativePath));
            }
        }

        private static string ReadPathOverride()
        {
            try
            {
                return Environment.GetEnvironmentVariable(PathOverrideVariable);
            }
            catch (Exception)
            {
                // 플랫폼에 따라 환경변수 접근이 막혀 있을 수 있다(보안 예외 등).
                // 그때는 조용히 기존 경로 계산으로 내려간다 — 에디터에서의 동작이 정본이다.
                return null;
            }
        }

        /// <summary>구획 하나의 키-값 전부. 구획이 없으면 빈 사전이 아니라 예외다 —
        /// 오타 난 구획 이름으로 "0건 통과"가 되는 것이 가장 나쁜 실패다.</summary>
        public static IReadOnlyDictionary<string, string> Section(string section)
        {
            EnsureLoaded();

            Dictionary<string, string> values;
            if (!cache.TryGetValue(section, out values))
            {
                throw new InvalidOperationException(
                    string.Format("골든 기준선에 {0} 구획이 없다. 파일: {1}", section, FilePath));
            }

            return values;
        }

        /// <summary>접두사로 시작하는 키만 골라 준다. 대상이 0개면 예외 — 조용한 통과를 막는다.</summary>
        public static IReadOnlyDictionary<string, string> Keys(string section, string prefix)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in Section(section))
            {
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    result[pair.Key] = pair.Value;
            }

            if (result.Count == 0)
            {
                throw new InvalidOperationException(string.Format(
                    "골든 {0} 구획에 '{1}' 로 시작하는 키가 하나도 없다. 기준선을 다시 뜨거나 " +
                    "접두사를 확인할 것. 파일: {2}", section, prefix, FilePath));
            }

            return result;
        }

        public static string Raw(string section, string key)
        {
            string value;
            if (!Section(section).TryGetValue(key, out value))
            {
                throw new InvalidOperationException(
                    string.Format("골든에 키가 없다: {0} / {1}", section, key));
            }

            return value;
        }

        public static int Int(string section, string key)
        {
            return int.Parse(Raw(section, key), CultureInfo.InvariantCulture);
        }

        public static float Float(string section, string key)
        {
            return float.Parse(Raw(section, key), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        public static double Double(string section, string key)
        {
            return double.Parse(Raw(section, key), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void EnsureLoaded()
        {
            if (cache != null)
                return;

            string path = FilePath;
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(string.Format(
                    "골든 기준선이 없다: {0}\n플레이 중 F7 로 다시 뜰 것.", path));
            }

            var parsed = new Dictionary<string, Dictionary<string, string>>();
            Dictionary<string, string> current = null;

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    if (!parsed.TryGetValue(line, out current))
                    {
                        current = new Dictionary<string, string>();
                        parsed[line] = current;
                    }

                    continue;
                }

                if (current == null)
                    continue;

                int separator = line.IndexOf(" = ", StringComparison.Ordinal);
                if (separator < 0)
                    continue;

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 3).Trim();

                // 같은 키가 두 번 나오는 것 자체는 문제가 아니다. 덤퍼가 파생 값을 여러
                // 입력에서 뱉으면 자연스럽게 생긴다(예: reward.rewardFlags[Minimum] 은
                // 벽 체력 0/1/49 세 표본에서 모두 Minimum 등급이라 세 번 나온다).
                //
                // 위험한 것은 <b>값이 다른</b> 중복이다. 그때는 뒤엣것이 앞엣것을 조용히
                // 덮어 무엇을 검사하고 있는지 알 수 없게 된다. 그것만 막는다.
                string existing;
                if (current.TryGetValue(key, out existing))
                {
                    if (existing != value)
                    {
                        throw new InvalidOperationException(string.Format(
                            "골든에 값이 다른 중복 키가 있다: {0} — '{1}' vs '{2}'. " +
                            "덤퍼가 같은 키로 서로 다른 값을 뱉고 있다.", key, existing, value));
                    }

                    continue;
                }

                current[key] = value;
            }

            cache = parsed;
        }
    }
}
