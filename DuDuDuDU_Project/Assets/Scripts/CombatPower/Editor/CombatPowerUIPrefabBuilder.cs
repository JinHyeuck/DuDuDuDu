#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OJ.Editor
{
    // Editor-only authoring. Gameplay code never creates this UI hierarchy.
    [InitializeOnLoad]
    public static class CombatPowerUIPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefab/Lobby";
        private const string PrefabPath = PrefabFolder + "/UICombatPowerDisplay.prefab";
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string FontPath = "Assets/NotoSansKR-Black/NotoSansKR-Black SDF.asset";
        private const string IconPath = "Assets/Resources/Art/Ingame/Icon_Fight.png";
        private const string AutoInstallSessionKey = "OJ.CombatPowerUI.AutoInstall.v1";

        static CombatPowerUIPrefabBuilder()
        {
            EditorApplication.delayCall += AutoInstallIfMissing;
        }

        [MenuItem("Tools/OJ/Combat Power/Rebuild UI Prefab And Install")]
        public static void RebuildAndInstall()
        {
            GameObject prefab = BuildPrefab();
            InstallIntoLobby(prefab, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            Debug.Log($"Combat power UI rebuilt: {PrefabPath}");
        }

        private static void AutoInstallIfMissing()
        {
            if (SessionState.GetBool(AutoInstallSessionKey, false))
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoInstallIfMissing;
                return;
            }

            SessionState.SetBool(AutoInstallSessionKey, true);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                prefab = BuildPrefab();

            InstallIntoLobby(prefab, false);
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildPrefab()
        {
            EnsureFolder(PrefabFolder);

            GameObject root = CreateRectObject("UICombatPowerDisplay", null);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0.5f);
            rootRect.anchorMax = new Vector2(1f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(0f, 285f);
            rootRect.sizeDelta = new Vector2(0f, 108f);

            GameObject toastRoot = CreateRectObject("ToastRoot", root.transform);
            SetStretch(toastRoot.GetComponent<RectTransform>());
            Image background = toastRoot.AddComponent<Image>();
            background.color = new Color(0.075f, 0.075f, 0.14f, 0.96f);
            background.raycastTarget = false;
            CanvasGroup toastCanvasGroup = toastRoot.AddComponent<CanvasGroup>();
            toastCanvasGroup.alpha = 0f;
            toastCanvasGroup.interactable = false;
            toastCanvasGroup.blocksRaycasts = false;

            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            Image icon = CreateImage("Icon", toastRoot.transform, iconSprite);
            SetAnchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(72f, 72f), new Vector2(-205f, 0f));

            TMP_Text powerText = CreateText("PowerText", toastRoot.transform, "0", 52f, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.05f, 1f));
            SetAnchor(powerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(340f, 90f), Vector2.zero);

            GameObject deltaRoot = CreateRectObject("Delta", toastRoot.transform);
            SetAnchor(deltaRoot.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(210f, 80f), new Vector2(255f, 0f));

            Image deltaArrowImage = CreateImage("DeltaArrowImage", deltaRoot.transform, null);
            SetAnchor(deltaArrowImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(36f, 36f), new Vector2(-78f, 0f));

            TMP_Text deltaText = CreateText("DeltaText", deltaRoot.transform, "50", 38f, TextAlignmentOptions.MidlineLeft, new Color(0.2f, 1f, 0.2f, 1f));
            SetAnchor(deltaText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(150f, 80f), new Vector2(25f, 0f));

            UICombatPowerDisplay display = root.AddComponent<UICombatPowerDisplay>();
            SerializedObject serializedDisplay = new SerializedObject(display);
            SetObject(serializedDisplay, "powerText", powerText);
            SetObject(serializedDisplay, "deltaText", deltaText);
            SetObject(serializedDisplay, "deltaArrowImage", deltaArrowImage);
            SetObject(serializedDisplay, "toastRoot", toastRoot);
            SetObject(serializedDisplay, "toastCanvasGroup", toastCanvasGroup);
            serializedDisplay.ApplyModifiedPropertiesWithoutUndo();

            toastRoot.SetActive(false);

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void InstallIntoLobby(GameObject prefab, bool userInitiated)
        {
            if (prefab == null)
                return;

            Scene lobbyScene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasAlreadyLoaded = lobbyScene.IsValid() && lobbyScene.isLoaded;
            if (wasAlreadyLoaded && lobbyScene.isDirty && !userInitiated)
            {
                Debug.LogWarning("Combat power UI prefab was created, but LobbyScene has unsaved changes. Use Tools/OJ/Combat Power/Rebuild UI Prefab And Install after saving the scene.");
                return;
            }

            if (!wasAlreadyLoaded)
                lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            UICombatPowerDisplay existing = FindInScene<UICombatPowerDisplay>(lobbyScene);
            if (existing == null)
            {
                Canvas canvas = FindCanvas(lobbyScene);
                if (canvas == null)
                {
                    Debug.LogError("Combat power UI could not find Canvas in LobbyScene.");
                }
                else
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
                    if (instance != null)
                    {
                        instance.name = "UICombatPowerDisplay";
                        RectTransform rect = instance.GetComponent<RectTransform>();
                        rect.SetAsLastSibling();
                        EditorSceneManager.MarkSceneDirty(lobbyScene);
                    }
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

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite)
        {
            GameObject go = CreateRectObject(name, parent);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject go = CreateRectObject(name, parent);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null)
                text.font = font;
            text.SetText(value);
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void SetAnchor(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0)
                layer = 5;
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }

        private static void EnsureFolder(string folder)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
    }
}
#endif
