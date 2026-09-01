#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using OJ.Relic;

namespace OJ.EditorTools
{
    public static class RelicUIPrefabBuilder
    {
        private const string PrefabFolder = "Assets/Prefab/Relic";
        private const string ElementPath = PrefabFolder + "/UIRelicElement.prefab";
        private const string SummonDialogPath = PrefabFolder + "/UIRelicSummonDialog.prefab";
        private const string DialogPath = PrefabFolder + "/UIRelicDialog.prefab";

        [MenuItem("Tools/Relic/Create Relic UI Prefabs")]
        public static void CreateRelicUIPrefabs()
        {
            EnsureFolder(PrefabFolder);

            UIRelicElement elementPrefab = CreateRelicElementPrefab();
            CreateRelicSummonDialogPrefab();
            CreateRelicDialogPrefab(elementPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath);
        }

        private static UIRelicElement CreateRelicElementPrefab()
        {
            GameObject root = CreateRectObject("UIRelicElement", null);
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(160f, 190f);

            Image hitImage = root.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0f);
            Button button = root.AddComponent<Button>();

            Image background = CreateImage("Background", root.transform, LoadRelicSprite("Passive_Normal"), Color.white);
            SetStretch(background.rectTransform, 0f, 0f, 1f, 1f);

            Image icon = CreateImage("Icon", root.transform, LoadRelicSprite("Relic_1"), Color.white);
            SetAnchor(icon.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(96f, 96f), Vector2.zero);

            TMP_Text nameText = CreateText("NameText", root.transform, "초심자의 주머니", 24f, TextAlignmentOptions.Center, Color.white);
            SetAnchor(nameText.rectTransform, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.40f), Vector2.zero, Vector2.zero);

