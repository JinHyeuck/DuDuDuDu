using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace OJ.Headless
{
    /// <summary>
    /// TestHost.exe 를 Unity 의 Mono 에서 돌리고 NUnit3 결과 XML 을 받아 온다.
    ///
    /// 부모(.NET 10)와 자식(Mono)을 나눈 이유는 float 중간 정밀도 때문이다 — TestHost.cs 의
    /// 클래스 주석에 자세히 적어 뒀다. 요약하면, Unity 에디터와 <b>같은 답</b>을 내려면
    /// 테스트 실행은 반드시 Unity 가 쓰는 Mono 위에서 이뤄져야 한다.
    /// </summary>
    internal sealed class MonoTestRunner
    {
        private readonly UnityToolchain toolchain;
        private readonly string testHostExe;
        private readonly string workDirectory;

        public MonoTestRunner(UnityToolchain toolchain, string testHostExe, string workDirectory)
        {
            this.toolchain = toolchain;
            this.testHostExe = testHostExe;
            this.workDirectory = workDirectory;
        }

        public XElement Run(IEnumerable<string> testAssemblies, string filter, IDictionary<string, string> environment)
        {
            string resultPath = Path.Combine(workDirectory, "nunit-result.xml");
            if (File.Exists(resultPath))
                File.Delete(resultPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = toolchain.MonoExe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                WorkingDirectory = workDirectory,
            };

            startInfo.ArgumentList.Add(testHostExe);
            startInfo.ArgumentList.Add("--nunit");
            startInfo.ArgumentList.Add(toolchain.NUnitFrameworkDll);
            startInfo.ArgumentList.Add("--out");
            startInfo.ArgumentList.Add(resultPath);

            foreach (string assembly in testAssemblies)
            {
                startInfo.ArgumentList.Add("--test");
                startInfo.ArgumentList.Add(assembly);
            }

            foreach (string directory in ProbeDirectories())
            {
                startInfo.ArgumentList.Add("--probe");
                startInfo.ArgumentList.Add(directory);
            }

            if (!string.IsNullOrEmpty(filter))
            {
                startInfo.ArgumentList.Add("--filter");
                startInfo.ArgumentList.Add(filter);
            }

            // Mono 는 시작 시점에 MONO_PATH 를 읽어 어셈블리를 찾는다. 엔진 모듈끼리의 의존
            // (UnityEngine.CoreModule -> UnityEngine.SharedInternalsModule 등)까지 여기서 해결된다.
            startInfo.EnvironmentVariables["MONO_PATH"] =
                string.Join(Path.PathSeparator.ToString(), ProbeDirectories());

            foreach (KeyValuePair<string, string> pair in environment)
                startInfo.EnvironmentVariables[pair.Key] = pair.Value;

            string stdout, stderr;
            int exitCode;
            using (Process process = Process.Start(startInfo))
            {
                stdout = process.StandardOutput.ReadToEnd();
                stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }

            if (!string.IsNullOrWhiteSpace(stdout))
                Console.WriteLine(stdout.TrimEnd());

            if (exitCode != 0 || !File.Exists(resultPath))
            {
                throw new InvalidOperationException(
                    "Mono 테스트 호스트가 실패했다 (종료 코드 " + exitCode + ")." + Environment.NewLine +
                    stderr.Trim());
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                // Mono 는 에디터 밖에서 엔진 모듈을 로드할 때 경고를 뱉는 일이 있다.
                // 결과 자체는 나왔으므로 죽이지 않고 보여만 준다.
                Console.Error.WriteLine(stderr.TrimEnd());
            }

            return XElement.Parse(File.ReadAllText(resultPath));
        }

        private List<string> ProbeDirectories()
        {
            return toolchain.RuntimeProbeDirectories
                .Concat(toolchain.ExtraProbeDirectories)
                .Concat(new[] { workDirectory })
                .Where(Directory.Exists)
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
