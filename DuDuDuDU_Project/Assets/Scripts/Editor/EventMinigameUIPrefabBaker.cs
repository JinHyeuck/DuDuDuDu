using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.EventMinigame;

namespace OJ.EditorTools
{
    /// <summary>
    /// 이벤트 미니게임 화면 3개와 로비 입구를 굽는다.
    ///
    /// <b>메뉴로만 돈다.</b> 씬을 건드리는 도구는 자동 훅으로 두지 않는다(MIGRATION_BASELINE 1.1).
    /// <b>두 번 눌러도 안전하다.</b> 프리팹은 덮어쓰고(굽기는 멱등), 로비의 입구는 이미 있으면 건드리지 않는다.
    /// <b>메뉴를 연속으로 누르지 말 것</b> — 앞 메뉴의 Refresh 가 재컴파일을 시작하면 그 사이 클릭은 무시된다.
    /// 화면을 굽고 나면 'OJ/개발/다이얼로그 카탈로그/훑어서 갱신' 을 돌려야 열린다.
    /// </summary>
    public static class EventMinigameUIPrefabBaker
    {
        private const string DialogFolder = "Assets/Prefab/UI";
        private const string LobbyFolder = "Assets/Prefab/Lobby";
        private const string LobbyButtonPath = LobbyFolder + "/UIEventMinigameLobbyButton.prefab";
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string FontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        /// <summary>퀘스트 입구(-105, -300) 바로 아래. 우상단 앵커라 둘 다 음수다. 마음에 안 들면 씬에서 옮긴다 — 이미 있는 것은 옮기지 않는다.</summary>
        private static readonly Vector2 LobbyAnchoredPosition = new Vector2(-105f, -470f);

        [MenuItem("OJ/개발/이벤트/화면 3종 굽기")]
        private static void BakePages()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            System.IO.Directory.CreateDirectory(DialogFolder);

            int ok = 0;
            ok += BakeOne("UIFishingPage", font, (t, f) => UIFishingPage.Create(t, f).gameObject) ? 1 : 0;
            ok += BakeOne("UITreasureHuntPage", font, (t, f) => UITreasureHuntPage.Create(t, f).gameObject) ? 1 : 0;
            ok += BakeOne("UIRaftingPage", font, (t, f) => UIRaftingPage.Create(t, f).gameObject) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 사후 검사: 일부만 성공했다면 그것을 말한다.
            if (ok == 3)
                Debug.Log("[이벤트] 화면 3종을 모두 구웠다. 다음으로 'OJ/개발/다이얼로그 카탈로그/훑어서 갱신' 을 돌릴 것(스피너가 멈춘 뒤).");
            else
                Debug.LogError("[이벤트] 화면 " + ok + "/3 만 구웠다. 위 오류를 보고 다시 누를 것(두 번 눌러도 안전하다).");
        }

        private static bool BakeOne(string name, TMP_FontAsset font, Func<Transform, TMP_FontAsset, GameObject> create)
        {
            string path = DialogFolder + "/" + name + ".prefab";
            var temp = new GameObject("__EventBakeRoot");
            try
            {
                GameObject root = create(temp.transform, font);
                root.name = name;
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                UnityEngine.Object.DestroyImmediate(root);

                if (!saved)
                {
                    Debug.LogError("[이벤트] 프리팹 저장에 실패했다: " + path);
                    return false;
                }

                Debug.Log("[이벤트] 구웠다: " + path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[이벤트] " + name + " 굽기가 실패했다: " + e);
                return false;
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        [MenuItem("OJ/개발/이벤트/로비 입구 설치")]
        private static void InstallLobbyEntry()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            System.IO.Directory.CreateDirectory(LobbyFolder);

            var temp = new GameObject("__EventEntryBakeRoot");
            GameObject prefab;
            try
            {
                UIEventMinigameLobbyButton entry = UIEventMinigameLobbyButton.Create(temp.transform, font);
                GameObject root = entry.gameObject;
                root.name = "UIEventMinigameLobbyButton";
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, LobbyButtonPath, out bool saved);
                UnityEngine.Object.DestroyImmediate(root);

                if (!saved)
                {
                    Debug.LogError("[이벤트] 입구 프리팹 저장에 실패했다: " + LobbyButtonPath);
                    return;
                }

                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyButtonPath);
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }

            InstallIntoLobby(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void InstallIntoLobby(GameObject prefab)
        {
            Scene lobbyScene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasAlreadyLoaded = lobbyScene.IsValid() && lobbyScene.isLoaded;
            if (!wasAlreadyLoaded)
                lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            UIEventMinigameLobbyButton existing = FindInScene(lobbyScene);
            if (existing != null)
            {
                Debug.Log("[이벤트] 로비에 입구가 이미 있다. 그대로 둔다 — 자리를 바꾸려면 씬에서 옮길 것.");
            }
            else
            {
                Canvas canvas = FindCanvas(lobbyScene);
                if (canvas == null)
                {
                    Debug.LogError("[이벤트] LobbyScene 에서 Canvas 를 못 찾아 입구를 심지 못했다.");
                }
                else if (PrefabUtility.InstantiatePrefab(prefab, canvas.transform) is GameObject instance)
                {
                    instance.name = "UIEventMinigameLobbyButton";

                    var rect = instance.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(1f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = LobbyAnchoredPosition;
                    rect.SetAsLastSibling();

                    EditorSceneManager.MarkSceneDirty(lobbyScene);
                    Debug.Log("[이벤트] LobbyScene 에 입구를 심었다.");
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

        private static UIEventMinigameLobbyButton FindInScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                UIEventMinigameLobbyButton found = roots[i].GetComponentInChildren<UIEventMinigameLobbyButton>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>폰트를 읽는다. <b>없으면 멈춘다</b> — null 폰트로 구우면 글자가 안 보이는 프리팹이 조용히 저장된다.</summary>
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
                Debug.LogError("[이벤트] 한글 TMP 폰트를 못 찾았다. 굽기를 멈춘다. 기대 경로: " + FontPath);
                return null;
            }

            Debug.LogWarning("[이벤트] " + FontPath + " 를 못 찾아 " + fallback.name + " 으로 굽는다. 폰트는 BM HANNA 로 고정이다.");
            return fallback;
        }
    }
}
