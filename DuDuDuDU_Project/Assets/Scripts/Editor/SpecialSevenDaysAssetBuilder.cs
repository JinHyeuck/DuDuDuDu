using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.Shop;

namespace OJ.EditorTools
{
    /// <summary>
    /// 특별한 7일 창·로비 입구를 굽고 로비에 심는다. <c>MembershipAssetBuilder</c> 와 같은 자리다.
    ///
    /// <b>메뉴로만 돈다.</b> 씬을 건드리는 도구가 <c>[InitializeOnLoad]</c> 로 돌면 에디터를 켤 때마다
    /// LobbyScene 을 덮어쓴다. <b>메뉴를 연속으로 누르지 말 것</b> — 앞 메뉴의 Refresh 가 재컴파일을
    /// 시작하면 그 사이의 클릭은 조용히 무시된다.
    /// </summary>
    public static class SpecialSevenDaysAssetBuilder
    {
        private const string DialogPath = "Assets/Prefab/UI/UISpecialSevenDaysDialog.prefab";
        private const string LobbyButtonPath = "Assets/Prefab/Lobby/UISpecialSevenDaysLobbyButton.prefab";
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

        private const string FontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        /// <summary>멤버십 입구에서 아래로 얼마나 띄우나. 입구 높이(190)와 같다 — 이름 줄 아래에 바로 붙는다.</summary>
        private const float BelowMembership = 200f;

        [MenuItem("OJ/개발/특별한 7일/창 굽기")]
        private static void BakeDialog()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            if (!BakePrefab(DialogPath, "UISpecialSevenDaysDialog", t => UISpecialSevenDaysDialog.Create(t, font).gameObject))
                return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[특별한 7일] 창 프리팹을 구웠다: " + DialogPath + Environment.NewLine +
                      "  새로 만들었으면 'OJ/개발/다이얼로그 카탈로그/훑어서 갱신' 을 돌려야 열린다.");
        }

        [MenuItem("OJ/개발/특별한 7일/로비 입구 설치")]
        private static void InstallLobbyEntry()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            if (!BakePrefab(LobbyButtonPath, "UISpecialSevenDaysLobbyButton", t => UISpecialSevenDaysLobbyButton.Create(t, font).gameObject))
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyButtonPath);

            Scene lobby = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasLoaded = lobby.IsValid() && lobby.isLoaded;
            if (!wasLoaded)
                lobby = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            InstallButton(lobby, prefab);

            if (lobby.isDirty)
                EditorSceneManager.SaveScene(lobby);
            if (!wasLoaded)
                EditorSceneManager.CloseScene(lobby, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 멤버십 입구 <b>바로 아래</b>에 심는다(사용자 지정). 멤버십 입구가 씬에서 옮겨졌어도 그 자리를 따라간다 —
        /// 좌표를 박지 않고 그 입구의 부모·앵커·위치를 읽는다.
        /// </summary>
        private static void InstallButton(Scene lobby, GameObject prefab)
        {
            if (FindInScene<UISpecialSevenDaysLobbyButton>(lobby) != null)
            {
                Debug.Log("[특별한 7일] 로비에 입구가 이미 있다. 그대로 둔다 — 자리를 바꾸려면 씬에서 옮길 것.");
                return;
            }

            UIMembershipLobbyButton membership = FindInScene<UIMembershipLobbyButton>(lobby);
            if (membership == null)
            {
                Debug.LogError("[특별한 7일] LobbyScene 에서 멤버십 입구를 못 찾았다. 'OJ/개발/멤버십/로비 입구 설치' 를 먼저 돌릴 것.");
                return;
            }

            var anchor = membership.GetComponent<RectTransform>();
            if (!(PrefabUtility.InstantiatePrefab(prefab, anchor.parent) is GameObject instance))
                return;

            instance.name = "UISpecialSevenDaysLobbyButton";
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = anchor.anchorMin;
            rect.anchorMax = anchor.anchorMax;
            rect.pivot = anchor.pivot;
            rect.anchoredPosition = anchor.anchoredPosition - new Vector2(0f, BelowMembership);
            rect.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
            EditorSceneManager.MarkSceneDirty(lobby);
            Debug.Log("[특별한 7일] 멤버십 입구 아래에 입구를 심었다: " + rect.anchoredPosition);
        }

        private static bool BakePrefab(string path, string name, Func<Transform, GameObject> create)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));

            var temp = new GameObject("__SevenDaysBakeRoot");
            try
            {
                GameObject root = create(temp.transform);
                root.name = name;
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                UnityEngine.Object.DestroyImmediate(root);

                if (!ok)
                    Debug.LogError("[특별한 7일] 프리팹 저장에 실패했다: " + path);

                return ok;
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>폰트를 읽는다. <b>없으면 멈춘다</b> — null 폰트로 구우면 글자가 하나도 안 보인다.</summary>
        private static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogError("[특별한 7일] 폰트를 못 찾았다: " + FontPath);
            return font;
        }
    }
}
