using System;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Equipment;
using OJ.Point;
using OJ.Save;

namespace OJ.Shop
{
    /// <summary>구매 시도의 결과. 화면이 <b>왜 안 샀는지</b>를 말할 수 있어야 한다.</summary>
    public enum ShopPurchaseResult
    {
        Success = 0,

        /// <summary>재화가 모자란다.</summary>
        NotEnoughCurrency,

        /// <summary>이미 산 슬롯이다. (일일상점)</summary>
        AlreadySold,

        /// <summary>가리키는 상품이 없다. 데이터와 화면이 어긋난 것이므로 로그를 남긴다.</summary>
        InvalidOffer,

        /// <summary>오늘 갱신 횟수를 다 썼다. (일일상점)</summary>
        RefreshLimitReached,
    }

    /// <summary>
    /// 상점 구매의 실행부. 재화 지불·지급과 일일 기록(SOLD·한도·누진)을 한 자리에서 한다.
    ///
    /// <b>왜 UI 가 아니라 여기인가.</b> 일곱 섹션이 각자 <c>PointManager.TrySpend</c> 를 부르면
    /// "한도를 확인하고 → 지불하고 → 기록한다"는 순서가 일곱 벌이 되고, 그중 하나가 기록을
    /// 빠뜨리면 <b>한도가 없는 섹션이 하나 생긴다.</b> 그 사고는 화면상 아무 표시도 없다.
    ///
    /// <b>현금(IAP)은 지금 "결제된 셈 치고" 지급한다.</b> 기획서 9장의 결제 모듈·영수증 검증·
    /// 복원 처리가 아직 없어서, 그때까지 로직(지급·갱신·표시)을 눌러 볼 수 있게 하려는 것이다.
    /// <see cref="TryBuyCashProduct"/> 하나만 이 경로를 타므로 <b>SDK 를 붙일 때 고칠 곳도
    /// 그 메서드 하나다.</b> 출시 전에 반드시 실제 결제로 바꿀 것 — 안 바꾸면 무료 상점이 된다.
    /// </summary>
    [Preserve]
    public sealed class ShopPurchaseManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static ShopPurchaseManager Instance { get; internal set; }

        /// <summary>구매·리셋으로 화면이 바뀌어야 할 때. 상점 페이지가 통째로 다시 그린다.</summary>
        public event Action OnPurchaseStateChanged;

        /// <summary>
        /// 무언가를 <b>실제로 지급했을 때</b>. 화면이 획득 팝업을 띄운다.
        ///
        /// <b>왜 각 구매 메서드의 반환값이 아니라 이벤트인가.</b> 지급하는 경로가 여섯이고
        /// (일일상점·다이스석·골드·교환·성장 패키지·유료젬) 앞으로 더 는다. 반환값으로
        /// 넘기면 여섯 곳이 각자 팝업을 띄우게 되고, <b>새 경로를 추가할 때 팝업을 빠뜨리는
        /// 것이 기본값</b>이 된다. 지급을 실제로 하는 두 곳(<see cref="Settle"/>,
        /// <see cref="GrantContents"/>)에서만 쏘면 빠질 수가 없다.
        /// </summary>
        public event Action<System.Collections.Generic.IReadOnlyList<PointRewardEntry>> OnRewardGranted;

        /// <summary>보석을 뽑았을 때. 화면이 결과 팝업을 띄운다.</summary>
        public event Action<System.Collections.Generic.IReadOnlyList<GemDefinition>> OnGemsDrawn;

        private readonly OJ.Core.ShopSave state = new OJ.Core.ShopSave();

        // ── 구매 키 ────────────────────────────────────────────────────
        // 세이브에 그대로 문자열로 들어간다. 바꾸면 그날의 기록이 초기화되므로
        // (다음 날이면 어차피 리셋되는 값이라) 사고는 아니지만, 이유 없이 바꾸지 말 것.

        public static string DailySlotKey(int slotIndex) => "daily:" + slotIndex;
        public static string GoldKey(int offerIndex) => "gold:" + offerIndex;

        // ── 조회 ───────────────────────────────────────────────────────

