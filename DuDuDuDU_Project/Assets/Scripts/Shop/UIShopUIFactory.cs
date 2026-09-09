using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Shop
{
    /// <summary>
    /// 상점 UI 여섯 파일이 함께 쓰는 조립 도구. <c>UITowerUIFactory</c>·<c>UIBountyUIFactory</c> 와
    /// 같은 자리이고 이유도 같다 — 헬퍼를 각자 들면 같은 함수가 여섯 벌이 되고, 여섯이 조금씩
    /// 어긋나는 순간 섹션마다 여백이 다른 화면이 나온다. 그 어긋남은 눈으로 못 찾는다.
    ///
    /// <b>탑과 다른 점: 좌표를 안 쓰고 레이아웃 그룹을 쓴다.</b> 탑은 고정 크기 팝업이라
    /// 좌표를 박아도 됐지만, 상점은 <b>로비 탭 내용물</b>이라 부모 폭에 맞춰 늘어나야 하고
    /// 섹션 높이가 데이터 개수(유료젬 5줄·성장 패키지 2개)에 따라 달라진다. 좌표로 짜면
    /// 데이터가 하나 늘 때마다 아래 섹션이 전부 밀린다.
    ///
    /// <b>런타임에 쓰지 않는다.</b> 전부 에디터 굽기 경로에서만 불린다.
    /// </summary>
    internal static class UIShopUIFactory
    {
        internal const int UILayer = 5;

        /// <summary>
        /// 굽는 시점에 가정하는 내용물 폭. 1080 기준 해상도에서 로비 <c>Content</c> 영역이
        /// 대략 이 값이다(<c>UIDiceGrowthPage</c> 의 그리드와 같은 자리).
        ///
        /// <b>폭이 필요한 곳은 그리드 셀 하나뿐이다.</b> 나머지는 레이아웃 그룹이 부모 폭을
        /// 따라가므로 이 값이 틀려도 안 깨진다. 그리드만 uGUI 가 셀 크기를 자동으로 못 맞춘다.
        /// </summary>
        internal const float ContentWidth = 1036f;

        internal const float SidePadding = 24f;

        // 스크린샷의 어두운 남색 계열을 그대로 옮겼다.
        internal static readonly Color PageBg = new Color(0.055f, 0.060f, 0.110f, 1f);
        internal static readonly Color SectionBg = new Color(0.098f, 0.106f, 0.180f, 1f);
        internal static readonly Color CardColor = new Color(0.145f, 0.160f, 0.250f, 1f);
        internal static readonly Color CardEdge = new Color(0.280f, 0.320f, 0.480f, 1f);
        internal static readonly Color RowColor = new Color(0.120f, 0.135f, 0.215f, 1f);

        /// <summary>게임 재화로 사는 것. 파랑.</summary>
        internal static readonly Color Accent = new Color(0.235f, 0.500f, 0.920f, 1f);

        /// <summary>현금(IAP)으로 사는 것. 금색 — <b>지불 수단이 다르면 색이 달라야 한다</b>.</summary>
        internal static readonly Color CashAccent = new Color(0.950f, 0.680f, 0.200f, 1f);

        internal static readonly Color DisabledColor = new Color(0.220f, 0.235f, 0.310f, 1f);
        internal static readonly Color MutedText = new Color(0.680f, 0.720f, 0.840f, 1f);
        internal static readonly Color GoldText = new Color(1f, 0.840f, 0.380f, 1f);
        internal static readonly Color DangerText = new Color(1f, 0.450f, 0.420f, 1f);
        internal static readonly Color SoldOverlay = new Color(0f, 0f, 0f, 0.68f);

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

            // 글자는 클릭을 먹지 않는다. 켜 두면 카드 안의 글자가 그 카드의 버튼보다 위에 있어서
            // <b>글자 위를 누르면 눌리지 않는</b> 자리가 생긴다.
            label.raycastTarget = false;
            return label;
        }

        /// <summary>배경 + 라벨 + <see cref="Button"/> 한 벌. 크기는 부르는 쪽이 정한다.</summary>
        internal static Button CreateButton(
            string name, Transform parent, string label, Color background,
            Color textColor, float fontSize, TMP_FontAsset font)
        {
            Image image = CreateImage(name, parent, background);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = CreateText("Label", image.transform, label, fontSize,
                TextAlignmentOptions.Center, textColor, font);
            Stretch(text.rectTransform);

            return button;
        }

        /// <summary>
        /// 섹션 한 칸. 반환값은 <b>내용물을 붙일 Body</b> 다.
        ///
        /// 머리글에 오른쪽 보조 칸(<paramref name="sideText"/>)을 항상 만든다 — 일일상점의
        /// 갱신 타이머처럼 섹션마다 붙는 것이 다르지만, <b>있다가 없다가 하면 제목의
        /// 세로 위치가 섹션마다 달라진다.</b> 안 쓰는 섹션은 빈 문자열로 둔다.
        /// </summary>
        internal static RectTransform CreateSection(
            string name, Transform parent, string title, string sideText,
            TMP_FontAsset font, out TMP_Text sideLabel)
        {
            GameObject section = CreateRect(name, parent);
            AddVertical(section, spacing: 12f, padding: 0);
            AddVerticalFitter(section);

            GameObject header = CreateRect("Header", section.transform);
            SetPreferredHeight(header, 58f);

            TMP_Text titleLabel = CreateText("Title", header.transform, title, 40f,
                TextAlignmentOptions.Left, Color.white, font);
            StretchWithMargin(titleLabel.rectTransform, SidePadding, 0f);

            sideLabel = CreateText("Side", header.transform, sideText, 26f,
                TextAlignmentOptions.Right, MutedText, font);
            StretchWithMargin(sideLabel.rectTransform, 0f, SidePadding);

            GameObject body = CreateRect("Body", section.transform);
            return body.GetComponent<RectTransform>();
        }

        // ── 레이아웃 그룹 ──────────────────────────────────────────────

        internal static VerticalLayoutGroup AddVertical(GameObject go, float spacing, float padding)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            // 높이는 늘리지 않는다. 켜면 자식들이 남는 공간을 나눠 먹어 <b>내용과 무관한
            // 높이</b>가 되고, ContentSizeFitter 와 함께 쓰면 서로를 밀며 진동한다.
            layout.childForceExpandHeight = false;
            return layout;
        }

        internal static HorizontalLayoutGroup AddHorizontal(GameObject go, float spacing, float padding)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        internal static ContentSizeFitter AddVerticalFitter(GameObject go)
        {
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return fitter;
        }

        internal static LayoutElement SetPreferredHeight(GameObject go, float height)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();

            element.preferredHeight = height;
            element.flexibleHeight = 0f;
            return element;
        }

        /// <summary>
        /// 3열 그리드. 일일상점 6칸(2행 × 3열)이 유일한 사용처다.
        /// 셀 폭만 <see cref="ContentWidth"/> 에서 계산한다 — uGUI 가 이것만 자동으로 못 맞춘다.
        /// </summary>
        internal static GridLayoutGroup AddGrid(GameObject go, int columns, float spacing, float cellHeight)
        {
            var grid = go.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset((int)SidePadding, (int)SidePadding, 0, 0);

            float inner = ContentWidth - SidePadding * 2f - spacing * (columns - 1);
            grid.cellSize = new Vector2(inner / columns, cellHeight);

            AddVerticalFitter(go);
            return grid;
        }

        // ── RectTransform ────────────────────────────────────────────

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

        internal static void StretchWithMargin(RectTransform rect, float left, float right)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, 0f);
        }

        // 표기(금액·기간·구성품)는 ShopText 에 있다. UnityEngine.UI 를 참조하는 타입에 두면
        // 헤드리스 테스트가 불러올 수 없어 규칙이 검증 밖으로 빠진다.
    }
}
