using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.UI;

namespace OJ.Shop
{
    /// <summary>
    /// 콘텐츠 화면의 "패스 버튼"이 여는 재화 팝업. <b>그 콘텐츠에 쓰는 재화만</b> 판다
    /// (<see cref="CurrencyShopRules.SellsIn"/>). 상품·가격·한도는 재화 상점과 같은 표(<c>ShopDatabase.CurrencyOffers</c>)이고
    /// 구매도 같은 <c>TryBuyCurrency</c> 라 <b>하루 한도가 한 곳에서 센다</b> — 핀볼 1,000발을 상점에서 한 번,
    /// 핀볼 화면에서 한 번 사면 둘이 합쳐 2회다.
    ///
    /// <b>UIService 카탈로그에 올리지 않고 코드로 만든다.</b> 팝업 프리팹을 새로 만들면 다섯 화면이 공유하는 창 하나를 위해
    /// 에디터 작업이 따라붙는데, 이 창은 카드 격자 하나뿐이라 코드가 더 가볍다. 카드는 상점 카드를 그대로 쓴다.
    /// </summary>
    public sealed class CurrencyPassPopup : MonoBehaviour
    {
        private static CurrencyPassPopup current;

        private ContentPass pass;
        private TMP_FontAsset font;
        private RectTransform panel;
        private RectTransform grid;
        private TMP_Text emptyText;

        private const int Columns = 2;
        private const float GridGap = 30f;
        private const float PanelPadding = 44f;
        private const float HeaderHeight = 150f;
        private const float FooterHeight = 160f;

        public static void Show(ContentPass pass, TMP_FontAsset font)
        {
            if (current != null)
                Destroy(current.gameObject);

            var go = new GameObject("CurrencyPassPopup", typeof(RectTransform));
            go.layer = 5;
            var popup = go.AddComponent<CurrencyPassPopup>();
            current = popup;
            popup.Build(pass, font);
        }

        public static string TitleOf(ContentPass pass)
        {
            switch (pass)
            {
                case ContentPass.Pinball: return "핀볼 충전";
                case ContentPass.DiceGrowth: return "다이스 성장 재화";
                case ContentPass.Synergy: return "시너지 재화";
                case ContentPass.Relic: return "유물 재화";
                default: return "보석 뽑기권";
            }
        }

        private void Build(ContentPass contentPass, TMP_FontAsset fontAsset)
        {
            pass = contentPass;
            font = fontAsset;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 페이드(short.MaxValue - 100) 바로 아래. 어떤 콘텐츠 창 위에서 열어도 맨 위에 온다.
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue - 200;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();

            // 바깥을 누르면 닫힌다.
            Image dim = UIShopUIFactory.CreateImage("Dim", transform, new Color(0f, 0f, 0f, 0.78f));
            UIShopUIFactory.Stretch(dim.rectTransform);
            var dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Close);

            Image panelImage = UIShopUIFactory.CreateImage("Panel", transform, new Color(0.15f, 0.18f, 0.30f, 1f));
            panel = panelImage.rectTransform;
            // 패널 안을 눌러도 닫히지 않게 레이캐스트를 먹는다(Image 기본값).

            TMP_Text title = UIShopUIFactory.CreateText("Title", panel, TitleOf(pass), 54f,
                TextAlignmentOptions.Center, Color.white, font);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(0f, 90f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -36f);

            GameObject gridGo = UIShopUIFactory.CreateRect("Grid", panel);
            grid = gridGo.GetComponent<RectTransform>();
            var layout = gridGo.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Columns;
            layout.cellSize = UIShopGridCard.CardSize;
            layout.spacing = new Vector2(GridGap, GridGap);
            layout.childAlignment = TextAnchor.UpperCenter;

            emptyText = UIShopUIFactory.CreateText("Empty", panel, "지금 살 수 있는 상품이 없어요", 36f,
                TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 0.95f), font);
            emptyText.rectTransform.sizeDelta = new Vector2(700f, 80f);
            emptyText.rectTransform.anchoredPosition = Vector2.zero;

