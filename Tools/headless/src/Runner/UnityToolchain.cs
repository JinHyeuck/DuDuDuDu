using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OJ.Headless
{
    /// <summary>
    /// Unity 설치본에서 "컴파일에 필요한 것"만 골라 내는 곳.
    ///
    /// 핵심 원칙: <b>Library/ScriptAssemblies 의 DLL 은 절대 쓰지 않는다.</b> 그 폴더는 Unity 에디터가
    /// 포커스를 받아야 갱신되므로, 그걸 쓰는 순간 다시 사람 손이 필요해진다. 여기서 모으는 것은
    /// 전부 Unity <i>설치 경로</i>의 참조 어셈블리와 엔진 모듈이고, 프로젝트 코드는 소스에서 직접 컴파일한다.
    ///
    /// 참조 집합은 Unity 가 스스로 만들어 주는 &lt;Assembly&gt;.csproj 의 참조 목록과 같은 구성이다:
    ///   - Editor/Data/UnityReferenceAssemblies/unity-4.8-api/*.dll (+ Facades)  ← BCL, apiCompatibilityLevel=NET_Unity_4_8
    ///   - Editor/Data/Managed/UnityEngine/UnityEngine*.dll                       ← 엔진 모듈
    ///
    /// <para>
    /// 위 "항상 들어가는" 집합에는 <b>ScriptAssemblies 가 들어 있지 않다.</b> 다만 프로젝트 코드가
    /// 패키지(UniTask, TextMeshPro, uGUI …)를 쓰면 그 어셈블리는 Unity 밖에서 만들 수 없으므로,
    /// 그때만 설정에서 "scriptAssemblies" 토큰으로 <i>명시적으로</i> 끌어 쓴다.
    /// <see cref="ScriptAssembliesDirectory"/> 주석에 대가와 한계를 적어 뒀다.
    /// </para>
    /// </summary>
    internal sealed class UnityToolchain
    {
        public string EditorRoot { get; private set; }
        public string Version { get; private set; }
        public string CscDll { get; private set; }
        public string NUnitFrameworkDll { get; private set; }
        public IReadOnlyList<string> ReferenceAssemblies { get; private set; }

        /// <summary>
        /// UnityEditor 참조 집합(UnityEditor.dll + UnityEditor.*Module.dll).
        ///
        /// 런타임 어셈블리인데도 필요하다 — 프로젝트 defines 에 UNITY_EDITOR 가 들어 있어
        /// 런타임 코드의 <c>#if UNITY_EDITOR</c> 블록이 살아난다. 에디터 안에서 Unity 가
        /// Assembly-CSharp 을 컴파일하는 방식과 같다.
        /// </summary>
        public IReadOnlyList<string> EditorAssemblies { get; private set; }

        /// <summary>
        /// 프로젝트의 Library/ScriptAssemblies. <b>기본 참조 집합에는 절대 넣지 않는다.</b>
        ///
        /// 이 폴더는 Unity 에디터가 포커스를 받아야 갱신되므로, 여기에 기대는 순간 도구가 다시
        /// 사람 손을 요구하게 된다. 그래서 OJ.Core / OJ.Core.Tests 는 지금도 이걸 쳐다보지 않는다.
        ///
        /// 예외는 <i>여기서만 얻을 수 있는 것</i>이다: UniTask·TextMeshPro·uGUI 같은 패키지
        /// 어셈블리는 소스에서 다시 컴파일할 대상이 아니고 Unity 설치본에도 없다. 그 참조가 필요한
        /// 어셈블리(Assembly-CSharp)만 설정에서 "scriptAssemblies" 를 골라 쓴다.
        ///
        /// 대가: 패키지 버전이 바뀌면 에디터를 한 번 열어 이 폴더를 갱신해야 한다. 반대로 게임
        /// 코드는 여전히 소스에서 컴파일되므로 "에디터를 열지 않으면 내 수정이 반영되지 않는다"는
        /// 문제는 생기지 않는다.
        /// </summary>
        public string ScriptAssembliesDirectory { get; private set; }

        /// <summary>
        /// Unity 에디터가 EditMode 테스트를 돌리는 그 런타임(MonoBleedingEdge).
        /// 테스트는 반드시 여기서 실행해야 한다 — 자세한 이유는 TestHost.cs 주석 참고.
        /// </summary>
        public string MonoExe { get; private set; }

        /// <summary>엔진 모듈이 들어 있는 폴더. 런타임 탐색 경로로도 쓴다.</summary>
        public string EngineDirectory { get; private set; }

        /// <summary>런타임에 어셈블리를 되찾을 때 뒤질 디렉터리들(엔진 모듈, nunit).</summary>
        public IReadOnlyList<string> RuntimeProbeDirectories { get; private set; }

        /// <summary>패키지 DLL 을 찾을 때 쓰는 프로젝트 루트.</summary>
        public string ProjectRoot { get; private set; }

        /// <summary>
        /// <see cref="FindPackageDll"/> 가 찾아낸 DLL 의 폴더. 컴파일 참조로 넣는 것만으로는
        /// 부족하다 — 테스트 호스트가 실행 중에 그 어셈블리를 로드해야 하므로 탐색 경로에도
        /// 들어가야 한다. 여기 넣지 않으면 컴파일은 통과하고 <b>테스트가 FileNotFound 로 죽는다.</b>
        /// </summary>
        public List<string> ExtraProbeDirectories { get; } = new List<string>();

        public static UnityToolchain Resolve(string repoRoot, string projectRoot, HeadlessConfig config)
        {
            string version = config.UnityVersion;
            if (string.IsNullOrEmpty(version) || version == "auto")
                version = ReadProjectEditorVersion(projectRoot);

            string editorRoot = FindEditorRoot(repoRoot, config, version);

            string data = Path.Combine(editorRoot, "Editor", "Data");
            string csc = Path.Combine(data, "DotNetSdkRoslyn", "csc.dll");
            if (!File.Exists(csc))
                throw new FileNotFoundException("Unity 가 들고 있는 Roslyn 컴파일러를 못 찾았다: " + csc, csc);

            string engineDir = Path.Combine(data, "Managed", "UnityEngine");
            if (!Directory.Exists(engineDir))
                throw new DirectoryNotFoundException("엔진 모듈 폴더가 없다: " + engineDir);

            var references = new List<string>();

            // BCL. unity-4.8-api 는 Unity 의 apiCompatibilityLevel = .NET Framework(=NET_Unity_4_8) 용
            // 참조 어셈블리다. Unity 가 실제로 쓰는 것과 같은 걸 써야 컴파일 결과가 같아진다.
            string bcl = Path.Combine(data, "UnityReferenceAssemblies", "unity-4.8-api");
            if (Directory.Exists(bcl))
            {
                references.AddRange(Directory.GetFiles(bcl, "*.dll", SearchOption.TopDirectoryOnly));
                string facades = Path.Combine(bcl, "Facades");
                if (Directory.Exists(facades))
                    references.AddRange(Directory.GetFiles(facades, "*.dll", SearchOption.TopDirectoryOnly));
            }
            else
            {
                // 설치본에 unity-4.8-api 가 없는 구성(.NET Standard 프로필)일 때의 대안.
                string netstandard = Path.Combine(data, "NetStandard", "ref", "2.1.0", "netstandard.dll");
                if (!File.Exists(netstandard))
                    throw new FileNotFoundException("BCL 참조 어셈블리를 못 찾았다: " + bcl + " / " + netstandard);

                references.Add(netstandard);
                string shims = Path.Combine(data, "NetStandard", "compat", "2.1.0", "shims", "netfx");
                if (Directory.Exists(shims))
                    references.AddRange(Directory.GetFiles(shims, "*.dll", SearchOption.TopDirectoryOnly));
            }

            // 엔진 모듈 전부. UnityEngine.CoreModule.dll(Mathf 가 여기 있다)이 포함된다.
            references.AddRange(Directory.GetFiles(engineDir, "UnityEngine*.dll", SearchOption.TopDirectoryOnly));

            // UnityEditor 는 기본 집합이 아니라 별도 목록이다. 필요한 어셈블리만 설정에서 골라 쓴다.
            //
            // 반드시 engineDir(Managed/UnityEngine) 쪽을 쓴다. 같은 이름의 DLL 이 Managed/ 에도 있는데
            // 그건 에디터가 실제로 로드하는 17MB 본체이고, engineDir 것은 모듈로 타입을 넘겨 주는
            // 300KB 짜리 facade 다. 본체를 참조하면 EditorApplication 같은 타입이 UnityEditor 와
            // UnityEditor.CoreModule 양쪽에 존재하게 되어 CS0433 이 난다.
            // (Unity 자신의 Assembly-CSharp.rsp 도 engineDir 쪽을 참조한다.)
            var editorAssemblies = new List<string>(
                Directory.GetFiles(engineDir, "UnityEditor*.dll", SearchOption.TopDirectoryOnly));

            var probe = new List<string> { engineDir };

            string nunit = FindNUnit(projectRoot);
            if (nunit != null)
                probe.Add(Path.GetDirectoryName(nunit));

            return new UnityToolchain
            {
                EditorRoot = editorRoot,
                Version = version,
                CscDll = csc,
                NUnitFrameworkDll = nunit,
                MonoExe = FindMono(data),
                EngineDirectory = engineDir,
                ReferenceAssemblies = Dedupe(references),
                EditorAssemblies = Dedupe(editorAssemblies),
                ScriptAssembliesDirectory = Path.Combine(projectRoot, "Library", "ScriptAssemblies"),
                RuntimeProbeDirectories = probe,
                ProjectRoot = projectRoot,
            };
        }

        /// <summary>
        /// PackageCache 안의 미리 컴파일된 DLL 을 이름으로 찾는다 (예: Newtonsoft.Json.dll).
        ///
        /// <b>ScriptAssemblies 와 다르다.</b> ScriptAssemblies 는 Unity 가 asmdef 를 <i>컴파일해서</i>
        /// 만드는 산출물이라 에디터를 열어야 갱신되지만, 이런 DLL 은 패키지에 그대로 들어 있어
        /// UPM 이 패키지를 내려받는 순간 존재한다. 낡을 일이 없으므로 참조해도 안전하다.
        ///
        /// 같은 이름이 여러 개 나오면 <b>경로가 가장 얕은 것</b>을 쓴다. Newtonsoft 가 그런 경우로
        /// <c>Runtime/Newtonsoft.Json.dll</c>(에디터·Mono)과 <c>Runtime/AOT/Newtonsoft.Json.dll</c>
        /// (IL2CPP 용)이 같이 들어 있다. 이 러너는 Mono 에서 돌므로 앞의 것이 맞다.
        /// </summary>
        public string FindPackageDll(string fileName)
        {
            var roots = new List<string>
            {
                Path.Combine(ProjectRoot, "Library", "PackageCache"),
                Path.Combine(ProjectRoot, "Packages"),
                Path.Combine(ProjectRoot, "Assets", "Plugins"),
            };

            var hits = new List<string>();
            foreach (string root in roots)
            {
                if (Directory.Exists(root))
                    hits.AddRange(Directory.GetFiles(root, fileName, SearchOption.AllDirectories));
            }

            string best = hits
                .OrderBy(p => p.Count(c => c == Path.DirectorySeparatorChar || c == '/'))
                .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (best != null)
            {
                string directory = Path.GetDirectoryName(best);
                if (!ExtraProbeDirectories.Contains(directory, StringComparer.OrdinalIgnoreCase))
                    ExtraProbeDirectories.Add(directory);
            }

            return best;
        }

        private static string FindMono(string dataDirectory)
        {
            string[] candidates =
            {
                Path.Combine(dataDirectory, "MonoBleedingEdge", "bin", "mono.exe"),
                Path.Combine(dataDirectory, "MonoBleedingEdge", "bin", "mono"),
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static List<string> Dedupe(IEnumerable<string> paths)
        {
            // 같은 단순 이름(mscorlib 등)이 두 경로에서 잡히면 csc 가 CS1703 으로 죽는다. 먼저 나온 것을 남긴다.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (string path in paths)
            {
                if (seen.Add(Path.GetFileNameWithoutExtension(path)))
                    result.Add(path);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static string ReadProjectEditorVersion(string projectRoot)
        {
            string file = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
            if (!File.Exists(file))
                throw new FileNotFoundException("ProjectVersion.txt 가 없어 Unity 버전을 못 정했다: " + file, file);

            foreach (string line in File.ReadAllLines(file))
            {
                if (line.StartsWith("m_EditorVersion:", StringComparison.Ordinal))
                    return line.Substring("m_EditorVersion:".Length).Trim();
            }

            throw new InvalidOperationException("ProjectVersion.txt 에 m_EditorVersion 이 없다: " + file);
        }

        private static string FindEditorRoot(string repoRoot, HeadlessConfig config, string version)
        {
            var candidates = new List<string>();

            string fromEnv = Environment.GetEnvironmentVariable("UNITY_EDITOR_ROOT");
            if (!string.IsNullOrEmpty(fromEnv))
                candidates.Add(fromEnv);

            foreach (string hub in config.UnityHubRoots)
            {
                string expanded = Environment.ExpandEnvironmentVariables(hub);
                string root = Path.IsPathRooted(expanded) ? expanded : Path.Combine(repoRoot, expanded);
                candidates.Add(Path.Combine(root, version));
            }

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(Path.Combine(candidate, "Editor", "Data")))
                    return Path.GetFullPath(candidate);
            }

            throw new DirectoryNotFoundException(
                "Unity " + version + " 설치 경로를 못 찾았다. 찾아본 곳:" + Environment.NewLine +
                string.Join(Environment.NewLine, candidates.Select(c => "  " + c)) + Environment.NewLine +
                "UNITY_EDITOR_ROOT 환경변수로 직접 지정하거나 headless.config.json 의 unityHubRoots 를 고칠 것.");
        }

        /// <summary>
        /// nunit.framework.dll 은 PackageCache 폴더 이름에 해시가 붙어 버전마다 바뀐다.
        /// 그래서 경로를 박아 두지 않고 매번 찾는다.
        /// </summary>
        private static string FindNUnit(string projectRoot)
        {
            var roots = new List<string>
            {
                Path.Combine(projectRoot, "Library", "PackageCache"),
                Path.Combine(projectRoot, "Packages"),
            };

            // Library/ 가 통째로 지워진 뒤에도 도구가 살아 있도록 UPM 전역 캐시까지 뒤진다.
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                string globalCache = Path.Combine(localAppData, "Unity", "cache", "packages");
                if (Directory.Exists(globalCache))
                    roots.AddRange(Directory.GetDirectories(globalCache));
            }

            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (string dir in Directory.GetDirectories(root, "com.unity.ext.nunit*"))
                {
                    string[] hits = Directory.GetFiles(dir, "nunit.framework.dll", SearchOption.AllDirectories);
                    if (hits.Length == 0)
                        continue;

                    // net40/unity-custom 을 우선한다 — Unity Test Runner 가 실제로 로드하는 바로 그 빌드다.
                    string preferred = hits.FirstOrDefault(h => h.Replace('\\', '/').Contains("/unity-custom/"));
                    return preferred ?? hits[0];
                }
            }

            return null;
        }
    }
}
