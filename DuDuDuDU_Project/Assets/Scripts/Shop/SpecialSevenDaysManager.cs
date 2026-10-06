using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.Point;
using OJ.Save;

namespace OJ.Shop
{
    /// <summary>특별한 7일 한 칸의 현재 모습. 화면이 그대로 그린다.</summary>
    public readonly struct SpecialSevenDaysCellView
    {
        public SpecialSevenDaysCellView(int day, IReadOnlyList<ShopDatabase.Reward> rewards, SpecialSevenDaysCellState state,
            bool isToday, bool highlighted)
        {
            Day = day;
            Rewards = rewards;
            State = state;
            IsToday = isToday;
            Highlighted = highlighted;
        }

        public int Day { get; }
        public IReadOnlyList<ShopDatabase.Reward> Rewards { get; }
        public SpecialSevenDaysCellState State { get; }

        /// <summary>오늘 칸인가. 7일차가 지난 뒤에는 어느 칸도 오늘이 아니다.</summary>
        public bool IsToday { get; }

        /// <summary>테두리 강조(1·3일차 젬 — 기획서 ShopPackageDesign 4.2-2).</summary>
        public bool Highlighted { get; }
    }

    /// <summary>
    /// 특별한 7일 출석의 실행부. (기획서 BMDesign 6장 · ShopPackageDesign 4장)
    ///
    /// <b>사기 전에도 출석은 쌓인다.</b> 시작일은 처음 앱을 켠 날이고, 그날로부터 지난 날짜만큼
    /// 칸이 열린다. 사기 전에는 열린 칸이 자물쇠 + 수량으로 보이고, <b>사는 순간 그동안 쌓인 칸을
    /// 한꺼번에 받는다</b>(6.1 · 시즌 패스 유료 트랙과 같은 장치).
    ///
    /// <b>결제는 아직 없다.</b> <see cref="ShopPurchaseManager.TryBuyCashProduct"/> 를 지난다 —
    /// 현금의 유일한 관문이고, SDK 를 붙이면 그 한 곳만 고친다.
    /// 규칙은 <see cref="SpecialSevenDaysRules"/>, 수치는 <see cref="ShopDatabase.SpecialSevenDays"/>.
    /// </summary>
    [Preserve]
    public sealed class SpecialSevenDaysManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static SpecialSevenDaysManager Instance { get; internal set; }

        private const string ProductId = "special_seven_days";

        /// <summary>상품 자체는 재화를 안 준다 — 칸이 준다. <c>TryBuyCashProduct</c> 는 null 을 "잘못된 상품" 으로 본다.</summary>
        private static readonly ShopDatabase.Reward[] NoContents = new ShopDatabase.Reward[0];

        /// <summary>구매·수령·날짜 변경으로 화면이 바뀌어야 할 때.</summary>
        public event Action OnChanged;

        private readonly SpecialSevenDaysSave state = new SpecialSevenDaysSave();

        private static ShopDatabase.SpecialSevenDaysOffer Offer => ShopDatabaseProvider.Database.SpecialSevenDays;

        // ── 조회 ───────────────────────────────────────────────────────

        public bool Purchased => state.Purchased;

        public int PriceWon => Offer.priceWon;

        /// <summary>오늘이 몇 일차인가(1부터, 위로 접지 않는다).</summary>
        public int CurrentDay
        {
            get
            {
                EnsureStarted();
                return SpecialSevenDaysRules.CurrentDay(state.StartDate, DateTime.Now);
            }
        }

        public bool CanPurchase => SpecialSevenDaysRules.CanPurchase(CurrentDay, state.Purchased);

        public bool HasClaimable => SpecialSevenDaysRules.ClaimableDays(CurrentDay, state.Purchased, state.ClaimedDays).Count > 0;

        public bool AllClaimed => SpecialSevenDaysRules.ClaimedCount(state.ClaimedDays) >= SpecialSevenDaysRules.DayCount;

