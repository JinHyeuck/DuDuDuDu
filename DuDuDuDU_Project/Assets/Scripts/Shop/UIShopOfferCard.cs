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
        [SerializeField] private Image edge;
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
        public void Bind(
            string title, Sprite art, string caption, string footnote,
            string primaryText, bool primaryEnabled, Action primary,
            string secondaryText, bool secondaryEnabled, Action secondary)
        {
            onPrimary = primary;
            onSecondary = secondary;

            if (titleText != null)
                titleText.SetText(title);

            if (artImage != null)
            {
                artImage.sprite = art;

                // 아트가 없으면 흰 사각형이 뜬다. 아직 이미지가 없는 상품이 대부분이라
                // (기획서 10장) 없을 때는 카드색으로 눕혀 자리만 남긴다.
                artImage.color = art != null ? Color.white : UIShopUIFactory.RowColor;
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

            if (primaryButton != null)
            {
                // 버튼이 하나뿐이면 카드 폭을 다 쓴다. 반쪽짜리 버튼이 왼쪽에 홀로 남으면
                // 오른쪽이 비어 "버튼이 하나 빠진 카드"로 읽힌다.
                RectTransform rect = primaryButton.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(hasSecondary ? 0.06f : 0.12f, 0f);
                rect.anchorMax = new Vector2(hasSecondary ? 0.48f : 0.88f, 0f);
                rect.offsetMin = new Vector2(0f, 24f);
                rect.offsetMax = new Vector2(0f, 24f + ButtonHeight);
            }

            if (background != null)
                background.color = UIShopUIFactory.CardColor;
            if (edge != null)
                edge.color = UIShopUIFactory.CardEdge;
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

            button.interactable = enabled;

            var image = button.targetGraphic as Image;
            if (image != null)
                image.color = enabled ? color : UIShopUIFactory.DisabledColor;
        }

        private void HandlePrimary() => onPrimary?.Invoke();
        private void HandleSecondary() => onSecondary?.Invoke();

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        internal const float CardHeight = 420f;

        /// <summary>등급 아이콘 칸 수. 뽑기 상자의 등급 구간이 최대 3단계다.</summary>
        public const int RarityIconSlots = 3;
        private const float ButtonHeight = 72f;

        /// <summary>에디터 굽기 전용.</summary>
        internal static UIShopOfferCard Create(Transform parent, TMP_FontAsset font)
        {
            Image edge = UIShopUIFactory.CreateImage("OfferCard", parent, UIShopUIFactory.CardEdge);

            var card = edge.gameObject.AddComponent<UIShopOfferCard>();
            card.edge = edge;

            Image background = UIShopUIFactory.CreateImage("Panel", edge.transform, UIShopUIFactory.CardColor);
            UIShopUIFactory.StretchWithMargin(background.rectTransform, 3f, 3f);
            background.rectTransform.offsetMin = new Vector2(3f, 3f);
            background.rectTransform.offsetMax = new Vector2(-3f, -3f);
            card.background = background;

            Transform p = background.transform;

            card.titleText = UIShopUIFactory.CreateText("Title", p, "일반 보석 상자", 32f,
                TextAlignmentOptions.Center, Color.white, font);
            card.titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            card.titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            card.titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
            card.titleText.rectTransform.offsetMin = new Vector2(12f, -58f);
            card.titleText.rectTransform.offsetMax = new Vector2(-12f, -14f);

            card.artImage = UIShopUIFactory.CreateImage("Art", p, UIShopUIFactory.RowColor);
            UIShopUIFactory.SetRect(card.artImage.rectTransform, new Vector2(140f, 140f), new Vector2(0f, 86f));
            card.artImage.raycastTarget = false;

            card.captionText = UIShopUIFactory.CreateText("Caption", p, "Com / Nor / Rare", 24f,
                TextAlignmentOptions.Center, UIShopUIFactory.MutedText, font);
            UIShopUIFactory.SetRect(card.captionText.rectTransform, new Vector2(360f, 32f), new Vector2(0f, 8f));

            card.footnoteText = UIShopUIFactory.CreateText("Footnote", p, "확률 보기", 22f,
                TextAlignmentOptions.Center, UIShopUIFactory.MutedText, font);
            UIShopUIFactory.SetRect(card.footnoteText.rectTransform, new Vector2(360f, 30f), new Vector2(0f, -88f));

            // 등급 아이콘 3칸. 아트 아래 가로로 나란히 둔다.
            card.rarityIcons = new Image[RarityIconSlots];
            for (int i = 0; i < RarityIconSlots; i++)
            {
                Image slot = UIShopUIFactory.CreateImage("Rarity" + i, p, Color.white);
                UIShopUIFactory.SetRect(slot.rectTransform, new Vector2(62f, 62f),
                    new Vector2((i - 1) * 72f, -38f));
                slot.raycastTarget = false;
                card.rarityIcons[i] = slot;
            }

            card.primaryButton = UIShopUIFactory.CreateButton("Primary", p, "1회",
                UIShopUIFactory.Accent, Color.white, 28f, font);
            card.primaryLabel = card.primaryButton.GetComponentInChildren<TMP_Text>();

            card.secondaryButton = UIShopUIFactory.CreateButton("Secondary", p, "10회",
                UIShopUIFactory.Accent, Color.white, 28f, font);
            card.secondaryLabel = card.secondaryButton.GetComponentInChildren<TMP_Text>();

            RectTransform secondRect = card.secondaryButton.GetComponent<RectTransform>();
            secondRect.anchorMin = new Vector2(0.52f, 0f);
            secondRect.anchorMax = new Vector2(0.94f, 0f);
            secondRect.offsetMin = new Vector2(0f, 24f);
            secondRect.offsetMax = new Vector2(0f, 24f + ButtonHeight);

            RectTransform firstRect = card.primaryButton.GetComponent<RectTransform>();
            firstRect.anchorMin = new Vector2(0.06f, 0f);
            firstRect.anchorMax = new Vector2(0.48f, 0f);
            firstRect.offsetMin = new Vector2(0f, 24f);
            firstRect.offsetMax = new Vector2(0f, 24f + ButtonHeight);

            return card;
        }
    }
}
