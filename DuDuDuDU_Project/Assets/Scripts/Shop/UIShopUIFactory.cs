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

        // ── 시안 수치 (Art/Layout/Shop_*.png · Art/0PSD/상점.psd) ──────
        //
        // 1080x1920 기준이고 로비 Content 가 화면 전체라 <b>PSD 좌표가 그대로 Unity 좌표</b>다
        // (x = psdX - 540, y = 960 - psdY). 아래 상수는 전부 PSD 에서 실측한 값이다.

        /// <summary>고정 배너의 높이. 이 아래부터 스크롤이다(시안 구분띠 하단 = 567).</summary>
        internal const float BannerHeight = 567f;

        /// <summary>하단 탭 바가 덮는 높이. 로비 Bottom 이 y 1709 부터다.</summary>
        internal const float TabBarHeight = 211f;

        /// <summary>섹션 머리글 바(Reward_Ui_TextBar)의 보이는 크기. 리소스 x4 배다.</summary>
        internal static readonly Vector2 SectionBarSize = new Vector2(438f, 80f);

        /// <summary>재화 카드 한 장. Shop_Item_Btn 을 x3 배 한 크기다.</summary>
        internal static readonly Vector2 TierCardSize = new Vector2(327f, 465f);

        /// <summary>가격 버튼(Big_Btn_Yellow). 시안 주석 "버튼크기 x3배 / 276x100".</summary>
        internal static readonly Vector2 PriceButtonSize = new Vector2(276f, 100f);

        /// <summary>킷 스프라이트를 쓰는 기본 배율. 시안 주석이 거의 전부 x3 이다.</summary>
        internal const float KitScale = 3f;

        /// <summary>페이지 배경. 시안에서 뽑은 #1E1828.</summary>
        internal static readonly Color PageBackground = new Color32(30, 24, 40, 255);

        /// <summary>구분띠 안쪽. #1D1C30</summary>
        internal static readonly Color DividerFill = new Color32(29, 28, 48, 255);

        /// <summary>구분띠 테두리 선. #3E3F68</summary>
        internal static readonly Color DividerEdge = new Color32(62, 63, 104, 255);

        /// <summary>배너 제목 "상점". #F6FF00</summary>
        internal static readonly Color BannerTitleColor = new Color32(246, 255, 0, 255);

        /// <summary>청약 철회 안내. #A395B9</summary>
        internal static readonly Color NoticeColor = new Color32(163, 149, 185, 255);

        /// <summary>남은 횟수 0 일 때. #FF5757</summary>
        internal static readonly Color SoldOutColor = new Color32(255, 87, 87, 255);

        /// <summary>토글 OFF 라벨. #8E91A4</summary>
        internal static readonly Color ToggleOffColor = new Color32(142, 145, 164, 255);

        /// <summary>섹션 머리글 바. 시안 주석 "컬러 772e9d" — 스프라이트가 흰색이라 곱하면 그 색이 된다.</summary>
        internal static readonly Color SectionBarColor = new Color32(119, 46, 157, 255);

        /// <summary>토글 바탕. #443D4F</summary>
        internal static readonly Color ToggleTrackColor = new Color32(68, 61, 79, 255);

        /// <summary>녹색 버튼 위 글자. #003518</summary>
        internal static readonly Color OnGreenText = new Color32(0, 53, 24, 255);

        /// <summary>금색 버튼 위 글자. #5D2C00</summary>
        internal static readonly Color OnGoldText = new Color32(93, 44, 0, 255);

        // ── 스프라이트 ────────────────────────────────────────────────

        /// <summary>
        /// <c>Resources/Art/</c> 아래 스프라이트. <b>굽기 전용</b>이라 없으면 던진다 —
        /// 조용히 null 을 돌려주면 흰 사각형이 박힌 프리팹이 저장되고,
        /// 그것이 "원래 저런 디자인인가" 와 구별되지 않는다.
        /// </summary>
        internal static Sprite Art(string pathUnderArt)
        {
            var sprite = Resources.Load<Sprite>("Art/" + pathUnderArt);
            if (sprite == null)
                throw new System.InvalidOperationException("[상점 굽기] 스프라이트가 없다: Resources/Art/" + pathUnderArt);

            return sprite;
        }

        /// <summary>
        /// 스프라이트 한 장. <paramref name="pixelScale"/> 는 시안의 "x3배" 다.
        ///
        /// <b><paramref name="size"/> 는 보이는 크기가 아니라 텍스처 크기 x 배율이다.</b>
        /// 킷 스프라이트에는 투명 여백이 있고 uGUI 는 그 여백까지 그린다 —
        /// 상점 스프라이트는 전부 보이는 그림이 텍스처 한가운데에 있어서(실측 확인),
        /// 중심을 맞추면 여백은 사방으로 똑같이 삐져나간다.
        ///
        /// <paramref name="sliced"/> 면 테두리가 배율대로 그려지고 가운데만 늘어난다.
        /// <c>pixelsPerUnitMultiplier = 1/배율</c> — <c>UITowerUIFactory</c> 와 같은 방식이다.
        /// <b>9슬라이스 테두리(.meta 의 spriteBorder)는 사람이 정한다.</b> 여기서 건드리지 않는다.
        /// </summary>
        internal static Image CreateSprite(
            string name, Transform parent, Sprite sprite, float pixelScale,
            Vector2 size, Vector2 position, bool sliced = false)
        {
            Image image = CreateImage(name, parent, Color.white);
            image.sprite = sprite;
            image.raycastTarget = false;

            if (sliced)
            {
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                image.pixelsPerUnitMultiplier = 1f / pixelScale;
            }

            SetRect(image.rectTransform, size, position);
            return image;
        }

        /// <summary>텍스처 전체를 <paramref name="pixelScale"/> 배로 놓는다. 크기를 안 세도 되는 쪽.</summary>
        internal static Image CreateSpriteAt(
            string name, Transform parent, Sprite sprite, float pixelScale, Vector2 center)
        {
            Rect rect = sprite.rect;
            return CreateSprite(name, parent, sprite, pixelScale,
                new Vector2(rect.width * pixelScale, rect.height * pixelScale), center);
        }

        /// <summary>
        /// 9슬라이스 스프라이트 버튼 + 가운데 라벨. 시안의 노랑·초록·보라회색 버튼이 전부 이 모양이다.
        /// </summary>
        internal static Button CreateSpriteButton(
            string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position,
            string label, float fontSize, Color textColor, TMP_FontAsset font, float pixelScale = KitScale)
        {
            Image image = CreateSprite(name, parent, sprite, pixelScale, size, position, sliced: true);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = CreateText("Label", image.transform, label, fontSize,
                TextAlignmentOptions.Center, textColor, font);
            Stretch(text.rectTransform);

            return button;
        }

        // ── 킷 스프라이트: 보이는 크기 → Image 크기 ────────────────────
        //
        // <b>시안에서 잰 것은 "보이는 사각형"이고 uGUI 가 그리는 것은 "스프라이트 전체"다.</b>
        // 킷 스프라이트에는 투명 여백이 있어서 둘이 다르다. 여백을 더하지 않으면
        // 버튼이 시안보다 여백만큼 작게 구워지는데, 몇 px 라 눌러 보기 전에는 안 보인다.
        //
        // 여백이 최종 픽셀로 얼마가 되는지는 <b>축마다 다르다</b>:
        //   · 그 축의 9슬라이스 테두리가 있으면(border > 0) 여백은 테두리 안에 들어가
        //     배율만큼 고정으로 그려진다      → 여백 = 텍스처여백 x 배율
        //   · 테두리가 0 이면 그 축은 통째로 늘어난다 → 여백 = 보이는크기 x (텍스처여백 / 보이는텍스처크기)
        //
        // 아래 값은 전부 `Tools/ui/art/sprite_sheet.py` 로 실측한 것이다.
        // <b>.meta 의 spriteBorder 는 여기서 건드리지 않는다</b> — 9슬라이스는 사람이 정한다.

        /// <summary>
        /// 보이는 사각형으로 스프라이트를 놓는다.
        /// <paramref name="pad"/> 는 최종 화면 픽셀로 환산한 투명 여백 (왼, 아래, 오른, 위).
        /// </summary>
        internal static Image CreateKit(
            string name, Transform parent, Sprite sprite, float pixelScale,
            Vector2 visibleSize, Vector2 visibleCenter, Vector4 pad, bool sliced = true)
        {
            var size = new Vector2(visibleSize.x + pad.x + pad.z, visibleSize.y + pad.y + pad.w);
            var center = new Vector2(
                visibleCenter.x - (pad.x - pad.z) * 0.5f,
                visibleCenter.y - (pad.y - pad.w) * 0.5f);

            return CreateSprite(name, parent, sprite, pixelScale, size, center, sliced);
        }

        /// <summary>
        /// 섹션 머리글 바(<c>Reward_Ui_TextBar</c>, 시안 438x80 · 리소스 x4).
        /// 텍스처 64x64 에 보이는 것이 (11,21)-(53,43) 이고 테두리가 (23,0,23,0) 이다 —
        /// <b>가로만 9슬라이스이고 세로는 통째로 늘어난다.</b>
        /// </summary>
        internal static Image CreateSectionBar(string name, Transform parent, Vector2 visibleSize, Vector2 center)
        {
            const float Scale = 4f;
            float side = 11f * Scale;                       // 가로: 테두리 안이라 고정
            float vertical = visibleSize.y * (21f / 22f);   // 세로: 통째로 늘어나므로 비례

            Image bar = CreateKit(name, parent, Art("Clear/Reward_Ui_TextBar"), Scale,
                visibleSize, center, new Vector4(side, vertical, side, vertical));
            bar.color = SectionBarColor;
            return bar;
        }

        /// <summary>
        /// 어두운 패널(<c>Ui_Popup_SmallBox</c>, 시안 x4 · 알파 70%).
        /// 텍스처 32x32 에 보이는 것이 (6,6)-(25,25), 테두리 (15,15,15,15) — 네 축 다 고정이다.
        /// </summary>
        internal static Image CreatePanel(
            string name, Transform parent, Vector2 visibleSize, Vector2 center, float alpha)
        {
            const float Scale = 4f;
            Image panel = CreateKit(name, parent, Art("Upgrade/Ui_Popup_SmallBox"), Scale,
                visibleSize, center,
                new Vector4(6f * Scale, 7f * Scale, 7f * Scale, 6f * Scale));

            // 시안의 패널은 스프라이트 색이 아니라 <b>검정을 알파로 깐 것</b>이다 —
            // 광고 패널 안쪽을 재면 (9,7,12) 이고, 그것은 페이지 배경(30,24,40)의 30% 다.
            panel.color = new Color(0f, 0f, 0f, alpha);
            return panel;
        }

        /// <summary>
        /// 킷 버튼 셋(노랑·초록·보라회색)의 공통 여백. 셋 다 32x32 에 보이는 것이 (4,6)-(28,30) 이고
        /// 네 축 모두 테두리가 있다.
        /// </summary>
        internal static Vector4 ButtonPad(float scale)
        {
            return new Vector4(4f * scale, 2f * scale, 4f * scale, 6f * scale);
        }

        /// <summary>킷 버튼 + 가운데 라벨. 크기·중심은 <b>시안에서 잰 보이는 사각형</b>이다.</summary>
        internal static Button CreateKitButton(
            string name, Transform parent, string spritePath, Vector2 visibleSize, Vector2 center,
            string label, float fontSize, Color textColor, TMP_FontAsset font, float scale = KitScale)
        {
            Image image = CreateKit(name, parent, Art(spritePath), scale,
                visibleSize, center, ButtonPad(scale));
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // 버튼 면이 아래쪽 입술만큼 위로 치우쳐 있다. 라벨을 Image 한가운데 두면 내려앉는다.
            TMP_Text text = CreateText("Label", image.transform, label, fontSize,
                TextAlignmentOptions.Center, textColor, font);
            Stretch(text.rectTransform);
            text.rectTransform.anchoredPosition = new Vector2(0f, (6f - 2f) * scale * 0.5f);

            return button;
        }

        /// <summary>
        /// 시안의 가로 구분띠. 검정 5 / 밝은선 5 / 안쪽 72 / 밝은선 5 / 검정 5 로 쌓인 86px 이다.
        /// 스프라이트가 따로 없어 색 네모로 쌓는다.
        /// </summary>
        internal static GameObject CreateDivider(string name, Transform parent, float width, Vector2 center)
        {
            GameObject strip = CreateRect(name, parent);
            SetRect(strip.GetComponent<RectTransform>(), new Vector2(width, 86f), center);

            Band(strip.transform, "Top", Color.black, 40.5f, 5f);
            Band(strip.transform, "TopEdge", DividerEdge, 35.5f, 5f);
            Band(strip.transform, "Fill", DividerFill, 0f, 62f);
            Band(strip.transform, "BottomEdge", DividerEdge, -35.5f, 5f);
            Band(strip.transform, "Bottom", Color.black, -40.5f, 5f);
            return strip;
        }

        private static void Band(Transform parent, string name, Color color, float y, float height)
        {
            Image band = CreateImage(name, parent, color);
            band.raycastTarget = false;
            RectTransform rect = band.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

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
