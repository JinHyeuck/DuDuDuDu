using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.Shop
{
    /// <summary>
    /// 성장 패키지 대형 배너 한 장. (기획서 8.1)
    ///
    /// <b>크다는 것이 이 컴포넌트의 요구사항이다.</b> 기획서 설계규칙 1번 — 이 섹션의 목적은
    /// 정보 전달이 아니라 시선 점유이고, 작게 줄여 여러 개를 나열하지 않는다. 그래서 높이가
    /// 다른 섹션의 두 배 이상이고, 상점에 들어온 첫 화면이 이것 하나로 차야 한다(7.2-2).
    /// </summary>
    public sealed class UIShopGrowthBanner : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image artImage;
        [SerializeField] private TMP_Text titleText;

        /// <summary>구성품을 한 줄로 요약한 글. 시안이 줄 네 개를 쓰므로 지금은 만들지 않는다.</summary>
        [SerializeField] private TMP_Text contentsText;

        /// <summary>
        /// 구성품 줄. 시안은 아이콘 + 수량을 <b>네 줄</b>로 나눠 적는다 —
        /// 한 줄 요약은 "유료젬 300 + 부위별 Rare 보석 + …" 처럼 길어지면
        /// <b>무엇이 몇 개인지</b>가 글자 덩어리에 묻힌다.
        /// 데이터가 네 개보다 적으면 남는 줄은 꺼진다.
        /// </summary>
        [SerializeField] private Image[] rewardIcons = new Image[RewardRows];

        [SerializeField] private TMP_Text[] rewardAmounts = new TMP_Text[RewardRows];

        [SerializeField] private RectTransform[] rewardRows = new RectTransform[RewardRows];

        /// <summary>남은 시간. 기간 제한이 없으면 노출 조건을 대신 적는다.</summary>
        [SerializeField] private TMP_Text timerText;

        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyLabel;

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

        public void Bind(ShopDatabase.GrowthPackage package, Action clickCallback)
        {
            onClick = clickCallback;

            if (package == null)
                return;

            if (titleText != null)
                titleText.SetText(package.title);

            if (artImage != null)
            {
                // <b>스프라이트가 없을 때 색으로 눕히지 않는다.</b> 그러면 그림 자리(768x768)가
                // 통째로 불투명한 네모가 되어 <b>카드 배경의 오른쪽 2/3을 덮는다</b> —
                // 카드가 좁아 보이는 것으로만 드러나서, 레이아웃을 의심하게 만든다.
                // 상품 그림이 따로 없으면 구워 둔 기본 그림(Shop_Package_Image)을 그대로 쓴다.
                if (package.art != null)
                    artImage.sprite = package.art;

                artImage.color = Color.white;
                artImage.enabled = artImage.sprite != null;
            }

            if (contentsText != null)
                contentsText.SetText(ShopText.DescribeContents(package));

            BindRewardRows(package);

            if (timerText != null)
            {
                // 기간 한정이 아니면 타이머 자리에 노출 조건을 적는다. 칸을 비우면
                // 배너 아래쪽이 뜬금없이 넓어 보이고, "00:00" 을 적으면 만료된 것으로 읽힌다.
                timerText.SetText(package.durationHours > 0
                    ? "남은 시간 " + ShopText.FormatDuration(package.durationHours)
                    : package.conditionText);
            }

            if (buyLabel != null)
                buyLabel.SetText(ShopText.FormatWon(package.priceWon));

            // 버튼 색을 칠하지 않는다 — 금색 버튼은 스프라이트(Big_Btn_Yellow)다.
            if (buyButton != null)
                buyButton.interactable = true;
        }

        /// <summary>
        /// 구성품을 줄에 나눠 담는다. <see cref="RewardRows"/> 줄을 넘는 구성품은
        /// 마지막 줄에 합쳐지지도, 잘려 나오지도 않는다 — <b>그냥 안 보인다</b>.
        /// 보이지 않는 보상이 생기는 쪽이 나쁘므로 에디터에서 경고한다.
        /// </summary>
        private void BindRewardRows(ShopDatabase.GrowthPackage package)
        {
            if (rewardRows == null)
                return;

            int count = package.contents != null ? package.contents.Count : 0;

#if UNITY_EDITOR
            if (count > RewardRows)
            {
                Debug.LogWarning("[상점] 성장 패키지 '" + package.title + "' 의 구성품이 " + count +
                                 "개라 시안의 " + RewardRows + "줄을 넘는다. 넘는 것은 화면에 안 나온다.");
            }
#endif

            for (int i = 0; i < rewardRows.Length; i++)
            {
                bool used = i < count;
                if (rewardRows[i] != null)
                    rewardRows[i].gameObject.SetActive(used);

                if (!used)
                    continue;

                ShopDatabase.Reward reward = package.contents[i];

                if (rewardIcons != null && i < rewardIcons.Length && rewardIcons[i] != null)
                {
                    Sprite sprite = PointRewardUtility.GetPointIcon(reward.pointType);
                    rewardIcons[i].sprite = sprite;
                    rewardIcons[i].enabled = sprite != null;
                }

                if (rewardAmounts != null && i < rewardAmounts.Length && rewardAmounts[i] != null)
                    rewardAmounts[i].SetText(ShopText.FormatAmount(reward.amount));
            }
        }

        // 구성품 표기와 기간 표기는 ShopText 에 있다. 여기 두면 Image 필드 때문에
        // 헤드리스 테스트가 이 타입을 아예 못 불러와, 기획서가 못 박은 "유료젬이 맨 앞"
        // 규칙이 테스트 밖으로 빠진다.

        private void HandleClick()
        {
            onClick?.Invoke();
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        /// <summary>시안 실측. <c>Shop_Package_Btn</c> 을 x3 배 한 크기다.</summary>
        internal static readonly Vector2 CardSize = new Vector2(1018f, 489f);

        internal static float BannerHeight => CardSize.y;

        /// <summary>구성품 줄 수. 시안이 네 줄이다.</summary>
        internal const int RewardRows = 4;

        /// <summary>구성품 줄 하나의 보이는 크기(시안 주석 "386 x 76").</summary>
        private static readonly Vector2 RowSize = new Vector2(386f, 76f);

        /// <summary>
        /// 에디터 굽기 전용. 시안 <c>Art/Layout/Shop_PackageDiaBox_Layout.png</c> 의 패키지 카드다.
        ///
        /// 좌표는 PSD 에서 잰 절대값을 <see cref="Local"/> 로 카드 안쪽 좌표로 바꿔 적는다 —
        /// 실측한 숫자를 그대로 두어야 나중에 시안과 대조할 때 다시 뺄셈을 하지 않는다.
        /// </summary>
        internal static UIShopGrowthBanner Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIShopUIFactory.CreateRect("GrowthBanner", parent);
            UIShopUIFactory.SetRect(root.GetComponent<RectTransform>(), CardSize, Vector2.zero);
            UIShopUIFactory.SetPreferredHeight(root, CardSize.y);

            var banner = root.AddComponent<UIShopGrowthBanner>();
            Transform p = root.transform;

            banner.background = UIShopUIFactory.CreateSpriteAt(
                "Bg", p, UIShopUIFactory.Art("Shop/Shop_Package_Btn"), UIShopUIFactory.KitScale, Vector2.zero);

            banner.titleText = UIShopUIFactory.CreateText("Title", p, "패키지 이름", 45f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.SetRect(banner.titleText.rectTransform, new Vector2(700f, 60f), Local(532.5f, 635f));

            banner.rewardRows = new RectTransform[RewardRows];
            banner.rewardIcons = new Image[RewardRows];
            banner.rewardAmounts = new TMP_Text[RewardRows];

            for (int i = 0; i < RewardRows; i++)
            {
                // 시안의 네 줄은 중심이 720 / 811 / 902 / 993 이다(간격 91).
                GameObject row = UIShopUIFactory.CreateRect("Reward" + i, p);
                UIShopUIFactory.SetRect(row.GetComponent<RectTransform>(), RowSize, Local(251f, 720f + i * 91f));
                banner.rewardRows[i] = row.GetComponent<RectTransform>();

                UIShopUIFactory.CreatePanel("Bg", row.transform, RowSize, Vector2.zero, 0.3f);

                Image icon = UIShopUIFactory.CreateImage("Icon", row.transform, Color.white);
                UIShopUIFactory.SetRect(icon.rectTransform, new Vector2(78f, 78f), new Vector2(-141f, 0f));
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.enabled = false;   // 스프라이트가 붙기 전에는 흰 사각형이다
                banner.rewardIcons[i] = icon;

                banner.rewardAmounts[i] = UIShopUIFactory.CreateText("Amount", row.transform, "10,000", 45f,
                    TextAlignmentOptions.Center, Color.white, font);
                UIShopUIFactory.SetRect(banner.rewardAmounts[i].rectTransform,
                    new Vector2(220f, 56f), new Vector2(24.5f, 2f));
            }

            banner.artImage = UIShopUIFactory.CreateSpriteAt(
                "Art", p, UIShopUIFactory.Art("Shop/Shop_Package_Image"),
                UIShopUIFactory.KitScale, Local(776.5f, 842.5f));

            banner.buyButton = UIShopUIFactory.CreateKitButton(
                "Buy", p, "Ingame/Big_Btn_Yellow", new Vector2(323f, 100f), Local(791.5f, 987f),
                "9,900원", 45f, Color.white, font);
            banner.buyLabel = banner.buyButton.GetComponentInChildren<TMP_Text>();

            return banner;
        }

        /// <summary>PSD 절대 좌표 → 카드 안쪽 좌표. 카드가 (30,582)-(1048,1071) 이라 중심이 (539, 826.5) 다.</summary>
        private static Vector2 Local(float psdX, float psdY)
        {
            return new Vector2(psdX - 539f, 826.5f - psdY);
        }
    }
}
