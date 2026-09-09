using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Equipment;
using OJ.Hunting;
using OJ.Point;
using OJ.UI;

namespace OJ.Shop
{
    /// <summary>
    /// 상점 탭. 세로 1열 스크롤에 7개 섹션을 쌓는다. (기획서 7·8장)
    ///
    /// <b>섹션 순서가 곧 결제 부담 순서다.</b> 위에서 아래로 현금 → 게임 재화 → 재화 교환이며
    /// (7.2-1), 그 순서를 <see cref="SectionOrder"/> 한 곳에만 적는다. 굽는 코드와 채우는
    /// 코드가 각자 순서를 들면 둘이 갈라지는 날 <b>섹션이 뒤바뀐 화면</b>이 나온다.
    ///
    /// <b>모든 항목은 템플릿을 복제해 만든다.</b> 프리팹 안에 꺼진 템플릿이 들어 있고
    /// 런타임에 <see cref="ShopDatabase"/> 개수만큼 찍는다 — 유료젬 5줄이 6줄이 되는 것은
    /// 에셋 수정이지 프리팹 재굽기가 아니어야 한다.
    /// </summary>
    public sealed class UIShopPage : DialogBase
    {
        /// <summary>섹션 순서. 기획서 7.1 표 그대로다.</summary>
        internal static readonly string[] SectionOrder =
        {
            "성장 패키지",
            "보석뽑기",
            "일일상점",
            "유료젬 상점",
            "무료젬 상점",
            "다이스석 상점",
            "골드 상점",
        };

        [Header("섹션 본체")]
        [SerializeField] private RectTransform growthBody;
        [SerializeField] private RectTransform gemDrawBody;
        [SerializeField] private RectTransform dailyBody;
        [SerializeField] private RectTransform paidGemBody;
        [SerializeField] private RectTransform freeGemBody;
        [SerializeField] private RectTransform stoneBody;
        [SerializeField] private RectTransform goldBody;

        [Header("머리글 보조 칸")]
        [SerializeField] private TMP_Text dailyTimerText;
        [SerializeField] private TMP_Text freeGemRateText;

        [Header("일일상점 갱신")]
        [SerializeField] private RectTransform dailyGrid;
        [SerializeField] private UIShopDailyRefreshBar refreshBar;

        [Header("템플릿 (프리팹 안에 꺼진 채로 들어 있다)")]
        [SerializeField] private UIShopGrowthBanner growthTemplate;
        [SerializeField] private UIShopOfferCard cardTemplate;
        [SerializeField] private UIShopDailySlot dailySlotTemplate;
        [SerializeField] private UIShopGridCard gridCardTemplate;

        private readonly List<UIShopGrowthBanner> growthBanners = new List<UIShopGrowthBanner>();
        private readonly List<UIShopOfferCard> gemBoxCards = new List<UIShopOfferCard>();
        private readonly List<UIShopDailySlot> dailySlots = new List<UIShopDailySlot>();
        private readonly List<UIShopGridCard> paidGemCards = new List<UIShopGridCard>();
        private readonly List<UIShopGridCard> freeGemCards = new List<UIShopGridCard>();
        private readonly List<UIShopGridCard> stoneCards = new List<UIShopGridCard>();
        private readonly List<UIShopGridCard> goldCards = new List<UIShopGridCard>();

        /// <summary>다음 갱신 표기를 몇 초마다 다시 그릴지. 초 단위로 적히므로 1초면 된다.</summary>
        private float timerRefreshAt;

        protected override void OnLoad()
        {
            // 템플릿은 꺼 둔다. 켜진 채로 남으면 데이터가 0개인 섹션에 <b>예시 값이 박힌
            // 항목 하나</b>가 남아, 그것이 진짜 상품인지 굽다 만 흔적인지 알 수 없다.
            SetTemplateActive(growthTemplate, false);
            SetTemplateActive(cardTemplate, false);
            SetTemplateActive(dailySlotTemplate, false);
            SetTemplateActive(gridCardTemplate, false);
        }

        protected override void OnEnter()
        {
            RefreshAll();

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            if (ShopPurchaseManager.Instance != null)
            {
                ShopPurchaseManager.Instance.OnPurchaseStateChanged += RefreshAll;
                ShopPurchaseManager.Instance.OnRewardGranted += ShowRewardPopup;
                ShopPurchaseManager.Instance.OnGemsDrawn += ShowGemPopup;
            }
        }