            // 닫기 버튼.
            Image closeBack = UIShopUIFactory.CreateImage("Close", panel, new Color(0.22f, 0.30f, 0.52f, 1f));
            closeBack.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            closeBack.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            closeBack.rectTransform.pivot = new Vector2(0.5f, 0f);
            closeBack.rectTransform.sizeDelta = new Vector2(360f, 100f);
            closeBack.rectTransform.anchoredPosition = new Vector2(0f, 34f);
            var closeButton = closeBack.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeBack;
            closeButton.onClick.AddListener(Close);
            TMP_Text closeLabel = UIShopUIFactory.CreateText("Label", closeBack.transform, "닫기", 42f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.Stretch(closeLabel.rectTransform);

            if (ShopPurchaseManager.Instance != null)
            {
                ShopPurchaseManager.Instance.OnPurchaseStateChanged += Rebuild;
                ShopPurchaseManager.Instance.OnRewardGranted += ShowReward;
            }

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            Rebuild();
        }

        private void OnDestroy()
        {
            if (ShopPurchaseManager.Instance != null)
            {
                ShopPurchaseManager.Instance.OnPurchaseStateChanged -= Rebuild;
                ShopPurchaseManager.Instance.OnRewardGranted -= ShowReward;
            }

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;

            if (current == this)
                current = null;
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            if (pointType == PointType.PaidGem)
                Rebuild();
        }

        private void Close()
        {
            Destroy(gameObject);
        }

        private static void ShowReward(IReadOnlyList<PointRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return;

            UIRewardResultDialog dialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (dialog == null)
                return;

            dialog.transform.SetAsLastSibling();
            dialog.Open(rewards, "구매한 상품을 받았습니다.");
        }

