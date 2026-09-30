using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Utils;

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

        /// <summary>
        /// 게이지 채움 비율을 먹인다. <b>Filled</b> 이미지면 fillAmount 로, 옛 방식(폭을 줄이는
        /// 채움)이면 오른쪽 가장자리로 먹인다 — 결과 화면은 아직 옛 방식이다.
        /// </summary>
        internal static void SetGauge(Image fill, float trackWidth, float ratio01)
        {
            if (fill == null)
                return;

            if (fill.type == Image.Type.Filled)
            {
                fill.fillAmount = Mathf.Clamp01(ratio01);
                return;
            }

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

        // ── 아트 시안 (Art/Layout/Infinity_Layout*.png) ─────────────────────
        //
        // 층 선택·편성 두 화면은 성 안의 한 장면이다. 두 화면이 같은 틀(뒷벽·성 프레임·
        // 횃불·타이틀·뒤로 버튼)을 쓰므로 그 틀을 여기서 한 번만 만든다.
        //
        // <b>좌표는 시안 픽셀 그대로 적는다.</b> 캔버스가 1080x1920 폭 맞춤이라 시안 좌표가
        // 곧 화면 좌표이고, <see cref="Pos"/> 가 그것을 중앙 기준 anchoredPosition 으로 바꾼다.
        // 옮겨 적을 때 뺄셈을 손으로 하면 그 자리에서 오차가 난다.

        internal const string ArtRoot = "Art/";

        internal static readonly Color ScreenBase = Hex(0x1D1C30);
        internal static readonly Color TextMuted = Hex(0xAAAACC);
        internal static readonly Color TextGold = Hex(0xFFDE00);
        internal static readonly Color TextCyan = Hex(0x00F6FF);
        internal static readonly Color RewardBoxColor = new Color(0.14f, 0.13f, 0.24f, 1f);

        /// <summary>
        /// 패널이 놓이는 세로 축. 시안의 패널·카드·버튼이 전부 x=550 을 중심으로 대칭이다
        /// (성 프레임 그림 자체가 오른쪽으로 10px 치우쳐 있다). 540 으로 두면 패널이
        /// 프레임 기둥 안쪽에서 한쪽으로 쏠려 보인다.
        /// </summary>
        internal const float PanelCenterX = 550f;

        /// <summary>패널·카드의 <b>보이는</b> 폭. 시안 x169~932.</summary>
        internal const float PanelWidth = 764f;

        // ── 스프라이트 투명 여백 ──────────────────────────────────────────
        //
        // 킷 스프라이트는 32px 안에 그림이 조금 작게 들어 있다(패널 사방 3px, 버튼 좌우 4·위 6·
        // 아래 2px). 시안에서 잰 것은 <b>보이는 외곽선</b>이므로 Image 크기는 그 여백 x 배율만큼
        // 더 커야 한다. 이것을 빼먹으면 모든 패널·버튼이 한 둘레씩 작게 나온다.

        /// <summary>Infinity_Popup_Bg_* · Number_* 의 여백(3px) x4.</summary>
        internal const float PanelPad = 12f;

        /// <summary>보이는 패널 크기 → Image 크기.</summary>
        internal static Vector2 PanelRect(float visibleWidth, float visibleHeight)
        {
            return new Vector2(visibleWidth + PanelPad * 2f, visibleHeight + PanelPad * 2f);
        }

        /// <summary>
        /// 버튼(Btn_Gray · Big_Btn_Yellow · Btn_PurpleGray)의 보이는 사각형 → Image 크기·중심.
        /// 여백이 위(6px)가 아래(2px)보다 커서 중심이 (6-2)/2 x 배율 만큼 위로 간다.
        /// </summary>
        internal static void ButtonRect(
            float visibleCenterX, float visibleCenterY, float visibleWidth, float visibleHeight, float pixelScale,
            out Vector2 size, out Vector2 position)
        {
            size = new Vector2(visibleWidth + 8f * pixelScale, visibleHeight + 8f * pixelScale);
            position = Pos(visibleCenterX, visibleCenterY - 2f * pixelScale);
        }

        internal static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>시안 좌표(좌상단 원점, 1080x1920) → 화면 중앙 기준 anchoredPosition.</summary>
        internal static Vector2 Pos(float designX, float designY)
        {
            return new Vector2(designX - 540f, 960f - designY);
        }

        /// <summary>
        /// 스프라이트를 읽는다. <b>없으면 멈춘다.</b> 굽기가 null 스프라이트로 끝나면
        /// 흰 사각형만 가득한 프리팹이 조용히 저장되고, 그것은 실행해 보기 전에는 모른다.
        /// </summary>
        internal static Sprite LoadSprite(string pathUnderArt)
        {
            Sprite sprite = Resources.Load<Sprite>(ArtRoot + pathUnderArt);
            if (sprite == null)
                throw new InvalidOperationException("[탑 굽기] 스프라이트가 없다: Resources/" + ArtRoot + pathUnderArt);
            return sprite;
        }

        /// <summary>
        /// 스프라이트 이미지 한 장. <paramref name="pixelScale"/> 는 시안의 "x4배" 다.
        /// 9슬라이스면 테두리가 그 배율로 그려지고(<c>pixelsPerUnitMultiplier = 1/배율</c>,
        /// 이 프로젝트의 다른 픽셀 UI 와 같은 방식), 가운데만 늘어난다.
        /// </summary>
        internal static Image CreateSprite(
            string name, Transform parent, Sprite sprite, float pixelScale,
            Vector2 size, Vector2 position, bool sliced)
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

        /// <summary>스프라이트 버튼 + 라벨. 시안의 버튼 셋(노랑·회청·보라회색)이 전부 이 모양이다.</summary>
        internal static Button CreateSpriteButton(
            string name, Transform parent, Sprite sprite, float pixelScale, Vector2 size, Vector2 position,
            string label, float fontSize, Color textColor, TMP_FontAsset font)
        {
            Image image = CreateSprite(name, parent, sprite, pixelScale, size, position, true);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            if (label != null)
            {
                TMP_Text text = CreateText("Label", image.transform, label, fontSize,
                    TextAlignmentOptions.Center, textColor, font);

                // Image 중심은 위 여백 때문에 보이는 버튼보다 조금 위에 있고, 보이는 버튼의
                // 윗면은 아래 턱 때문에 또 조금 위에 있다. 둘이 거의 상쇄돼 가운데에 둔다.
                SetRect(text.rectTransform, size, Vector2.zero);
            }

            return button;
        }

        /// <summary>
        /// 성 화면의 틀. 두 화면(층 선택·편성)이 같은 것을 쓴다.
        ///
        /// 반환하는 뒤로 버튼은 <c>DialogBase.AddExitButton</c> 에 물려야 한다 —
        /// 이 틀이 전체 화면을 덮으므로 바깥을 눌러 닫는 길이 없다.
        /// </summary>
        internal static void CreateCastleScreen(
            Transform parent, TMP_FontAsset font, out TMP_Text title, out Button backButton)
        {
            // 바탕. 레이캐스트를 켜서 뒤의 로비 버튼이 눌리지 않게 막는다.
            Image baseFill = CreateImage("Base", parent, ScreenBase);
            Stretch(baseFill.rectTransform);

            // 뒷벽과 성 프레임. 배율·위치는 시안과 픽셀 대조로 맞춘 값이다
            // (프레임 x3.5 에서 평균 색 오차 4.7). 시안 주석의 "x3배" 로는 폭이 모자란다.
            CreateSprite("BackWall", parent, LoadSprite("InfinityMode/Infinity_Bg_Back"), 2.35f,
                new Vector2(2406f, 2406f), Pos(540f, 863f), false);

            CreateSprite("CastleFrame", parent, LoadSprite("InfinityMode/Infinity_Bg"), 3.5f,
                new Vector2(1792f, 1792f), Pos(550f, 897f), false);

            // 횃불 여섯 개. 받침(x4)과 불꽃(x3, 다섯 장 반복). 불꽃은 시안 주석이 x2 지만
            // 픽셀 대조로는 x3 이다(오차 1.4).
            Sprite holder = LoadSprite("InfinityMode/Dungeon_Fire_Bg");
            var flames = new Sprite[5];
            for (int i = 0; i < flames.Length; i++)
                flames[i] = LoadSprite("InfinityMode/Dungeon_Fire_" + i);

            Vector2[] holders =
            {
                new Vector2(83f, 486f), new Vector2(96f, 919f), new Vector2(83f, 1335f),
                new Vector2(1007f, 486f), new Vector2(1002f, 919f), new Vector2(1007f, 1335f),
            };

            for (int i = 0; i < holders.Length; i++)
            {
                Vector2 h = holders[i];
                CreateSprite("TorchHolder" + i, parent, holder, 4f, new Vector2(128f, 128f), Pos(h.x, h.y), false);

                Image flame = CreateSprite("TorchFlame" + i, parent, flames[0], 3f,
                    new Vector2(138f, 135f), Pos(h.x - 1f, h.y - 79.5f), false);

                // 여섯 개가 같은 박자로 깜빡이면 기계처럼 보인다. 시작 장을 엇갈린다.
                var flipbook = flame.gameObject.AddComponent<UIImageFlipbook>();
                flipbook.BakeSetup(flames, 0.12f, i % flames.Length);
            }

            title = CreateText("Title", parent, "무한의 탑", 45f, TextAlignmentOptions.Center, Color.white, font);
            SetRect(title.rectTransform, new Vector2(460f, 80f), Pos(540f, 180f));

            // 뒤로 — 보이는 버튼 x12~175 · y1735~1903 (x5), 화살표 x4.
            ButtonRect(93.5f, 1819f, 164f, 169f, 5f, out Vector2 backSize, out Vector2 backPos);
            backButton = CreateSpriteButton("Back", parent, LoadSprite("Ingame/Btn_Gray"), 5f,
                backSize, backPos, null, 0f, Color.white, font);
            CreateSprite("Icon", backButton.transform, LoadSprite("Main/Icon_Back"), 4f,
                new Vector2(128f, 128f), Vector2.zero, false);
        }

        /// <summary>
        /// "보상 목록" 오버레이. 층 선택과 편성 두 화면이 같은 것을 띄운다 —
        /// 내용을 만드는 곳도 <see cref="TowerRewardListText"/> 하나다.
        /// </summary>
        internal static GameObject CreateRewardOverlay(
            Transform parent, TMP_FontAsset font, out TMP_Text body, out Button closeButton)
        {
            GameObject overlay = CreateRect("RewardOverlay", parent);
            Stretch(overlay.GetComponent<RectTransform>());

            Image dim = CreateImage("Dim", overlay.transform, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);
            var block = dim.gameObject.AddComponent<Button>();
            block.targetGraphic = dim;
            block.transition = Selectable.Transition.None;

            CreateSprite("Panel", overlay.transform, LoadSprite("InfinityMode/Infinity_Popup_Bg_Dark"), 4f,
                PanelRect(PanelWidth, 1200f), Pos(PanelCenterX, 880f), true);

            TMP_Text overlayTitle = CreateText("Title", overlay.transform, "보상 목록", 45f,
                TextAlignmentOptions.Center, Color.white, font);
            SetRect(overlayTitle.rectTransform, new Vector2(700f, 70f), Pos(PanelCenterX, 350f));

            body = CreateText("Body", overlay.transform, string.Empty, 30f,
                TextAlignmentOptions.TopLeft, Color.white, font);
            SetRect(body.rectTransform, new Vector2(680f, 900f), Pos(PanelCenterX, 860f));
            body.textWrappingMode = TextWrappingModes.Normal;

            ButtonRect(PanelCenterX, 1574f, 360f, 150f, 3f, out Vector2 closeSize, out Vector2 closePos);
            closeButton = CreateSpriteButton("Close", overlay.transform, LoadSprite("Ingame/Btn_Gray"), 3f,
                closeSize, closePos, "닫기", 45f, Color.white, font);

            overlay.SetActive(false);
            return overlay;
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