        protected override void OnExit()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;

            if (ShopPurchaseManager.Instance != null)
            {
                ShopPurchaseManager.Instance.OnPurchaseStateChanged -= RefreshAll;
                ShopPurchaseManager.Instance.OnRewardGranted -= ShowRewardPopup;
                ShopPurchaseManager.Instance.OnGemsDrawn -= ShowGemPopup;
            }
        }

        /// <summary>
        /// 획득 팝업. 결과창·스테이지 보상·별 보상이 쓰는 <c>UIRewardResultDialog</c> 를 그대로 쓴다 —
        /// 상점만 자기 팝업을 만들면 <b>같은 재화가 화면마다 다른 모양으로</b> 뜬다.
        ///
        /// 못 열어도 구매는 이미 끝났다. 그래서 막지 않고 로그만 남긴다 — 여기서 되돌리면
        /// 팝업이 없다는 이유로 <b>산 것이 취소되는</b> 더 나쁜 상태가 된다.
        /// </summary>
        private void ShowRewardPopup(IReadOnlyList<PointRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return;

            UIRewardResultDialog dialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (dialog == null)
            {
                Debug.LogError("[상점] UIRewardResultDialog 를 못 열었다. 지급은 이미 끝났다 — " +
                               PointRewardUtility.BuildRewardSummary(rewards));
                return;
            }

            dialog.Open(rewards, "구매한 상품을 받았습니다.");
        }

        private void Update()
        {
            // 갱신 타이머만 매초 다시 적는다. 전체 Refresh 를 매초 돌리면 재화가 안 바뀌어도
            // 일곱 섹션이 통째로 다시 그려진다.
            if (!isEnter || dailyTimerText == null)
                return;

            if (Time.unscaledTime < timerRefreshAt)
                return;

            timerRefreshAt = Time.unscaledTime + 1f;
            dailyTimerText.SetText("갱신까지 " +
                ShopText.FormatTimer(ShopPurchaseManager.TimeUntilReset(DateTime.Now)));
        }

        private void OnPointChanged(PointType pointType, int value) => RefreshAll();

