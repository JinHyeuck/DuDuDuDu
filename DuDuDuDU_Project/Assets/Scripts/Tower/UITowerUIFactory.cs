using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Tower
{
    /// <summary>
    /// 탑 UI 네 개(<see cref="UITowerFloorSelectDialog"/>·<see cref="UITowerFloorCard"/>·
    /// <see cref="UITowerLoadoutDialog"/>·<see cref="UITowerResultDialog"/>)가 함께 쓰는 조립 도구.
    ///
    /// <c>UIBountyUIFactory</c> 와 같은 자리이고 이유도 같다 — 파일이 넷이라 헬퍼를 각자
    /// 들면 <b>같은 함수가 네 벌</b>이 되고, 넷이 조금씩 어긋나는 순간 화면마다 여백이
    /// 다른 UI 가 나온다. 그 어긋남은 눈으로 못 찾는다.
    ///
    /// <b>현상금 쪽을 여기로 합치지 않았다.</b> 그 파일은 이번 작업의 대상이 아니고,
    /// 건드리면 이미 구운 프리팹 세 개와 대조할 것이 늘어난다.
    ///
    /// <b>런타임에 쓰지 않는다.</b> 전부 에디터 굽기 경로에서만 불린다.
    /// </summary>
    internal static class UITowerUIFactory
    {
        internal const int UILayer = 5;

        // 화면 전체의 색. 기획서 목업의 남색 계열을 그대로 옮겼다.
        internal static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.72f);
        internal static readonly Color PanelColor = new Color(0.086f, 0.11f, 0.23f, 0.99f);
        internal static readonly Color PanelEdge = new Color(0.42f, 0.56f, 0.85f, 1f);
        internal static readonly Color CardColor = new Color(0.14f, 0.17f, 0.31f, 1f);
        internal static readonly Color CardDimColor = new Color(0.10f, 0.12f, 0.20f, 1f);
        internal static readonly Color Accent = new Color(0.36f, 0.42f, 0.92f, 1f);
        internal static readonly Color AccentSoft = new Color(0.24f, 0.28f, 0.55f, 1f);
        internal static readonly Color GoldText = new Color(1f, 0.84f, 0.38f, 1f);
        internal static readonly Color MutedText = new Color(0.70f, 0.76f, 0.88f, 1f);
        internal static readonly Color DangerColor = new Color(0.86f, 0.36f, 0.38f, 1f);
        internal static readonly Color TrackColor = new Color(0.20f, 0.23f, 0.38f, 1f);

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

            // 글자는 클릭을 먹지 않는다. 켜 두면 카드 안의 글자가 그 카드의 버튼보다
            // 위에 있어서 <b>글자 위를 누르면 선택이 안 되는</b> 자리가 생긴다.
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 배경 + 라벨 + <see cref="Button"/> 한 벌. 탑 UI 의 버튼이 전부 이 모양이다.
        /// </summary>
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
        /// </summary>
        internal static Image CreateGauge(
            string name, Transform parent, Vector2 size, Vector2 position, Color fillColor)
        {
            Image track = CreateImage(name, parent, TrackColor);
            SetRect(track.rectTransform, size, position);
            track.raycastTarget = false;

            Image fill = CreateImage("Fill", track.transform, fillColor);
            fill.raycastTarget = false;

            // 왼쪽 가장자리에 고정하고 너비만 바꾼다. sizeDelta.x 하나로 채움을 조절할 수
            // 있어야 갱신 코드가 한 줄로 끝난다.
            RectTransform rect = fill.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(size.x, 0f);

            return fill;
        }

        /// <summary>게이지 채움 비율을 먹인다. 트랙 너비를 알아야 해서 같이 받는다.</summary>
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
        /// <c>ScrollRect</c> 는 마스크(<see cref="RectMask2D"/>)가 있어야 밖으로
        /// 삐져나온 항목이 잘린다. 없으면 목록이 화면 전체를 덮고, 그 상태에서도
        /// 스크롤은 되기 때문에 <b>버그로 보이지 않는다</b>.
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
            contentRect.sizeDelta = new Vector2(0f, 0f);

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

        /// <summary>
        /// 초를 "42.3초" 로 적는다. 기획서 5.6 의 표기 그대로다.
        /// <b>소수 한 자리를 지킨다</b> — 기록 갱신이 0.1초 단위로 읽혀야
        /// "조금 나아졌다" 가 화면에 남는다.
        /// </summary>
        internal static string FormatSeconds(int milliseconds)
        {
            return (milliseconds / 1000f).ToString("0.0") + "초";
        }
    }
}
