using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.Pinball;

namespace OJ.EditorTools
{
    /// <summary>
    /// 로비에 핀볼 입구 버튼을 심는다.
    ///
    /// <b>메뉴로만 돈다.</b> 씬을 건드리는 도구는 사람이 누를 때만 돈다
    /// (<c>TowerLobbyEntryInstaller</c> 와 같은 판단 — 자동 훅은 MIGRATION_BASELINE 1.1 에서
    /// 전 프로젝트에서 제거됐다).
    ///
    /// <b>이미 있으면 아무 일도 하지 않는다.</b> 두 번 눌러도 버튼이 둘이 되지 않고,
    /// 손으로 옮겨 둔 위치도 그대로 남는다.
    /// </summary>
    public static class PinballLobbyEntryInstaller
    {
        private const string PrefabFolder = "Assets/Prefab/Lobby";
        private const string PrefabPath = PrefabFolder + "/UIPinballEntryButton.prefab";
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

        /// <summary>
        /// 로비 화면에서의 자리. 탑 입구(330, 120)의 왼쪽이다. 1080x1920 기준이고,
        /// 마음에 안 들면 씬에서 옮기면 된다 — 이 도구는 이미 있는 것을 옮기지 않는다.
        /// </summary>
        private static readonly Vector2 AnchoredPosition = new Vector2(-330f, 120f);

        [MenuItem("OJ/개발/핀볼/로비 입구 설치")]
        private static void Install()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                Debug.LogError("[핀볼] 한글 TMP 폰트를 못 찾았다. 입구를 만들지 않는다.");
                return;
            }

            GameObject prefab = BuildPrefab(font);
            if (prefab == null)
                return;

            InstallIntoLobby(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject BuildPrefab(TMP_FontAsset font)
        {
            System.IO.Directory.CreateDirectory(PrefabFolder);

            var temp = new GameObject("__PinballEntryBakeRoot");
            try
            {
                UIPinballEntryButton entry = UIPinballEntryButton.Create(temp.transform, font);
                GameObject root = entry.gameObject;
                root.name = "UIPinballEntryButton";
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[핀볼] 입구 프리팹 저장에 실패했다: " + PrefabPath);
                    return null;
                }

                Debug.Log("[핀볼] 입구 프리팹을 구웠다: " + PrefabPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
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

            UIPinballEntryButton existing = FindInScene<UIPinballEntryButton>(lobbyScene);
            if (existing != null)
            {
                Debug.Log("[핀볼] 로비에 입구가 이미 있다. 그대로 둔다 — " +
                          "자리를 바꾸려면 씬에서 옮길 것.");
            }
            else
            {
                Canvas canvas = FindCanvas(lobbyScene);
                if (canvas == null)
                {
                    Debug.LogError("[핀볼] LobbyScene 에서 Canvas 를 못 찾아 입구를 심지 못했다.");
                }
                else if (PrefabUtility.InstantiatePrefab(prefab, canvas.transform) is GameObject instance)
                {
                    instance.name = "UIPinballEntryButton";

                    var rect = instance.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.5f, 0f);
                    rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = AnchoredPosition;
                    rect.SetAsLastSibling();

                    EditorSceneManager.MarkSceneDirty(lobbyScene);
                    Debug.Log("[핀볼] LobbyScene 에 입구를 심었다.");
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

        private static TMP_FontAsset FindKoreanFont()
        {
            const int Sample = '가';

            return AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .FirstOrDefault(f => f != null && f.HasCharacter(Sample));
        }
    }
}