            TMP_Text levelText = CreateText("LevelText", root.transform, "Lv.1", 28f, TextAlignmentOptions.Center, Color.white);
            SetAnchor(levelText.rectTransform, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.22f), Vector2.zero, Vector2.zero);

            TMP_Text unknownText = CreateText("UnknownText", root.transform, "?", 74f, TextAlignmentOptions.Center, Color.white);
            SetStretch(unknownText.rectTransform, 0f, 0f, 1f, 1f);
            unknownText.gameObject.SetActive(false);

            Image selectedFrame = CreateImage("SelectedFrame", root.transform, null, new Color(1f, 0.9f, 0.1f, 0.28f));
            SetStretch(selectedFrame.rectTransform, 0f, 0f, 1f, 1f);
            selectedFrame.gameObject.SetActive(false);

            UIRelicElement element = root.AddComponent<UIRelicElement>();
            SerializedObject so = new SerializedObject(element);
            SetObject(so, "button", button);
            SetObject(so, "backgroundImage", background);
            SetObject(so, "iconImage", icon);
            SetObject(so, "nameText", nameText);
            SetObject(so, "levelText", levelText);
            SetObject(so, "unknownText", unknownText);
            SetObject(so, "selectedFrame", selectedFrame.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = SavePrefab(root, ElementPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<UIRelicElement>();
        }

        private static UIRelicSummonDialog CreateRelicSummonDialogPrefab()
        {
            GameObject root = CreateRectObject("UIRelicSummonDialog", null);
            SetStretch(root.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

            GameObject view = CreateRectObject("View", root.transform);
            SetStretch(view.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

            Image dim = view.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);

            Button tapButton = view.AddComponent<Button>();

            Image background = CreateImage("RelicBackground", view.transform, LoadRelicSprite("Passive_Normal"), Color.white);
            SetAnchor(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380f, 450f), new Vector2(0f, 80f));

            Image icon = CreateImage("Icon", background.transform, LoadRelicSprite("Relic_1"), Color.white);
            SetAnchor(icon.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(210f, 210f), Vector2.zero);

            TMP_Text unknownText = CreateText("UnknownText", background.transform, "?", 130f, TextAlignmentOptions.Center, Color.white);
            SetStretch(unknownText.rectTransform, 0f, 0f, 1f, 1f);

            TMP_Text nameText = CreateText("NameText", background.transform, "유물 이름", 38f, TextAlignmentOptions.Center, Color.white);
            SetAnchor(nameText.rectTransform, new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.38f), Vector2.zero, Vector2.zero);

            TMP_Text levelText = CreateText("LevelText", background.transform, "Lv.1", 52f, TextAlignmentOptions.Center, Color.white);
            SetAnchor(levelText.rectTransform, new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.22f), Vector2.zero, Vector2.zero);

            TMP_Text effectText = CreateText("EffectText", view.transform, "효과", 42f, TextAlignmentOptions.Center, Color.white);
            SetAnchor(effectText.rectTransform, new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.34f), Vector2.zero, Vector2.zero);

            TMP_Text guideText = CreateText("GuideText", view.transform, "탭하여 확인", 32f, TextAlignmentOptions.Center, new Color(0.85f, 0.9f, 1f, 1f));
            SetAnchor(guideText.rectTransform, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.15f), Vector2.zero, Vector2.zero);

            UIRelicSummonDialog dialog = root.AddComponent<UIRelicSummonDialog>();
            SerializedObject so = new SerializedObject(dialog);
            SetObject(so, "dialogView", view);
            SetObject(so, "tapButton", tapButton);
            SetObject(so, "backgroundImage", background);
            SetObject(so, "iconImage", icon);
            SetObject(so, "nameText", nameText);
            SetObject(so, "levelText", levelText);
            SetObject(so, "effectText", effectText);
            SetObject(so, "unknownText", unknownText);
            SetObject(so, "guideText", guideText);
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = SavePrefab(root, SummonDialogPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<UIRelicSummonDialog>();
        }

        private static void CreateRelicDialogPrefab(UIRelicElement elementPrefab)
        {
            GameObject root = CreateRectObject("UIRelicDialog", null);
            SetStretch(root.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

            GameObject view = CreateRectObject("View", root.transform);
            SetStretch(view.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

            Image background = view.AddComponent<Image>();
            background.color = new Color(0.08f, 0.26f, 0.38f, 1f);

            TMP_Text titleText = CreateText("TitleText", view.transform, "유물", 48f, TextAlignmentOptions.Center, Color.white);
            SetAnchor(titleText.rectTransform, new Vector2(0.12f, 0.91f), new Vector2(0.88f, 0.97f), Vector2.zero, Vector2.zero);

            Button closeButton = CreateButton("CloseButton", view.transform, "X", new Vector2(96f, 72f), new Vector2(0.93f, 0.945f), new Color(0.14f, 0.34f, 0.48f, 1f));

            GameObject scrollRoot = CreateRectObject("RelicScroll", view.transform);
            SetAnchor(scrollRoot.GetComponent<RectTransform>(), new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);
            ScrollRect scrollRect = scrollRoot.AddComponent<ScrollRect>();
            Image scrollImage = scrollRoot.AddComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0.08f);

            GameObject viewport = CreateRectObject("Viewport", scrollRoot.transform);
            SetStretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
            viewport.AddComponent<RectMask2D>();
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);

            GameObject content = CreateRectObject("Content", viewport.transform);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 1200f);

            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(160f, 190f);
            grid.spacing = new Vector2(22f, 24f);
            grid.padding = new RectOffset(22, 22, 22, 22);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            Button summonButton = CreateButton("SummonButton", view.transform, "유물 뽑기", new Vector2(520f, 115f), new Vector2(0.5f, 0.06f), new Color(1f, 0.77f, 0.18f, 1f));

            TMP_Text goldCostText = CreateText("GoldCostText", summonButton.transform, "x500", 26f, TextAlignmentOptions.Center, new Color(0.28f, 0.18f, 0.05f, 1f));
            SetAnchor(goldCostText.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.48f, 0.38f), Vector2.zero, Vector2.zero);

            TMP_Text ticketCostText = CreateText("TicketCostText", summonButton.transform, "x1", 26f, TextAlignmentOptions.Center, new Color(0.28f, 0.18f, 0.05f, 1f));
            SetAnchor(ticketCostText.rectTransform, new Vector2(0.52f, 0.05f), new Vector2(0.92f, 0.38f), Vector2.zero, Vector2.zero);

            GameObject detailPopupRoot = CreateRectObject("DetailPopupRoot", view.transform);
            SetStretch(detailPopupRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image detailDim = detailPopupRoot.AddComponent<Image>();
            detailDim.color = new Color(0f, 0f, 0f, 0.68f);
            Button detailPopupCloseButton = detailPopupRoot.AddComponent<Button>();

            UIRelicElement detailRelicElement = null;
            if (elementPrefab != null)
            {
                GameObject detailElementObject = PrefabUtility.InstantiatePrefab(elementPrefab.gameObject, detailPopupRoot.transform) as GameObject;
                if (detailElementObject != null)
                {
                    detailElementObject.name = "DetailRelicElement";
                    RectTransform detailElementRt = detailElementObject.GetComponent<RectTransform>();
                    SetAnchor(detailElementRt, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(160f, 190f), Vector2.zero);
                    detailElementRt.localScale = Vector3.one * 2.35f;
                    detailRelicElement = detailElementObject.GetComponent<UIRelicElement>();
                }
            }

            TMP_Text detailEffect = CreateText("DetailEffectText", detailPopupRoot.transform, "효과", 42f, TextAlignmentOptions.Center, Color.white);
            SetAnchor(detailEffect.rectTransform, new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.32f), Vector2.zero, Vector2.zero);

            TMP_Text detailExample = CreateText("DetailExampleText", detailPopupRoot.transform, "예시", 24f, TextAlignmentOptions.Center, new Color(0.78f, 0.9f, 1f, 1f));
            SetAnchor(detailExample.rectTransform, new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.22f), Vector2.zero, Vector2.zero);

            Button detailPreviousButton = CreateButton("PreviousButton", detailPopupRoot.transform, "<", new Vector2(86f, 120f), new Vector2(0.18f, 0.50f), new Color(1f, 1f, 1f, 0f));
            TMP_Text previousText = detailPreviousButton.GetComponentInChildren<TMP_Text>();
            if (previousText != null)
                previousText.fontSize = 72f;

            Button detailNextButton = CreateButton("NextButton", detailPopupRoot.transform, ">", new Vector2(86f, 120f), new Vector2(0.82f, 0.50f), new Color(1f, 1f, 1f, 0f));
            TMP_Text nextText = detailNextButton.GetComponentInChildren<TMP_Text>();
            if (nextText != null)
                nextText.fontSize = 72f;

            detailPopupRoot.SetActive(false);

            UIRelicDialog dialog = root.AddComponent<UIRelicDialog>();
            SerializedObject so = new SerializedObject(dialog);
            SetObject(so, "dialogView", view);
            SetExitButtons(so, closeButton);
            SetObject(so, "relicRoot", content.transform);
            SetObject(so, "relicElementPrefab", elementPrefab);
            SetObject(so, "detailPopupRoot", detailPopupRoot);
            SetObject(so, "detailPopupCloseButton", detailPopupCloseButton);
            SetObject(so, "detailPreviousButton", detailPreviousButton);
            SetObject(so, "detailNextButton", detailNextButton);
            SetObject(so, "detailRelicElement", detailRelicElement);
            SetObject(so, "detailEffectText", detailEffect);
            SetObject(so, "detailExampleText", detailExample);
            SetObject(so, "summonButton", summonButton);
            SetObject(so, "summonButtonText", summonButton.GetComponentInChildren<TMP_Text>());
            SetObject(so, "goldCostText", goldCostText);
            SetObject(so, "ticketCostText", ticketCostText);
            so.ApplyModifiedPropertiesWithoutUndo();

            SavePrefab(root, DialogPath);
            Object.DestroyImmediate(root);
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 size, Vector2 anchor, Color color)
        {
            GameObject go = CreateRectObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetAnchor(rt, anchor, anchor, size, Vector2.zero);

            Image image = go.AddComponent<Image>();
            image.color = color;
            Button button = go.AddComponent<Button>();

            TMP_Text text = CreateText("Text", go.transform, label, 34f, TextAlignmentOptions.Center, Color.white);
            SetStretch(text.rectTransform, 0f, 0f, 1f, 1f);
            return button;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = CreateRectObject(name, parent);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = sprite != null;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject go = CreateRectObject(name, parent);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private static void SetStretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            rt.localScale = Vector3.one;
        }

        private static void SetAnchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPosition;
            rt.localScale = Vector3.one;
        }

        private static void SetObject(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetExitButtons(SerializedObject so, Button button)
        {
            SerializedProperty property = so.FindProperty("_exitBtn");
            if (property == null)
                return;

            property.arraySize = button != null ? 1 : 0;
            if (button != null)
                property.GetArrayElementAtIndex(0).objectReferenceValue = button;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            EditorUtility.SetDirty(prefab);
            return prefab;
        }

        private static Sprite LoadRelicSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Art/Relic/{name}.png");
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }
    }
}
#endif
