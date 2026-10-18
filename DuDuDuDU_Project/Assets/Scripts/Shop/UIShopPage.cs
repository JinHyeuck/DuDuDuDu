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
        /// <summary>
        /// 섹션 순서와 머리글. 기획서 7.1 의 순서를 지키되 <b>이름은 시안을 따른다</b> —
        /// 시안이 재화를 그림으로 부르기 때문에("깃털", "다이아") 글자도 같아야 한다.
        /// 어느 재화인지는 <c>PointMetadataDatabase</c> 의 아이콘과 짝이 맞는다:
        /// 깃털 = 유료젬, 다이아 = 무료젬, 강화석 = 다이스석.
        ///
        /// <b>빈 문자열은 머리글 바를 만들지 않는다는 뜻이다.</b> 시안에서 패키지와
        /// 광고 칸은 바 없이 바로 내용이 온다.
        /// </summary>
        internal static readonly string[] SectionOrder =
        {
            "",               // 0 패키지 (8.1 성장 패키지)
            "보석 보물상자",  // 1 (8.2 보석뽑기)
            "",               // 2 광고 상점 — 시안에 새로 생겼다
            "깃털 상점",      // 3 (8.4 유료젬 상점)
            "다이아",         // 4 (8.5 무료젬 상점)
            "강화석",         // 5 (8.6 다이스석 상점)
            "골드",           // 6 (8.7 골드 상점)
        };

        [Header("섹션 본체")]
        [SerializeField] private RectTransform growthBody;
        [SerializeField] private RectTransform gemDrawBody;
        [SerializeField] private RectTransform paidGemBody;
        [SerializeField] private RectTransform freeGemBody;
        [SerializeField] private RectTransform stoneBody;
        [SerializeField] private RectTransform goldBody;

        /// <summary>교환비 안내. 시안에 자리가 없어 굽지 않는다 — 비어 있어도 된다.</summary>
        [SerializeField] private TMP_Text freeGemRateText;

        /// <summary>광고 보상 칸과 원터치 토글. 겉모습만이고 동작은 아직 안 붙었다.</summary>
        [SerializeField] private UIShopAdSection adSection;


        [Header("템플릿 (프리팹 안에 꺼진 채로 들어 있다)")]
        [SerializeField] private UIShopGrowthBanner growthTemplate;
        [SerializeField] private UIShopOfferCard cardTemplate;
        [SerializeField] private UIShopGridCard gridCardTemplate;

        private readonly List<UIShopGrowthBanner> growthBanners = new List<UIShopGrowthBanner>();
        private readonly List<UIShopOfferCard> gemBoxCards = new List<UIShopOfferCard>();
        private readonly List<UIShopGridCard> paidGemCards = new List<UIShopGridCard>();
        private readonly List<UIShopGridCard> freeGemCards = new List<UIShopGridCard>();
        private readonly List<UIShopGridCard> stoneCards = new List<UIShopGridCard>();
        private readonly List<UIShopGridCard> goldCards = new List<UIShopGridCard>();

        /// <summary>다음 갱신 표기를 몇 초마다 다시 그릴지. 초 단위로 적히므로 1초면 된다.</summary>

        protected override void OnLoad()
        {
            // 템플릿은 꺼 둔다. 켜진 채로 남으면 데이터가 0개인 섹션에 <b>예시 값이 박힌
            // 항목 하나</b>가 남아, 그것이 진짜 상품인지 굽다 만 흔적인지 알 수 없다.
            SetTemplateActive(growthTemplate, false);
            SetTemplateActive(cardTemplate, false);
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


        private void OnPointChanged(PointType pointType, int value) => RefreshAll();

        public void RefreshAll()
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null)
                return;

            RefreshGrowth(db);
            RefreshGemDraw(db);
            RefreshAdShop();
            RefreshPaidGem(db);
            RefreshFreeGem(db);
            RefreshStone(db);
            RefreshGold(db);
        }

        // ── 광고 상점 (시안에 새로 생긴 칸) ──────────────────────────

        /// <summary>
        /// 광고 칸을 채운다. <b>아직 데이터가 없다</b> — 하루 한도와 지급은 세이브에 자리가
        /// 없어서, 지금은 시안의 숫자를 그대로 두고 누르면 준비 중을 알린다.
        /// 연결할 때는 여기 두 콜백과 <c>ShopPurchaseManager</c> 만 고치면 된다.
        /// </summary>
        private void RefreshAdShop()
        {
            if (adSection == null)
                return;

            adSection.Bind(
                adRemaining: AdShopDailyCount, adTotal: AdShopDailyCount,
                goldAmount: AdShopGoldAmount,
                goldRemaining: AdShopDailyCount, goldTotal: AdShopDailyCount,
                oneTouch: false,
                watchAd: () => Debug.Log("[상점] 광고 보상은 아직 연결되지 않았다(MIGRATION_BASELINE 알려진 미동작)."),
                claimGold: () => Debug.Log("[상점] 광고 골드 지급은 아직 연결되지 않았다."),
                oneTouchChanged: on => Debug.Log("[상점] 원터치 구매 설정은 아직 저장되지 않는다: " + on));
        }

        /// <summary>시안에 적힌 하루 한도. 데이터가 생기면 <c>ShopDatabase</c> 로 올린다.</summary>
        private const int AdShopDailyCount = 2;

        /// <summary>시안에 적힌 광고 보상 골드.</summary>
        private const int AdShopGoldAmount = 10000;

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

                // 등급 구간과 확률을 한 줄로 합친다. 카드가 시안 크기라 줄을 둘 둘 자리가 없다.
                string caption = UIEquipmentText.GetRarityName(box.minRarity) + " ~ " +
                                 UIEquipmentText.GetRarityName(box.maxRarity) + " · 확률 미정";
                string costName = PointRewardUtility.GetPointName(box.costType);

                gemBoxCards[i].SetRarityIcons(BuildRarityIcons(box.minRarity, box.maxRarity));

                gemBoxCards[i].Bind(
                    box.title,
                    // 시안은 상자 그림이 곧 상품이다. 등급 보석 아이콘을 쓰면 상자가 사라진다.
                    ShopTierArt.GemBoxArt(i),
                    caption,
                    // 확률은 caption 에 합쳐 적는다(8.2-4). 카드에 둘째 줄 자리가 없다.
                    string.Empty,
                    costName + " " + ShopText.FormatAmount(box.costSingle) + " · 1회",
                    Affordable(box.costType, box.costSingle),
                    () => Report(ShopPurchaseManager.Instance?.TryDrawGemBox(index, 1)),
                    costName + " " + ShopText.FormatAmount(box.costTen) + " · 10회",
                    Affordable(box.costType, box.costTen),
                    () => Report(ShopPurchaseManager.Instance?.TryDrawGemBox(index, 10)),
                    ShopTierArt.GemBoxCard(i));
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
                    () => Report(ShopPurchaseManager.Instance?.TryBuyPaidGem(index)),
                    ShopTierArt.Feather(i + 1));
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
                    () => ConfirmExchange(paid, gained),
                    ShopTierArt.Diamond(i + 1));
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
                    () => Report(ShopPurchaseManager.Instance?.TryBuyStone(index)),
                    ShopTierArt.Stone(offer.stoneType, i + 1));
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
                    () => Report(purchases?.TryBuyGold(index)),
                    ShopTierArt.Gold(i + 1));
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

        /// <summary>
        /// 에디터 굽기 전용. 시안 <c>Art/Layout/Shop_*.png</c> 다섯 장이 한 화면의
        /// 스크롤 구간들이라, <b>배너는 고정 머리글이고 그 아래만 스크롤</b>한다 —
        /// 다섯 장 모두 같은 배너를 달고 있고, 각 장의 내용이 전부 뷰포트 1142 안에 든다.
        ///
        /// 좌표는 PSD(1080x1920)에서 잰 절대값이다. 로비 <c>Content</c> 가 화면 전체라
        /// PSD 좌표가 그대로 캔버스 좌표가 된다 — <see cref="Page"/> 가 그 변환이다.
        /// </summary>
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

            BuildBanner(view.transform, font);
            RectTransform content = CreateScrollBody(view.transform);

            // 섹션은 SectionOrder 순서 그대로 쌓인다. 머리글이 빈 섹션은 바 없이 내용만 온다.
            page.growthBody = BuildSection(content, 0, font, SectionBody.Vertical);
            page.gemDrawBody = BuildSection(content, 1, font, SectionBody.Horizontal);

            RectTransform adBody = BuildSection(content, 2, font, SectionBody.Free);
            page.adSection = UIShopAdSection.Create(adBody, font);

            page.paidGemBody = BuildSection(content, 3, font, SectionBody.Grid);
            page.freeGemBody = BuildSection(content, 4, font, SectionBody.Grid);
            page.stoneBody = BuildSection(content, 5, font, SectionBody.Grid);
            page.goldBody = BuildSection(content, 6, font, SectionBody.Grid);

            BuildFooter(content, font);

            // 템플릿은 꺼진 채로 프리팹에 들어간다. 복제될 때 부모가 바뀌므로 어디에
            // 두어도 되지만, 각자 쓰이는 섹션 밑에 두면 프리팹을 열었을 때 눈에 띈다.
            page.growthTemplate = UIShopGrowthBanner.Create(page.growthBody, font);
            page.cardTemplate = UIShopOfferCard.Create(page.gemDrawBody, font);
            page.gridCardTemplate = UIShopGridCard.Create(page.paidGemBody, font);

            return page;
        }

        /// <summary>PSD 절대 좌표 → 캔버스 좌표(1080x1920, 가운데 원점).</summary>
        private static Vector2 Page(float psdX, float psdY)
        {
            return new Vector2(psdX - 540f, 960f - psdY);
        }

        /// <summary>
        /// 고정 머리글. 스크롤을 타지 않는다 — 시안 다섯 장이 모두 같은 배너를 달고 있다.
        ///
        /// <b>로비 상단 HUD(프로필·재화)가 이 위에 겹친다.</b> 시안은 좌상단에 "상점" 제목을
        /// 두어 둘이 같은 자리를 쓴다. 상점 탭에서 HUD 를 끄는 것은
        /// <c>LobbyLayoutController</c> 의 일이라 여기서는 시안대로만 그린다.
        /// </summary>
        private static void BuildBanner(Transform parent, TMP_FontAsset font)
        {
            GameObject banner = UIShopUIFactory.CreateRect("Banner", parent);
            RectTransform rect = banner.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, UIShopUIFactory.BannerHeight);
            rect.anchoredPosition = Vector2.zero;

            // 배너 안쪽 좌표는 배너 중심 기준이다. 아래 Local 이 PSD 좌표를 그리로 옮긴다.
            Vector2 Local(float psdX, float psdY) =>
                new Vector2(psdX - 540f, UIShopUIFactory.BannerHeight * 0.5f - psdY);

            // 그림은 Shop_Benner 한 장이다. 세로 x2 가 시안과 맞고(실측), 가로만 1080 에
            // 맞춰 늘린다 — x2 로 두면 좌우에 13px 씩 빈다(PSD 는 광선 레이어가 따로 있었다).
            Image art = UIShopUIFactory.CreateSprite(
                "Art", banner.transform, UIShopUIFactory.Art("Shop/Shop_Benner"), 2f,
                new Vector2(1080f, 1024f), Local(540f, 239f));
            art.transform.SetAsFirstSibling();

            TMP_Text title = UIShopUIFactory.CreateText("Title", banner.transform, "상점", 95f,
                TextAlignmentOptions.Left, UIShopUIFactory.BannerTitleColor, font);
            UIShopUIFactory.SetRect(title.rectTransform, new Vector2(520f, 120f), Local(291f, 86.5f));

            TMP_Text subtitle = UIShopUIFactory.CreateText("Subtitle", banner.transform,
                "모험에 필요한 모든 것!", 50f, TextAlignmentOptions.Left, Color.white, font);
            UIShopUIFactory.SetRect(subtitle.rectTransform, new Vector2(620f, 64f), Local(350f, 169.5f));

            // 배너와 목록을 가르는 띠. 시안 (481)-(567) 이다.
            // <b>첫 결제 띠보다 먼저 만든다</b> — 둘이 481~534 에서 겹치는데, 나중에 만들면
            // 띠가 첫 결제 버튼의 아래 절반을 덮는다.
            UIShopUIFactory.CreateDivider("Divider", banner.transform, 1080f, Local(540f, 524f));

            // 첫 결제 띠. Pass_premium_Btn 은 가로만 9슬라이스라(테두리 56,0,19,0)
            // 세로 여백은 보이는 높이에 비례한다(텍스처 128 에 보이는 것이 34).
            float strip = 102f;
            UIShopUIFactory.CreateKit("FirstPay", banner.transform,
                UIShopUIFactory.Art("Pass/Pass_premium_Btn"), 3f,
                new Vector2(701f, strip), Local(545.5f, 483f),
                new Vector4(36f, strip * 47f / 34f, 36f, strip * 47f / 34f));

            TMP_Text promo = UIShopUIFactory.CreateText("Promo", banner.transform,
                "첫 결제 보너스! <color=#FF5A00>킹 다이스</color> 증정!", 45f,
                TextAlignmentOptions.Center, Color.white, font);
            UIShopUIFactory.SetRect(promo.rectTransform, new Vector2(680f, 60f), Local(545f, 483f));

        }

        /// <summary>
        /// 맨 아래 구매 복원과 청약 철회 안내. 섹션이 아니라 목록 꼬리라 따로 만든다 —
        /// <b>법에서 요구하는 문구</b>라 상품이 하나도 없어도 사라지면 안 된다.
        /// </summary>
        private static void BuildFooter(RectTransform content, TMP_FontAsset font)
        {
            GameObject footer = UIShopUIFactory.CreateRect("Footer", content);
            UIShopUIFactory.SetPreferredHeight(footer, FooterHeight);

            // 꼬리 안쪽 좌표. 시안에서 이 묶음은 1211 부터 1620 까지이고,
            // 앞 섹션과의 사이에 섹션 간격(38)보다 조금 더 벌어져 있다.
            Vector2 Local(float psdX, float psdY) =>
                new Vector2(psdX - 540f, FooterHeight * 0.5f - (psdY - 1211f));

            UIShopUIFactory.CreateKitButton(
                "Restore", footer.transform, "InfinityMode/Btn_PurpleGray",
                new Vector2(292f, 189f), Local(547f, 1345.5f),
                "구매 복원", 40f, Color.white, font, scale: 4f);

            TMP_Text first = UIShopUIFactory.CreateText("Notice1", footer.transform,
                "청약 철회는 구매일로부터 7일 이내 가능합니다.", 40f,
                TextAlignmentOptions.Center, UIShopUIFactory.NoticeColor, font);
            UIShopUIFactory.SetRect(first.rectTransform, new Vector2(1000f, 52f), Local(540f, 1537.5f));

            TMP_Text second = UIShopUIFactory.CreateText("Notice2", footer.transform,
                "(단, 즉시 적용 및 일부 사용 시 청약 철회 불가)", 40f,
                TextAlignmentOptions.Center, UIShopUIFactory.NoticeColor, font);
            UIShopUIFactory.SetRect(second.rectTransform, new Vector2(1000f, 52f), Local(540f, 1601.5f));
        }

        /// <summary>꼬리 높이. 시안 1211 ~ 1620 에 아래 숨통을 조금 더 준 값이다.</summary>
        private const float FooterHeight = 449f;

        private static RectTransform CreateScrollBody(Transform parent)
        {
            GameObject viewport = UIShopUIFactory.CreateRect("Scroll", parent);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();

            // 배너 아래부터 하단 탭 바 위까지. 탭 바 밑으로 내려보내지 않으므로
            // 예전처럼 목록 끝에 빈 여백(BottomInset)을 둘 필요가 없다.
            //
            // <b>높이를 1920 에서 빼서 계산하면 안 된다.</b> 캔버스가 폭 기준으로 맞춰져
            // (match=0) 화면 비율이 다르면 캔버스 높이가 1920 이 아니다 — 에디터 게임 뷰가
            // 가로이기만 해도 607 이 된다. 그때 고정 크기로 둔 뷰포트는 화면 밖으로 나간다.
            // 위아래를 앵커로 물려 두면 어느 비율에서도 배너 아래 ~ 탭 바 위에 붙는다.
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.offsetMin = new Vector2(0f, UIShopUIFactory.TabBarHeight);
            viewportRect.offsetMax = new Vector2(0f, -UIShopUIFactory.BannerHeight);

            // <b>배경을 뷰포트 자체에 붙인다.</b> 형제로 따로 두면 배경 위를 끌어도
            // 스크롤이 안 된다 — ScrollRect 는 드래그를 <c>Graphic</c> 으로 받는데
            // 뷰포트에 그래픽이 없으면 <b>카드와 버튼 위에서만</b> 끌리고 빈 곳은 안 먹는다.
            Image background = viewport.AddComponent<Image>();
            background.color = UIShopUIFactory.PageBackground;
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

            // 시안 실측: 첫 머리글이 뷰포트 위에서 30, 섹션 사이가 38 이다.
            UIShopUIFactory.AddVertical(content, spacing: SectionSpacing, padding: 0f)
                .padding = new RectOffset(0, 0, 30, 40);
            UIShopUIFactory.AddVerticalFitter(content);

            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            return contentRect;
        }

        /// <summary>섹션 사이 간격. 시안에서 카드 아래와 다음 머리글 위가 38 이다.</summary>
        private const float SectionSpacing = 38f;

        /// <summary>머리글 바와 내용 사이. 시안에서 바 아래와 카드 위가 20 이다.</summary>
        private const float HeaderGap = 20f;

        /// <summary>섹션 본체의 배치 방식. 굽기 전용.</summary>
        private enum SectionBody
        {
            /// <summary>세로 나열. 항목 수가 데이터에 따라 변하는 섹션(패키지).</summary>
            Vertical,

            /// <summary>가로 2열. 보석 보물상자가 쓴다.</summary>
            Horizontal,

            /// <summary>3열 그리드. 재화 상점 넷이 쓴다.</summary>
            Grid,

            /// <summary>자식이 제 높이를 직접 정한다. 광고 칸처럼 좌표로 짠 덩어리.</summary>
            Free,
        }

        /// <summary>
        /// 섹션 한 칸을 만들고 <b>내용을 붙일 Body</b> 를 돌려준다.
        /// <see cref="SectionOrder"/> 의 제목이 비어 있으면 머리글 바를 만들지 않는다.
        /// </summary>
        private static RectTransform BuildSection(
            RectTransform content, int orderIndex, TMP_FontAsset font, SectionBody kind)
        {
            string title = SectionOrder[orderIndex];

            GameObject section = UIShopUIFactory.CreateRect("Section" + orderIndex + "_" + SafeName(title, kind), content);
            UIShopUIFactory.AddVertical(section, spacing: HeaderGap, padding: 0);
            UIShopUIFactory.AddVerticalFitter(section);

            if (!string.IsNullOrEmpty(title))
            {
                GameObject header = UIShopUIFactory.CreateRect("Header", section.transform);
                UIShopUIFactory.SetPreferredHeight(header, UIShopUIFactory.SectionBarSize.y + 9f);

                UIShopUIFactory.CreateSectionBar("Bar", header.transform,
                    new Vector2(UIShopUIFactory.SectionBarSize.x, UIShopUIFactory.SectionBarSize.y + 9f), Vector2.zero);

                TMP_Text label = UIShopUIFactory.CreateText("Title", header.transform, title, 45f,
                    TextAlignmentOptions.Center, Color.white, font);
                UIShopUIFactory.SetRect(label.rectTransform, new Vector2(420f, 56f), new Vector2(0f, 1f));
            }

            GameObject body = UIShopUIFactory.CreateRect("Body", section.transform);

            switch (kind)
            {
                case SectionBody.Grid:
                    // 시안 실측: 카드 폭 327, 왼쪽 여백 30 · 오른쪽 20, 가로 간격 24.5, 세로 37.
                    var grid = body.AddComponent<GridLayoutGroup>();
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 3;
                    grid.cellSize = UIShopGridCard.CardSize;
                    grid.spacing = new Vector2(24.5f, 37f);
                    grid.padding = new RectOffset(30, 20, 0, 0);
                    UIShopUIFactory.AddVerticalFitter(body);
                    break;

                case SectionBody.Horizontal:
                    // 보석 보물상자 두 장. 시안 (30,1208)-(521,·) 과 (551,·)-(1042,·) 이다.
                    var row = UIShopUIFactory.AddHorizontal(body, spacing: 30f, padding: 0f);
                    row.padding = new RectOffset(30, 38, 0, 0);
                    row.childForceExpandHeight = false;
                    UIShopUIFactory.SetPreferredHeight(body, UIShopOfferCard.CardHeight);
                    break;

                case SectionBody.Free:
                    UIShopUIFactory.AddVertical(body, spacing: 0f, padding: 0f);
                    UIShopUIFactory.AddVerticalFitter(body);
                    break;

                default:
                    UIShopUIFactory.AddVertical(body, spacing: 12f, padding: 0f);
                    UIShopUIFactory.AddVerticalFitter(body);
                    break;
            }

            return body.GetComponent<RectTransform>();
        }

        /// <summary>
        /// 머리글이 없는 섹션에도 이름이 있어야 한다. 하이어라키에서 "Section2_" 가
        /// 둘이면 어느 쪽을 보고 있는지 알 수 없다.
        /// </summary>
        private static string SafeName(string title, SectionBody kind)
        {
            return string.IsNullOrEmpty(title) ? kind.ToString() : title;
        }
    }
}
