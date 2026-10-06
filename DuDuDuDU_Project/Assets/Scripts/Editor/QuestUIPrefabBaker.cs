using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.Mission;

namespace OJ.EditorTools
{
    /// <summary>
    /// 퀘스트 창 프리팹과 로비 입구를 굽는다.
    ///
    /// <b>메뉴로만 돈다.</b> 씬을 건드리는 도구가 <c>[InitializeOnLoad]</c> 로 돌면
    /// 에디터를 켤 때마다 LobbyScene 을 덮어써서 손본 작업이 조용히 사라진다
    /// (MIGRATION_BASELINE 1.1 에서 그 경로를 전부 걷어냈다).
    ///
    /// <b>두 번 눌러도 안전하다.</b> 프리팹은 덮어쓰고(굽기는 멱등이다), 씬의 입구는
    /// 이미 있으면 건드리지 않는다 — 손으로 옮겨 둔 자리가 남는다.
    ///
    /// <b>메뉴를 연속으로 누르지 말 것.</b> 앞 메뉴의 <c>AssetDatabase.Refresh</c> 가
    /// 재컴파일을 시작하면 그 사이의 클릭은 조용히 무시된다. 우하단 스피너가 멈춘 뒤에 다음 것.
    /// </summary>
    public static class QuestUIPrefabBaker
    {
        private const string DialogFolder = "Assets/Prefab/UI";
        private const string DialogPath = DialogFolder + "/UIQuestDialog.prefab";

        private const string LobbyFolder = "Assets/Prefab/Lobby";
        private const string LobbyButtonPath = LobbyFolder + "/UIQuestLobbyButton.prefab";

        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

        /// <summary>
        /// 폰트는 BM HANNA 하나다. 시안이 다른 폰트로 그려져 있어도 이 게임의 글자는 전부 이것이다.
        /// </summary>
        private const string FontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        /// <summary>
        /// 로비 우측 상단. 앵커가 우상단이라 둘 다 음수다. 1080x1920 기준.
        ///
        /// <b>y 를 -300 으로 내린 이유.</b> -150 은 재화 표시줄(고기·골드·젬) 높이와 겹쳐
        /// 젬 보유량을 가렸다. -300 은 그 줄 아래이자 스테이지 배너(x 는 905 에서 끝난다)
        /// 오른쪽의 빈 칸이라 무엇도 가리지 않는다.
        ///
        /// 마음에 안 들면 씬에서 옮기면 된다 — 이 도구는 이미 있는 것을 옮기지 않는다.
        /// </summary>
        private static readonly Vector2 LobbyAnchoredPosition = new Vector2(-105f, -300f);

        [MenuItem("OJ/개발/미션/퀘스트 창 굽기")]
        private static void BakeDialog()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            System.IO.Directory.CreateDirectory(DialogFolder);

            var temp = new GameObject("__QuestBakeRoot");
            try
            {
                UIQuestDialog dialog = UIQuestDialog.Create(temp.transform, font);
                GameObject root = dialog.gameObject;
                root.name = "UIQuestDialog";
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, DialogPath, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[퀘스트] 창 프리팹 저장에 실패했다: " + DialogPath);
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[퀘스트] 창 프리팹을 구웠다: " + DialogPath + System.Environment.NewLine +
                          "  다음으로 'OJ/개발/다이얼로그 카탈로그/훑어서 갱신' 을 돌려야 창이 열린다. " +
                          "등재되지 않으면 UIService 가 프리팹을 못 찾아 아무것도 안 뜬다.");
            }
            finally
            {
                if (temp != null)
                    Object.DestroyImmediate(temp);
            }
        }

        [MenuItem("OJ/개발/미션/로비 입구 설치")]
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

            var temp = new GameObject("__QuestEntryBakeRoot");
            try
            {
                UIQuestLobbyButton entry = UIQuestLobbyButton.Create(temp.transform, font);
                GameObject root = entry.gameObject;
                root.name = "UIQuestLobbyButton";
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, LobbyButtonPath, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[퀘스트] 입구 프리팹 저장에 실패했다: " + LobbyButtonPath);
                    return null;
                }

                Debug.Log("[퀘스트] 입구 프리팹을 구웠다: " + LobbyButtonPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(LobbyButtonPath);
            }
            finally
            {
                if (temp != null)
                    Object.DestroyImmediate(temp);
            }
        }

        private static void InstallIntoLobby(GameObject prefab)
        {
            Scene lobbyScene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasAlreadyLoaded = lobbyScene.IsValid() && lobbyScene.isLoaded;
            if (!wasAlreadyLoaded)
                lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            UIQuestLobbyButton existing = FindInScene<UIQuestLobbyButton>(lobbyScene);
            if (existing != null)
            {
                Debug.Log("[퀘스트] 로비에 입구가 이미 있다. 그대로 둔다 — 자리를 바꾸려면 씬에서 옮길 것.");
            }
            else
            {
                Canvas canvas = FindCanvas(lobbyScene);
                if (canvas == null)
                {
                    Debug.LogError("[퀘스트] LobbyScene 에서 Canvas 를 못 찾아 입구를 심지 못했다.");
                }
                else if (PrefabUtility.InstantiatePrefab(prefab, canvas.transform) is GameObject instance)
                {
                    instance.name = "UIQuestLobbyButton";

                    var rect = instance.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(1f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = LobbyAnchoredPosition;
                    rect.SetAsLastSibling();

                    EditorSceneManager.MarkSceneDirty(lobbyScene);
                    Debug.Log("[퀘스트] LobbyScene 에 입구를 심었다.");
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
        /// 폰트를 읽는다. <b>없으면 멈춘다.</b> 굽기가 null 폰트로 끝나면 글자가 하나도
        /// 안 보이는 프리팹이 조용히 저장되고, 그것은 눌러 보기 전에는 모른다.
        /// </summary>
        private static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
                return font;

            // 경로가 바뀌었을 수 있다. 한글이 되는 폰트를 찾아 보되, 찾은 것을 반드시 알린다 —
            // 조용히 다른 폰트로 구우면 이 화면만 서체가 다른 상태가 된다.
            TMP_FontAsset fallback = AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .FirstOrDefault(f => f != null && f.HasCharacter('가'));

            if (fallback == null)
            {
                Debug.LogError("[퀘스트] 한글 TMP 폰트를 못 찾았다. 굽기를 멈춘다. 기대 경로: " + FontPath);
                return null;
            }

            Debug.LogWarning("[퀘스트] " + FontPath + " 를 못 찾아 " + fallback.name +
                             " 으로 굽는다. 폰트는 BM HANNA 로 고정이므로 경로를 확인할 것.");
            return fallback;
        }
    }
}
