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

        // ── 창 색. 전부 PSD(Art/0PSD/퀘스트.psd) 실측·주석이다 ──────────────

        /// <summary>안쪽 큰 판(SmallBox x4)의 색.</summary>
        internal static readonly Color InnerBoxColor = Hex(0x636481);

        /// <summary>갱신 타이머 칸·진행 게이지 바탕의 색.</summary>
        internal static readonly Color DarkBoxColor = Hex(0x45465f);

        /// <summary>게이지 채움(시안 78fe04 근처의 연두).</summary>
        internal static readonly Color LimeColor = Hex(0x78fe04);

        /// <summary>선택 안 된 탭의 글자색(시안 주석 c3c4e2).</summary>
        internal static readonly Color TabOffTextColor = Hex(0xc3c4e2);

        /// <summary>문턱을 넘긴 칸 밑 숫자(시안 주석 fcf900).</summary>
        internal static readonly Color ReachedNumberColor = Hex(0xfcf900);

        /// <summary>줄 아이콘 뒤 Mail_Mark 틴트(시안 주석 c4b5e3).</summary>
        internal static readonly Color MailMarkTint = Hex(0xc4b5e3);

        internal static readonly Color Black = new Color(0f, 0f, 0f, 1f);

        /// <summary>받은 줄·칸을 눌러 두는 색.</summary>
        internal static readonly Color DoneTint = new Color(0.62f, 0.62f, 0.66f, 1f);

        private const string ArtRoot = "Art/";

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
                throw new System.InvalidOperationException("[퀘스트 굽기] 스프라이트가 없다: Resources/" + ArtRoot + pathUnderArt);
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

        /// <summary>그림 버튼. 꺼졌을 때 어둡게 덮지 않는다 — 글자까지 같이 어두워져 안 읽힌다.</summary>
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

        /// <summary>
        /// 칸 밖으로 넘치지 않는 글자. 보상 수량처럼 <b>자릿수가 데이터에 달린</b> 값에 쓴다.
        ///
        /// <b>칸을 넓히는 것으로는 못 막는다.</b> 30000 은 들어가도 2000000 은 안 들어가고,
        /// 그때 TMP 는 글자를 칸 밖으로 흘려보낸다 — 옆 줄 위에 숫자가 겹쳐 찍힌다.
        /// 자동 축소는 자릿수가 늘어나는 만큼 글자가 작아져서 그 경로가 아예 없다.
        /// </summary>
        internal static TMP_Text CreateFittedText(
            string name, Transform parent, string text, float maxSize, float minSize,
            TextAlignmentOptions align, Color color, TMP_FontAsset font)
        {
            TMP_Text label = CreateText(name, parent, text, maxSize, align, color, font);

            // <b>줄바꿈을 꺼야 가로로 줄어든다.</b> TMP 의 자동 축소는 글자가 칸의 <i>세로</i>를
            // 넘칠 때 작아진다. 줄바꿈이 켜져 있으면 "30000" 처럼 한 덩이인 글자는 줄을 못 바꿔
            // 가로로만 넘치고, 세로는 멀쩡하니 축소가 아예 돌지 않는다 — 그대로 잘려 나간다.
            // (첫 굽기에서 30000 이 "3000" 으로 잘린 것이 이것이다.)
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
            return CreateGauge(name, parent, size, position, fillColor, TrackColor, 0f);
        }

        /// <summary>
        /// 테두리가 있는 게이지. <paramref name="border"/> 가 0 보다 크면 바깥에 검은 틀이 서고
        /// 트랙·채움은 그 안쪽에 앉는다 — <b>채움이 달릴 폭은 size.x - 2 x border</b> 이므로
        /// 비율을 먹일 때(<see cref="SetGauge"/>)는 그 안쪽 폭을 넘겨야 한다.
        /// </summary>
        internal static Image CreateGauge(
            string name, Transform parent, Vector2 size, Vector2 position,
            Color fillColor, Color trackColor, float border)
        {
            Transform trackParent = parent;
            if (border > 0f)
            {
                Image frame = CreateImage(name + "Frame", parent, Black);
                SetRect(frame.rectTransform, size, position);
                frame.raycastTarget = false;
                trackParent = frame.transform;
                position = Vector2.zero;
                size = new Vector2(size.x - 2f * border, size.y - 2f * border);
            }

            Image track = CreateImage(name, trackParent, trackColor);
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
            string name, Transform parent, Vector2 size, Vector2 position, float spacing, int topPadding = 0)
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
            layout.padding = new RectOffset(0, 0, topPadding, 0);
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
