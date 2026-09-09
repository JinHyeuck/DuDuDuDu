using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.Shop
{
    /// <summary>
    /// 재화 상점의 3열 카드 한 장. 유료젬(8.4)·다이스석(8.6)·골드(8.7)가 함께 쓴다.
    ///
    /// <b>세로 줄이 아니라 카드다.</b> 레퍼런스 스샷의 재화 상점이 전부 3열 카드이고,
    /// 이유가 있다 — 같은 재화의 수량 티어(5 / 50 / 150)는 <b>나란히 놓여야 비교</b>가 된다.
    /// 세로로 늘어놓으면 스크롤하며 값을 기억해야 한다.
    ///
    /// 지불 수단은 버튼 <b>색</b>으로 구분한다(현금=금색, 게임 재화=파랑).
    /// </summary>
    public sealed class UIShopGridCard : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image edge;
        [SerializeField] private Image icon;

        /// <summary>받는 수량. 스샷의 카드 한가운데 큰 숫자다.</summary>
        [SerializeField] private TMP_Text amountText;

        /// <summary>재화 이름. 같은 그리드에 두 종류가 섞일 때(다이스석) 없으면 구별이 안 된다.</summary>
        [SerializeField] private TMP_Text nameText;

        [SerializeField] private Button buyButton;
        [SerializeField] private Image priceIcon;
        [SerializeField] private TMP_Text priceText;

        private Action onClick;

        private void Awake()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveListener(HandleClick);
        }

        /// <summary>
        /// 카드를 채운다.
        /// </summary>
        /// <param name="rewardType">받는 재화. 아이콘과 이름의 출처다.</param>
        /// <param name="rewardAmount">받는 수량.</param>
        /// <param name="costType">지불 재화. <see cref="PointType.Max"/> 면 현금이다.</param>
        /// <param name="priceLabel">버튼에 적을 가격.</param>
        /// <param name="affordable">지금 살 수 있나. 현금일 때는 무시된다.</param>
        public void Bind(
            PointType rewardType, int rewardAmount,
            PointType costType, string priceLabel, bool affordable, Action clickCallback)
        {
            onClick = clickCallback;

            bool isCash = costType == PointType.Max;

            SetIcon(icon, PointRewardUtility.GetPointIcon(rewardType));

            if (amountText != null)
                amountText.SetText(ShopText.FormatAmount(rewardAmount));

            if (nameText != null)
                nameText.SetText(PointRewardUtility.GetPointName(rewardType));

            // 현금 카드에는 지불 아이콘이 없다. "KRW" 는 글자로 적히므로 아이콘 자리를 비운다.
            SetIcon(priceIcon, isCash ? null : PointRewardUtility.GetPointIcon(costType));

            if (priceText != null)
                priceText.SetText(priceLabel);

            // 현금은 항상 누를 수 있다. 재화가 모자란 개념이 없다.
            bool interactable = isCash || affordable;

            if (buyButton != null)
            {
                buyButton.interactable = interactable;

                var image = buyButton.targetGraphic as Image;
                if (image != null)
                {
                    image.color = !interactable
                        ? UIShopUIFactory.DisabledColor
                        : (isCash ? UIShopUIFactory.CashAccent : UIShopUIFactory.Accent);
                }
            }

            if (priceText != null)
                priceText.color = isCash ? Color.black : Color.white;

            if (background != null)
                background.color = UIShopUIFactory.CardColor;
            if (edge != null)
                edge.color = UIShopUIFactory.CardEdge;
        }

        /// <summary>
        /// 스프라이트가 없는 <see cref="Image"/> 는 <b>흰 사각형</b>으로 그려져 아이콘인 척한다.
        /// 아직 아이콘이 등록되지 않은 재화가 있어(기획서 9장) 통째로 끈다.
        /// </summary>
        private static void SetIcon(Image target, Sprite sprite)
        {
            if (target == null)
                return;

            target.sprite = sprite;
            target.enabled = sprite != null;
        }

        private void HandleClick() => onClick?.Invoke();

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        internal const float CardHeight = 300f;

        /// <summary>에디터 굽기 전용.</summary>
        internal static UIShopGridCard Create(Transform parent, TMP_FontAsset font)
        {
            Image edge = UIShopUIFactory.CreateImage("GridCard", parent, UIShopUIFactory.CardEdge);

            var card = edge.gameObject.AddComponent<UIShopGridCard>();
            card.edge = edge;

            Image background = UIShopUIFactory.CreateImage("Panel", edge.transform, UIShopUIFactory.CardColor);
            UIShopUIFactory.Stretch(background.rectTransform);
            background.rectTransform.offsetMin = new Vector2(3f, 3f);
            background.rectTransform.offsetMax = new Vector2(-3f, -3f);
            card.background = background;

            Transform p = background.transform;

            card.nameText = UIShopUIFactory.CreateText("Name", p, "무료젬", 22f,
                TextAlignmentOptions.Center, UIShopUIFactory.MutedText, font);
            UIShopUIFactory.SetRect(card.nameText.rectTransform, new Vector2(280f, 28f), new Vector2(0f, 112f));

            card.icon = UIShopUIFactory.CreateImage("Icon", p, Color.white);
            UIShopUIFactory.SetRect(card.icon.rectTransform, new Vector2(110f, 110f), new Vector2(0f, 34f));
            card.icon.raycastTarget = false;

            card.amountText = UIShopUIFactory.CreateText("Amount", p, "50", 44f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.SetRect(card.amountText.rectTransform, new Vector2(280f, 52f), new Vector2(0f, -42f));

            card.buyButton = UIShopUIFactory.CreateButton("Buy", p, string.Empty,
                UIShopUIFactory.Accent, Color.white, 26f, font);
            UIShopUIFactory.SetRect(card.buyButton.GetComponent<RectTransform>(),
                new Vector2(250f, 68f), new Vector2(0f, -104f));

            // 기본 CreateButton 의 라벨은 가운데 정렬로 판을 다 쓴다. 지불 아이콘이 왼쪽에
            // 들어가야 하므로 라벨을 오른쪽으로 밀고 아이콘 자리를 낸다.
            card.priceText = card.buyButton.GetComponentInChildren<TMP_Text>();
            UIShopUIFactory.StretchWithMargin(card.priceText.rectTransform, 58f, 12f);
            card.priceText.alignment = TextAlignmentOptions.Left;

            card.priceIcon = UIShopUIFactory.CreateImage("PriceIcon", card.buyButton.transform, Color.white);
            UIShopUIFactory.SetRect(card.priceIcon.rectTransform, new Vector2(40f, 40f), new Vector2(-92f, 0f));
            card.priceIcon.raycastTarget = false;

            return card;
        }
    }
}
