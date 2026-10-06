using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.SeasonPass;

namespace OJ.EditorTools
{
    /// <summary>
    /// 시즌 패스의 에셋·프리팹·로비 입구를 만든다.
    /// <c>MissionDatabaseAssetBuilder</c>·<c>QuestUIPrefabBaker</c> 와 같은 자리다.
    ///
    /// <b>메뉴로만 돈다.</b> 씬을 건드리는 도구가 <c>[InitializeOnLoad]</c> 로 돌면
    /// 에디터를 켤 때마다 LobbyScene 을 덮어써 손본 작업이 조용히 사라진다.
    ///
    /// <b>메뉴를 연속으로 누르지 말 것.</b> 앞 메뉴의 <c>AssetDatabase.Refresh</c> 가
    /// 재컴파일을 시작하면 그 사이의 클릭은 조용히 무시된다. 우하단 스피너가 멈춘 뒤 다음 것.
    /// </summary>
    public static class SeasonPassAssetBuilder
    {
        private const string AssetPath = "Assets/ScriptableObject/SeasonPassDatabase.asset";

        private const string DialogFolder = "Assets/Prefab/UI";
        private const string DialogPath = DialogFolder + "/UISeasonPassDialog.prefab";
        private const string PremiumDialogPath = DialogFolder + "/UISeasonPassPremiumDialog.prefab";

        private const string LobbyFolder = "Assets/Prefab/Lobby";
        private const string LobbyButtonPath = LobbyFolder + "/UISeasonPassLobbyButton.prefab";

        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

        /// <summary>폰트는 BM HANNA 하나다. 시안이 다른 폰트여도 이 게임의 글자는 전부 이것이다.</summary>
        private const string FontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        /// <summary>로비 좌하단. 소탕/탑 버튼 줄 위의 빈 자리다. 마음에 안 들면 씬에서 옮기면 된다.</summary>
        private static readonly Vector2 LobbyAnchoredPosition = new Vector2(0f, 560f);

        // ── 데이터베이스 ───────────────────────────────────────────────

        [MenuItem("OJ/개발/시즌패스/데이터베이스 에셋 만들기")]
        private static void CreateOrValidate()
        {
            if (AssetDatabase.LoadAssetAtPath<SeasonPassDatabase>(AssetPath) == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));

                var database = ScriptableObject.CreateInstance<SeasonPassDatabase>();
                database.PopulateDefaults();
                AssetDatabase.CreateAsset(database, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[시즌패스] 에셋을 새로 만들었다: " + AssetPath + Environment.NewLine +
                          "  StaticResource 빈 슬롯 채우기가 다음 컴파일에 자동으로 잇는다.");
            }
            else
            {
                Debug.Log("[시즌패스] 에셋이 이미 있다: " + AssetPath);
            }

