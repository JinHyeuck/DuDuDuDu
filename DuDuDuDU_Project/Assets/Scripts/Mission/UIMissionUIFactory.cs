using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Mission
{
    /// <summary>
    /// 퀘스트 UI 셋(<see cref="UIQuestDialog"/>·<see cref="UIMissionRow"/>·
    /// <see cref="UIMissionTierItem"/>)이 함께 쓰는 조립 도구.
    ///
    /// <c>UITowerUIFactory</c>·<c>UIBountyUIFactory</c> 와 같은 자리이고 이유도 같다 —
    /// 파일이 셋이라 헬퍼를 각자 들면 <b>같은 함수가 세 벌</b>이 되고, 셋이 조금씩
    /// 어긋나는 순간 줄마다 여백이 다른 목록이 나온다. 그 어긋남은 눈으로 못 찾는다.
    ///
    /// <b>런타임에 쓰지 않는다.</b> 전부 에디터 굽기 경로에서만 불린다 —
    /// 화면을 매 프레임 다시 조립하는 코드는 여기 없다.
    /// </summary>
    internal static class UIMissionUIFactory
    {
        internal const int UILayer = 5;

        internal static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.72f);
        internal static readonly Color PanelColor = new Color(0.94f, 0.91f, 0.84f, 1f);
        internal static readonly Color RowColor = new Color(0.78f, 0.74f, 0.68f, 1f);
        internal static readonly Color RowDoneColor = new Color(0.60f, 0.82f, 0.62f, 1f);
        internal static readonly Color DarkText = new Color(0.16f, 0.13f, 0.11f, 1f);
        internal static readonly Color LightText = new Color(0.98f, 0.96f, 0.92f, 1f);
        internal static readonly Color MutedText = new Color(0.42f, 0.38f, 0.34f, 1f);
        internal static readonly Color TrackColor = new Color(0.24f, 0.18f, 0.16f, 1f);
        internal static readonly Color FillColor = new Color(0.44f, 0.83f, 0.52f, 1f);
        internal static readonly Color ClaimColor = new Color(0.96f, 0.72f, 0.24f, 1f);
        internal static readonly Color DisabledColor = new Color(0.55f, 0.52f, 0.49f, 1f);
        internal static readonly Color TabOnColor = new Color(0.52f, 0.42f, 0.38f, 1f);
        internal static readonly Color TabOffColor = new Color(0.74f, 0.70f, 0.64f, 1f);
        internal static readonly Color RedDotColor = new Color(0.92f, 0.22f, 0.22f, 1f);

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

            // 글자는 클릭을 먹지 않는다. 켜 두면 줄 안의 글자가 그 줄의 버튼보다 위에 있어서
            // <b>글자 위를 누르면 안 눌리는</b> 자리가 생긴다.
            label.raycastTarget = false;
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

            TMP_Text text = CreateText("Label", image.transform, label, fontSize,
                TextAlignmentOptions.Center, textColor, font);
            SetRect(text.rectTransform, size, Vector2.zero);

            return button;
        }

        /// <summary>
        /// 게이지 한 벌. 바탕(트랙)과 채움(필)을 <b>형제가 아니라 부모-자식</b>으로 둔다 —
        /// 채움이 트랙 안에서 왼쪽 정렬로 늘어나야 하기 때문이다.
        /// 반환값은 채움이고, 비율은 <see cref="SetGauge"/> 로 먹인다.
        /// </summary>
        internal static Image CreateGauge(
            string name, Transform parent, Vector2 size, Vector2 position, Color fillColor)
        {
            Image track = CreateImage(name, parent, TrackColor);
            SetRect(track.rectTransform, size, position);
            track.raycastTarget = false;

            Image fill = CreateImage("Fill", track.transform, fillColor);
            fill.raycastTarget = false;

            // 왼쪽 가장자리에 고정하고 너비만 바꾼다. offsetMax.x 하나로 채움이 조절되어야
            // 갱신 코드가 한 줄로 끝난다.
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
        /// 세로 스크롤 목록 한 벌. 반환값은 <b>항목을 붙일 Content</b> 다.
        ///
        /// <c>ScrollRect</c> 는 마스크(<see cref="RectMask2D"/>)가 있어야 밖으로 삐져나온
        /// 항목이 잘린다. 없으면 목록이 창 밖까지 그려지는데 스크롤은 되기 때문에
        /// <b>버그로 보이지 않는다</b>.
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
            scroll.scrollSensitivity = 30f;

            GameObject content = CreateRect("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();

            // 위에서 아래로 쌓인다. 앵커를 위쪽 가장자리에 붙여야 항목이 늘어날 때
            // 목록이 아래로 자라고 첫 항목이 제자리에 남는다.
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