        public void RefreshAll()
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null)
                return;

            RefreshGrowth(db);
            RefreshGemDraw(db);
            RefreshDaily(db);
            RefreshPaidGem(db);
            RefreshFreeGem(db);
            RefreshStone(db);
            RefreshGold(db);
        }

        // ── 8.1 성장 패키지 ────────────────────────────────────────────

        private void RefreshGrowth(ShopDatabase db)
        {
            IReadOnlyList<ShopDatabase.GrowthPackage> packages = db.GrowthPackages;
            EnsurePool(growthBanners, growthTemplate, growthBody, packages.Count);

            for (int i = 0; i < growthBanners.Count; i++)
            {
                bool used = i < packages.Count;
                growthBanners[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                ShopDatabase.GrowthPackage package = packages[i];
                int index = i;
                growthBanners[i].Bind(package,
                    () => Report(ShopPurchaseManager.Instance?.TryBuyGrowthPackage(index)));
            }
        }

        // ── 8.2 보석뽑기 ──────────────────────────────────────────────

        private void RefreshGemDraw(ShopDatabase db)
        {
            IReadOnlyList<ShopDatabase.GemBox> boxes = db.GemBoxes;
            EnsurePool(gemBoxCards, cardTemplate, gemDrawBody, boxes.Count);

            for (int i = 0; i < gemBoxCards.Count; i++)
            {
                bool used = i < boxes.Count;
                gemBoxCards[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                ShopDatabase.GemBox box = boxes[i];
                int index = i;

                string caption = UIEquipmentText.GetRarityName(box.minRarity) + " ~ " +
                                 UIEquipmentText.GetRarityName(box.maxRarity);
                string costName = PointRewardUtility.GetPointName(box.costType);

                gemBoxCards[i].SetRarityIcons(BuildRarityIcons(box.minRarity, box.maxRarity));

                gemBoxCards[i].Bind(
                    box.title,
                    UIEquipmentSpriteResolver.GetGemIconSprite(box.maxRarity),
                    caption,
                    // 확률은 카드 안에 상시 노출한다(8.2-4). 표가 미정이라 지금은 자리만 잡는다.
                    "확률 미정",
                    costName + " " + ShopText.FormatAmount(box.costSingle) + " · 1회",
                    Affordable(box.costType, box.costSingle),
                    () => Report(ShopPurchaseManager.Instance?.TryDrawGemBox(index, 1)),
                    costName + " " + ShopText.FormatAmount(box.costTen) + " · 10회",
                    Affordable(box.costType, box.costTen),
                    () => Report(ShopPurchaseManager.Instance?.TryDrawGemBox(index, 10)));
            }
        }

        /// <summary>
        /// 뽑은 보석을 결과창에 띄운다. 같은 보석은 <b>한 칸에 모아</b> 수량으로 적는다 —
        /// 10회를 뽑으면 칸이 열 개가 되는데, 그중 여섯이 같은 것이면 무엇을 얼마나 얻었는지가
        /// 오히려 안 읽힌다.
        /// </summary>
        private void ShowGemPopup(IReadOnlyList<GemDefinition> gems)
        {
            if (gems == null || gems.Count == 0)
                return;

            var counts = new Dictionary<string, int>();
            var order = new List<GemDefinition>();

            for (int i = 0; i < gems.Count; i++)
            {
                GemDefinition gem = gems[i];
                if (gem == null)
                    continue;

                if (counts.ContainsKey(gem.gemId))
                {
                    counts[gem.gemId]++;
                    continue;
                }

                counts[gem.gemId] = 1;
                order.Add(gem);
            }

            // 높은 등급이 앞에 온다. 기획서 8.2-5 의 "최고 등급만 강조" 를 정렬로 대신한다 —
            // 연출은 아직 없지만 <b>제일 좋은 것이 먼저 보이는</b> 것까지는 지금 할 수 있다.
            order.Sort((left, right) => ((int)right.rarity).CompareTo((int)left.rarity));

            var items = new List<IconRewardView>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                items.Add(new IconRewardView(
                    UIEquipmentSpriteResolver.GetGemIconSprite(order[i].rarity),
                    counts[order[i].gemId]));
            }

            UIRewardResultDialog dialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (dialog == null)
            {
                Debug.LogError("[상점] UIRewardResultDialog 를 못 열었다. 보석은 이미 들어갔다.");
                return;
            }

            dialog.OpenIcons(items, "보석을 획득했습니다.");
        }

        /// <summary>
        /// 등급 구간을 아이콘 목록으로 편다. 장비/보석 화면과 <b>같은 스프라이트</b>를 쓴다
        /// (<c>Resources/Art/ItemSlot/Slot_Gem_n</c>) — 상점만 다른 그림을 쓰면 같은 등급이
        /// 화면마다 다르게 보인다.
        ///
        /// 칸이 <see cref="UIShopOfferCard.RarityIconSlots"/> 개뿐이라 구간이 그보다 넓으면
        /// <b>앞에서부터</b> 자른다. 지금 두 상자는 정확히 3단계다.
        /// </summary>
        private static List<Sprite> BuildRarityIcons(Rarity min, Rarity max)
        {
            var sprites = new List<Sprite>(UIShopOfferCard.RarityIconSlots);

            for (int rarity = (int)min; rarity <= (int)max; rarity++)
            {
                if (sprites.Count >= UIShopOfferCard.RarityIconSlots)
                    break;

                sprites.Add(UIEquipmentSpriteResolver.GetGemIconSprite((Rarity)rarity));
            }

            return sprites;
        }

        // ── 8.3 일일상점 ──────────────────────────────────────────────

        private void RefreshDaily(ShopDatabase db)
        {
            IReadOnlyList<ShopDatabase.DailyOffer> offers = db.DailyOffers;
            EnsurePool(dailySlots, dailySlotTemplate, dailyGrid, offers.Count);

            ShopPurchaseManager purchases = ShopPurchaseManager.Instance;

            for (int i = 0; i < dailySlots.Count; i++)
            {
                bool used = i < offers.Count;
                dailySlots[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                ShopDatabase.DailyOffer offer = offers[i];
                int index = i;

                bool sold = purchases != null && purchases.IsDailySlotSold(index);
                int cost = ShopPurchaseManager.DiscountedCost(offer.cost, offer.discountPercent);

                dailySlots[i].Bind(offer, sold, Affordable(offer.costType, cost),
                    () => Report(purchases?.TryBuyDailySlot(index)));
            }

            if (dailyTimerText != null)
            {
                dailyTimerText.SetText("갱신까지 " +
                    ShopText.FormatTimer(ShopPurchaseManager.TimeUntilReset(DateTime.Now)));
            }

            if (refreshBar != null)
            {
                int adUsed = purchases != null ? purchases.GetAdRefreshCount() : 0;
                int gemUsed = purchases != null ? purchases.GetGemRefreshCount() : 0;

                refreshBar.Bind(
                    adUsed, db.DailyAdRefreshPerDay,
                    () => Report(purchases?.TryRefreshDailyByAd()),
                    gemUsed, db.DailyGemRefreshPerDay, db.DailyGemRefreshCost,
                    Affordable(PointType.FreeGem, db.DailyGemRefreshCost),
                    () => Report(purchases?.TryRefreshDailyByGem()));
            }
        }

        // ── 8.4 유료젬 상점 ───────────────────────────────────────────

        private void RefreshPaidGem(ShopDatabase db)
        {
            IReadOnlyList<ShopDatabase.PaidGemOffer> offers = db.PaidGemOffers;
            EnsurePool(paidGemCards, gridCardTemplate, paidGemBody, offers.Count);

            for (int i = 0; i < paidGemCards.Count; i++)
            {
                bool used = i < offers.Count;
                paidGemCards[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                ShopDatabase.PaidGemOffer offer = offers[i];
                int index = i;

                // costType 이 Max 면 현금이다. 카드가 그것으로 버튼 색과 아이콘 유무를 정한다.
                paidGemCards[i].Bind(
                    PointType.PaidGem, offer.gemAmount,
                    PointType.Max, ShopText.FormatWon(offer.priceWon), true,
                    () => Report(ShopPurchaseManager.Instance?.TryBuyPaidGem(index)));
            }
        }

        // ── 8.5 무료젬 상점 ───────────────────────────────────────────

        private void RefreshFreeGem(ShopDatabase db)
        {
            IReadOnlyList<int> units = db.FreeGemExchangeUnits;
            EnsurePool(freeGemCards, gridCardTemplate, freeGemBody, units.Count);

            for (int i = 0; i < freeGemCards.Count; i++)
            {
                bool used = i < units.Count;
                freeGemCards[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                int paid = units[i];
                int gained = paid * db.FreeGemPerPaidGem;

                // 받는 것이 무료젬, 내는 것이 유료젬. 카드가 지불 아이콘을 버튼에 그려 주므로
                // "무엇으로 사는지"가 글자가 아니라 그림으로 먼저 읽힌다.
                freeGemCards[i].Bind(
                    PointType.FreeGem, gained,
                    PointType.PaidGem, ShopText.FormatAmount(paid),
                    Affordable(PointType.PaidGem, paid),
                    () => ConfirmExchange(paid, gained));
            }

            if (freeGemRateText != null)
                freeGemRateText.SetText("유료젬 1 = 무료젬 {0}", db.FreeGemPerPaidGem);
        }

        /// <summary>
        /// 되돌릴 수 없는 방향이라 확인을 한 번 받는다(기획서 8.5-2).
        /// 팝업을 못 띄우면 <b>교환하지 않는다</b> — 확인 없이 진행하는 것이 더 나쁘다.
        /// </summary>
        private void ConfirmExchange(int paidGemAmount, int gained)
        {
            UIConfirmDialog dialog = GameContainer.UI?.Get<UIConfirmDialog>();
            if (dialog == null)
            {
                Debug.LogError("[상점] UIConfirmDialog 를 못 열어 교환을 중단한다. " +
                               "되돌릴 수 없는 거래라 확인 없이 진행하지 않는다.");
                return;
            }

            dialog.Open(
                "무료젬 교환",
                string.Format("유료젬 {0} 을 무료젬 {1} 로 바꿉니다.\n되돌릴 수 없습니다.",
                    ShopText.FormatAmount(paidGemAmount), ShopText.FormatAmount(gained)),
                "교환",
                "취소",
                () => Report(ShopPurchaseManager.Instance?.TryExchangeFreeGem(paidGemAmount)));
        }

        // ── 8.6 다이스석 상점 ─────────────────────────────────────────

        private void RefreshStone(ShopDatabase db)
        {
            IReadOnlyList<ShopDatabase.StoneOffer> offers = db.StoneOffers;
            EnsurePool(stoneCards, gridCardTemplate, stoneBody, offers.Count);

            for (int i = 0; i < stoneCards.Count; i++)
            {
                bool used = i < offers.Count;
                stoneCards[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                ShopDatabase.StoneOffer offer = offers[i];
                int index = i;

                // 한도도 누진도 없다. 값이 그대로 가격이고, 살 수 있는지는 보유량만 본다.
                stoneCards[i].Bind(
                    offer.stoneType, offer.amountPerPurchase,
                    PointType.FreeGem, ShopText.FormatAmount(offer.costFreeGem),
                    Affordable(PointType.FreeGem, offer.costFreeGem),
                    () => Report(ShopPurchaseManager.Instance?.TryBuyStone(index)));
            }
        }

        // ── 8.7 골드 상점 ─────────────────────────────────────────────

        private void RefreshGold(ShopDatabase db)
        {
            IReadOnlyList<ShopDatabase.GoldOffer> offers = db.GoldOffers;
            EnsurePool(goldCards, gridCardTemplate, goldBody, offers.Count);

            ShopPurchaseManager purchases = ShopPurchaseManager.Instance;

            for (int i = 0; i < goldCards.Count; i++)
            {
                bool used = i < offers.Count;
                goldCards[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                ShopDatabase.GoldOffer offer = offers[i];
                int index = i;

                // 누진이라 오늘 산 횟수만큼 값이 오른다(기획서 8.7-2). 카드에 적히는 것은
                // <b>지금 눌렀을 때의 값</b>이라, 사고 나면 그 자리에서 숫자가 바뀐다.
                int bought = purchases != null ? purchases.GetTodayCount(ShopPurchaseManager.GoldKey(index)) : 0;
                int cost = ShopPurchaseManager.ProgressiveCost(
                    offer.baseCostFreeGem, offer.costIncreasePerPurchase, bought);

                goldCards[i].Bind(
                    PointType.Gold, offer.goldAmount,
                    PointType.FreeGem, ShopText.FormatAmount(cost),
                    Affordable(PointType.FreeGem, cost),
                    () => Report(purchases?.TryBuyGold(index)));
            }
        }

        // ── 공통 ──────────────────────────────────────────────────────

        private static bool Affordable(PointType pointType, int cost)
        {
            PointManager points = PointManager.Instance;
            return points != null && points.Get(pointType) >= cost;
        }

        /// <summary>
        /// 실패 사유를 알린다. <b>지금은 로그뿐이다</b> — 토스트 UI 가 프로젝트에 없고,
        /// 여기서 새로 만들면 상점 전용 토스트가 하나 생겨 나중에 공용과 둘이 된다.
        /// 토스트가 생기면 이 메서드 하나만 고치면 된다.
        /// </summary>
        private void Report(ShopPurchaseResult? result)
        {
            if (result == null || result == ShopPurchaseResult.Success)
                return;

            switch (result.Value)
            {
                case ShopPurchaseResult.NotEnoughCurrency:
                    Debug.Log("[상점] 재화가 모자랍니다.");
                    break;
                case ShopPurchaseResult.AlreadySold:
                    Debug.Log("[상점] 이미 구매한 상품입니다.");
                    break;
                case ShopPurchaseResult.RefreshLimitReached:
                    Debug.Log("[상점] 오늘 갱신 횟수를 다 썼습니다.");
                    break;
            }
        }

        private static void SetTemplateActive(Component template, bool active)
        {
            if (template != null)
                template.gameObject.SetActive(active);
        }

        /// <summary>
        /// 항목 수를 <paramref name="count"/> 이상으로 맞춘다. <b>줄이지는 않는다</b> —
        /// 파괴하고 다시 만들면 재화가 바뀔 때마다 (Refresh 가 그때마다 돈다) 일곱 섹션의
        /// 오브젝트가 통째로 다시 생성된다.
        /// </summary>
        private void EnsurePool<T>(List<T> pool, T template, RectTransform parent, int count)
            where T : Component
        {
            if (template == null || parent == null)
                return;

            while (pool.Count < count)
            {
                T item = Instantiate(template, parent);
                item.gameObject.SetActive(false);
                pool.Add(item);
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        /// <summary>에디터 굽기 전용.</summary>
        public static UIShopPage Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIShopUIFactory.CreateRect("UIShopPage", parent);
            UIShopUIFactory.Stretch(root.GetComponent<RectTransform>());

            var page = root.AddComponent<UIShopPage>();

            GameObject view = UIShopUIFactory.CreateRect("View", root.transform);
            UIShopUIFactory.Stretch(view.GetComponent<RectTransform>());
            page.dialogView = view;

            // 로비 탭 내용물이다. 백키는 창을 닫는 것이 아니라 홈 탭으로 가야 하고,
            // 그 판단은 LobbyLayoutController 가 BackKeyOverride 로 넣는다.
            page.UseBackBtn = true;

            RectTransform content = CreateScrollBody(view.transform);

            // 성장 패키지는 <b>가로 스와이프가 아니라 세로 나열</b>이다. 기획서 8.1 목업은
            // 페이저(인디케이터 * o)지만, 동시 노출 상한이 2개이고 배너 하나가 440 이라
            // 둘을 세로로 쌓아도 접히지 않는다(로비 내용물 높이 약 1300). 페이저는 두 번째
            // 배너의 존재를 점 하나로만 알리는데, 나열은 그냥 보인다.
            // <b>상한이 3개 이상으로 바뀌면 이 판단이 뒤집힌다.</b>
            page.growthBody = BuildSection(content, 0, font, out _, SectionBody.Vertical);
            page.gemDrawBody = BuildSection(content, 1, font, out _, SectionBody.Horizontal);

            // 일일상점만 본체가 두 겹이다 — 6칸 그리드 + 그 아래 갱신 버튼 줄.
            // 그리드에 갱신 버튼을 같이 넣으면 그것도 슬롯 한 칸으로 앉는다.
            page.dailyBody = BuildSection(content, 2, font, out page.dailyTimerText, SectionBody.Vertical);
            page.dailyGrid = BuildGridChild(page.dailyBody, UIShopDailySlot.SlotHeight);
            page.refreshBar = UIShopDailyRefreshBar.Create(page.dailyBody, font);

            // 재화 상점 셋은 레퍼런스 스샷대로 3열 카드다(수량 티어가 나란히 비교돼야 한다).
            page.paidGemBody = BuildSection(content, 3, font, out _, SectionBody.Grid);
            page.freeGemBody = BuildSection(content, 4, font, out page.freeGemRateText, SectionBody.Grid);
            page.stoneBody = BuildSection(content, 5, font, out _, SectionBody.Grid);
            page.goldBody = BuildSection(content, 6, font, out _, SectionBody.Grid);

            // 템플릿은 꺼진 채로 프리팹에 들어간다. 복제될 때 부모가 바뀌므로 어디에
            // 두어도 되지만, 각자 쓰이는 섹션 밑에 두면 프리팹을 열었을 때 눈에 띈다.
            page.growthTemplate = UIShopGrowthBanner.Create(page.growthBody, font);
            page.cardTemplate = UIShopOfferCard.Create(page.gemDrawBody, font);
            page.dailySlotTemplate = UIShopDailySlot.Create(page.dailyGrid, font);
            page.gridCardTemplate = UIShopGridCard.Create(page.paidGemBody, font);

            return page;
        }

        private static RectTransform CreateScrollBody(Transform parent)
        {
            GameObject viewport = UIShopUIFactory.CreateRect("Scroll", parent);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            UIShopUIFactory.Stretch(viewportRect);

            // <b>배경을 뷰포트 자체에 붙인다.</b> 형제로 따로 두면 배경 위를 끌어도
            // 스크롤이 안 된다 — ScrollRect 는 드래그를 <c>Graphic</c> 으로 받는데
            // 뷰포트에 그래픽이 없으면 <b>카드와 버튼 위에서만</b> 끌리고 빈 곳은 안 먹는다.
            // 섹션 사이 여백과 머리글이 전부 그 "빈 곳" 이라 체감이 크다.
            Image background = viewport.AddComponent<Image>();
            background.color = UIShopUIFactory.PageBg;
            background.raycastTarget = true;

            // 마스크가 없으면 목록이 로비 화면 전체를 덮는다. 그 상태에서도 스크롤은
            // 되기 때문에 버그로 보이지 않는다.
            viewport.AddComponent<RectMask2D>();

            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            GameObject content = UIShopUIFactory.CreateRect("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();

            // 위에서 아래로 쌓인다. 앵커를 위쪽에 붙여야 섹션이 늘어날 때 목록이 아래로
            // 자라고 첫 섹션이 제자리에 남는다.
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            // 아래 여백이 크다. 이 페이지는 로비 <c>Content</c> 를 꽉 채우는데 그 아래에
            // <b>하단 탭 바가 겹쳐 있어</b>, 여백이 없으면 마지막 섹션(골드)이 탭 바에
            // 가려 끝까지 못 내려간다. 탭 바 높이보다 넉넉히 준다.
            UIShopUIFactory.AddVertical(content, spacing: 32f, padding: 0f)
                .padding = new RectOffset(0, 0, 24, BottomInset);
            UIShopUIFactory.AddVerticalFitter(content);

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            return contentRect;
        }

        /// <summary>
        /// 목록 맨 아래 여백. 로비 하단 탭 바가 이 페이지 위에 겹치므로 그만큼 비워 둔다.
        /// 1080x1920 기준 탭 바가 약 200 이고, 스크롤이 끝났다는 것이 보이도록 조금 더 준다.
        /// </summary>
        private const int BottomInset = 260;

        /// <summary>섹션 본체의 배치 방식. 굽기 전용.</summary>
        private enum SectionBody
        {
            /// <summary>세로 나열. 항목 수가 데이터에 따라 변하는 섹션.</summary>
            Vertical,

            /// <summary>가로 2열 카드. 높이가 카드 하나로 고정된다.</summary>
            Horizontal,

            /// <summary>3열 그리드. 일일상점 6칸이 유일하다.</summary>
            Grid,
        }

        /// <summary>세로 본체 안에 3열 그리드를 한 겹 더 넣는다. 일일상점만 쓴다.</summary>
        private static RectTransform BuildGridChild(RectTransform body, float cellHeight)
        {
            GameObject grid = UIShopUIFactory.CreateRect("Grid", body);
            UIShopUIFactory.AddGrid(grid, columns: 3, spacing: 16f, cellHeight: cellHeight);

            // 바깥 본체가 이미 좌우 여백을 줬다. 그리드가 또 주면 두 배가 된다.
            grid.GetComponent<GridLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            return grid.GetComponent<RectTransform>();
        }

        private static RectTransform BuildSection(
            RectTransform content, int orderIndex, TMP_FontAsset font,
            out TMP_Text sideLabel, SectionBody kind)
        {
            string title = SectionOrder[orderIndex];

            RectTransform body = UIShopUIFactory.CreateSection(
                "Section" + orderIndex + "_" + title, content, title, string.Empty, font, out sideLabel);

            switch (kind)
            {
                case SectionBody.Grid:
                    UIShopUIFactory.AddGrid(body.gameObject, columns: 3, spacing: 16f,
                        cellHeight: UIShopGridCard.CardHeight);
                    break;

                case SectionBody.Horizontal:
                    UIShopUIFactory.AddHorizontal(body.gameObject, spacing: 20f,
                        padding: UIShopUIFactory.SidePadding);
                    UIShopUIFactory.SetPreferredHeight(body.gameObject, UIShopOfferCard.CardHeight);
                    break;

                default:
                    // 좌우 여백만 준다. 위아래는 섹션 사이 간격이 이미 벌려 준다.
                    UIShopUIFactory.AddVertical(body.gameObject, spacing: 12f, padding: 0f).padding =
                        new RectOffset((int)UIShopUIFactory.SidePadding, (int)UIShopUIFactory.SidePadding, 0, 0);
                    UIShopUIFactory.AddVerticalFitter(body.gameObject);
                    break;
            }

            return body;
        }
    }
}