        /// <summary>카드를 다시 그린다. 구매·유료젬 변동마다 돈다.</summary>
        private void Rebuild()
        {
            if (grid == null)
                return;

            for (int i = grid.childCount - 1; i >= 0; i--)
                Destroy(grid.GetChild(i).gameObject);

            ShopDatabase db = ShopDatabaseProvider.Database;
            ShopPurchaseManager purchases = ShopPurchaseManager.Instance;
            bool eventOpen = EventCurrencyRules.IsEventOpen(System.DateTime.UtcNow);

            int shown = 0;
            if (db != null)
            {
                IReadOnlyList<ShopDatabase.CurrencyOffer> offers = db.CurrencyOffers;
                for (int i = 0; i < offers.Count; i++)
                {
                    ShopDatabase.CurrencyOffer offer = offers[i];
                    if (!CurrencyShopRules.SellsIn(pass, offer.pointType)
                        || !CurrencyShopRules.IsVisible(offer.pointType, eventOpen))
                        continue;

                    int index = i;
                    int bought = purchases != null ? purchases.GetTodayCount(ShopPurchaseManager.CurrencyKey(index)) : 0;
                    bool soldOut = CurrencyShopRules.IsLimitReached(offer.dailyLimit, bought);
                    PointManager points = PointManager.Instance;
                    bool affordable = points != null && points.Get(PointType.PaidGem) >= offer.priceGem;

                    string price = ShopText.FormatAmount(offer.priceGem);
                    if (offer.dailyLimit > 0)
                        price += " (" + CurrencyShopRules.RemainingToday(offer.dailyLimit, bought) + "/" + offer.dailyLimit + ")";

                    UIShopGridCard card = UIShopGridCard.Create(grid, font);
                    card.Bind(
                        offer.pointType, offer.amount,
                        PointType.PaidGem, price,
                        !soldOut && affordable,
                        () => purchases?.TryBuyCurrency(index));
                    shown++;
                }
            }

            emptyText.gameObject.SetActive(shown == 0);

            int columns = Mathf.Min(Columns, Mathf.Max(1, shown));
            int rows = Mathf.Max(1, Mathf.CeilToInt(shown / (float)Columns));
            Vector2 card0 = UIShopGridCard.CardSize;
            float gridWidth = columns * card0.x + (columns - 1) * GridGap;
            float gridHeight = rows * card0.y + (rows - 1) * GridGap;

            float panelWidth = Mathf.Max(gridWidth + PanelPadding * 2f, 760f);
            float panelHeight = HeaderHeight + gridHeight + FooterHeight;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(panelWidth, panelHeight);
            panel.anchoredPosition = Vector2.zero;

            grid.anchorMin = new Vector2(0.5f, 1f);
            grid.anchorMax = new Vector2(0.5f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.sizeDelta = new Vector2(gridWidth, gridHeight);
            grid.anchoredPosition = new Vector2(0f, -HeaderHeight);
        }

        // ── 패스 버튼 ────────────────────────────────────────────────

        /// <summary>
        /// 기준 버튼의 <b>오른쪽</b>에 붙인다. 기준 버튼과 같은 앵커·부모를 쓰고 가로로만 비켜 선다 —
        /// 화면마다 좌표를 따로 재지 않으려는 것이다(강화 버튼 옆 초기화 버튼과 같은 방식).
        /// </summary>
        public static Button AttachBeside(
            Button reference, ContentPass pass, TMP_FontAsset font, Vector2 size, float gap = 24f, string label = "충전")
        {
            if (reference == null)
                return null;

            var refRect = (RectTransform)reference.transform;
            Button button = AttachButton(refRect.parent, pass, font, refRect.anchorMin, Vector2.zero, size, label);
            var rect = (RectTransform)button.transform;
            rect.anchorMax = refRect.anchorMax;

            Vector2 center = refRect.anchoredPosition +
                             Vector2.Scale(new Vector2(0.5f, 0.5f) - refRect.pivot, refRect.rect.size);
            float x = center.x + refRect.rect.width * 0.5f + gap + size.x * 0.5f;
            rect.anchoredPosition = new Vector2(x, center.y);
            return button;
        }

        /// <summary>기준 버튼의 <b>아래</b>에 붙인다. 오른쪽에 자리가 없는 화면(장비 페이지의 슬롯 카드 옆)용이다.</summary>
        public static Button AttachBelow(
            Button reference, ContentPass pass, TMP_FontAsset font, Vector2 size, float gap = 16f, string label = "충전")
        {
            if (reference == null)
                return null;

            var refRect = (RectTransform)reference.transform;
            Button button = AttachButton(refRect.parent, pass, font, refRect.anchorMin, Vector2.zero, size, label);
            var rect = (RectTransform)button.transform;
            rect.anchorMax = refRect.anchorMax;

            Vector2 center = refRect.anchoredPosition +
                             Vector2.Scale(new Vector2(0.5f, 0.5f) - refRect.pivot, refRect.rect.size);
            float y = center.y - refRect.rect.height * 0.5f - gap - size.y * 0.5f;
            rect.anchoredPosition = new Vector2(center.x, y);
            return button;
        }

        /// <summary>
        /// 콘텐츠 화면에 붙이는 작은 "충전" 버튼. 누르면 그 콘텐츠의 재화 팝업이 뜬다.
        /// 코드로 만든다 — 프리팹 YAML 을 건드리지 않고 이미 배선이 끝난 화면 위에 얹으려는 것이다.
        /// </summary>
        public static Button AttachButton(
            Transform parent, ContentPass pass, TMP_FontAsset font,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, string label = "충전")
        {
            Image back = UIShopUIFactory.CreateImage("ContentPassButton", parent, Color.white);
            back.sprite = Resources.Load<Sprite>("Art/Ingame/Big_Btn_Yellow");
            back.type = Image.Type.Sliced;
            RectTransform rect = back.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var button = back.gameObject.AddComponent<Button>();
            button.targetGraphic = back;
            button.onClick.AddListener(() => Show(pass, font));

            TMP_Text text = UIShopUIFactory.CreateText("Label", back.transform, label, 38f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.Stretch(text.rectTransform);
            return button;
        }
    }
}
