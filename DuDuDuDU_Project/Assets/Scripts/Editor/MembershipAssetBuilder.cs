using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using OJ.Lobby;
using OJ.Shop;

namespace OJ.EditorTools
{
    /// <summary>
    /// 멤버십 창·로비 입구를 굽고 로비에 심는다. <c>SeasonPassAssetBuilder</c> 와 같은 자리다.
    ///
    /// <b>메뉴로만 돈다.</b> 씬을 건드리는 도구가 <c>[InitializeOnLoad]</c> 로 돌면 에디터를 켤 때마다
    /// LobbyScene 을 덮어쓴다. <b>메뉴를 연속으로 누르지 말 것</b> — 앞 메뉴의 Refresh 가 재컴파일을
    /// 시작하면 그 사이의 클릭은 조용히 무시된다.
    /// </summary>
    public static class MembershipAssetBuilder
    {
        private const string DialogPath = "Assets/Prefab/UI/UIMembershipDialog.prefab";
        private const string LobbyButtonPath = "Assets/Prefab/Lobby/UIMembershipLobbyButton.prefab";
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

        private const string FontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        /// <summary>외곽선 없는 글자 재질 — 밝은 혜택 줄 위의 어두운 글자에 쓴다.</summary>
        private const string PlainMaterialPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF Material_NoneOutLine.mat";

        /// <summary>
        /// 로비 좌상단 — 메뉴(≡) 버튼(화면 x 22~162, y 199~325) 바로 아래. 캔버스 좌상단 기준.
        /// 마음에 안 들면 씬에서 옮기면 된다(다시 설치해도 이미 있으면 건드리지 않는다).
        /// </summary>
        private static readonly Vector2 LobbyAnchoredPosition = new Vector2(92f, -440f);

        [MenuItem("OJ/개발/멤버십/창 굽기")]
        private static void BakeDialog()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            var plain = AssetDatabase.LoadAssetAtPath<Material>(PlainMaterialPath);
            if (plain == null)
                Debug.LogWarning("[멤버십] 외곽선 없는 재질을 못 찾았다: " + PlainMaterialPath + " — 혜택 줄 글자에 외곽선이 붙는다.");

            if (!BakePrefab(DialogPath, "UIMembershipDialog", t => UIMembershipDialog.Create(t, font, plain).gameObject))
                return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[멤버십] 창 프리팹을 구웠다: " + DialogPath + Environment.NewLine +
                      "  새로 만들었으면 'OJ/개발/다이얼로그 카탈로그/훑어서 갱신' 을 돌려야 열린다.");
        }

        [MenuItem("OJ/개발/멤버십/로비 입구 설치")]
        private static void InstallLobbyEntry()
        {
            TMP_FontAsset font = LoadFont();
            if (font == null)
                return;

            if (!BakePrefab(LobbyButtonPath, "UIMembershipLobbyButton", t => UIMembershipLobbyButton.Create(t, font).gameObject))
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyButtonPath);

            Scene lobby = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasLoaded = lobby.IsValid() && lobby.isLoaded;
            if (!wasLoaded)
                lobby = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            InstallButton(lobby, prefab);
            WireEntryCostLabel(lobby);

            if (lobby.isDirty)
                EditorSceneManager.SaveScene(lobby);
            if (!wasLoaded)
                EditorSceneManager.CloseScene(lobby, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void InstallButton(Scene lobby, GameObject prefab)
        {
            if (FindInScene<UIMembershipLobbyButton>(lobby) != null)
            {
                Debug.Log("[멤버십] 로비에 입구가 이미 있다. 그대로 둔다 — 자리를 바꾸려면 씬에서 옮길 것.");
                return;
            }

            Canvas canvas = FindCanvas(lobby);
            if (canvas == null)
            {
                Debug.LogError("[멤버십] LobbyScene 에서 Canvas 를 못 찾아 입구를 심지 못했다.");
                return;
            }

            if (!(PrefabUtility.InstantiatePrefab(prefab, canvas.transform) is GameObject instance))
                return;

            instance.name = "UIMembershipLobbyButton";
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = LobbyAnchoredPosition;
            rect.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(lobby);
            Debug.Log("[멤버십] LobbyScene 좌상단에 입구를 심었다.");
        }

        /// <summary>
        /// 입장 버튼의 "x5" 글자를 <c>LobbyLayoutController.enterCostText</c> 에 잇는다.
        /// 멤버십이면 그 글자가 "무료" 로 바뀌어야 한다 — 안 이으면 고기를 안 빼면서 "x5" 라고 적는다.
        /// </summary>
        private static void WireEntryCostLabel(Scene lobby)
        {
            LobbyLayoutController controller = FindInScene<LobbyLayoutController>(lobby);
            if (controller == null)
            {
                Debug.LogError("[멤버십] LobbyLayoutController 를 못 찾았다. 입장료 글자를 잇지 못했다.");
                return;
            }

            var so = new SerializedObject(controller);
            SerializedProperty costProp = so.FindProperty("enterCostText");
            if (costProp.objectReferenceValue != null)
            {
                Debug.Log("[멤버십] 입장료 글자가 이미 이어져 있다.");
                return;
            }

            var button = so.FindProperty("enterStageButton").objectReferenceValue as Button;
            if (button == null)
            {
                Debug.LogError("[멤버십] 입장 버튼이 비어 있다. 입장료 글자를 잇지 못했다.");
                return;
            }

            TMP_Text label = null;
            foreach (TMP_Text t in button.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.text != null && t.text.Trim().StartsWith("x", StringComparison.Ordinal))
                {
                    label = t;
                    break;
                }
            }

            if (label == null)
            {
                Debug.LogError("[멤버십] 입장 버튼 안에서 \"x5\" 글자를 못 찾았다. 씬에서 손으로 이을 것.");
                return;
            }

            costProp.objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(lobby);
            Debug.Log("[멤버십] 입장료 글자를 이었다: " + label.name);
        }

        private static bool BakePrefab(string path, string name, Func<Transform, GameObject> create)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));

            var temp = new GameObject("__MembershipBakeRoot");
            try
            {
                GameObject root = create(temp.transform);
                root.name = name;
                root.transform.SetParent(null, false);

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                UnityEngine.Object.DestroyImmediate(root);

                if (!ok)
                    Debug.LogError("[멤버십] 프리팹 저장에 실패했다: " + path);

                return ok;
            }
            finally
            {
                if (temp != null)
                    UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static Canvas FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.gameObject.name == "Canvas")
                        return canvas;
                }
            }

            return null;
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
                Debug.LogError("[멤버십] 폰트를 못 찾았다: " + FontPath);
            return font;
        }
    }
}