        /// <summary>오늘 이 키를 몇 번 샀나. 날짜가 바뀌었으면 먼저 리셋한다.</summary>
        public int GetTodayCount(string key) => GetTodayCount(key, DateTime.Now);

        public int GetTodayCount(string key, DateTime now)
        {
            ResetIfDateChanged(now);
            state.DailyPurchaseCounts.TryGetValue(key, out int count);
            return count;
        }

        public bool IsDailySlotSold(int slotIndex) => GetTodayCount(DailySlotKey(slotIndex)) > 0;

        /// <summary>
        /// 누진 단가. 기획서 8.6-2(다이스석, 검토)·8.7-2(골드, 확정) 가 같은 식을 쓴다.
        /// <b>static 이다</b> — 매니저·세이브 없이 값만으로 검증할 수 있어야 한다.
        /// </summary>
        public static int ProgressiveCost(int baseCost, int increasePerPurchase, int purchasedToday)
        {
            if (purchasedToday <= 0 || increasePerPurchase <= 0)
                return Mathf.Max(0, baseCost);

            return Mathf.Max(0, baseCost + increasePerPurchase * purchasedToday);
        }

        /// <summary>할인 적용가. 기획서 8.3 "확률적으로 할인 슬롯 등장".</summary>
        public static int DiscountedCost(int cost, int discountPercent)
        {
            if (discountPercent <= 0)
                return Mathf.Max(0, cost);

            int clamped = Mathf.Clamp(discountPercent, 0, 90);
            return Mathf.Max(0, Mathf.CeilToInt(cost * (100 - clamped) / 100f));
        }

        // ── 구매 ───────────────────────────────────────────────────────

