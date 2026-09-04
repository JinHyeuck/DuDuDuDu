using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace OJ.Headless
{
    /// <summary>
    /// Unity 에디터를 열지 않고 EditMode 테스트를 돌린다.
    ///
    /// 흐름: 설정 읽기 -> Unity 툴체인 찾기 -> 소스에서 어셈블리 컴파일 -> NUnit 으로 실행 -> 요약 출력.
    /// 실패가 하나라도 있으면 종료 코드 1.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (IOException)
            {
                // 콘솔이 리다이렉트된 환경에서는 인코딩을 못 바꿀 수 있다. 치명적이지 않다.
            }

            var options = Options.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var total = Stopwatch.StartNew();

            try
            {
                return Execute(options, total);
            }
            catch (CompileFailedException compileFailed)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("=== " + compileFailed.Message + " ===");
                Console.Error.WriteLine(compileFailed.CompilerOutput);
                Console.Error.WriteLine();
                Console.Error.WriteLine("소요 " + Format(total.Elapsed));
                return 2;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("러너가 죽었다: " + error.Message);
                if (options.Verbose)
                    Console.Error.WriteLine(error.ToString());

                return 3;
            }
        }

        private static int Execute(Options options, Stopwatch total)
        {
            string configPath = Path.GetFullPath(options.ConfigPath);
            HeadlessConfig config = HeadlessConfig.Load(configPath);

            // 설정 파일이 Tools/headless/ 에 있으므로 리포 루트는 그 두 단계 위다.
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configPath), "..", ".."));
            string projectRoot = Path.GetFullPath(Path.Combine(repoRoot, config.ProjectPath));
            string buildRoot = Path.Combine(Path.GetDirectoryName(configPath), ".build", "asm");

            UnityToolchain toolchain = UnityToolchain.Resolve(repoRoot, projectRoot, config);

            if (toolchain.MonoExe == null && !options.CoreClr)
            {
                throw new InvalidOperationException(
                    "Unity 의 Mono(MonoBleedingEdge/bin/mono.exe)를 못 찾았다." + Environment.NewLine +
                    "테스트는 Unity 와 같은 런타임에서 돌려야 결과가 같다. 굳이 .NET 런타임에서 " +
                    "돌리려면 --coreclr 를 줄 것 — 단, 부동소수 결과가 에디터와 갈릴 수 있다.");
            }

            Console.WriteLine("헤드리스 EditMode 러너");
            Console.WriteLine("  리포      : " + repoRoot);
            Console.WriteLine("  프로젝트  : " + projectRoot);
            Console.WriteLine("  Unity     : " + toolchain.Version + "  (" + toolchain.EditorRoot + ")");
            Console.WriteLine("  컴파일러  : " + toolchain.CscDll);
            Console.WriteLine("  NUnit     : " + (toolchain.NUnitFrameworkDll ?? "(없음)"));
            Console.WriteLine("  실행 런타임: " + (options.CoreClr
                ? ".NET " + Environment.Version + "  ※ Unity(Mono)와 부동소수 결과가 갈릴 수 있다"
                : "Mono  " + toolchain.MonoExe));
            Console.WriteLine("  산출물    : " + buildRoot);
            Console.WriteLine();

            // 테스트가 Unity API 없이 골든 기준선을 찾을 수 있게 경로를 환경변수로 넘긴다.
            var testEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in config.Environment)
            {
                string value = pair.Value;
                if (!string.IsNullOrEmpty(value) && !Path.IsPathRooted(value))
                    value = Path.GetFullPath(Path.Combine(repoRoot, value));

                testEnvironment[pair.Key] = value;
                Environment.SetEnvironmentVariable(pair.Key, value);
                Console.WriteLine("  환경변수  : " + pair.Key + " = " + value);
            }

            if (config.Environment.Count > 0)
                Console.WriteLine();

            var compiler = new AssemblyCompiler(toolchain, config, buildRoot, options.Rebuild);
            var built = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var testAssemblies = new List<string>();

            foreach (HeadlessConfig.BuildTarget target in config.Assemblies)
            {
                AssemblyCompiler.Result result = compiler.Build(target, repoRoot, built);
                built[target.Name] = result.OutputPath;
                if (target.Test)
                    testAssemblies.Add(result.OutputPath);

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "[빌드] {0,-24} {1,3}개 파일 -> {2}  {3}",
                    target.Name, result.SourceCount, Path.GetFileName(result.OutputPath),
                    result.Skipped ? "(변경 없음)" : "(" + Format(result.Elapsed) + ")"));
            }

            if (testAssemblies.Count == 0)
            {
                Console.Error.WriteLine("실행할 테스트 어셈블리가 없다. headless.config.json 에서 \"test\": true 를 확인할 것.");
                return 3;
            }

            // 테스트 호스트도 같은 컴파일러로 만든다. Mono 가 실행할 수 있게 exe 로 뽑는다.
            var testHostTarget = new HeadlessConfig.BuildTarget
            {
                Name = "OJ.Headless.TestHost",
                Sources = { Path.Combine(Path.GetDirectoryName(configPath), "src", "TestHost") },
                References = { "nunit" },
                Executable = true,
                SkipProjectDefines = true,
            };

            AssemblyCompiler.Result testHost = compiler.Build(testHostTarget, repoRoot, built);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "[빌드] {0,-24} {1,3}개 파일 -> {2}  {3}",
                testHostTarget.Name, testHost.SourceCount, Path.GetFileName(testHost.OutputPath),
                testHost.Skipped ? "(변경 없음)" : "(" + Format(testHost.Elapsed) + ")"));

            Console.WriteLine();

            var cases = new List<TestCaseResult>();
            var suiteErrors = new List<TestCaseResult>();
            var runStopwatch = Stopwatch.StartNew();
            XElement xml;

            if (options.CoreClr)
            {
                // 진단용 경로. Unity 와 결과가 갈릴 수 있다는 것을 알고 쓰는 것이다.
                NUnitHost host = NUnitHost.Create(
                    toolchain.NUnitFrameworkDll,
                    toolchain.RuntimeProbeDirectories
                        .Concat(toolchain.ExtraProbeDirectories)
                        .Concat(new[] { buildRoot }));

                xml = new XElement("headless-run");
                foreach (string path in testAssemblies)
                    xml.Add(host.Run(host.LoadTestAssembly(path), options.Filter));
            }
            else
            {
                var mono = new MonoTestRunner(toolchain, testHost.OutputPath, buildRoot);
                xml = mono.Run(testAssemblies, options.Filter, testEnvironment);
            }

            runStopwatch.Stop();

            if (options.XmlPath != null)
            {
                string target = Path.GetFullPath(options.XmlPath);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                xml.Save(target);
                Console.WriteLine("결과 XML: " + target);
            }

            Collect(xml, cases, suiteErrors);

            if (options.List)
            {
                foreach (TestCaseResult item in cases.OrderBy(c => c.FullName, StringComparer.Ordinal))
                    Console.WriteLine(item.FullName);

                Console.WriteLine();
                Console.WriteLine(cases.Count + "개 테스트");

                // 나열 결과가 0건인 것도 통과가 아니다. --list 로 필터를 확인하는 스크립트가
                // 오타를 초록불로 넘기지 않게 여기서도 같은 규칙을 적용한다.
                if (cases.Count == 0)
                {
                    Console.Error.WriteLine("나열된 테스트가 0건이다 — 통과로 치지 않는다.");
                    return 1;
                }

                return 0;
            }

            List<string> shortfalls = CheckExpectedCounts(config, xml, cases, options);

            return Report(cases, suiteErrors, shortfalls, runStopwatch.Elapsed, total, options);
        }

        /// <summary>
        /// 어셈블리별로 수집된 test-case 개수를 센다.
        /// </summary>
        private static Dictionary<string, int> CountCasesByAssembly(XElement root)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement suite in root.DescendantsAndSelf("test-suite"))
            {
                if (NUnitHost.Attribute(suite, "type", null) != "Assembly")
                    continue;

                string key = Path.GetFileNameWithoutExtension(NUnitHost.Attribute(suite, "name", string.Empty));
                int existing;
                counts.TryGetValue(key, out existing);
                counts[key] = existing + suite.Descendants("test-case").Count();
            }

            return counts;
        }

        /// <summary>
        /// "조용히 적게 돌고 초록불" 을 막는다.
        ///
        /// 0건만 막는 것으로는 부족하다는 것을 실측했다. 테스트 파일 하나를 통째로 비우면
        /// 496개가 323개로 줄어드는데, 남은 것이 전부 통과라 러너는 종료 코드 0 을 냈다.
        /// 사라진 173개는 아무 데도 나타나지 않는다. 리팩토링처럼 파일이 어셈블리 사이를
        /// 오가는 작업에서 이건 현실적인 사고다.
        ///
        /// 그래서 headless.config.json 의 minTests 와 대조한다. 기대 개수를 커밋해 두는 것이
        /// 소실을 드러내는 유일한 방법이다. 다만 --filter 를 준 실행은 개수가 줄어드는 것이
        /// 정상이므로 검사하지 않는다(그 경우에도 0건 규칙은 그대로 살아 있다).
        /// </summary>
        private static List<string> CheckExpectedCounts(HeadlessConfig config, XElement xml,
            List<TestCaseResult> cases, Options options)
        {
            var problems = new List<string>();
            if (!string.IsNullOrEmpty(options.Filter))
                return problems;

            Dictionary<string, int> counts = CountCasesByAssembly(xml);

            foreach (HeadlessConfig.BuildTarget target in config.Assemblies)
            {
                if (!target.Test || target.MinTests <= 0)
                    continue;

                int actual;
                counts.TryGetValue(target.Name, out actual);
                if (actual < target.MinTests)
                {
                    problems.Add(string.Format(CultureInfo.InvariantCulture,
                        "{0}: 테스트가 {1}개만 수집됐다 — headless.config.json 의 minTests 는 {2} 다. " +
                        "테스트가 조용히 사라진 것은 아닌지 확인할 것. 줄인 것이 의도라면 같은 커밋에서 " +
                        "minTests 도 낮춰라.", target.Name, actual, target.MinTests));
                }
            }

            if (options.MinTests > 0 && cases.Count < options.MinTests)
            {
                problems.Add(string.Format(CultureInfo.InvariantCulture,
                    "전체 테스트가 {0}개만 수집됐다 — --min-tests 로 요구한 값은 {1} 다.",
                    cases.Count, options.MinTests));
            }

            return problems;
        }

        private sealed class TestCaseResult
        {
            public string FullName;
            public string Result;
            public string Label;
            public string Message;
            public string StackTrace;
        }

        /// <summary>
        /// NUnit3 결과 XML 을 훑어 test-case 를 모은다.
        ///
        /// 스위트(test-suite) 자체가 터진 경우도 따로 모은다. 픽스처 생성자나 TestCaseSource 가
        /// 예외를 던지면 그 안의 테스트가 아예 <b>생성되지 않아</b> test-case 가 0건이 되는데,
        /// 그걸 놓치면 "전부 통과"로 보인다. 가장 나쁜 실패라서 반드시 드러낸다.
        /// </summary>
        private static void Collect(XElement element, List<TestCaseResult> cases, List<TestCaseResult> suiteErrors)
        {
            if (element.Name == "test-case")
            {
                XElement failure = element.Element("failure") ?? element.Element("reason");
                cases.Add(new TestCaseResult
                {
                    FullName = NUnitHost.Attribute(element, "fullname", NUnitHost.Attribute(element, "name", "(이름 없음)")),
                    Result = NUnitHost.Attribute(element, "result", "Unknown"),
                    Label = NUnitHost.Attribute(element, "label", null),
                    Message = failure == null ? null : (string)failure.Element("message"),
                    StackTrace = failure == null ? null : (string)failure.Element("stack-trace"),
                });
                return;
            }

            if (element.Name == "test-suite")
            {
                string site = NUnitHost.Attribute(element, "site", "Test");
                XElement failure = element.Element("failure");
                if (failure != null && site != "Child" && site != "Parent")
                {
                    suiteErrors.Add(new TestCaseResult
                    {
                        FullName = NUnitHost.Attribute(element, "fullname", NUnitHost.Attribute(element, "name", "(이름 없음)")),
                        Result = NUnitHost.Attribute(element, "result", "Failed"),
                        Label = "스위트 " + site,
                        Message = (string)failure.Element("message"),
                        StackTrace = (string)failure.Element("stack-trace"),
                    });
                }
            }

            foreach (XElement child in element.Elements())
                Collect(child, cases, suiteErrors);
        }

        private static int Report(List<TestCaseResult> cases, List<TestCaseResult> suiteErrors,
            List<string> shortfalls, TimeSpan runElapsed, Stopwatch total, Options options)
        {
            int passed = cases.Count(c => c.Result == "Passed");
            int failed = cases.Count(c => c.Result == "Failed");
            int skipped = cases.Count(c => c.Result == "Skipped");
            int inconclusive = cases.Count(c => c.Result == "Inconclusive");
            int other = cases.Count - passed - failed - skipped - inconclusive;

            const string Line = "--------------------------------------------------------------------------";

            List<TestCaseResult> problems = suiteErrors
                .Concat(cases.Where(c => c.Result == "Failed"))
                .ToList();

            if (problems.Count > 0)
            {
                Console.WriteLine(Line);
                Console.WriteLine("실패 " + problems.Count + "건");
                Console.WriteLine(Line);

                int index = 0;
                foreach (TestCaseResult problem in problems)
                {
                    index++;
                    if (index > options.MaxFailures)
                    {
                        Console.WriteLine("... 그리고 " + (problems.Count - options.MaxFailures) +
                                          "건 더 (--max-failures 로 늘릴 것)");
                        break;
                    }

                    Console.WriteLine();
                    Console.WriteLine(index + ") " + problem.FullName);
                    if (!string.IsNullOrWhiteSpace(problem.Message))
                        Console.WriteLine(Indent(problem.Message.TrimEnd(), "   "));

                    if (options.Verbose && !string.IsNullOrWhiteSpace(problem.StackTrace))
                        Console.WriteLine(Indent(problem.StackTrace.TrimEnd(), "   "));
                }

                Console.WriteLine();
                Console.WriteLine(Line);
            }

            var summary = new StringBuilder();
            summary.Append("총 ").Append(cases.Count);
            summary.Append("   통과 ").Append(passed);
            summary.Append("   실패 ").Append(failed);
            summary.Append("   건너뜀 ").Append(skipped);
            summary.Append("   미결정 ").Append(inconclusive);
            if (other > 0)
                summary.Append("   기타 ").Append(other);
            if (suiteErrors.Count > 0)
                summary.Append("   스위트오류 ").Append(suiteErrors.Count);

            Console.WriteLine(summary.ToString());
            Console.WriteLine("테스트 실행 " + Format(runElapsed) + " / 전체 " + Format(total.Elapsed));

            if (cases.Count == 0 && suiteErrors.Count == 0)
            {
                // 0건은 통과가 아니다. 필터 오타나 빌드 사고를 통과로 착각하지 않게 막는다.
                Console.Error.WriteLine("실행된 테스트가 0건이다 — 통과로 치지 않는다.");
                return 1;
            }

            if (shortfalls.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("테스트 수가 기대보다 적다 — 통과로 치지 않는다.");
                foreach (string shortfall in shortfalls)
                    Console.Error.WriteLine("  " + shortfall);

                return 1;
            }

            int exitCode = failed + suiteErrors.Count > 0 ? 1 : 0;

            if (options.CoreClr)
            {
                // --coreclr 는 진단용인데 종료 코드는 0 이 나올 수 있다. 그 초록불을 그대로 믿으면
                // 안 된다 — 실측으로 이 모드는 Mono(=에디터)에서 실패하는 테스트를 통과시킨다.
                // CI 가 실수로 이 옵션을 달았을 때 조용히 넘어가지 않도록 크게 적어 둔다.
                Console.Error.WriteLine();
                Console.Error.WriteLine(
                    "※ --coreclr 로 돌렸다. 이 결과는 Unity 에디터와 다를 수 있으므로 판정에 쓰지 말 것 " +
                    "(에디터는 Mono 에서 돌린다). 판정용 실행은 옵션 없이 돌려라.");
            }

            return exitCode;
        }

        private static string Indent(string text, string prefix)
        {
            return string.Join(Environment.NewLine,
                text.Replace("\r\n", "\n").Split('\n').Select(line => prefix + line));
        }

        private static string Format(TimeSpan span)
        {
            return span.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s";
        }

        private static void PrintHelp()
        {
            Console.WriteLine(@"헤드리스 EditMode 테스트 러너

  Tools\headless\run-tests.cmd [옵션]

옵션
  --config <경로>       headless.config.json 경로 (기본: 러너 옆)
  --filter <정규식>     테스트 전체 이름에 대한 정규식 필터
  --rebuild             변경 없어도 다시 컴파일
  --list                테스트 이름만 나열
  --xml <경로>          NUnit3 결과 XML 저장
  --max-failures <n>    실패 상세 출력 개수 (기본 25)
  --min-tests <n>       수집된 테스트가 n 개 미만이면 실패 처리
                        (어셈블리별 기대치는 headless.config.json 의 minTests)
  --verbose             스택 트레이스와 예외 원문까지 출력
  --coreclr             Mono 대신 .NET 런타임에서 실행 (진단용)
                        ※ float 중간 정밀도가 달라 Unity 와 결과가 갈릴 수 있다
  -h, --help            이 도움말

종료 코드
  0 = 전부 통과, 1 = 테스트 실패, 2 = 컴파일 실패, 3 = 러너 오류");
        }

        private sealed class Options
        {
            public string ConfigPath;
            public string Filter;
            public string XmlPath;
            public bool Rebuild;
            public bool List;
            public bool Verbose;
            public bool ShowHelp;
            public bool CoreClr;
            public int MaxFailures = 25;
            public int MinTests;

            public static Options Parse(string[] args)
            {
                var options = new Options
                {
                    ConfigPath = Path.Combine(
                        Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? ".",
                        "headless.config.json"),
                };

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--config":
                            options.ConfigPath = args[++i];
                            break;
                        case "--filter":
                            options.Filter = args[++i];
                            break;
                        case "--xml":
                            options.XmlPath = args[++i];
                            break;
                        case "--max-failures":
                            options.MaxFailures = int.Parse(args[++i], CultureInfo.InvariantCulture);
                            break;
                        case "--min-tests":
                            options.MinTests = int.Parse(args[++i], CultureInfo.InvariantCulture);
                            break;
                        case "--rebuild":
                            options.Rebuild = true;
                            break;
                        case "--list":
                            options.List = true;
                            break;
                        case "--verbose":
                            options.Verbose = true;
                            break;
                        case "--coreclr":
                            options.CoreClr = true;
                            break;
                        case "-h":
                        case "--help":
                            options.ShowHelp = true;
                            break;
                        default:
                            throw new ArgumentException("모르는 옵션이다: " + args[i]);
                    }
                }

                return options;
            }
        }
    }
}
