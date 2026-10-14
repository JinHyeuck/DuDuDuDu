using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.Shop
{
    /// <summary>
    /// 카드형 상품 한 장. 보석뽑기 상자(8.2)와 다이스석(8.6)이 함께 쓴다.
    ///
    /// 둘 다 "제목 + 아트 + 부제 두 줄 + 버튼 두 개" 구조다. 뽑기는 [1회][10회],
    /// 다이스석은 [구매] 하나만 쓰고 두 번째를 끈다 — 버튼 수가 다르다고 카드를 둘로
    /// 나누면 <b>두 섹션의 카드 높이가 갈라져</b> 나란히 놓았을 때 어긋나 보인다.
    /// </summary>
    public sealed class UIShopOfferCard : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image artImage;

        /// <summary>등급 구간(뽑기) 또는 수량(다이스석).</summary>
        [SerializeField] private TMP_Text captionText;

        /// <summary>확률 안내(뽑기) 또는 "오늘 구매 0 / 3"(다이스석).</summary>
        [SerializeField] private TMP_Text footnoteText;

        /// <summary>
        /// 등장 등급 아이콘 줄. 보석뽑기 상자만 쓴다 — 등급 구간을 글자
        /// ("Common ~ Rare")로만 적으면 <b>무엇이 나오는지가 안 보인다</b>.
        /// 다이스석 카드는 비워 둔 채 꺼진다.
        /// </summary>
        [SerializeField] private Image[] rarityIcons = new Image[RarityIconSlots];

        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TMP_Text secondaryLabel;

        private Action onPrimary;
        private Action onSecondary;

        private void Awake()
        {
            if (primaryButton != null)
                primaryButton.onClick.AddListener(HandlePrimary);
            if (secondaryButton != null)
                secondaryButton.onClick.AddListener(HandleSecondary);
        }

        private void OnDestroy()
        {
            if (primaryButton != null)
                primaryButton.onClick.RemoveListener(HandlePrimary);
            if (secondaryButton != null)
                secondaryButton.onClick.RemoveListener(HandleSecondary);
        }

        /// <summary>
        /// 카드를 채운다. <paramref name="secondary"/> 가 null 이면 두 번째 버튼을 끄고
        /// 첫 번째를 가운데로 넓힌다.
        /// </summary>
        /// <param name="cardBackground">
        /// 카드 바탕 스프라이트. 시안이 상자마다 색을 달리한다(일반 청록 · 영웅 보라).
        /// 비어 있으면 구워진 바탕 그대로 둔다.
        /// </param>
        public void Bind(
            string title, Sprite art, string caption, string footnote,
            string primaryText, bool primaryEnabled, Action primary,
            string secondaryText, bool secondaryEnabled, Action secondary,
            Sprite cardBackground = null)
        {
            if (background != null && cardBackground != null)
                background.sprite = cardBackground;

            onPrimary = primary;
            onSecondary = secondary;

            if (titleText != null)
                titleText.SetText(title);

            if (artImage != null)
            {
                // 아트가 없으면 흰 사각형이 뜬다. 색으로 눕히지 말고 통째로 끈다 —
                // 시안의 상자 자리는 비면 비는 것이 맞고, 회색 네모가 서면 그것이 상품처럼 보인다.
                artImage.sprite = art;
                artImage.enabled = art != null;

                if (art != null)
                    artImage.rectTransform.sizeDelta = art.rect.size * GemBoxArtScale;
            }

            if (captionText != null)
                captionText.SetText(caption);

            if (footnoteText != null)
                footnoteText.SetText(footnote);

            ApplyButton(primaryButton, primaryLabel, primaryText, primaryEnabled, UIShopUIFactory.Accent);

            bool hasSecondary = secondary != null && !string.IsNullOrEmpty(secondaryText);
            if (secondaryButton != null)
                secondaryButton.gameObject.SetActive(hasSecondary);

            if (hasSecondary)
                ApplyButton(secondaryButton, secondaryLabel, secondaryText, secondaryEnabled, UIShopUIFactory.Accent);

            // 버튼이 하나뿐이면 카드 폭을 다 쓴다. 반쪽짜리 버튼이 왼쪽에 홀로 남으면
            // 오른쪽이 비어 "버튼이 하나 빠진 카드"로 읽힌다.
            LayoutButtons(this, hasSecondary);
        }

        /// <summary>
        /// 등급 아이콘을 채운다. 남는 칸은 끈다. <paramref name="sprites"/> 가 null 이면
        /// 줄 전체가 꺼진다(다이스석 카드).
        /// </summary>
        public void SetRarityIcons(System.Collections.Generic.IReadOnlyList<Sprite> sprites)
        {
            if (rarityIcons == null)
                return;

            for (int i = 0; i < rarityIcons.Length; i++)
            {
                Image slot = rarityIcons[i];
                if (slot == null)
                    continue;

                Sprite sprite = sprites != null && i < sprites.Count ? sprites[i] : null;
                slot.sprite = sprite;

                // 스프라이트 없는 Image 는 흰 사각형으로 그려진다. 통째로 끈다.
                slot.enabled = sprite != null;
            }
        }

        private static void ApplyButton(Button button, TMP_Text label, string text, bool enabled, Color color)
        {
            if (label != null)
                label.SetText(text);

            if (button == null)
                return;

            // 색을 곱하지 않는다. 시안의 버튼은 스프라이트이고, 못 누르는 상태는
            // Button 의 disabledColor 가 흐리게 처리한다.
            button.interactable = enabled;
        }

        private void HandlePrimary() => onPrimary?.Invoke();
        private void HandleSecondary() => onSecondary?.Invoke();

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        /// <summary>시안 실측. <c>Shop_DiaBox_Btn</c> 을 x3 배 한 크기다.</summary>
        internal static readonly Vector2 CardSize = new Vector2(491f, 465f);

        internal static float CardHeight => CardSize.y;

        /// <summary>등급 아이콘 칸 수. 뽑기 상자의 등급 구간이 최대 3단계다.</summary>
        public const int RarityIconSlots = 3;

        /// <summary>버튼 둘을 나란히 놓을 때의 크기·간격. 시안의 영웅 상자(187 + 242)를 고르게 나눈 값이다.</summary>
        private static readonly Vector2 PairButtonSize = new Vector2(212f, 100f);

        /// <summary>버튼 하나만 쓸 때. 시안의 일반 상자 "무료" 버튼이 440x100 이다.</summary>
        private static readonly Vector2 SoloButtonSize = new Vector2(440f, 100f);

        private const float ButtonCenterY = 1583f;

        /// <summary>상자 그림 배율. DiaBox 는 64x64 에 그림이 55x41 이라 x5 가 시안의 275x205 다.</summary>
        private const float GemBoxArtScale = 5f;

        /// <summary>
        /// 에디터 굽기 전용. 시안 <c>Art/Layout/Shop_PackageDiaBox_Layout.png</c> 의 보석 보물상자다.
        ///
        /// <b>버튼 수에 따라 폭이 달라진다.</b> 시안은 일반 상자가 넓은 버튼 하나,
        /// 영웅 상자가 좁은 버튼 둘이다. 템플릿은 하나뿐이므로 둘 다 만들어 두고
        /// <see cref="LayoutButtons"/> 가 쓰는 쪽에 맞춰 폭을 바꾼다 —
        /// 좁은 버튼 하나만 남겨 두면 <b>혼자 왼쪽에 치우쳐</b> 선다.
        /// </summary>
        internal static UIShopOfferCard Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIShopUIFactory.CreateRect("OfferCard", parent);
            UIShopUIFactory.SetRect(root.GetComponent<RectTransform>(), CardSize, Vector2.zero);

            // <b>가로 레이아웃 그룹 안에서는 크기를 스스로 밝혀야 한다.</b> 안 그러면 선호 높이가
            // 0 으로 읽혀 카드가 납작해지고, 바탕 스프라이트(768)만 제자리에 남아
            // <b>위아래 섹션을 덮는다</b> — 머리글 바가 사라진 것처럼 보인다.
            LayoutElement size = root.AddComponent<LayoutElement>();
            size.preferredWidth = CardSize.x;
            size.preferredHeight = CardSize.y;
            size.flexibleHeight = 0f;

            var card = root.AddComponent<UIShopOfferCard>();
            Transform p = root.transform;

            card.background = UIShopUIFactory.CreateSpriteAt(
                "Bg", p, UIShopUIFactory.Art("Shop/Shop_DiaBox_Btn"), UIShopUIFactory.KitScale, Vector2.zero);

            card.titleText = UIShopUIFactory.CreateText("Title", p, "일반 보석상자", 45f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.SetRect(card.titleText.rectTransform, new Vector2(420f, 60f), Local(283.5f, 1253.5f));

            // 상자 그림. DiaBox 는 64x64 에 그림이 55x41 이라 x5 배가 시안의 275x205 다.
            card.artImage = UIShopUIFactory.CreateSpriteAt(
                "Art", p, UIShopUIFactory.Art("Shop/DiaBox_1"), 5f, Local(274.5f, 1420f));

            // 등급 구간과 확률은 시안에 자리가 없다. 제목 바로 아래 한 줄로 작게 넣는다 —
            // 기획서 8.2-4 가 "확률 상시 노출"을 요구해서 지울 수는 없고, 상자 그림 위에
            // 겹쳐 놓으면 둘 다 안 읽힌다. <b>두 줄이 아니라 한 줄</b>이라
            // <see cref="footnoteText"/> 는 만들지 않고 호출부가 합쳐서 보낸다.
            card.captionText = UIShopUIFactory.CreateText("Caption", p, "커먼 ~ 레어 · 확률 미정", 30f,
                TextAlignmentOptions.Center, UIShopUIFactory.NoticeColor, font);
            UIShopUIFactory.SetRect(card.captionText.rectTransform, new Vector2(470f, 40f), Local(275.5f, 1296f));

            // 등급 아이콘 줄은 시안에 없다. 필드만 남겨 둔다(칸을 만들면 시안에 없는 줄이 하나 는다).
            card.rarityIcons = new Image[RarityIconSlots];

            card.primaryButton = UIShopUIFactory.CreateKitButton(
                "Primary", p, "Ingame/Big_Btn_Yellow", PairButtonSize, Vector2.zero,
                "1회", 32f, Color.white, font);
            card.primaryLabel = card.primaryButton.GetComponentInChildren<TMP_Text>();

            card.secondaryButton = UIShopUIFactory.CreateKitButton(
                "Secondary", p, "Ingame/Big_Btn_Green", PairButtonSize, Vector2.zero,
                "10회", 32f, Color.white, font);
            card.secondaryLabel = card.secondaryButton.GetComponentInChildren<TMP_Text>();

            LayoutButtons(card, both: true);
            return card;
        }

        /// <summary>
        /// 버튼 자리를 잡는다. 굽기와 <see cref="Bind"/> 가 같은 함수를 쓴다 —
        /// 둘이 각자 좌표를 들면 데이터에 따라 <b>버튼이 겹치거나 치우친</b> 카드가 나온다.
        /// </summary>
        private static void LayoutButtons(UIShopOfferCard card, bool both)
        {
            if (card.primaryButton == null)
                return;

            var primary = (RectTransform)card.primaryButton.transform;
            Vector4 pad = UIShopUIFactory.ButtonPad(UIShopUIFactory.KitScale);
            float padX = pad.x + pad.z;
            float padY = pad.y + pad.w;
            float offsetY = -(pad.y - pad.w) * 0.5f;

            if (both)
            {
                float half = PairButtonSize.x * 0.5f + 16f;
                primary.sizeDelta = new Vector2(PairButtonSize.x + padX, PairButtonSize.y + padY);
                primary.anchoredPosition = Local(275.5f - half, ButtonCenterY) + new Vector2(0f, offsetY);

                if (card.secondaryButton != null)
                {
                    var secondary = (RectTransform)card.secondaryButton.transform;
                    secondary.sizeDelta = primary.sizeDelta;
                    secondary.anchoredPosition = Local(275.5f + half, ButtonCenterY) + new Vector2(0f, offsetY);
                }

                return;
            }

            primary.sizeDelta = new Vector2(SoloButtonSize.x + padX, SoloButtonSize.y + padY);
            primary.anchoredPosition = Local(275.5f, ButtonCenterY) + new Vector2(0f, offsetY);
        }

        /// <summary>
        /// PSD 절대 좌표 → 카드 안쪽 좌표. 왼쪽 카드가 (30,1208)-(521,1673) 이라 중심이 (275.5, 1440.5) 다.
        /// 오른쪽 카드는 같은 카드를 옮긴 것이라 좌표를 따로 재지 않는다.
        /// </summary>
        private static Vector2 Local(float psdX, float psdY)
        {
            return new Vector2(psdX - 275.5f, 1440.5f - psdY);
        }
    }
}