        /// <summary>로비 입구를 보이는가(기획서 ShopPackageDesign 2.2).</summary>
        public bool IsEntryVisible =>
            SpecialSevenDaysRules.IsEntryVisible(CurrentDay, state.Purchased, SpecialSevenDaysRules.ClaimedCount(state.ClaimedDays));

        /// <summary>7칸. 1일차부터.</summary>
        public List<SpecialSevenDaysCellView> GetCells()
        {
            ShopDatabase.SpecialSevenDaysOffer offer = Offer;
            int today = CurrentDay;

            var cells = new List<SpecialSevenDaysCellView>(SpecialSevenDaysRules.DayCount);
            for (int day = 1; day <= SpecialSevenDaysRules.DayCount; day++)
            {
                SpecialSevenDaysCellState cellState = SpecialSevenDaysRules.CellState(
                    day, today, state.Purchased, state.ClaimedDays.Contains(day));
                cells.Add(new SpecialSevenDaysCellView(
                    day, offer.RewardsFor(day), cellState, day == today, offer.IsHighlighted(day)));
            }

            return cells;
        }

        // ── 구매 · 수령 ────────────────────────────────────────────────

        /// <summary>
        /// 사고, 그 자리에서 쌓인 칸을 전부 받는다. <paramref name="granted"/> 는 재화별로 합친 지급 내역.
        /// 결제가 안 됐으면 false.
        /// </summary>
        public bool TryPurchase(out List<PointRewardEntry> granted)
        {
            granted = null;

            ShopPurchaseManager shop = ShopPurchaseManager.Instance;
            if (shop == null || !CanPurchase)
                return false;

            if (shop.TryBuyCashProduct(ProductId, Offer.priceWon, NoContents) != ShopPurchaseResult.Success)
                return false;

            state.Purchased = true;

            // 받을 것이 없어도(그럴 일은 없지만) 산 것은 저장해야 한다 — ClaimAll 이 0 이면 저장을 안 한다.
            if (ClaimAll(out granted) == 0)
                Save();

            return true;
        }

        /// <summary>
        /// 받을 수 있는 칸을 전부 받는다. 받은 칸 수를 돌려준다.
        /// <b>한 번만 저장한다</b> — 늦게 산 사람은 칸 대여섯 개를 한꺼번에 받는다.
        /// </summary>
        public int ClaimAll(out List<PointRewardEntry> merged)
        {
            merged = null;

            List<int> days = SpecialSevenDaysRules.ClaimableDays(CurrentDay, state.Purchased, state.ClaimedDays);
            if (days.Count == 0)
                return 0;

            ShopDatabase.SpecialSevenDaysOffer offer = Offer;
            var granted = new List<PointRewardEntry>(days.Count);
            for (int i = 0; i < days.Count; i++)
            {
                state.ClaimedDays.Add(days[i]);
                AppendRewards(offer.RewardsFor(days[i]), granted);
            }

            merged = PointRewardUtility.MergeRewards(granted);
            if (merged.Count > 0)
                PointRewardUtility.GrantRewards(merged);

            Save();
            return days.Count;
        }

        /// <summary>
        /// 한 날을 받는다(줄의 "보상 받기"). <paramref name="granted"/> 는 지급 내역 — 획득 팝업이 그린다.
        /// </summary>
        public bool TryClaim(int day, out List<PointRewardEntry> granted)
        {
            granted = null;

            if (!SpecialSevenDaysRules.ClaimableDays(CurrentDay, state.Purchased, state.ClaimedDays).Contains(day))
                return false;

            state.ClaimedDays.Add(day);
            var list = new List<PointRewardEntry>();
            AppendRewards(Offer.RewardsFor(day), list);
            granted = PointRewardUtility.MergeRewards(list);
            if (granted.Count > 0)
                PointRewardUtility.GrantRewards(granted);

            Save();
            return true;
        }

        public int ClaimableCount => SpecialSevenDaysRules.ClaimableDays(CurrentDay, state.Purchased, state.ClaimedDays).Count;

