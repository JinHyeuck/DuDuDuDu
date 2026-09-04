using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OJ.Headless
{
    /// <summary>
    /// Unity 가 들고 있는 Roslyn(csc)으로 소스를 직접 컴파일한다.
    ///
    /// Unity 와 <b>같은 컴파일러 바이너리, 같은 langversion, 같은 참조 집합, 같은 define</b> 을 쓰기 때문에
    /// 나오는 IL 이 에디터 것과 사실상 동일하다. 부동소수 결합 순서처럼 IL 수준에서 갈리는 동작을
    /// 재현해야 하므로 이 점이 중요하다.
    /// </summary>
    internal sealed class AssemblyCompiler
    {
        /// <summary>
        /// Unity 가 스크립트 컴파일에서 끄는 경고들.
        ///   0169 사용되지 않는 필드 / 0649 대입되지 않는 필드 — 둘 다 [SerializeField] 의 정상 형태다
        ///   0282 partial 구조체 필드 순서 / 1701·1702 어셈블리 참조 버전 불일치
        /// </summary>
        private static readonly string[] UnitySuppressedWarnings = { "0169", "0649", "0282", "1701", "1702" };

        private readonly UnityToolchain toolchain;
        private readonly HeadlessConfig config;
        private readonly string outputDirectory;
        private readonly bool forceRebuild;

        public AssemblyCompiler(UnityToolchain toolchain, HeadlessConfig config, string outputDirectory, bool forceRebuild)
        {
            this.toolchain = toolchain;
            this.config = config;
            this.outputDirectory = outputDirectory;
            this.forceRebuild = forceRebuild;
            Directory.CreateDirectory(outputDirectory);
        }

        public sealed class Result
        {
            public string Name;
            public string OutputPath;
            public int SourceCount;
            public bool Skipped;
            public TimeSpan Elapsed;
        }

        public Result Build(HeadlessConfig.BuildTarget target, string repoRoot, IDictionary<string, string> alreadyBuilt)
        {
            var stopwatch = Stopwatch.StartNew();

            List<string> sources = CollectSources(target, repoRoot);
            if (sources.Count == 0)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} 의 소스를 하나도 못 찾았다. headless.config.json 의 sources 경로를 확인할 것: {1}",
                    target.Name, string.Join(", ", target.Sources)));
            }

            List<string> references = BuildReferenceList(target, alreadyBuilt);
            string outputPath = Path.Combine(outputDirectory, target.Name + (target.Executable ? ".exe" : ".dll"));
            string responsePath = Path.Combine(outputDirectory, target.Name + ".rsp");
            string stampPath = Path.Combine(outputDirectory, target.Name + ".stamp");

            string responseText = BuildResponseFile(outputPath, sources, references, target);
            string stamp = ComputeStamp(responseText, sources, references);

            if (!forceRebuild && File.Exists(outputPath) && File.Exists(stampPath) &&
                string.Equals(File.ReadAllText(stampPath), stamp, StringComparison.Ordinal))
            {
                stopwatch.Stop();
                return new Result
                {
                    Name = target.Name,
                    OutputPath = outputPath,
                    SourceCount = sources.Count,
                    Skipped = true,
                    Elapsed = stopwatch.Elapsed,
                };
            }

            File.WriteAllText(responsePath, responseText, new UTF8Encoding(false));

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                WorkingDirectory = repoRoot,
            };
            startInfo.ArgumentList.Add(toolchain.CscDll);
            startInfo.ArgumentList.Add("-noconfig");
            startInfo.ArgumentList.Add("@" + responsePath);

            string stdout, stderr;
            int exitCode;
            using (Process process = Process.Start(startInfo))
            {
                stdout = process.StandardOutput.ReadToEnd();
                stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }

            if (exitCode != 0)
            {
                // 스탬프를 지워 다음 실행이 반드시 다시 컴파일하게 한다.
                if (File.Exists(stampPath))
                    File.Delete(stampPath);

                throw new CompileFailedException(target.Name, (stdout + Environment.NewLine + stderr).Trim());
            }

            // 경고는 그대로 흘려 보여 준다. Unity 콘솔에서 보던 것과 같은 문구다.
            string warnings = (stdout + Environment.NewLine + stderr).Trim();
            if (warnings.Length > 0)
                Console.WriteLine(warnings);

            File.WriteAllText(stampPath, stamp, new UTF8Encoding(false));
            stopwatch.Stop();

            return new Result
            {
                Name = target.Name,
                OutputPath = outputPath,
                SourceCount = sources.Count,
                Skipped = false,
                Elapsed = stopwatch.Elapsed,
            };
        }

        private List<string> CollectSources(HeadlessConfig.BuildTarget target, string repoRoot)
        {
            var sources = new List<string>();
            foreach (string relative in target.Sources)
            {
                string root = Path.IsPathRooted(relative) ? relative : Path.Combine(repoRoot, relative);
                if (File.Exists(root))
                {
                    sources.Add(Path.GetFullPath(root));
                    continue;
                }

                if (!Directory.Exists(root))
                    throw new DirectoryNotFoundException(target.Name + " 의 소스 경로가 없다: " + root);

                string rootFull = Path.GetFullPath(root);

                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);

                    // Unity 규약: 이름이 '.' 으로 시작하거나 ~ 로 끝나는 폴더는 임포트되지 않는다.
                    if (file.Replace('\\', '/').Split('/').Any(part => part.StartsWith(".", StringComparison.Ordinal) || part.EndsWith("~", StringComparison.Ordinal)))
                        continue;

                    // 파일 이름과 소스 루트 기준 상대 경로 둘 다에 패턴을 대 본다. 폴더 단위로 빼야 하는
                    // 경우(Assembly-CSharp 에서 Editor 폴더와 다른 asmdef 소속 폴더를 걷어내는 것)가
                    // 실제 용례라 이름만으로는 부족하다.
                    string withinSourceRoot = RelativeSourcePath(rootFull, file);

                    // include 가 비어 있지 않으면 "적어도 하나에 맞아야 한다"가 된다.
                    // Assembly-CSharp-Editor 처럼 <b>흩어진 Editor 폴더만</b> 모으는 어셈블리가
                    // 이걸 쓴다. 폴더를 일일이 적으면 새 Editor 폴더가 생겼을 때 조용히 빠지는데,
                    // 그건 "컴파일 안 되는 에디터 도구"를 만들어 두고 모르는 상태가 된다.
                    if (target.Include.Count > 0 &&
                        !target.Include.Any(pattern => MatchesGlob(name, pattern) || MatchesGlob(withinSourceRoot, pattern)))
                    {
                        continue;
                    }

                    if (target.Exclude.Any(pattern => MatchesGlob(name, pattern) || MatchesGlob(withinSourceRoot, pattern)))
                        continue;

                    sources.Add(Path.GetFullPath(file));
                }
            }

            sources.Sort(StringComparer.OrdinalIgnoreCase);
            return sources;
        }

        /// <summary>소스 루트 기준 상대 경로를 '/' 구분자로 돌려준다.</summary>
        private static string RelativeSourcePath(string rootFull, string file)
        {
            string full = Path.GetFullPath(file);
            string prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;

            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                full = full.Substring(prefix.Length);

            return full.Replace('\\', '/');
        }

        private static bool MatchesGlob(string name, string pattern)
        {
            string regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(name, regex,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private List<string> BuildReferenceList(HeadlessConfig.BuildTarget target, IDictionary<string, string> alreadyBuilt)
        {
            var references = new List<string>(toolchain.ReferenceAssemblies);

            foreach (string reference in target.References)
            {
                if (string.Equals(reference, "nunit", StringComparison.OrdinalIgnoreCase))
                {
                    if (toolchain.NUnitFrameworkDll == null)
                    {
                        throw new InvalidOperationException(
                            "nunit.framework.dll 을 못 찾았다. 프로젝트의 Library/PackageCache 에 " +
                            "com.unity.ext.nunit 이 있는지 확인할 것.");
                    }

                    references.Add(toolchain.NUnitFrameworkDll);
                    continue;
                }

                if (string.Equals(reference, "unityEditor", StringComparison.OrdinalIgnoreCase))
                {
                    if (toolchain.EditorAssemblies.Count == 0)
                        throw new InvalidOperationException("UnityEditor 참조 어셈블리를 못 찾았다: " + toolchain.EngineDirectory);

                    references.AddRange(toolchain.EditorAssemblies);
                    continue;
                }

                if (string.Equals(reference, "scriptAssemblies", StringComparison.OrdinalIgnoreCase))
                {
                    references.AddRange(CollectScriptAssemblies(target));
                    continue;
                }

                // "package:Newtonsoft.Json.dll" — 패키지에 그대로 들어 있는 미리 컴파일된 DLL.
                // ScriptAssemblies 와 달리 에디터를 열지 않아도 최신이라 참조해도 낡지 않는다.
                if (reference.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = reference.Substring("package:".Length);
                    string dll = toolchain.FindPackageDll(fileName);
                    if (dll == null)
                    {
                        throw new FileNotFoundException(string.Format(
                            "{0} 이 참조하는 패키지 DLL '{1}' 을 못 찾았다.{2}" +
                            "Library/PackageCache · Packages · Assets/Plugins 를 뒤졌다. " +
                            "manifest.json 에 패키지를 넣은 뒤 Unity 를 한 번 열어 받아야 한다.",
                            target.Name, fileName, Environment.NewLine));
                    }

                    // FindPackageDll 이 폴더를 toolchain.ExtraProbeDirectories 에 넣어 준다.
                    // 컴파일 참조만으로는 부족하고 실행 시 탐색 경로에도 있어야 한다.
                    references.Add(dll);
                    continue;
                }

                string built;
                if (alreadyBuilt.TryGetValue(reference, out built))
                {
                    references.Add(built);
                    continue;
                }

                if (File.Exists(reference))
                {
                    references.Add(Path.GetFullPath(reference));
                    continue;
                }

                throw new InvalidOperationException(string.Format(
                    "{0} 의 참조 '{1}' 를 풀 수 없다. 이 설정에서 먼저 빌드되는 어셈블리 이름이거나 " +
                    "\"nunit\" / \"unityEditor\" / \"scriptAssemblies\" 이거나 실제 DLL 경로여야 한다.",
                    target.Name, reference));
            }

            // 같은 단순 이름이 두 번 들어가면 csc 가 CS1703 으로 죽는다. 먼저 나온 것을 남긴다 —
            // 그래서 순서가 곧 우선순위다: BCL/엔진 → 설정에 적힌 순서(소스에서 빌드한 것이 앞).
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return references.Where(path => seen.Add(Path.GetFileNameWithoutExtension(path))).ToList();
        }

        /// <summary>
        /// Library/ScriptAssemblies 의 DLL 을 참조로 끌어온다.
        ///
        /// 이 러너의 원칙은 "ScriptAssemblies 를 쓰지 않는다" 이고 그것이 에디터를 열지 않아도
        /// 되는 이유다. 여기는 그 원칙의 좁은 예외다 — UniTask·TextMeshPro·uGUI 같은 패키지
        /// 어셈블리는 Unity 밖에서 만들 수 없고 설치본에도 없다. <b>게임 코드는 여전히 소스에서
        /// 컴파일되므로</b> 내 수정이 반영되지 않는 문제는 생기지 않는다. 갱신이 필요한 순간은
        /// 패키지 목록이 바뀔 때뿐이다.
        ///
        /// 이 설정이 소스에서 빌드하는 어셈블리는 이름이 같아도 끌어오지 않는다. 낡은 DLL 이
        /// 방금 컴파일한 것을 가리면 "고쳤는데 옛날 코드로 테스트되는" 최악의 조용한 사고가 된다.
        /// </summary>
        private List<string> CollectScriptAssemblies(HeadlessConfig.BuildTarget target)
        {
            string directory = toolchain.ScriptAssembliesDirectory;
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(string.Format(
                    "{0} 이 참조하는 Library/ScriptAssemblies 가 없다: {1}{2}" +
                    "패키지 어셈블리(UniTask, TextMeshPro, uGUI …)는 Unity 가 만들어 주는 것이라 " +
                    "에디터를 한 번 열어야 생긴다.", target.Name, directory, Environment.NewLine));
            }

            var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HeadlessConfig.BuildTarget other in config.Assemblies)
                skip.Add(other.Name);

            foreach (string name in target.ExcludeReferences)
                skip.Add(name);

            var found = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly)
                .Where(path => !skip.Contains(Path.GetFileNameWithoutExtension(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (found.Count == 0)
            {
                throw new InvalidOperationException(
                    target.Name + " 이 참조할 DLL 이 ScriptAssemblies 에 하나도 없다: " + directory);
            }

            return found;
        }

        private string BuildResponseFile(string outputPath, List<string> sources, List<string> references,
            HeadlessConfig.BuildTarget target)
        {
            var builder = new StringBuilder();
            builder.AppendLine(target.Executable ? "-target:exe" : "-target:library");
            builder.AppendLine("-out:\"" + outputPath + "\"");
            builder.AppendLine("-nostdlib+");
            builder.AppendLine("-nologo");
            builder.AppendLine("-utf8output");
            builder.AppendLine("-deterministic");
            builder.AppendLine("-langversion:" + config.LangVersion);
            builder.AppendLine(target.Unsafe ? "-unsafe+" : "-unsafe-");

            // Unity 에디터가 스크립트 어셈블리를 만들 때 쓰는 값. 최적화를 켜면 IL 이 달라질 수 있어
            // 에디터와 동일하게 맞춘다(디버그 구성).
            builder.AppendLine("-optimize-");
            builder.AppendLine("-debug:portable");
            builder.AppendLine("-warnaserror-");

            // Unity 가 모든 스크립트 어셈블리에 거는 것과 같은 억제 목록이다
            // (Library/Bee/artifacts/*.dag/<어셈블리>.rsp 에서 그대로 뽑았다).
            // 여기를 맞춰야 러너가 뱉는 경고가 Unity 콘솔에서 보던 것과 같아진다.
            // 특히 0649/0169 는 [SerializeField] 필드마다 나므로, 안 막으면 진짜 오류가
            // 수백 줄 경고에 파묻힌다.
            foreach (string warning in UnitySuppressedWarnings)
                builder.AppendLine("-nowarn:" + warning);

            List<string> defines = (target.SkipProjectDefines ? Enumerable.Empty<string>() : config.Defines)
                .Concat(target.Defines ?? new List<string>())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (defines.Count > 0)
                builder.AppendLine("-define:" + string.Join(";", defines));

            foreach (string reference in references)
                builder.AppendLine("-r:\"" + reference + "\"");

            foreach (string source in sources)
                builder.AppendLine("\"" + source + "\"");

            return builder.ToString();
        }

        private static string ComputeStamp(string responseText, List<string> sources, List<string> references)
        {
            var builder = new StringBuilder();
            builder.Append(responseText);

            foreach (string file in sources.Concat(references))
            {
                var info = new FileInfo(file);
                builder.Append(file)
                    .Append('|')
                    .Append(info.Exists ? info.Length : -1L)
                    .Append('|')
                    .Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0L)
                    .Append('\n');
            }

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return string.Concat(hash.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }

    internal sealed class CompileFailedException : Exception
    {
        public CompileFailedException(string assemblyName, string output)
            : base("컴파일 실패: " + assemblyName)
        {
            AssemblyName = assemblyName;
            CompilerOutput = output;
        }

        public string AssemblyName { get; }
        public string CompilerOutput { get; }
    }
}
