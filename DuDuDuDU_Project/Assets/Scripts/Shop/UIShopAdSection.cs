using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Shop
{
    /// <summary>
    /// 상점 맨 위의 <b>광고 보상 칸과 원터치 구매 토글</b>. (시안 <c>Art/Layout/Shop_Ads_Layout.png</c>)
    ///
    /// <b>지금은 겉모습만이다.</b> 광고를 봐서 받는 보상·하루 한도·원터치 구매 설정은
    /// 세이브 스키마와 <c>ShopPurchaseManager</c> 에 자리가 없다. 레이아웃을 옮기는
    /// 단계라 칸과 글자만 시안대로 놓고, 누르면 <see cref="onWatchAd"/> 로 올려보낸다 —
    /// <b>받는 쪽이 아직 "준비 중"을 알린다.</b>
    /// <c>MIGRATION_BASELINE.md</c> 의 알려진 미동작에 적어 두었다.
    ///
    /// 연결할 때 고칠 곳은 <c>UIShopPage.RefreshAdShop</c> 의 콜백 두 개다 —
    /// 광고는 <c>GameContainer.RewardedAds</c> 가 이미 포트로 있다.
    /// </summary>
    public sealed class UIShopAdSection : MonoBehaviour
    {
        [SerializeField] private Button adButton;
        [SerializeField] private TMP_Text adCountText;

        [SerializeField] private Button goldButton;
        [SerializeField] private TMP_Text goldAmountText;
        [SerializeField] private TMP_Text goldCountText;

        [SerializeField] private Button oneTouchToggle;
        [SerializeField] private RectTransform toggleKnob;
        [SerializeField] private TMP_Text toggleLabel;

        private Action onWatchAd;
        private Action onClaimGold;
        private Action<bool> onOneTouchChanged;
        private bool oneTouchOn;

        private void Awake()
        {
            if (adButton != null)
                adButton.onClick.AddListener(HandleAd);
            if (goldButton != null)
                goldButton.onClick.AddListener(HandleGold);
            if (oneTouchToggle != null)
                oneTouchToggle.onClick.AddListener(HandleToggle);
        }

        private void OnDestroy()
        {
            if (adButton != null)
                adButton.onClick.RemoveListener(HandleAd);
            if (goldButton != null)
                goldButton.onClick.RemoveListener(HandleGold);
            if (oneTouchToggle != null)
                oneTouchToggle.onClick.RemoveListener(HandleToggle);
        }

        /// <summary>
        /// 칸을 채운다. 남은 횟수는 "<c>남은/전체</c>" 로 적고, 0 이 되면 빨강이 된다
        /// (시안의 "0 / 2" 가 빨강이다) — 버튼을 끄기만 하면 <b>왜 못 누르는지</b>가 안 보인다.
        /// </summary>
        public void Bind(
            int adRemaining, int adTotal,
            int goldAmount, int goldRemaining, int goldTotal,
            bool oneTouch,
            Action watchAd, Action claimGold, Action<bool> oneTouchChanged)
        {
            onWatchAd = watchAd;
            onClaimGold = claimGold;
            onOneTouchChanged = oneTouchChanged;

            SetCount(adCountText, adRemaining, adTotal, UIShopUIFactory.OnGreenText);
            SetCount(goldCountText, goldRemaining, goldTotal, UIShopUIFactory.OnGoldText);

            if (goldAmountText != null)
                goldAmountText.SetText(ShopText.FormatAmount(goldAmount));

            if (adButton != null)
                adButton.interactable = adRemaining > 0;
            if (goldButton != null)
                goldButton.interactable = goldRemaining > 0;

            ApplyToggle(oneTouch);
        }

        private static void SetCount(TMP_Text label, int remaining, int total, Color normal)
        {
            if (label == null)
                return;

            label.SetText("{0}/{1}", remaining, total);
            label.color = remaining > 0 ? normal : UIShopUIFactory.SoldOutColor;
        }

        private void ApplyToggle(bool on)
        {
            oneTouchOn = on;

            if (toggleLabel != null)
            {
                toggleLabel.SetText(on ? "ON" : "OFF");
                toggleLabel.color = on ? Color.white : UIShopUIFactory.ToggleOffColor;
            }

            if (toggleKnob != null)
            {
                // 손잡이가 좌우로 옮겨 다닌다. 색만 바꾸면 켜졌는지 꺼졌는지가
                // <b>글자를 읽어야</b> 알게 된다.
                Vector2 position = toggleKnob.anchoredPosition;
                position.x = on ? KnobOnX : KnobOffX;
                toggleKnob.anchoredPosition = position;
            }
        }

        private void HandleAd() => onWatchAd?.Invoke();

        private void HandleGold() => onClaimGold?.Invoke();

        private void HandleToggle()
        {
            ApplyToggle(!oneTouchOn);
            onOneTouchChanged?.Invoke(oneTouchOn);
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        /// <summary>섹션 전체 높이. 광고 패널 위(598)부터 원터치 패널 아래(1063)까지다.</summary>
        internal const float SectionHeight = 465f;

        /// <summary>손잡이의 좌우 끝. 트랙이 223 폭이라 좌우로 68 씩 움직인다.</summary>
        private const float KnobOffX = -68f;
        private const float KnobOnX = 68f;

        /// <summary>에디터 굽기 전용. 좌표는 PSD 에서 잰 절대값을 <see cref="Local"/> 로 바꾼다.</summary>
        internal static UIShopAdSection Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIShopUIFactory.CreateRect("AdSection", parent);
            UIShopUIFactory.SetRect(root.GetComponent<RectTransform>(),
                new Vector2(1080f, SectionHeight), Vector2.zero);
            UIShopUIFactory.SetPreferredHeight(root, SectionHeight);

            var section = root.AddComponent<UIShopAdSection>();
            Transform p = root.transform;

            // 광고 칸. 시안 주석 "Ui_Popup_SmallBox / 크기 x4배 864x341 / 알파값 70%".
            UIShopUIFactory.CreatePanel("AdPanel", p, new Vector2(864f, 341f), Local(548f, 768.5f), 0.7f);

            TMP_Text reset = UIShopUIFactory.CreateText("ResetTitle", p, "초기화", 40f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.SetRect(reset.rectTransform, new Vector2(400f, 52f), Local(537.5f, 660.5f));

            section.adButton = UIShopUIFactory.CreateKitButton(
                "AdButton", p, "Ingame/Big_Btn_Green", AdButtonSize, Local(373.5f, 806.5f),
                string.Empty, 40f, UIShopUIFactory.OnGreenText, font);

            Image adIcon = UIShopUIFactory.CreateSpriteAt(
                "Icon", section.adButton.transform, UIShopUIFactory.Art("Shop/Icon_AD"), 1f, Vector2.zero);
            adIcon.rectTransform.anchoredPosition = new Vector2(5f, 38.5f);
            adIcon.rectTransform.sizeDelta = new Vector2(129f * 128f / 98f, 94f * 128f / 71f);

            section.adCountText = UIShopUIFactory.CreateText("AdCount", p, "2/2", 40f,
                TextAlignmentOptions.Center, UIShopUIFactory.OnGreenText, font);
            UIShopUIFactory.SetRect(section.adCountText.rectTransform, new Vector2(200f, 50f), Local(373.5f, 842.5f));

            section.goldButton = UIShopUIFactory.CreateKitButton(
                "GoldButton", p, "Ingame/Big_Btn_Yellow", AdButtonSize, Local(704.5f, 806.5f),
                string.Empty, 40f, UIShopUIFactory.OnGoldText, font);

            Image goldIcon = UIShopUIFactory.CreateImage("Icon", section.goldButton.transform, Color.white);
            goldIcon.sprite = UIShopUIFactory.Art("Gem/Gem_Money");
            goldIcon.raycastTarget = false;
            goldIcon.preserveAspect = true;
            UIShopUIFactory.SetRect(goldIcon.rectTransform, new Vector2(70f, 70f), new Vector2(-96.5f, 24.5f));

            section.goldAmountText = UIShopUIFactory.CreateText("Amount", section.goldButton.transform, "10,000", 54f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.SetRect(section.goldAmountText.rectTransform, new Vector2(240f, 56f), new Vector2(38.5f, 22.5f));

            section.goldCountText = UIShopUIFactory.CreateText("GoldCount", p, "2/2", 40f,
                TextAlignmentOptions.Center, UIShopUIFactory.OnGoldText, font);
            UIShopUIFactory.SetRect(section.goldCountText.rectTransform, new Vector2(200f, 50f), Local(708.5f, 842.5f));

            // 원터치 구매 칸. 시안 주석 "864x109 / 알파값 70%".
            UIShopUIFactory.CreatePanel("OneTouchPanel", p, new Vector2(864f, 109f), Local(548f, 1008.5f), 0.7f);

            TMP_Text oneTouch = UIShopUIFactory.CreateText("OneTouchLabel", p, "상품 원터치 구매", 40f,
                TextAlignmentOptions.Left, Color.white, font);
            UIShopUIFactory.SetRect(oneTouch.rectTransform, new Vector2(520f, 52f), Local(426f, 1003.5f));

            // 토글. 트랙은 어두운 회보라(#443D4F)라 패널과 달리 알파를 깔지 않는다.
            Image track = UIShopUIFactory.CreateKit(
                "ToggleTrack", p, UIShopUIFactory.Art("Upgrade/Ui_Popup_SmallBox"), 4f,
                ToggleTrackSize, Local(852.5f, 1009f),
                new Vector4(24f, 28f, 28f, 24f));
            track.color = UIShopUIFactory.ToggleTrackColor;
            track.raycastTarget = true;

            section.oneTouchToggle = track.gameObject.AddComponent<Button>();
            section.oneTouchToggle.targetGraphic = track;

            Image knob = UIShopUIFactory.CreateImage("Knob", track.transform, Color.white);
            UIShopUIFactory.SetRect(knob.rectTransform, new Vector2(53f, 54f), new Vector2(KnobOffX, 0f));
            knob.raycastTarget = false;
            section.toggleKnob = knob.rectTransform;

            section.toggleLabel = UIShopUIFactory.CreateText("State", track.transform, "OFF", 40f,
                TextAlignmentOptions.Center, UIShopUIFactory.ToggleOffColor, font);
            UIShopUIFactory.SetRect(section.toggleLabel.rectTransform, new Vector2(140f, 50f), new Vector2(49.5f, 1f));

            return section;
        }

        /// <summary>광고·골드 버튼 한 짝(시안 주석 "버튼크기 325x175").</summary>
        private static readonly Vector2 AdButtonSize = new Vector2(325f, 175f);

        /// <summary>토글 트랙(시안 주석 "223x72 / 똑같이 크기 네배 / 443d4f").</summary>
        private static readonly Vector2 ToggleTrackSize = new Vector2(223f, 72f);

        /// <summary>
        /// PSD 절대 좌표 → 섹션 안쪽 좌표. 섹션이 (0,598)-(1080,1063) 이라 중심이 (540, 830.5) 다.
        /// </summary>
        private static Vector2 Local(float psdX, float psdY)
        {
            return new Vector2(psdX - 540f, 830.5f - psdY);
        }
    }
}