        private static void AppendRewards(IReadOnlyList<ShopDatabase.Reward> rewards, List<PointRewardEntry> into)
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].amount > 0)
                    into.Add(new PointRewardEntry(rewards[i].pointType, rewards[i].amount));
            }
        }

        // ── 자동 노출 ──────────────────────────────────────────────────

        /// <summary>
        /// 로비에 들어올 때 창을 띄울 것인가. 참이면 <b>오늘은 띄웠다고 적는다</b> —
        /// 부르는 쪽이 띄우지 못해도 다시 묻지 않는다(하루 1회, 기획서 ShopPackageDesign 2.3).
        /// </summary>
        public bool ConsumeAutoOpen()
        {
            string today = SpecialSevenDaysRules.DayKey(DateTime.Now);
            if (!SpecialSevenDaysRules.ShouldAutoOpen(CurrentDay, state.Purchased, state.ClaimedDays, state.LastAutoOpenDate, today))
                return false;

            state.LastAutoOpenDate = today;
            OJ.DI.GameContainer.SaveService?.SaveAll();
            return true;
        }

        // ── 시작일 ─────────────────────────────────────────────────────

        /// <summary>
        /// 앱이 켜졌을 때 한 번 부른다. 세이브를 읽은 <b>뒤</b>여야 한다 —
        /// 먼저 부르면 로드가 그 결과를 덮어 오늘 켠 사람의 시작일이 비어 버린다.
        /// </summary>
        public void NotifyAppStart()
        {
            if (EnsureStarted())
                OJ.DI.GameContainer.SaveService?.SaveAll();
        }

        /// <summary>시작일이 없으면 오늘로 적는다. 적었으면 true.</summary>
        private bool EnsureStarted()
        {
            if (SpecialSevenDaysRules.TryParseDayKey(state.StartDate, out _))
                return false;

            state.StartDate = SpecialSevenDaysRules.DayKey(DateTime.Now);
            return true;
        }

        private void Save()
        {
            OJ.DI.GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }

        // ── 저장 ───────────────────────────────────────────────────────

        public void WriteTo(SaveState saveState)
        {
            SpecialSevenDaysSave target = saveState.SpecialSevenDays;
            target.StartDate = state.StartDate ?? string.Empty;
            target.Purchased = state.Purchased;
            target.LastAutoOpenDate = state.LastAutoOpenDate ?? string.Empty;
            target.ClaimedDays.Clear();
            target.ClaimedDays.AddRange(state.ClaimedDays);
        }

        public void ReadFrom(SaveState saveState)
        {
            // 여기서 OnChanged 를 쏘지 않는다 — 다른 매니저가 아직 자기 몫을 안 읽었다.
            SpecialSevenDaysSave source = saveState.SpecialSevenDays;
            state.StartDate = source.StartDate ?? string.Empty;
            state.Purchased = source.Purchased;
            state.LastAutoOpenDate = source.LastAutoOpenDate ?? string.Empty;
            state.ClaimedDays.Clear();
            state.ClaimedDays.AddRange(source.ClaimedDays);
        }

#if UNITY_EDITOR || DEV_DEFINE
        /// <summary>개발용: 시작일을 하루 앞으로 당긴다(= 하루가 지난 것처럼). 기기 시계는 안 건드린다.</summary>
        public void DevAdvanceDay()
        {
            EnsureStarted();
            if (SpecialSevenDaysRules.TryParseDayKey(state.StartDate, out DateTime start))
                state.StartDate = SpecialSevenDaysRules.DayKey(start.AddDays(-1));
            state.LastAutoOpenDate = string.Empty;
            Save();
        }

        /// <summary>개발용: 오늘을 1일차로, 구매·수령 기록을 비운다.</summary>
        public void DevReset()
        {
            state.StartDate = SpecialSevenDaysRules.DayKey(DateTime.Now);
            state.Purchased = false;
            state.ClaimedDays.Clear();
            state.LastAutoOpenDate = string.Empty;
            Save();
        }

        /// <summary>개발용: 구매만 되돌린다(받은 기록은 남긴다).</summary>
        public void DevClearPurchase()
        {
            state.Purchased = false;
            Save();
        }
#endif
    }
}
