using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.Shop
{
    /// <summary>
    /// 일일상점 슬롯 한 칸. (기획서 8.3)
    ///
    /// <b>산 슬롯을 비우지 않는다.</b> SOLD 오버레이로 덮어 자리에 남긴다 — 빈 칸이 생기면
    /// "오늘 상점이 끝났다"는 인상을 줘서 <b>남은 슬롯을 안 본다</b>는 것이 기획서의 근거다.
    /// </summary>
    public sealed class UIShopDailySlot : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardNameText;
        [SerializeField] private TMP_Text rewardAmountText;
        [SerializeField] private TMP_Text costText;

        /// <summary>정가에 취소선을 그은 줄. 할인 슬롯에서만 켠다.</summary>
        [SerializeField] private TMP_Text originalCostText;

        [SerializeField] private GameObject discountBadge;
        [SerializeField] private TMP_Text discountText;

        /// <summary>구매 완료 오버레이. 슬롯을 덮되 <b>내용은 비치게</b> 둔다.</summary>
        [SerializeField] private GameObject soldOverlay;

        [SerializeField] private Button button;

        private Action onClick;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void Bind(ShopDatabase.DailyOffer offer, bool sold, bool affordable, Action clickCallback)
        {
            onClick = clickCallback;

            if (offer == null)
                return;

            if (rewardIcon != null)
            {
                Sprite icon = PointRewardUtility.GetPointIcon(offer.reward.pointType);
                rewardIcon.sprite = icon;
                rewardIcon.enabled = icon != null;
            }

            if (rewardNameText != null)
                rewardNameText.SetText(PointRewardUtility.GetPointName(offer.reward.pointType));

            // SetText 의 {0} 은 숫자만 받는다. 문자열을 끼우려면 먼저 합쳐야 한다.
            if (rewardAmountText != null)
                rewardAmountText.SetText("x" + ShopText.FormatAmount(offer.reward.amount));

            int cost = ShopPurchaseManager.DiscountedCost(offer.cost, offer.discountPercent);
            bool discounted = offer.discountPercent > 0;

            if (costText != null)
            {
                costText.SetText(PointRewardUtility.GetPointName(offer.costType) + " " +
                                 ShopText.FormatAmount(cost));

                costText.color = affordable || sold ? UIShopUIFactory.GoldText : UIShopUIFactory.DangerText;
            }

            if (originalCostText != null)
            {
                originalCostText.gameObject.SetActive(discounted);
                if (discounted)
                    originalCostText.SetText("<s>" + ShopText.FormatAmount(offer.cost) + "</s>");
            }

            if (discountBadge != null)
                discountBadge.SetActive(discounted);

            if (discountText != null && discounted)
                discountText.SetText("-{0}%", offer.discountPercent);

            if (soldOverlay != null)
                soldOverlay.SetActive(sold);

            if (button != null)
                button.interactable = !sold && affordable;

            if (background != null)
                background.color = UIShopUIFactory.CardColor;
        }

        private void HandleClick()
        {
            onClick?.Invoke();
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        internal const float SlotHeight = 300f;

        /// <summary>에디터 굽기 전용.</summary>
        internal static UIShopDailySlot Create(Transform parent, TMP_FontAsset font)
        {
            Image background = UIShopUIFactory.CreateImage("DailySlot", parent, UIShopUIFactory.CardColor);

            var slot = background.gameObject.AddComponent<UIShopDailySlot>();
            slot.background = background;
            Transform p = background.transform;

            slot.button = background.gameObject.AddComponent<Button>();
            slot.button.targetGraphic = background;

            slot.rewardIcon = UIShopUIFactory.CreateImage("Icon", p, Color.white);
            UIShopUIFactory.SetRect(slot.rewardIcon.rectTransform, new Vector2(112f, 112f), new Vector2(0f, 58f));
            slot.rewardIcon.raycastTarget = false;

            slot.rewardNameText = UIShopUIFactory.CreateText("Name", p, "소환권", 24f,
                TextAlignmentOptions.Center, UIShopUIFactory.MutedText, font);
            UIShopUIFactory.SetRect(slot.rewardNameText.rectTransform, new Vector2(280f, 30f), new Vector2(0f, -26f));

            slot.rewardAmountText = UIShopUIFactory.CreateText("Amount", p, "x5", 34f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.SetRect(slot.rewardAmountText.rectTransform, new Vector2(280f, 42f), new Vector2(0f, -62f));

            slot.originalCostText = UIShopUIFactory.CreateText("OriginalCost", p, "", 22f,
                TextAlignmentOptions.Center, UIShopUIFactory.MutedText, font);
            UIShopUIFactory.SetRect(slot.originalCostText.rectTransform, new Vector2(280f, 28f), new Vector2(0f, -96f));

            slot.costText = UIShopUIFactory.CreateText("Cost", p, "무료젬 50", 28f,
                TextAlignmentOptions.Center, UIShopUIFactory.GoldText, font);
            UIShopUIFactory.SetRect(slot.costText.rectTransform, new Vector2(280f, 36f), new Vector2(0f, -124f));

            // 할인 배지. 왼쪽 위 모서리 — 등급 프레임(추후)과 겹치지 않는 자리다.
            Image badge = UIShopUIFactory.CreateImage("DiscountBadge", p, UIShopUIFactory.DangerText);
            UIShopUIFactory.SetRect(badge.rectTransform, new Vector2(88f, 44f), new Vector2(-104f, 118f));
            badge.raycastTarget = false;
            slot.discountBadge = badge.gameObject;

            slot.discountText = UIShopUIFactory.CreateText("Text", badge.transform, "-20%", 24f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.Stretch(slot.discountText.rectTransform);

            // SOLD. 반투명이라 아래 내용이 비친다 — 무엇을 샀는지 남아야 한다.
            Image sold = UIShopUIFactory.CreateImage("SoldOverlay", p, UIShopUIFactory.SoldOverlay);
            UIShopUIFactory.Stretch(sold.rectTransform);
            sold.raycastTarget = false;
            slot.soldOverlay = sold.gameObject;

            TMP_Text soldLabel = UIShopUIFactory.CreateText("Label", sold.transform, "SOLD", 48f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.Stretch(soldLabel.rectTransform);

            return slot;
        }
    }
}