        /// <summary>일일상점 슬롯. 산 슬롯은 비우지 않고 SOLD 로 남긴다(기획서 8.3).</summary>
        public ShopPurchaseResult TryBuyDailySlot(int slotIndex)
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null || slotIndex < 0 || slotIndex >= db.DailyOffers.Count)
                return Invalid("일일상점 슬롯 " + slotIndex);

            if (IsDailySlotSold(slotIndex))
                return ShopPurchaseResult.AlreadySold;

            ShopDatabase.DailyOffer offer = db.DailyOffers[slotIndex];
            int cost = DiscountedCost(offer.cost, offer.discountPercent);

            return Settle(offer.costType, cost, offer.reward.pointType, offer.reward.amount,
                DailySlotKey(slotIndex));
        }

        /// <summary>
        /// 다이스석. <b>일일 한도도 누진도 없다</b> — 무료젬만 내면 그만큼 들어온다.
        /// 한도를 뺀 이유는 <see cref="ShopDatabase.StoneOffer"/> 주석에 적혀 있다.
        /// </summary>
        public ShopPurchaseResult TryBuyStone(int offerIndex)
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null || offerIndex < 0 || offerIndex >= db.StoneOffers.Count)
                return Invalid("다이스석 " + offerIndex);

            ShopDatabase.StoneOffer offer = db.StoneOffers[offerIndex];

            // 기록 키를 안 넘긴다(null). 한도도 누진도 없으니 셀 이유가 없고,
            // 세지 않는 것이 "한도가 없다"는 사실을 코드에서 바로 읽히게 한다.
            return Settle(PointType.FreeGem, offer.costFreeGem,
                offer.stoneType, offer.amountPerPurchase, null);
        }

        /// <summary>골드. 한도는 없고 누진만 있다(기획서 8.7).</summary>
        public ShopPurchaseResult TryBuyGold(int offerIndex)
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null || offerIndex < 0 || offerIndex >= db.GoldOffers.Count)
                return Invalid("골드 " + offerIndex);

            ShopDatabase.GoldOffer offer = db.GoldOffers[offerIndex];
            string key = GoldKey(offerIndex);
            int cost = ProgressiveCost(offer.baseCostFreeGem, offer.costIncreasePerPurchase, GetTodayCount(key));

            return Settle(PointType.FreeGem, cost, PointType.Gold, offer.goldAmount, key);
        }

        /// <summary>
        /// 유료젬 → 무료젬 교환. 기획서 8.5 의 <b>유일한 관문</b>이라 되돌리는 경로를 만들지 않는다.
        /// 확인 팝업은 화면 쪽 책임이다(오조작 방지, 8.5-2).
        /// </summary>
        public ShopPurchaseResult TryExchangeFreeGem(int paidGemAmount)
        {
            if (paidGemAmount <= 0)
                return Invalid("무료젬 교환 수량 " + paidGemAmount);

            ShopDatabase db = ShopDatabaseProvider.Database;
            int gained = paidGemAmount * (db != null ? db.FreeGemPerPaidGem : 1);

            return Settle(PointType.PaidGem, paidGemAmount, PointType.FreeGem, gained, null);
        }

        /// <summary>
        /// 보석뽑기. 비용을 내고 <paramref name="drawCount"/> 개를 뽑아 보석 인벤토리에 넣는다.
        ///
        /// <b>등급 확률은 데이터다.</b> <see cref="ShopDatabase.GemBox.rarityWeights"/> 가
        /// 비어 있으면 균등이고, 그 상태가 기획서 10장의 "등급별 확률 미정" 을 그대로 옮긴
        /// 것이다 — 코드가 임의의 곡선을 박으면 그것이 곧 기획이 되어 버린다.
        ///
        /// <b>어떤 보석이 나올지는 등급만으로 정한다.</b> 부위(<c>equipableType</c>)는 안 가린다 —
        /// 상자가 부위를 고른다는 기획이 없고, 가리면 그 규칙이 코드에만 남는다.
        /// </summary>
        public ShopPurchaseResult TryDrawGemBox(int boxIndex, int drawCount)
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null || boxIndex < 0 || boxIndex >= db.GemBoxes.Count || drawCount <= 0)
                return Invalid("보석뽑기 " + boxIndex);

            ShopDatabase.GemBox box = db.GemBoxes[boxIndex];

            EquipmentManager equipment = EquipmentManager.Instance;
            if (equipment == null)
                return Invalid("EquipmentManager 가 없다");

            PointManager points = PointManager.Instance;
            if (points == null)
                return Invalid("PointManager 가 없다");

            int cost = drawCount >= 10 ? box.costTen : box.costSingle * drawCount;

            // 뽑기 전에 <b>나올 수 있는지부터</b> 본다. 재화를 먼저 빼고 후보가 없으면
            // 그 재화가 조용히 사라진다.
            var drawn = new System.Collections.Generic.List<GemDefinition>(drawCount);
            if (!TryRoll(equipment, box, drawCount, drawn))
                return Invalid("보석뽑기 " + box.title + " 의 등급 구간에 보석이 하나도 없다");

            if (!points.TrySpend(box.costType, cost))
                return ShopPurchaseResult.NotEnoughCurrency;

            for (int i = 0; i < drawn.Count; i++)
                equipment.AddGem(drawn[i].gemId, 1);

            OnGemsDrawn?.Invoke(drawn);
            OnPurchaseStateChanged?.Invoke();
            return ShopPurchaseResult.Success;
        }

        /// <summary>
        /// 등급을 굴리고 그 등급의 보석 중 하나를 고른다. 하나도 못 뽑으면 false —
        /// <b>부분 성공을 만들지 않는다</b>(열 개 중 셋만 나오면 그것이 정상인지 사고인지
        /// 유저도 우리도 구별할 수 없다).
        /// </summary>
        private static bool TryRoll(
            EquipmentManager equipment, ShopDatabase.GemBox box, int drawCount,
            System.Collections.Generic.List<GemDefinition> result)
        {
            System.Collections.Generic.IReadOnlyList<GemDefinition> all = equipment.GetGemDefinitions();
            if (all == null || all.Count == 0)
                return false;

            int steps = ShopDraw.RarityStepCount(box.minRarity, box.maxRarity);
            var candidates = new System.Collections.Generic.List<GemDefinition>();

            for (int i = 0; i < drawCount; i++)
            {
                int step = ShopDraw.PickWeightedIndex(box.rarityWeights, UnityEngine.Random.value, steps);
                var rarity = (Rarity)((int)box.minRarity + Mathf.Max(0, step));

                candidates.Clear();
                for (int j = 0; j < all.Count; j++)
                {
                    if (all[j] != null && all[j].rarity == rarity)
                        candidates.Add(all[j]);
                }

                // 그 등급에 등재된 보석이 없으면 구간 전체에서 아무거나 고른다.
                // 여기서 그냥 실패로 두면 <b>데이터에 구멍이 하나 있다는 이유로</b>
                // 열 개짜리 뽑기가 통째로 막힌다.
                if (candidates.Count == 0)
                {
                    for (int j = 0; j < all.Count; j++)
                    {
                        if (all[j] == null)
                            continue;

                        int r = (int)all[j].rarity;
                        if (r >= (int)box.minRarity && r <= (int)box.maxRarity)
                            candidates.Add(all[j]);
                    }
                }

                if (candidates.Count == 0)
                    return false;

                result.Add(candidates[UnityEngine.Random.Range(0, candidates.Count)]);
            }

            return result.Count > 0;
        }

        /// <summary>성장 패키지 구매(현금). 구성품을 그대로 지급한다.</summary>
        public ShopPurchaseResult TryBuyGrowthPackage(int packageIndex)
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null || packageIndex < 0 || packageIndex >= db.GrowthPackages.Count)
                return Invalid("성장 패키지 " + packageIndex);

            ShopDatabase.GrowthPackage package = db.GrowthPackages[packageIndex];
            if (package.contents == null || package.contents.Count == 0)
                return Invalid("성장 패키지 " + package.id + " 의 구성품이 비었다");

            return TryBuyCashProduct(package.id, package.priceWon, package.contents);
        }

        /// <summary>유료젬 구매(현금). 젬만 들어온다.</summary>
        public ShopPurchaseResult TryBuyPaidGem(int offerIndex)
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null || offerIndex < 0 || offerIndex >= db.PaidGemOffers.Count)
                return Invalid("유료젬 " + offerIndex);

            ShopDatabase.PaidGemOffer offer = db.PaidGemOffers[offerIndex];

            var contents = new System.Collections.Generic.List<ShopDatabase.Reward>
            {
                new ShopDatabase.Reward(PointType.PaidGem, offer.gemAmount),
            };

            return TryBuyCashProduct("paidgem_" + offer.gemAmount, offer.priceWon, contents);
        }

        /// <summary>
        /// 현금 상품의 유일한 관문. <b>지금은 결제된 셈 치고 바로 지급한다.</b>
        ///
        /// 기획서 9장의 결제 모듈·영수증 검증·복원 처리가 아직 없다. 그때까지 아무것도
        /// 주지 않으면 이 아래(젬 보유량 → 무료젬 교환 → 다이스석/골드 구매)가 통째로
        /// 눌러 볼 수 없는 상태가 되어, <b>정작 검증하려는 재화 흐름이 막힌다.</b>
        ///
        /// <b>SDK 를 붙일 때 고칠 곳은 여기 하나다.</b> 영수증이 확인된 뒤에
        /// <see cref="GrantContents"/> 를 부르도록 바꾸면 나머지는 그대로 돈다.
        /// 로그를 경고로 남기는 이유는 <b>이 상태로 출시되면 무료 상점</b>이기 때문이다.
        /// </summary>
        public ShopPurchaseResult TryBuyCashProduct(
            string productId, int priceWon,
            System.Collections.Generic.IReadOnlyList<ShopDatabase.Reward> contents)
        {
            Debug.LogWarning("[상점] 테스트 결제로 처리한다(IAP 미연동). 상품=" + productId +
                             " 가격=" + priceWon + "원. 출시 전 실제 결제로 교체할 것.");

            System.Collections.Generic.List<PointRewardEntry> granted = GrantContents(contents);
            if (granted == null)
                return Invalid("현금 상품 " + productId);

            OnRewardGranted?.Invoke(granted);
            OnPurchaseStateChanged?.Invoke();
            return ShopPurchaseResult.Success;
        }

        /// <summary>지급하고 <b>지급한 것</b>을 돌려준다. 못 하면 null.</summary>
        private static System.Collections.Generic.List<PointRewardEntry> GrantContents(
            System.Collections.Generic.IReadOnlyList<ShopDatabase.Reward> contents)
        {
            PointManager points = PointManager.Instance;
            if (points == null || contents == null)
                return null;

            var granted = new System.Collections.Generic.List<PointRewardEntry>(contents.Count);

            for (int i = 0; i < contents.Count; i++)
            {
                ShopDatabase.Reward reward = contents[i];
                if (reward.amount <= 0)
                    continue;

                points.Add(reward.pointType, reward.amount, false);
                granted.Add(new PointRewardEntry(reward.pointType, reward.amount));
            }

            points.SaveAll();
            return granted;
        }

        // ── 일일상점 갱신 ──────────────────────────────────────────────

        public static string AdRefreshKey => "refresh:ad";
        public static string GemRefreshKey => "refresh:gem";

        public int GetAdRefreshCount() => GetTodayCount(AdRefreshKey);
        public int GetGemRefreshCount() => GetTodayCount(GemRefreshKey);

        /// <summary>광고를 보고 갱신. 하루 <c>DailyAdRefreshPerDay</c> 회.</summary>
        public ShopPurchaseResult TryRefreshDailyByAd()
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null)
                return Invalid("일일상점 갱신");

            if (GetAdRefreshCount() >= db.DailyAdRefreshPerDay)
                return ShopPurchaseResult.RefreshLimitReached;

            // 광고 재생은 IRewardedAdService 가 붙으면 여기로 들어온다. 지금은 본 것으로 친다 —
            // 현금 결제와 같은 판단이고, 이유도 같다(막아 두면 갱신 로직을 못 눌러 본다).
            Debug.LogWarning("[상점] 광고를 본 것으로 처리한다(광고 SDK 미연동).");

            ClearDailySlots();
            Record(AdRefreshKey);
            OnPurchaseStateChanged?.Invoke();
            return ShopPurchaseResult.Success;
        }

        /// <summary>무료젬으로 갱신. 하루 <c>DailyGemRefreshPerDay</c> 회.</summary>
        public ShopPurchaseResult TryRefreshDailyByGem()
        {
            ShopDatabase db = ShopDatabaseProvider.Database;
            if (db == null)
                return Invalid("일일상점 갱신");

            if (GetGemRefreshCount() >= db.DailyGemRefreshPerDay)
                return ShopPurchaseResult.RefreshLimitReached;

            PointManager points = PointManager.Instance;
            if (points == null)
                return Invalid("PointManager 가 없다");

            if (!points.TrySpend(PointType.FreeGem, db.DailyGemRefreshCost))
                return ShopPurchaseResult.NotEnoughCurrency;

            ClearDailySlots();
            Record(GemRefreshKey);
            OnPurchaseStateChanged?.Invoke();
            return ShopPurchaseResult.Success;
        }

        /// <summary>
        /// SOLD 표시만 지운다. <b>갱신 횟수는 남긴다</b> — 그것까지 지우면 갱신이 무한이 된다.
        ///
        /// 진열 품목 자체를 다시 뽑지는 않는다. 뽑기 풀과 등급 가중치가 미정이라
        /// (기획서 10장 "일일상점 갱신 시각 / 수동 갱신 비용") 지금은 <b>같은 6칸이 다시
        /// 살아나는</b> 것까지가 갱신이다. 풀이 정해지면 여기서 다시 굴린다.
        /// </summary>
        private void ClearDailySlots()
        {
            ResetIfDateChanged(DateTime.Now);

            for (int i = 0; i < ShopDatabase.DailySlotCount; i++)
                state.DailyPurchaseCounts.Remove(DailySlotKey(i));
        }

        // ── 내부 ───────────────────────────────────────────────────────

        /// <summary>
        /// 지불하고 지급하고 기록한다. <b>순서가 중요하다</b> —
        /// 지급을 먼저 하면 지불이 실패했을 때 재화가 발급된다.
        /// </summary>
        private ShopPurchaseResult Settle(
            PointType costType, int cost, PointType rewardType, int rewardAmount, string countKey)
        {
            PointManager points = PointManager.Instance;
            if (points == null)
                return Invalid("PointManager 가 없다");

            if (!points.TrySpend(costType, cost, false))
                return ShopPurchaseResult.NotEnoughCurrency;

            points.Add(rewardType, rewardAmount, false);
            points.SaveAll();

            if (!string.IsNullOrEmpty(countKey))
                Record(countKey);

            OnRewardGranted?.Invoke(new System.Collections.Generic.List<PointRewardEntry>
            {
                new PointRewardEntry(rewardType, rewardAmount),
            });

            OnPurchaseStateChanged?.Invoke();
            return ShopPurchaseResult.Success;
        }

        private void Record(string key)
        {
            ResetIfDateChanged(DateTime.Now);
            state.DailyPurchaseCounts.TryGetValue(key, out int count);
            state.DailyPurchaseCounts[key] = count + 1;

            OJ.DI.GameContainer.SaveService?.SaveAll();
        }

        private static ShopPurchaseResult Invalid(string what)
        {
            Debug.LogError("[상점] " + what + " 를 살 수 없다. 데이터와 화면이 어긋났다.");
            return ShopPurchaseResult.InvalidOffer;
        }

        /// <summary>
        /// 날짜가 바뀌었으면 오늘 기록을 버린다.
        ///
        /// <b>UTC 가 아니라 로컬 날짜다.</b> 유저가 "오늘"이라고 부르는 것이 로컬 날짜이고,
        /// 일일상점 갱신 시각은 기획서 10장 기준 미정이라 자정을 기준으로 둔다.
        /// 갱신 시각이 확정되면 여기 한 곳만 고친다.
        /// </summary>
        private void ResetIfDateChanged(DateTime now)
        {
            string today = FormatDate(now);
            if (state.DailyResetDate == today)
                return;

            bool had = state.DailyPurchaseCounts.Count > 0;

            state.DailyResetDate = today;
            state.DailyPurchaseCounts.Clear();

            // 첫 실행(기록 없음)에는 알리지 않는다. 아무것도 안 바뀐 것과 같다.
            if (had)
                OnPurchaseStateChanged?.Invoke();
        }

        /// <summary>
        /// 리셋 판정에 쓰는 날짜 키. <b>문화권을 안 탄다</b> — 기기 로캘에 따라 형식이 바뀌면
        /// 매번 리셋되거나 영영 리셋되지 않고, 그 사고는 그 기기에서만 재현된다.
        /// </summary>
        public static string FormatDate(DateTime value)
        {
            return value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>다음 갱신까지 남은 시간. 헤더의 "갱신까지 07:12:33" 이 이 값을 쓴다.</summary>
        public static TimeSpan TimeUntilReset(DateTime now)
        {
            return now.Date.AddDays(1) - now;
        }

        // ── 세이브 ─────────────────────────────────────────────────────

        public void WriteTo(OJ.Core.SaveState saveState)
        {
            saveState.Shop.DailyResetDate = state.DailyResetDate;

            saveState.Shop.DailyPurchaseCounts.Clear();
            foreach (var pair in state.DailyPurchaseCounts)
                saveState.Shop.DailyPurchaseCounts[pair.Key] = pair.Value;
        }

        public void ReadFrom(OJ.Core.SaveState saveState)
        {
            state.DailyResetDate = saveState.Shop.DailyResetDate ?? string.Empty;

            state.DailyPurchaseCounts.Clear();
            foreach (var pair in saveState.Shop.DailyPurchaseCounts)
                state.DailyPurchaseCounts[pair.Key] = Mathf.Max(0, pair.Value);

            // 여기서 날짜를 검사하지 않는다. StaticResource 를 건드리지 않는 순수 대입이지만,
            // 로드는 씬이 서기 전에 돌 수 있고 그때 OnPurchaseStateChanged 를 쏘면
            // 아직 구독하지 않은 화면이 갱신을 놓친다. 리셋은 첫 조회가 한다.
        }
    }
}
