using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Shop
{
    /// <summary>
    /// 일일상점 아래의 갱신 줄. 광고 갱신과 무료젬 갱신 두 버튼이다.
    ///
    /// <b>남은 횟수를 버튼 안에 적는다</b>(레퍼런스 스샷의 <c>2/2</c>). 눌러 보기 전에
    /// 몇 번 남았는지 알아야 "왜 안 눌리지"가 안 생긴다. 다 쓴 버튼은 회색으로 죽이되
    /// 숨기지 않는다 — 사라지면 <b>내일 다시 생기는 기능</b>이라는 것을 알 수 없다.
    /// </summary>
    public sealed class UIShopDailyRefreshBar : MonoBehaviour
    {
        [SerializeField] private Button adButton;
        [SerializeField] private TMP_Text adLabel;
        [SerializeField] private Button gemButton;
        [SerializeField] private TMP_Text gemLabel;

        private Action onAd;
        private Action onGem;

        private void Awake()
        {
            if (adButton != null)
                adButton.onClick.AddListener(HandleAd);
            if (gemButton != null)
                gemButton.onClick.AddListener(HandleGem);
        }

        private void OnDestroy()
        {
            if (adButton != null)
                adButton.onClick.RemoveListener(HandleAd);
            if (gemButton != null)
                gemButton.onClick.RemoveListener(HandleGem);
        }

        public void Bind(
            int adUsed, int adMax, Action adCallback,
            int gemUsed, int gemMax, int gemCost, bool gemAffordable, Action gemCallback)
        {
            onAd = adCallback;
            onGem = gemCallback;

            bool adLeft = adUsed < adMax;
            Apply(adButton, adLabel, "광고 갱신  " + adUsed + " / " + adMax, adLeft, UIShopUIFactory.Accent);

            bool gemLeft = gemUsed < gemMax;
            Apply(gemButton, gemLabel,
                "무료젬 " + ShopText.FormatAmount(gemCost) + "  " + gemUsed + " / " + gemMax,
                gemLeft && gemAffordable, UIShopUIFactory.Accent);
        }

        private static void Apply(Button button, TMP_Text label, string text, bool enabled, Color color)
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

        private void HandleAd() => onAd?.Invoke();
        private void HandleGem() => onGem?.Invoke();

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        internal const float BarHeight = 86f;

        /// <summary>에디터 굽기 전용.</summary>
        internal static UIShopDailyRefreshBar Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIShopUIFactory.CreateRect("RefreshBar", parent);
            UIShopUIFactory.SetPreferredHeight(root, BarHeight);

            var bar = root.AddComponent<UIShopDailyRefreshBar>();

            bar.adButton = UIShopUIFactory.CreateButton("AdRefresh", root.transform, "광고 갱신  0 / 2",
                UIShopUIFactory.Accent, Color.white, 26f, font);
            SetHalf(bar.adButton.GetComponent<RectTransform>(), 0.04f, 0.49f);
            bar.adLabel = bar.adButton.GetComponentInChildren<TMP_Text>();

            bar.gemButton = UIShopUIFactory.CreateButton("GemRefresh", root.transform, "무료젬 1,000  0 / 3",
                UIShopUIFactory.Accent, Color.white, 26f, font);
            SetHalf(bar.gemButton.GetComponent<RectTransform>(), 0.51f, 0.96f);
            bar.gemLabel = bar.gemButton.GetComponentInChildren<TMP_Text>();

            return bar;
        }

        private static void SetHalf(RectTransform rect, float minX, float maxX)
        {
            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.offsetMin = new Vector2(0f, 8f);
            rect.offsetMax = new Vector2(0f, -8f);
        }
    }
}
