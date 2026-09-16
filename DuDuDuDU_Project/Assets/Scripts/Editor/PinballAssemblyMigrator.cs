using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OJ.EditorTools
{
    /// <summary>
    /// <c>Assets/Pinball</c> 의 핀볼 툴킷을 게임 코드가 참조할 수 있는 상태로 정리한다.
    ///
    /// <b>왜 필요한가.</b> 툴킷은 asmdef 없이 들어와서 <c>Assembly-CSharp</c> 로 컴파일된다.
    /// <c>OJ.Game</c> 은 asmdef 을 가진 어셈블리라 <c>Assembly-CSharp</c> 를 참조할 수 없고,
    /// 그래서 <c>OJ.Pinball.PinballManager</c> 가 <c>Pinball.PinballBoard</c> 를 영영 못 본다.
    /// 참조 방향을 뒤집으려면 툴킷 쪽에 asmdef 이 있어야 한다.
    ///
    /// <b>덤으로 빌드도 고친다.</b> 에디터 코드 7개가 <c>Editor/</c> 폴더 밖에서
    /// <c>#if UNITY_EDITOR</c> 가드 없이 <c>using UnityEditor;</c> 를 하고 있어
    /// 지금 상태로는 플레이어 빌드가 깨진다. 툴킷 README 자신이 "<c>Editor/</c> 폴더명이
    /// 그대로여야 빌드에서 제외된다"고 적었는데 폴더가 평탄화된 채로 들어왔다.
    ///
    /// <b>메뉴로만 돈다.</b> 에셋을 옮기는 도구는 사람이 누를 때만 돈다
    /// (<c>TowerLobbyEntryInstaller</c> 와 같은 판단).
    ///
    /// <b>두 번 눌러도 안전하다.</b> 이미 옮겨졌거나 이미 참조가 있으면 건너뛴다.
    /// </summary>
    public static class PinballAssemblyMigrator
    {
        private const string PinballRoot = "Assets/Pinball";
        private const string PinballEditorFolder = PinballRoot + "/Editor";
        private const string TestsFolder = "Assets/Tests/EditMode/Game";

        private const string RuntimeAsmdefPath = PinballRoot + "/Pinball.Runtime.asmdef";
        private const string EditorAsmdefPath = PinballEditorFolder + "/Pinball.Editor.asmdef";

        private const string GameAsmdefPath = "Assets/Scripts/OJ.Game.asmdef";
        private const string GameEditorAsmdefPath = "Assets/Scripts/Editor/OJ.Game.Editor.asmdef";
        private const string GameTestsAsmdefPath = TestsFolder + "/OJ.Game.Tests.asmdef";

        /// <summary>
        /// <c>Editor/</c> 로 내려보낼 파일들. 전부 <c>using UnityEditor;</c> 를 가드 없이 쓴다.
        /// </summary>
        private static readonly string[] EditorScripts =
        {
            "PinballSimWindow.cs",
            "PinballSimWindow.Canvas.cs",
            "PinballSimWindow.Edit.cs",
            "PinballSimWindow.Prefab.cs",
            "SeedTableBaker.cs",
            "PinballBoardFactory.cs",
            "PinballPrefabBaker.cs",
        };

        /// <summary>
        /// 판이 베이크 이후 바뀌었는지를 CI 에서 잡아 주는 테스트다. 기존 EditMode 테스트
        /// 어셈블리로 옮긴다 — <c>Pinball.Editor</c> 에 nunit 을 끌어들이는 것보다,
        /// 이미 CI 가 도는 어셈블리에 얹는 편이 빠뜨릴 구석이 없다.
        /// </summary>
        private const string TestScript = "PinballSeedTableTests.cs";

        /// <summary>
        /// 보드를 편집하기 전에 구워진 프리팹. 핀이 46개라 현재 판(44개, 37/38번이 특수핀)과
        /// 어긋난다. <c>PinballBoardView.pegs</c> 는 <c>board.pegs</c> 와 인덱스 병렬이고
        /// <c>HitEvent.pegIndex</c> 가 그것을 직접 가리키므로, 이쪽을 배선하면 엉뚱한 핀이 튄다.
        /// </summary>
        private const string StalePrefabPath = "Assets/Prefab/Pinball/PinballBoard_View.prefab";

        [MenuItem("OJ/개발/핀볼/어셈블리 정리")]
        private static void Migrate()
        {
            var log = new List<string>();

            // <b>StartAssetEditing 으로 묶지 않는다.</b> 한 번 그렇게 했다가 이동이 통째로
            // 실패했다 — 배치 모드 중에는 방금 CreateFolder 한 폴더가 AssetDatabase 에
            // 아직 등록되지 않아서, MoveAsset 이 대상 경로를 찾지 못한다. 그때 이미 있던
            // 폴더(Assets/Tests/EditMode/Game)로 가는 파일만 옮겨져서, "일부만 성공"이라는
            // 가장 헷갈리는 상태가 남았다. 파일 여덟 개에 배치 모드는 필요하지도 않다.
            EnsureFolder(PinballRoot, "Editor", log);
            MoveScripts(log);
            WriteAsmdefs(log);
            AddReference(GameAsmdefPath, "Pinball.Runtime", log);
            AddReference(GameEditorAsmdefPath, "Pinball.Runtime", log);
            AddReference(GameTestsAsmdefPath, "Pinball.Runtime", log);
            AddReference(GameTestsAsmdefPath, "Pinball.Editor", log);
            TrashStalePrefab(log);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[핀볼] 어셈블리 정리 결과\n" + string.Join("\n", log) + "\n" + Verify());
        }

        /// <summary>
        /// 끝난 뒤 실제로 성한 상태인지 본다.
        ///
        /// <b>왜 따로 검사하나.</b> 이동이 실패해도 에디터 안에서는 컴파일이 통과한다 —
        /// asmdef 어셈블리도 에디터용 컴파일에서는 <c>UnityEditor</c> 를 참조받기 때문이다.
        /// 그래서 <b>깨진 것이 플레이어 빌드에서만 드러나고</b>, 그때는 왜 깨졌는지 알기 어렵다.
        /// 여기서 지금 말한다.
        /// </summary>
        private static string Verify()
        {
            var leftover = new List<string>();

            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { PinballRoot });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.StartsWith(PinballEditorFolder, System.StringComparison.Ordinal))
                    continue;

                string text = File.ReadAllText(path);
                if (text.Contains("using UnityEditor") && !text.Contains("#if UNITY_EDITOR"))
                    leftover.Add("    " + path);
            }

            if (leftover.Count == 0)
                return "  검사 통과 — 런타임 어셈블리에 에디터 코드가 없다.";

            return "  ⚠ 아직 " + leftover.Count + "개가 런타임 어셈블리에 남아 있다. " +
                   "에디터는 통과하지만 <b>플레이어 빌드가 깨진다</b>:\n" +
                   string.Join("\n", leftover);
        }

        private static void EnsureFolder(string parent, string name, List<string> log)
        {
            string path = parent + "/" + name;
            if (AssetDatabase.IsValidFolder(path))
            {
                log.Add("  건너뜀 (이미 있음): " + path);
                return;
            }

            AssetDatabase.CreateFolder(parent, name);
            log.Add("  폴더 생성: " + path);
        }

        private static void MoveScripts(List<string> log)
        {
            for (int i = 0; i < EditorScripts.Length; i++)
                Move(PinballRoot + "/" + EditorScripts[i], PinballEditorFolder + "/" + EditorScripts[i], log);

            Move(PinballRoot + "/" + TestScript, TestsFolder + "/" + TestScript, log);
        }

        /// <summary>
        /// <c>AssetDatabase.MoveAsset</c> 만 쓴다. 파일시스템으로 옮기면 <c>.meta</c> 가 따라가지 않아
        /// GUID 가 재발급되고, 그러면 프리팹·씬의 스크립트 참조가 전멸한다(AGENTS.md 절대 규칙 1·2).
        /// </summary>
        private static void Move(string from, string to, List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(from) == null)
            {
                log.Add("  건너뜀 (원본 없음): " + from);
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<Object>(to) != null)
            {
                log.Add("  건너뜀 (대상에 이미 있음): " + to);
                return;
            }

            string error = AssetDatabase.MoveAsset(from, to);
            if (string.IsNullOrEmpty(error))
                log.Add("  이동: " + from + "  ->  " + to);
            else
                log.Add("  실패: " + from + " — " + error);
        }

        private static void WriteAsmdefs(List<string> log)
        {
            WriteAsmdef(RuntimeAsmdefPath,
                "{\n" +
                "    \"name\": \"Pinball.Runtime\",\n" +
                "    \"rootNamespace\": \"Pinball\",\n" +
                "    \"references\": [],\n" +
                "    \"includePlatforms\": [],\n" +
                "    \"excludePlatforms\": [],\n" +
                "    \"allowUnsafeCode\": false,\n" +
                "    \"overrideReferences\": false,\n" +
                "    \"precompiledReferences\": [],\n" +
                "    \"autoReferenced\": true,\n" +
                "    \"defineConstraints\": [],\n" +
                "    \"versionDefines\": [],\n" +
                "    \"noEngineReferences\": false\n" +
                "}\n", log);

            WriteAsmdef(EditorAsmdefPath,
                "{\n" +
                "    \"name\": \"Pinball.Editor\",\n" +
                "    \"rootNamespace\": \"Pinball.EditorTools\",\n" +
                "    \"references\": [\n" +
                "        \"Pinball.Runtime\"\n" +
                "    ],\n" +
                "    \"includePlatforms\": [\n" +
                "        \"Editor\"\n" +
                "    ],\n" +
                "    \"excludePlatforms\": [],\n" +
                "    \"allowUnsafeCode\": false,\n" +
                "    \"overrideReferences\": false,\n" +
                "    \"precompiledReferences\": [],\n" +
                "    \"autoReferenced\": true,\n" +
                "    \"defineConstraints\": [],\n" +
                "    \"versionDefines\": [],\n" +
                "    \"noEngineReferences\": false\n" +
                "}\n", log);
        }

        private static void WriteAsmdef(string path, string json, List<string> log)
        {
            if (File.Exists(path))
            {
                log.Add("  건너뜀 (이미 있음): " + path);
                return;
            }

            File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            log.Add("  생성: " + path);
        }

        /// <summary>
        /// asmdef 의 <c>references</c> 배열에 이름을 하나 끼워 넣는다.
        ///
        /// JSON 파서를 쓰지 않고 문자열로 만지는 이유는 <b>출력 형식을 보존하기 위해서다</b> —
        /// 다시 직렬화하면 들여쓰기와 키 순서가 바뀌어 diff 가 파일 전체로 번진다.
        /// asmdef 의 <c>references</c> 는 항상 문자열 배열 하나뿐이라 이 정도로 충분하다.
        /// </summary>
        private static void AddReference(string asmdefPath, string reference, List<string> log)
        {
            if (!File.Exists(asmdefPath))
            {
                log.Add("  실패 (asmdef 없음): " + asmdefPath);
                return;
            }

            string text = File.ReadAllText(asmdefPath);
            if (text.Contains("\"" + reference + "\""))
            {
                log.Add("  건너뜀 (이미 참조함): " + asmdefPath + " -> " + reference);
                return;
            }

            const string emptyToken = "\"references\": []";
            const string openToken = "\"references\": [";

            string next;
            if (text.Contains(emptyToken))
            {
                next = text.Replace(emptyToken,
                    "\"references\": [\n        \"" + reference + "\"\n    ]");
            }
            else
            {
                int at = text.IndexOf(openToken, System.StringComparison.Ordinal);
                if (at < 0)
                {
                    log.Add("  실패 (references 배열을 못 찾음): " + asmdefPath);
                    return;
                }

                int insert = at + openToken.Length;
                next = text.Substring(0, insert)
                     + "\n        \"" + reference + "\","
                     + text.Substring(insert);
            }

            File.WriteAllText(asmdefPath, next, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(asmdefPath);
            log.Add("  참조 추가: " + asmdefPath + " -> " + reference);
        }

        /// <summary>
        /// 삭제가 아니라 휴지통으로 보낸다. 판단이 틀렸을 때 되돌릴 수 있어야 한다.
        /// </summary>
        private static void TrashStalePrefab(List<string> log)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(StalePrefabPath) == null)
            {
                log.Add("  건너뜀 (이미 없음): " + StalePrefabPath);
                return;
            }

            if (AssetDatabase.MoveAssetToTrash(StalePrefabPath))
                log.Add("  휴지통으로: " + StalePrefabPath + " (핀 46개 — 현재 판과 어긋남)");
            else
                log.Add("  실패: " + StalePrefabPath + " 를 버리지 못했다");
        }
    }
}
