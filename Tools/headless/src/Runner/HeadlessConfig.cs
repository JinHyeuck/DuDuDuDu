using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OJ.Headless
{
    /// <summary>
    /// headless.config.json 을 읽어 담는 그릇.
    ///
    /// 도구를 재사용 가능하게 만드는 지점이 여기다. 리팩토링이 진행되면서 어셈블리가
    /// 늘어나면 이 JSON 에 항목을 추가하는 것만으로 러너가 따라온다 — C# 은 건드릴 필요가 없다.
    /// 모든 경로는 <b>리포 루트 기준 상대 경로</b>다.
    /// </summary>
    internal sealed class HeadlessConfig
    {
        public string ProjectPath { get; private set; }
        public string UnityVersion { get; private set; }
        public List<string> UnityHubRoots { get; private set; }
        public string LangVersion { get; private set; }
        public List<string> Defines { get; private set; }
        public Dictionary<string, string> Environment { get; private set; }
        public List<BuildTarget> Assemblies { get; private set; }
        public string ConfigPath { get; private set; }

        /// <summary>어셈블리 하나를 어떤 소스에서 어떤 참조로 빌드할지.</summary>
        internal sealed class BuildTarget
        {
            /// <summary>산출 DLL 이름(확장자 제외). Unity 의 asmdef 이름과 맞추면 헷갈리지 않는다.</summary>
            public string Name;

            /// <summary>소스 루트 디렉터리들. 각각 *.cs 를 재귀로 긁는다.</summary>
            public List<string> Sources = new List<string>();

            /// <summary>
            /// 추가 참조. UnityEngine 모듈과 BCL 은 항상 자동으로 들어가므로 여기 적을 필요가 없다.
            /// 다음 셋 중 하나로 해석된다.
            ///
            /// <list type="bullet">
            /// <item><c>"nunit"</c> — PackageCache 의 nunit.framework.dll</item>
            /// <item><c>"unityEditor"</c> — UnityEditor.dll + UnityEditor.*Module.dll.
            ///   런타임 코드라도 <c>#if UNITY_EDITOR</c> 블록이 있으면 필요하다</item>
            /// <item><c>"scriptAssemblies"</c> — 프로젝트 Library/ScriptAssemblies 의 DLL 전부.
            ///   Unity 밖에서 만들 수 없는 패키지 어셈블리(UniTask, TextMeshPro, uGUI …) 전용이다.
            ///   이 설정이 소스에서 직접 빌드하는 어셈블리와 <see cref="ExcludeReferences"/> 는 빠진다</item>
            /// <item>그 밖의 이름 — 이 설정에서 먼저 빌드된 어셈블리, 또는 실제 DLL 경로</item>
            /// </list>
            /// </summary>
            public List<string> References = new List<string>();

            /// <summary>
            /// <c>"scriptAssemblies"</c> 가 끌어오는 DLL 중 이름으로 빼 버릴 것들.
            ///
            /// 소스에서 직접 빌드하는 어셈블리는 이름만 보고 자동으로 빠지지만, 그 짝(예:
            /// Assembly-CSharp 의 에디터 절반인 Assembly-CSharp-Editor)까지는 자동으로 알 수 없다.
            /// 낡은 DLL 을 참조에 남겨 두면 확장 메서드 탐색이 그 안을 들여다보다 엉뚱한 CS0012 를
            /// 내므로 여기서 명시적으로 뺀다.
            /// </summary>
            public List<string> ExcludeReferences = new List<string>();

            /// <summary>
            /// true 면 <c>-unsafe+</c>. Unity 는 asmdef 의 allowUnsafeCode 로 켜는 값이고,
            /// Assembly-CSharp 은 Player Settings 의 "Allow 'unsafe' Code" 를 따른다.
            /// 끄고 unsafe 블록이 있는 파일을 만나면 CS0227 로 죽는다.
            /// </summary>
            public bool Unsafe;

            /// <summary>true 면 빌드 후 NUnit 으로 실행한다.</summary>
            public bool Test;

            /// <summary>
            /// 이 어셈블리에서 최소한 몇 개가 수집돼야 하는가. 0 이면 검사하지 않는다.
            ///
            /// 0건만 막아서는 부족하다는 것을 실측으로 확인했다 — 테스트 파일 하나가 통째로
            /// 사라져도(예: 리팩토링 중 다른 어셈블리로 옮기다 누락) 남은 테스트는 전부 통과라
            /// 러너가 초록불을 낸다. 그 조용한 소실을 잡는 유일한 방법이 "기대 개수"를 커밋해 두고
            /// 대조하는 것이다. 테스트를 의도적으로 줄였다면 이 숫자도 같은 커밋에서 낮춰라 —
            /// 그러면 리뷰에 드러난다.
            /// </summary>
            public int MinTests;

            /// <summary>공통 defines 에 이 어셈블리에만 더할 심볼(예: UNITY_EDITOR_ONLY_COMPILATION).</summary>
            public List<string> Defines = new List<string>();

            /// <summary>도구 내부용. true 면 라이브러리가 아니라 실행 파일(.exe)로 컴파일한다.</summary>
            public bool Executable;

            /// <summary>도구 내부용. true 면 프로젝트 defines 를 적용하지 않는다(러너 자체 코드용).</summary>
            public bool SkipProjectDefines;

            /// <summary>
            /// 제외할 소스 패턴. 파일 이름(예: <c>"*.Editor.cs"</c>) 또는 소스 루트 기준 상대 경로
            /// (예: <c>"*/Editor/*"</c>, <c>"Core/*"</c>)에 대해 매칭한다. 구분자는 <c>/</c> 로 정규화되고
            /// <c>*</c> 는 <c>/</c> 도 넘어간다 — 그래서 <c>"Core/*"</c> 가 하위 폴더까지 다 걷어낸다.
            /// 다만 <c>"*/Editor/*"</c> 는 앞에 폴더가 하나는 있어야 하므로 최상위
            /// <c>Editor/</c> 까지 빼려면 <c>"Editor/*"</c> 를 따로 적어야 한다.
            /// </summary>
            public List<string> Exclude = new List<string>();

            /// <summary>
            /// 비어 있지 않으면 <b>여기 하나라도 맞는 파일만</b> 컴파일한다.
            /// <see cref="Exclude"/> 보다 먼저 걸러지고, 매칭 규칙은 같다.
            ///
            /// 용례는 <c>Assembly-CSharp-Editor</c> 다 — 소스가 <c>Assets/Scripts</c> 아래 여기저기
            /// 흩어진 <c>Editor/</c> 폴더들이라 exclude 로는 "나머지 전부"를 적어야 해서 표현이 안 된다.
            /// 폴더를 하나씩 나열하는 대신 패턴으로 두면 새 <c>Editor/</c> 폴더가 자동으로 들어온다.
            /// </summary>
            public List<string> Include = new List<string>();
        }

        public static HeadlessConfig Load(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("설정 파일이 없다: " + path, path);

            var options = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            using (var doc = JsonDocument.Parse(File.ReadAllText(path), options))
            {
                JsonElement root = doc.RootElement;
                var config = new HeadlessConfig
                {
                    ConfigPath = Path.GetFullPath(path),
                    ProjectPath = GetString(root, "projectPath", "."),
                    UnityVersion = GetString(root, "unityVersion", "auto"),
                    LangVersion = GetString(root, "langVersion", "9.0"),
                    UnityHubRoots = GetStringList(root, "unityHubRoots"),
                    Defines = GetStringList(root, "defines"),
                    Environment = new Dictionary<string, string>(StringComparer.Ordinal),
                    Assemblies = new List<BuildTarget>(),
                };

                JsonElement env;
                if (root.TryGetProperty("environment", out env) && env.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty pair in env.EnumerateObject())
                        config.Environment[pair.Name] = pair.Value.GetString();
                }

                JsonElement assemblies;
                if (root.TryGetProperty("assemblies", out assemblies) && assemblies.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in assemblies.EnumerateArray())
                    {
                        var target = new BuildTarget
                        {
                            Name = GetString(item, "name", null),
                            Sources = GetStringList(item, "sources"),
                            References = GetStringList(item, "references"),
                            ExcludeReferences = GetStringList(item, "excludeReferences"),
                            Exclude = GetStringList(item, "exclude"),
                            Include = GetStringList(item, "include"),
                            Defines = GetStringList(item, "defines"),
                            Test = GetBool(item, "test", false),
                            Unsafe = GetBool(item, "unsafe", false),
                            MinTests = GetInt(item, "minTests", 0),
                        };

                        if (string.IsNullOrEmpty(target.Name))
                            throw new InvalidOperationException("assemblies 항목에 name 이 없다: " + path);

                        config.Assemblies.Add(target);
                    }
                }

                if (config.Assemblies.Count == 0)
                    throw new InvalidOperationException("빌드할 어셈블리가 하나도 없다: " + path);

                return config;
            }
        }

        private static string GetString(JsonElement element, string name, string fallback)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();

            return fallback;
        }

        private static int GetInt(JsonElement element, string name, int fallback)
        {
            JsonElement value;
            int parsed;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt32(out parsed))
                return parsed;

            return fallback;
        }

        private static bool GetBool(JsonElement element, string name, bool fallback)
        {
            JsonElement value;
            if (element.TryGetProperty(name, out value) &&
                (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
                return value.GetBoolean();

            return fallback;
        }

        private static List<string> GetStringList(JsonElement element, string name)
        {
            var result = new List<string>();
            JsonElement value;
            if (element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        result.Add(item.GetString());
                }
            }

            return result;
        }
    }
}
