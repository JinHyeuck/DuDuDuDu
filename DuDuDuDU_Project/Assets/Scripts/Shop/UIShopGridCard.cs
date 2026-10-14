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
        [SerializeField] private Image icon;

        /// <summary>받는 수량. 스샷의 카드 한가운데 큰 숫자다.</summary>
        [SerializeField] private TMP_Text amountText;

        /// <summary>재화 이름. 같은 그리드에 두 종류가 섞일 때(다이스석) 없으면 구별이 안 된다.</summary>
        [SerializeField] private TMP_Text nameText;

        [SerializeField] private Button buyButton;
        [SerializeField] private Image priceIcon;
        [SerializeField] private TMP_Text priceText;

        private Action onClick;

        /// <summary>티어 그림이 없어 재화 아이콘으로 떨어질 때 쓰는 칸 크기.</summary>
        private static readonly Vector2 FallbackIconSize = new Vector2(130f, 130f);

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
        /// <param name="tierArt">
        /// 수량 티어 전용 그림(낱개 → 봉지 → 상자). 시안이 티어마다 다른 그림을 쓴다 —
        /// 재화 아이콘 하나를 세 번 그리면 <b>5 와 150 이 같은 그림</b>이 되어 티어가 안 읽힌다.
        /// 비어 있으면 재화 아이콘으로 떨어진다.
        /// </param>
        public void Bind(
            PointType rewardType, int rewardAmount,
            PointType costType, string priceLabel, bool affordable, Action clickCallback,
            Sprite tierArt = null)
        {
            onClick = clickCallback;

            bool isCash = costType == PointType.Max;

            // 티어 그림은 텍스처 한가운데 제 크기로 그려져 있다. Image 를 텍스처 크기로 맞추면
            // 투명 여백째로 올라가 <b>그림이 시안 크기 그대로</b> 나온다 — 늘이거나 줄이지 않는다.
            // 재화 아이콘으로 떨어질 때만 고정 칸에 맞춘다.
            if (tierArt != null)
            {
                SetIcon(icon, tierArt);
                if (icon != null)
                {
                    icon.preserveAspect = false;
                    icon.rectTransform.sizeDelta = tierArt.rect.size;
                }
            }
            else
            {
                SetIcon(icon, PointRewardUtility.GetPointIcon(rewardType));
                if (icon != null)
                {
                    icon.preserveAspect = true;
                    icon.rectTransform.sizeDelta = FallbackIconSize;
                }
            }

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

            // 버튼 색을 코드로 칠하지 않는다 — 노란 버튼은 스프라이트(Big_Btn_Yellow)이고,
            // 거기에 색을 곱하면 <b>시안에 없는 색</b>이 된다. 못 사는 상태는 Button 의
            // disabledColor 가 흐리게 처리한다.
            if (buyButton != null)
                buyButton.interactable = interactable;
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

        /// <summary>
        /// 카드 한 장의 보이는 크기. 시안 실측 327x465 이고 <c>Shop_Item_Btn</c> 을 x3 배 한 것과 같다.
        /// </summary>
        internal static Vector2 CardSize => UIShopUIFactory.TierCardSize;

        internal static float CardHeight => UIShopUIFactory.TierCardSize.y;

        /// <summary>
        /// 에디터 굽기 전용. 시안(Art/Layout/Shop_Feather_Layout.png)의 카드를 그대로 옮긴다.
        ///
        /// <b>좌표는 카드 위쪽 기준으로 실측한 값이다.</b> PSD 에서 카드가 (30,706)-(357,1171) 이고
        /// 머리 숫자·그림·가격 버튼이 그 안 어디에 있는지를 재서 중심 좌표로 바꿔 적었다.
        /// </summary>
        internal static UIShopGridCard Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIShopUIFactory.CreateRect("GridCard", parent);
            UIShopUIFactory.SetRect(root.GetComponent<RectTransform>(), CardSize, Vector2.zero);

            var card = root.AddComponent<UIShopGridCard>();

            // 카드 바탕. 머리 밴드까지 한 장에 그려져 있어 9슬라이스가 아니라 통짜 x3 배다.
            card.background = UIShopUIFactory.CreateSpriteAt(
                "Bg", root.transform, UIShopUIFactory.Art("Shop/Shop_Item_Btn"),
                UIShopUIFactory.KitScale, Vector2.zero);

            // 수량. 시안은 이 숫자가 카드 제목이라 50pt 다("숫자가 제목일땐 50pt").
            card.amountText = UIShopUIFactory.CreateText("Amount", root.transform, "50", 50f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.SetRect(card.amountText.rectTransform, new Vector2(300f, 64f), new Vector2(0f, 181f));

            // 티어 그림은 배율 1 이다 — 시안의 그림 뭉치를 그대로 한 장으로 뽑은 것이라
            // 이미 화면 크기다. 크기는 Bind 가 스프라이트마다 다시 맞춘다.
            card.icon = UIShopUIFactory.CreateImage("Icon", root.transform, Color.white);
            UIShopUIFactory.SetRect(card.icon.rectTransform, FallbackIconSize, new Vector2(0f, 32f));
            card.icon.raycastTarget = false;

            // 스프라이트 없는 Image 는 흰 사각형으로 그려진다. Bind 가 켤 때까지 꺼 둔다.
            card.icon.enabled = false;

            card.buyButton = UIShopUIFactory.CreateSpriteButton(
                "Buy", root.transform, UIShopUIFactory.Art("Ingame/Big_Btn_Yellow"),
                UIShopUIFactory.PriceButtonSize, new Vector2(0f, -145f),
                string.Empty, 45f, Color.white, font);

            // CreateSpriteButton 이 만든 빈 라벨은 안 쓴다. 가격은 아래 Row 안에 들어간다.
            UnityEngine.Object.DestroyImmediate(card.buyButton.GetComponentInChildren<TMP_Text>().gameObject);

            // 지불 아이콘 + 가격을 한 줄로 가운데 모은다. 현금이면 아이콘이 꺼지는데,
            // 좌표로 박아 두면 그때 가격만 왼쪽에 치우쳐 남는다.
            var row = UIShopUIFactory.CreateRect("Row", card.buyButton.transform);
            UIShopUIFactory.Stretch(row.GetComponent<RectTransform>());
            row.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 5f);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            card.priceIcon = UIShopUIFactory.CreateImage("PriceIcon", row.transform, Color.white);
            card.priceIcon.raycastTarget = false;
            card.priceIcon.enabled = false;
            card.priceIcon.preserveAspect = true;
            var iconSize = card.priceIcon.gameObject.AddComponent<LayoutElement>();
            iconSize.preferredWidth = 52f;
            iconSize.preferredHeight = 52f;

            card.priceText = UIShopUIFactory.CreateText("Price", row.transform, "1,2000원", 45f,
                TextAlignmentOptions.Center, Color.white, font);

            return card;
        }
    }
}
