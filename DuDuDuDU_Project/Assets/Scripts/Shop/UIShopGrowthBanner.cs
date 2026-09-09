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

        /// <summary>구성품 한 줄. "유료젬 300 + 부위별 Rare 보석" 꼴.</summary>
        [SerializeField] private TMP_Text contentsText;

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
                artImage.sprite = package.art;
                artImage.color = package.art != null ? Color.white : UIShopUIFactory.RowColor;
            }

            if (contentsText != null)
                contentsText.SetText(ShopText.DescribeContents(package));

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

            if (buyButton != null)
            {
                buyButton.interactable = true;

                var image = buyButton.targetGraphic as Image;
                if (image != null)
                    image.color = UIShopUIFactory.CashAccent;
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

        internal const float BannerHeight = 440f;

        /// <summary>에디터 굽기 전용.</summary>
        internal static UIShopGrowthBanner Create(Transform parent, TMP_FontAsset font)
        {
            Image background = UIShopUIFactory.CreateImage("GrowthBanner", parent, UIShopUIFactory.CardColor);
            UIShopUIFactory.SetPreferredHeight(background.gameObject, BannerHeight);

            var banner = background.gameObject.AddComponent<UIShopGrowthBanner>();
            banner.background = background;
            Transform p = background.transform;

            // 아트가 배너 전체를 덮는다. 글자는 그 위에 올라간다.
            banner.artImage = UIShopUIFactory.CreateImage("Art", p, UIShopUIFactory.RowColor);
            UIShopUIFactory.Stretch(banner.artImage.rectTransform);
            banner.artImage.raycastTarget = false;

            banner.titleText = UIShopUIFactory.CreateText("Title", p, "스타트 패키지", 52f,
                TextAlignmentOptions.Left, Color.white, font);
            UIShopUIFactory.SetRect(banner.titleText.rectTransform, new Vector2(760f, 64f), new Vector2(-90f, 44f));

            banner.contentsText = UIShopUIFactory.CreateText("Contents", p,
                "유료젬 300 + 부위별 Rare 보석", 30f,
                TextAlignmentOptions.Left, UIShopUIFactory.GoldText, font);
            UIShopUIFactory.SetRect(banner.contentsText.rectTransform, new Vector2(760f, 42f), new Vector2(-90f, -12f));

            banner.timerText = UIShopUIFactory.CreateText("Timer", p, "남은 시간 2일", 26f,
                TextAlignmentOptions.Left, UIShopUIFactory.MutedText, font);
            UIShopUIFactory.SetRect(banner.timerText.rectTransform, new Vector2(500f, 34f), new Vector2(-220f, -110f));

            banner.buyButton = UIShopUIFactory.CreateButton("Buy", p, "3,000원",
                UIShopUIFactory.CashAccent, Color.black, 36f, font);
            UIShopUIFactory.SetRect(banner.buyButton.GetComponent<RectTransform>(),
                new Vector2(300f, 88f), new Vector2(320f, -118f));
            banner.buyLabel = banner.buyButton.GetComponentInChildren<TMP_Text>();

            return banner;
        }
    }
}
