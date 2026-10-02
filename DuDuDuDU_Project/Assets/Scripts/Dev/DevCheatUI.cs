#if UNITY_EDITOR || DEV_DEFINE
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Dev
{
    /// <summary>
    /// 개발용 치트 화면의 uGUI 조립 도구.
    ///
    /// <b>프리팹을 굽지 않고 런타임에 세운다.</b> 치트 화면은 릴리스에 들어가면 안 되는데,
    /// 프리팹으로 만들면 그것을 가리키는 참조(<c>DialogCatalog</c>·<c>Resources</c>) 때문에
    /// 빌드에 딸려 들어가고, 정작 컴포넌트는 <c>#if</c> 로 사라져 <b>스크립트가 빠진 프리팹</b>이
    /// 남는다. 이 리포는 Missing script 를 기준선 0 으로 지키고 있어서 그 상태를 만들 수 없다.
    /// 코드로 세우면 이 파일 전체가 <c>#if</c> 안이라 릴리스에는 아무것도 남지 않는다.
    ///
    /// <b>폰트는 <c>TMP_Settings.defaultFontAsset</c> 이다.</b> 확인해 보니 그 값이 이미
    /// BM HANNA 라(TMP Settings.asset → BMHANNAProOTF SDF) 따로 들고 올 것이 없다.
    /// 씬의 아무 <c>TMP_Text</c> 에서 폰트를 훔쳐 오는 방식(<c>UIIdleRewardDialog</c>)은
    /// 쓰지 않는다 — 그 창이 규약 밖이라고 적혀 있는 이유 중 하나가 그것이다.
    /// </summary>
    internal static class DevCheatUI
    {
        internal const int UILayer = 5;

        /// <summary>디자인 해상도. 게임 UI 와 같은 기준이어야 손가락 크기가 맞는다.</summary>
        internal static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        internal static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.82f);
        internal static readonly Color PanelColor = new Color(0.16f, 0.17f, 0.20f, 0.98f);
        internal static readonly Color SectionColor = new Color(0.22f, 0.23f, 0.27f, 1f);
        internal static readonly Color ButtonColor = new Color(0.33f, 0.35f, 0.40f, 1f);
        internal static readonly Color TabOnColor = new Color(0.20f, 0.47f, 0.90f, 1f);
        internal static readonly Color TabOffColor = new Color(0.30f, 0.32f, 0.37f, 1f);
        internal static readonly Color DangerColor = new Color(0.72f, 0.24f, 0.26f, 1f);
        internal static readonly Color AccentColor = new Color(0.78f, 0.22f, 0.52f, 1f);
        internal static readonly Color DisabledColor = new Color(0.24f, 0.25f, 0.28f, 1f);
        internal static readonly Color TextColor = new Color(0.94f, 0.95f, 0.97f, 1f);
        internal static readonly Color MutedColor = new Color(0.62f, 0.65f, 0.70f, 1f);
        internal static readonly Color FieldColor = new Color(0.12f, 0.13f, 0.16f, 1f);

        /// <summary>치트 화면이 쓰는 글꼴. 없으면 null 이고, 그때는 TMP 가 기본값으로 그린다.</summary>
        internal static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;

        // ── 뼈대 ───────────────────────────────────────────────────────

        /// <summary>
        /// 치트 전용 캔버스. <b>게임 캔버스에 끼워 넣지 않는다</b> — 씬마다 캔버스 구성이
        /// 다르고, 로비 탭 레이아웃 안에 들어가면 그쪽 <c>LayoutGroup</c> 이 치트 창의
        /// 좌표를 가져가 버린다. 제 캔버스를 갖고 맨 위에 그린다.
        /// </summary>
        internal static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        internal static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static TMP_Text CreateLabel(
            string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            TextMeshProUGUI label = CreateRect(name, parent).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.font = Font;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 버튼 한 벌. <paramref name="height"/> 는 손가락 기준이다 —
        /// 1080 폭에서 70 미만으로 내리면 실기에서 옆 버튼이 눌린다.
        /// </summary>
        internal static Button CreateButton(
            string name, Transform parent, string text, Color background, Action onClick,
            float height = 84f, float fontSize = 34f)
        {
            Image image = CreateImage(name, parent, background);

            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleWidth = 1f;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
                button.onClick.AddListener(() => onClick());

            TMP_Text label = CreateLabel(
                "Label", image.transform, text, fontSize, TextColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            return button;
        }

        /// <summary>
        /// 숫자 입력칸. <b>키보드가 숫자로 열리게 한다</b> — 실기에서 전체 키보드가 뜨면
        /// 금액 하나 넣는 데 화면의 절반이 가린다.
        /// </summary>
        internal static TMP_InputField CreateNumberField(
            Transform parent, string value, float width = 240f, float height = 84f)
        {
            Image background = CreateImage("Field", parent, FieldColor);

            var layout = background.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;

            GameObject area = CreateRect("TextArea", background.transform);
            RectTransform areaRect = area.GetComponent<RectTransform>();
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(12f, 6f);
            areaRect.offsetMax = new Vector2(-12f, -6f);
            area.AddComponent<RectMask2D>();

            TMP_Text text = CreateLabel("Text", area.transform, value, 34f, TextColor);
            Stretch(text.rectTransform);

            var field = background.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = areaRect;
            field.textComponent = text;
            field.text = value;
            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.targetGraphic = background;
            return field;
        }

        /// <summary>
        /// 고르는 칸. 항목이 많은 것(재화 19종·다이스 15종·보석 수십 개)은 버튼을 다 늘어놓는
        /// 것보다 이쪽이 낫다 — 예전 치트창은 <c>SelectionGrid</c> 로 전부 깔아서 화면의
        /// 대부분을 목록이 먹었다.
        ///
        /// <b>TMP_Dropdown 은 템플릿을 손으로 지어 줘야 한다.</b> 에디터에서 만들 때 딸려
        /// 오는 자식 구조(Template/Viewport/Content/Item)를 코드로 똑같이 세운다.
        /// 하나라도 빠지면 눌렀을 때 목록이 안 열리고, <b>예외도 안 난다.</b>
        /// </summary>
        internal static TMP_Dropdown CreateDropdown(
            Transform parent, string[] options, int value, Action<int> onChanged, float height = 84f)
        {
            Image root = CreateImage("Dropdown", parent, FieldColor);

            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleWidth = 1f;

            var dropdown = root.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = root;

            TMP_Text caption = CreateLabel("Label", root.transform, string.Empty, 32f, TextColor);
            RectTransform captionRect = caption.rectTransform;
            Stretch(captionRect);
            captionRect.offsetMin = new Vector2(16f, 0f);
            captionRect.offsetMax = new Vector2(-40f, 0f);
            caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            dropdown.captionText = caption;

            // 펼쳐지는 목록. 비활성으로 둬야 평소에 안 보인다 — TMP 가 열 때 복제해 쓴다.
            Image template = CreateImage("Template", root.transform, PanelColor);
            RectTransform templateRect = template.rectTransform;
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, 420f);

            var templateScroll = template.gameObject.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.vertical = true;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;

            Image viewport = CreateImage("Viewport", template.transform, new Color(0f, 0f, 0f, 0f));
            RectTransform viewportRect = viewport.rectTransform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            GameObject content = CreateRect("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, height);

            GameObject item = CreateRect("Item", content.transform);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, height);

            Image itemBackground = CreateImage("Item Background", item.transform, SectionColor);
            Stretch(itemBackground.rectTransform);

            Image itemChecked = CreateImage("Item Checkmark", item.transform, TabOnColor);
            Stretch(itemChecked.rectTransform);

            TMP_Text itemLabel = CreateLabel("Item Label", item.transform, "Option", 32f, TextColor);
            RectTransform itemLabelRect = itemLabel.rectTransform;
            Stretch(itemLabelRect);
            itemLabelRect.offsetMin = new Vector2(16f, 0f);
            itemLabelRect.offsetMax = new Vector2(-16f, 0f);

            var toggle = item.AddComponent<Toggle>();
            toggle.targetGraphic = itemBackground;
            toggle.graphic = itemChecked;
            toggle.isOn = true;

            templateScroll.viewport = viewportRect;
            templateScroll.content = contentRect;

            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            template.gameObject.SetActive(false);

            dropdown.ClearOptions();
            var list = new System.Collections.Generic.List<string>(options);
            dropdown.AddOptions(list);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, options.Length - 1)));
            dropdown.RefreshShownValue();

            if (onChanged != null)
                dropdown.onValueChanged.AddListener(v => onChanged(v));

            return dropdown;
        }

        /// <summary>
        /// 켜고 끄는 줄. <c>Toggle</c> 대신 <b>버튼 + 색</b>으로 둔다 —
        /// 체크박스는 손가락으로 때리기엔 작고, 켜졌는지도 멀리서 안 보인다.
        /// </summary>
        internal static Button CreateToggleButton(
            string name, Transform parent, string text, bool on, Action onClick)
        {
            Button button = CreateButton(
                name, parent, text, on ? TabOnColor : ButtonColor, onClick);
            return button;
        }

        internal static void SetToggleState(Button button, bool on)
        {
            Image image = button != null ? button.targetGraphic as Image : null;
            if (image != null)
                image.color = on ? TabOnColor : ButtonColor;
        }

        /// <summary>
        /// 가로로 늘어놓는 줄. 버튼 두세 개를 나란히 둘 때 쓴다.
        /// </summary>
        internal static RectTransform CreateRow(Transform parent, float spacing = 10f)
        {
            GameObject row = CreateRect("Row", parent);
            RectTransform rect = row.GetComponent<RectTransform>();

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var element = row.AddComponent<LayoutElement>();
            element.minHeight = 84f;

            return rect;
        }

        /// <summary>제목 한 줄. 구역을 가르는 것이 목적이라 위에 여백을 둔다.</summary>
        internal static TMP_Text CreateSectionTitle(Transform parent, string text)
        {
            TMP_Text label = CreateLabel("SectionTitle", parent, text, 36f, MutedColor);
            var element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 64f;
            element.minHeight = 64f;
            return label;
        }

        /// <summary>설명 한 줄. 치트가 무엇을 건드리는지 짧게 적는 자리다.</summary>
        internal static TMP_Text CreateHelpText(Transform parent, string text)
        {
            TMP_Text label = CreateLabel("Help", parent, text, 26f, MutedColor);
            label.textWrappingMode = TextWrappingModes.Normal;

            var element = label.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 56f;
            element.minHeight = 56f;
            return label;
        }

        /// <summary>
        /// 세로 스크롤 목록. 반환값은 <b>내용을 붙일 Content</b> 다.
        /// 마스크가 없으면 내용이 창 밖까지 그려지는데 스크롤은 되기 때문에 고장으로 안 보인다.
        /// </summary>
        internal static RectTransform CreateScroll(Transform parent, float spacing = 10f)
        {
            GameObject viewport = CreateRect("Viewport", parent);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewport.AddComponent<RectMask2D>();

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            GameObject content = CreateRect("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            return contentRect;
        }

        internal static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