            Validate();
        }

        [MenuItem("OJ/개발/시즌패스/데이터베이스 검사")]
        private static void Validate()
        {
            var database = AssetDatabase.LoadAssetAtPath<SeasonPassDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError("[시즌패스] 에셋이 없다: " + AssetPath + " — 지금은 코드 기본값으로 돌아가고 있다.");
                return;
            }

            var sb = new StringBuilder("[시즌패스] 검사: " + AssetPath).AppendLine();
            sb.AppendLine("  레벨당 포인트 " + database.PointsPerLevel + " (소탕 1회 = 고기 5)");
            sb.AppendLine("  시즌 " + database.Seasons.Count + "개");

            foreach (SeasonPassSeason season in database.Seasons)
            {
                if (season == null)
                    continue;

                sb.AppendLine("    " + season.id + " — " + season.displayName +
                              " / " + season.MaxLevel + "레벨");
            }

            List<string> problems = database.Validate();
            problems.AddRange(SeasonPassDatabase.CheckUpcoming(database.Seasons, DateTime.Now));

            if (problems.Count == 0)
            {
                sb.Append("  문제 없음.");
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine("  문제 " + problems.Count + "건:");
            foreach (string p in problems)
                sb.AppendLine("    !! " + p);

            Debug.LogError(sb.ToString());
        }

        // ── 프리팹 ─────────────────────────────────────────────────────

        /// <summary>패스 창과 프리미엄 구매 창을 함께 굽는다. 둘은 그림·색을 같이 쓴다.</summary>
        [MenuItem("OJ/개발/시즌패스/창 굽기")]
        private static void BakeDialog()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            System.IO.Directory.CreateDirectory(DialogFolder);

            bool ok = BakePrefab("UISeasonPassDialog", DialogPath,
                    parent => UISeasonPassDialog.Create(parent, font).gameObject)
                & BakePrefab("UISeasonPassPremiumDialog", PremiumDialogPath,
                    parent => UISeasonPassPremiumDialog.Create(parent, font).gameObject);

            if (!ok)
                return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[시즌패스] 창 프리팹을 구웠다: " + DialogPath + ", " + PremiumDialogPath + Environment.NewLine +
                      "  새 창이 생겼으면 'OJ/개발/다이얼로그 카탈로그/훑어서 갱신' 을 돌려야 열린다.");
        }

        private static bool BakePrefab(string name, string path, Func<Transform, GameObject> create)
        {
            var temp = new GameObject("__SeasonPassBakeRoot");
            try
            {
                GameObject root = create(temp.transform);
                root.name = name;
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                UnityEngine.Object.DestroyImmediate(root);

                if (!ok)
                    Debug.LogError("[시즌패스] 프리팹 저장에 실패했다: " + path);

                return ok;
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        [MenuItem("OJ/개발/시즌패스/로비 입구 설치")]
        private static void InstallLobbyEntry()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            GameObject prefab = BakeLobbyButton(font);
            if (prefab == null)
                return;

            InstallIntoLobby(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject BakeLobbyButton(TMP_FontAsset font)
        {
            System.IO.Directory.CreateDirectory(LobbyFolder);

            var temp = new GameObject("__SeasonPassEntryBakeRoot");
            try
            {
                UISeasonPassLobbyButton entry = UISeasonPassLobbyButton.Create(temp.transform, font);
                GameObject root = entry.gameObject;
                root.name = "UISeasonPassLobbyButton";
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, LobbyButtonPath, out bool ok);
                UnityEngine.Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[시즌패스] 입구 프리팹 저장에 실패했다: " + LobbyButtonPath);
                    return null;
                }

                Debug.Log("[시즌패스] 입구 프리팹을 구웠다: " + LobbyButtonPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(LobbyButtonPath);
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static void InstallIntoLobby(GameObject prefab)
        {
            Scene lobbyScene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasAlreadyLoaded = lobbyScene.IsValid() && lobbyScene.isLoaded;
            if (!wasAlreadyLoaded)
                lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            UISeasonPassLobbyButton existing = FindInScene<UISeasonPassLobbyButton>(lobbyScene);
            if (existing != null)
            {
                Debug.Log("[시즌패스] 로비에 입구가 이미 있다. 그대로 둔다 — 자리를 바꾸려면 씬에서 옮길 것.");
            }
            else
            {
                Canvas canvas = FindCanvas(lobbyScene);
                if (canvas == null)
                {
                    Debug.LogError("[시즌패스] LobbyScene 에서 Canvas 를 못 찾아 입구를 심지 못했다.");
                }
                else if (PrefabUtility.InstantiatePrefab(prefab, canvas.transform) is GameObject instance)
                {
                    instance.name = "UISeasonPassLobbyButton";

                    var rect = instance.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = LobbyAnchoredPosition;
                    rect.SetAsLastSibling();

                    EditorSceneManager.MarkSceneDirty(lobbyScene);
                    Debug.Log("[시즌패스] LobbyScene 에 입구를 심었다.");
                }
            }

            if (lobbyScene.isDirty)
                EditorSceneManager.SaveScene(lobbyScene);

            if (!wasAlreadyLoaded)
                EditorSceneManager.CloseScene(lobbyScene, true);
        }

        private static Canvas FindCanvas(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Canvas[] canvases = roots[i].GetComponentsInChildren<Canvas>(true);
                for (int j = 0; j < canvases.Length; j++)
                {
                    if (canvases[j].gameObject.name == "Canvas")
                        return canvases[j];
                }
            }

            return null;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// 폰트를 읽는다. <b>없으면 멈춘다.</b> null 폰트로 구우면 글자가 하나도 안 보이는
        /// 프리팹이 조용히 저장되고, 그것은 눌러 보기 전에는 모른다.
        /// </summary>
        private static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
                return font;

            TMP_FontAsset fallback = AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .FirstOrDefault(f => f != null && f.HasCharacter('가'));

            if (fallback == null)
            {
                Debug.LogError("[시즌패스] 한글 TMP 폰트를 못 찾았다. 굽기를 멈춘다. 기대 경로: " + FontPath);
                return null;
            }

            Debug.LogWarning("[시즌패스] " + FontPath + " 를 못 찾아 " + fallback.name +
                             " 으로 굽는다. 폰트는 BM HANNA 로 고정이므로 경로를 확인할 것.");
            return fallback;
        }
    }
}
