using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 시즌 패스 UI 둘(<see cref="UISeasonPassDialog"/>·<see cref="UISeasonPassSlot"/>)이
    /// 함께 쓰는 조립 도구. <c>UIMissionUIFactory</c> 와 같은 자리이고 이유도 같다 —
    /// 헬퍼를 각자 들면 같은 함수가 두 벌이 되고, 둘이 조금씩 어긋나면 칸마다 여백이 다르다.
    ///
    /// <b>런타임에 쓰지 않는다.</b> 전부 에디터 굽기 경로에서만 불린다.
    /// </summary>
    internal static class UISeasonPassUIFactory
    {
        internal const int UILayer = 5;

        internal static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.74f);

        /// <summary>창 바탕. 스샷의 보라빛 밤하늘 계열이다.</summary>
        internal static readonly Color PanelColor = new Color(0.20f, 0.15f, 0.29f, 1f);

        internal static readonly Color HeaderColor = new Color(0.34f, 0.22f, 0.52f, 1f);

        /// <summary>무료 트랙 배경. 유료보다 어둡다 — 두 트랙이 색으로 먼저 갈려야 한다.</summary>
        internal static readonly Color FreeTrackColor = new Color(0.27f, 0.22f, 0.29f, 1f);

        /// <summary>유료 트랙 배경. 스샷의 금색 띠.</summary>
        internal static readonly Color PremiumTrackColor = new Color(0.78f, 0.59f, 0.17f, 1f);

        internal static readonly Color SlotColor = new Color(0.42f, 0.36f, 0.28f, 1f);
        internal static readonly Color SlotDimColor = new Color(0.30f, 0.30f, 0.32f, 1f);
        internal static readonly Color SlotDoneColor = new Color(0.33f, 0.52f, 0.36f, 1f);
        internal static readonly Color LevelBadgeColor = new Color(0.96f, 0.76f, 0.22f, 1f);
        internal static readonly Color LevelBadgeDimColor = new Color(0.45f, 0.40f, 0.35f, 1f);
        internal static readonly Color TrackLineColor = new Color(0.98f, 0.84f, 0.32f, 1f);
        internal static readonly Color GaugeTrackColor = new Color(0.16f, 0.12f, 0.22f, 1f);
        internal static readonly Color GaugeFillColor = new Color(0.98f, 0.84f, 0.32f, 1f);
        internal static readonly Color PremiumButtonColor = new Color(0.95f, 0.66f, 0.16f, 1f);
        internal static readonly Color ClaimAllColor = new Color(0.27f, 0.62f, 0.35f, 1f);
        internal static readonly Color DarkText = new Color(0.16f, 0.12f, 0.10f, 1f);
        internal static readonly Color LightText = new Color(0.98f, 0.96f, 0.93f, 1f);
        internal static readonly Color MutedText = new Color(0.74f, 0.70f, 0.78f, 1f);

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

        internal static TMP_Text CreateText(
            string name, Transform parent, string text, float size,
            TextAlignmentOptions align, Color color, TMP_FontAsset font)
        {
            TextMeshProUGUI label = CreateRect(name, parent).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.font = font;

            // 글자는 클릭을 먹지 않는다. 켜 두면 칸 안의 글자가 그 칸의 버튼보다 위에 있어
            // 글자 위를 누르면 안 눌리는 자리가 생긴다.
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 칸 밖으로 넘치지 않는 글자. 보상 수량처럼 자릿수가 데이터에 달린 값에 쓴다.
        /// <b>줄바꿈을 꺼야 가로로 줄어든다</b> — TMP 의 자동 축소는 세로 넘침에서 돌아서,
        /// 한 덩이 숫자는 줄을 못 바꿔 가로로만 넘치고 그대로 잘린다.
        /// </summary>
        internal static TMP_Text CreateFittedText(
            string name, Transform parent, string text, float maxSize, float minSize,
            TextAlignmentOptions align, Color color, TMP_FontAsset font)
        {
            TMP_Text label = CreateText(name, parent, text, maxSize, align, color, font);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = maxSize;
            label.fontSizeMin = minSize;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        internal static Button CreateButton(
            string name, Transform parent, string label, Vector2 size, Vector2 position,
            Color background, Color textColor, float fontSize, TMP_FontAsset font)
        {
            Image image = CreateImage(name, parent, background);
            SetRect(image.rectTransform, size, position);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // 라벨을 자동 축소로 둔다. 버튼 글자가 상태에 따라 길어지는데
            // ("프리미엄 활성화" → "프리미엄 활성화됨") 고정 크기면 긴 쪽이 버튼 밖으로
            // 흘러 양끝이 잘린다 — 실제로 첫 굽기에서 그랬다.
            TMP_Text text = CreateFittedText("Label", image.transform, label, fontSize, fontSize * 0.6f,
                TextAlignmentOptions.Center, textColor, font);
            SetRect(text.rectTransform, new Vector2(size.x - 16f, size.y), Vector2.zero);

            return button;
        }

        /// <summary>
        /// 게이지 한 벌. 바탕과 채움을 <b>부모-자식</b>으로 둔다 — 채움이 트랙 안에서
        /// 왼쪽 정렬로 늘어나야 한다. 반환값은 채움이고 비율은 <see cref="SetGauge"/> 가 먹인다.
        /// </summary>
        internal static Image CreateGauge(
            string name, Transform parent, Vector2 size, Vector2 position)
        {
            Image track = CreateImage(name, parent, GaugeTrackColor);
            SetRect(track.rectTransform, size, position);
            track.raycastTarget = false;

            Image fill = CreateImage("Fill", track.transform, GaugeFillColor);
            fill.raycastTarget = false;

            RectTransform rect = fill.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(size.x, 0f);

            return fill;
        }

        internal static void SetGauge(Image fill, float trackWidth, float ratio01)
        {
            if (fill == null)
                return;

            RectTransform rect = fill.rectTransform;
            Vector2 offset = rect.offsetMax;
            offset.x = trackWidth * Mathf.Clamp01(ratio01);
            rect.offsetMax = offset;
        }

        /// <summary>
        /// 세로 스크롤 목록. 반환값은 항목을 붙일 Content 다.
        /// 마스크가 없으면 목록이 창 밖까지 그려지는데 스크롤은 되기 때문에 고장으로 안 보인다.
        /// </summary>
        internal static RectTransform CreateScrollList(
            string name, Transform parent, Vector2 size, Vector2 position, float spacing)
        {
            GameObject viewport = CreateRect(name, parent);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            SetRect(viewportRect, size, position);
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
            layout.childControlWidth = true;
            layout.childControlHeight = false;
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
