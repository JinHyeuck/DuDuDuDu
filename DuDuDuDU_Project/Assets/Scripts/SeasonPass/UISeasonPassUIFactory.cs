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

        // 로비 입구(UISeasonPassLobbyButton)가 쓰는 색. 창은 아래 PSD 색을 쓴다.
        internal static readonly Color HeaderColor = new Color(0.34f, 0.22f, 0.52f, 1f);
        internal static readonly Color LightText = new Color(0.98f, 0.96f, 0.93f, 1f);
        internal static readonly Color MutedText = new Color(0.74f, 0.70f, 0.78f, 1f);

        // ── 창 색. 전부 PSD(Art/0PSD/패스.psd) 실측이다 ─────────────────

        /// <summary>무료 트랙 바탕(창 전체 바탕이기도 하다).</summary>
        internal static readonly Color FreeTrackColor = Hex(0x4e323e);

        /// <summary>유료 트랙 바탕.</summary>
        internal static readonly Color PremiumTrackColor = Hex(0xffc600);

        /// <summary>Pass_Pattern 틴트. 그림 자체가 알파 15/255 라 색만 준다.</summary>
        internal static readonly Color FreePatternTint = Hex(0xd98faf);
        internal static readonly Color PremiumPatternTint = Hex(0x3d1300);

        /// <summary>레벨 줄·하단 띠: 검은 선 5 + 밝은 선 5 + 바탕.</summary>
        internal static readonly Color BandColor = Hex(0x1d1c30);
        internal static readonly Color BandLineColor = Hex(0x3d3f67);

        /// <summary>남은 기간·포인트 칸. SmallBox 를 검정 80% 로 깐다.</summary>
        internal static readonly Color SmallBoxColor = new Color(0f, 0f, 0f, 0.8f);

        internal static readonly Color TitleColor = Hex(0xffc600);
        internal static readonly Color FreeLabelColor = Hex(0xefbed3);

        /// <summary>프리미엄 버튼 글자 — 사기 전 흰색, 산 뒤 노란색(PSD 주석 "구매시 노란글씨로 ffe400").</summary>
        internal static readonly Color PremiumActiveText = Hex(0xffe400);

        /// <summary>받은 칸을 누르는 색.</summary>
        internal static readonly Color DimTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        /// <summary>못 닿은 레벨 줄 전체를 덮는 딤.</summary>
        internal static readonly Color LockedDim = new Color(0f, 0f, 0f, 0.5f);

        /// <summary>구매 창 뒤를 덮는 딤.</summary>
        internal static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.85f);

        /// <summary>구매 창 판 색(짙은 보라). 무료 트랙 색보다 한 단계 어둡다.</summary>
        internal static readonly Color CardColor = Hex(0x2e2033);

        /// <summary>구매 창의 혜택 줄 바탕(어두운 금색).</summary>
        internal static readonly Color BenefitBarColor = Hex(0xa8780c);

        private const string ArtRoot = "Art/";

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

        // ── 그림 ───────────────────────────────────────────────────────
        //
        // 좌표는 PSD 레이어 bbox(보이는 픽셀) 그대로 적고, Image 크기에는 스프라이트
        // 투명 여백 x 배율을 더한다(Tools/ui/PORTING.md).

        internal static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>PSD 좌표(좌상단 원점, 1080x1920) → 화면 중앙 기준 anchoredPosition.</summary>
        internal static Vector2 Pos(float x, float y)
        {
            return new Vector2(x - 540f, 960f - y);
        }

        /// <summary>스프라이트를 읽는다. <b>없으면 멈춘다</b> — null 로 구우면 흰 사각형이 조용히 저장된다.</summary>
        internal static Sprite LoadSprite(string pathUnderArt)
        {
            Sprite sprite = Resources.Load<Sprite>(ArtRoot + pathUnderArt);
            if (sprite == null)
                throw new System.InvalidOperationException("[시즌패스 굽기] 스프라이트가 없다: Resources/" + ArtRoot + pathUnderArt);
            return sprite;
        }

        internal static Image Picture(Transform parent, string name, string path, Vector2 size, Vector2 position)
        {
            Image image = CreateImage(name, parent, Color.white);
            image.sprite = LoadSprite(path);
            image.raycastTarget = false;
            SetRect(image.rectTransform, size, position);
            return image;
        }

        /// <summary>9슬라이스 판. 테두리는 스프라이트 .meta 의 것을 쓰고 여기서는 배율만 준다.</summary>
        internal static Image Sliced(Transform parent, string name, string path, float pixelScale,
            Vector2 size, Vector2 position, Color color)
        {
            Image image = Picture(parent, name, path, size, position);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / pixelScale;
            image.color = color;
            return image;
        }

        /// <summary>
        /// 그림 버튼. 꺼졌을 때 어둡게 덮지 않는다 — 바탕이 밝은 그림이라 덮으면 글자까지
        /// 같이 어두워져 안 읽힌다. 상태는 그림·글자 색으로 직접 말한다.
        /// </summary>
        internal static Button SpriteButton(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;
            return button;
        }

        internal static Image Solid(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            Image image = CreateImage(name, parent, color);
            image.raycastTarget = false;
            SetRect(image.rectTransform, new Vector2(x1 - x0, y1 - y0), Pos((x0 + x1) * 0.5f, (y0 + y1) * 0.5f));
            return image;
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
