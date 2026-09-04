using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace OJ.Headless
{
    /// <summary>
    /// NUnit 을 실제로 돌리는 자식 프로세스.
    ///
    /// 왜 별도 프로세스인가 — <b>런타임이 달라야 하기 때문이다.</b>
    /// Unity 에디터는 EditMode 테스트를 Mono(MonoBleedingEdge)에서 돌린다. Mono 의 JIT 는
    /// float 식의 중간 결과를 float 로 매번 접지 않고 더 높은 정밀도로 들고 가다가 대입 시점에
    /// 한 번 접는다(C# 명세가 허용하는 동작이다). CoreCLR 은 연산마다 float 로 접는다.
    /// 그래서 같은 IL 이라도 결과가 갈린다. 실제로 갈린 예:
    ///
    ///   1f + (7f * 0.145f) + (7f * 7f * 0.015f)
    ///     Mono    -> 2.75f       -> RoundToInt(2 * 2.75f)      = 6
    ///     CoreCLR -> 2.7499998f  -> RoundToInt(2 * 2.7499998f) = 5
    ///
    /// 이 도구의 목적은 "Unity 와 같은 답"을 내는 것이므로, 실행만은 반드시 Unity 의 Mono 에서 한다.
    /// 부모 프로세스(.NET 콘솔 앱)는 컴파일과 보고만 맡는다.
    ///
    /// 테스트 수집도 단언 평가도 전부 NUnit 이 한다. 이 파일에는 NUnit 을 흉내 낸 코드가 없다.
    /// 결과는 NUnit3 결과 XML 로 파일에 떨군다 — 부모가 그걸 읽어 요약한다.
    /// </summary>
    public static class TestHost
    {
        public static int Main(string[] args)
        {
            var testAssemblies = new List<string>();
            var probeDirectories = new List<string>();
            string nunitPath = null;
            string outputPath = null;
            string filter = null;

            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--nunit": nunitPath = args[++i]; break;
                        case "--test": testAssemblies.Add(args[++i]); break;
                        case "--out": outputPath = args[++i]; break;
                        case "--filter": filter = args[++i]; break;
                        case "--probe": probeDirectories.Add(args[++i]); break;
                        default: throw new ArgumentException("unknown option: " + args[i]);
                    }
                }
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TestHost: bad arguments: " + error.Message);
                return 64;
            }

            if (testAssemblies.Count == 0 || outputPath == null)
            {
                Console.Error.WriteLine("TestHost: --test and --out are required.");
                return 64;
            }

            if (nunitPath != null)
                probeDirectories.Add(Path.GetDirectoryName(Path.GetFullPath(nunitPath)));

            Install(probeDirectories);

            try
            {
                return RunAll(testAssemblies, outputPath, filter);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TestHost: " + error);
                return 65;
            }
        }

        private static void Install(List<string> probeDirectories)
        {
            var directories = new List<string>();
            foreach (string directory in probeDirectories)
            {
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory) && !directories.Contains(directory))
                    directories.Add(Path.GetFullPath(directory));
            }

            // MONO_PATH 로도 넘기지만, 그것만으로 안 잡히는 경우(엔진 모듈 간 의존 등)를 위해
            // 명시적 해석기도 건다.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
            {
                string simpleName = new AssemblyName(eventArgs.Name).Name;
                foreach (string directory in directories)
                {
                    string candidate = Path.Combine(directory, simpleName + ".dll");
                    if (File.Exists(candidate))
                        return Assembly.LoadFrom(candidate);
                }

                return null;
            };
        }

        private static int RunAll(List<string> testAssemblies, string outputPath, string filter)
        {
            var builder = new StringBuilder();
            builder.Append("<headless-run>");

            foreach (string path in testAssemblies)
            {
                Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(path));

                var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
                runner.Load(assembly, new Dictionary<string, object>());

                ITestFilter testFilter = string.IsNullOrEmpty(filter)
                    ? TestFilter.Empty
                    : TestFilter.FromXml("<filter><test re='1'>" + Escape(filter) + "</test></filter>");

                ITestResult result = runner.Run(TestListener.NULL, testFilter);
                builder.Append(result.ToXml(true).OuterXml);
            }

            builder.Append("</headless-run>");

            string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static string Escape(string value)
        {
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
