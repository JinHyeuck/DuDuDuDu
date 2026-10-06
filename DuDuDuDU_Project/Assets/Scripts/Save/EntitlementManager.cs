using System;
using UnityEngine.Scripting;
using OJ.Core;

namespace OJ.Save
{
    /// <summary>
    /// 계정 단위 권리(현금으로 사서 영구히 남는 것)를 들고 있는 자리.
    ///
    /// <b>광고제거권(영구)과 고기 멤버십(기간제)</b>, 그리고 멤버십이 푸는 <b>무료 소탕 한도</b>를 든다.
    /// 파는 곳은 멤버십 창(<c>OJ.Shop.UIMembershipDialog</c>)이다.
    /// 클래스를 따로 두는 이유는 <b>소유자를 하나로 못 박기 위해서</b>다 —
    /// 이 플래그는 보상 라운드·상점·다른 광고 지점이 모두 보게 되는데,
    /// <c>ISaveStateOwner</c> 는 자기 몫만 써야 하므로 여러 매니저가 같은 필드를 쓰면
    /// <b>나중에 쓴 쪽이 앞의 것을 덮는다.</b> 그 사고는 조용하고, 유저가 산 것이 사라진다.
    ///
    /// <b>MonoBehaviour 가 아니다.</b> <c>GameContainer</c> 가 만들고 <c>SaveService</c> 가 저장한다.
    /// </summary>
    [Preserve]
    public sealed class EntitlementManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static EntitlementManager Instance { get; internal set; }

        private bool adFree;
        private long membershipExpiryUtcTicks;
        private string sweepUsageDate = string.Empty;
        private int sweepUsedCount;

        /// <summary>바뀌었을 때. 화면이 광고 버튼과 스킵 버튼을 다시 그린다.</summary>
        public event Action OnChanged;

        /// <summary>
        /// 광고제거권을 샀는가.
        ///
        /// <b>이 값이 true 면 보상 라운드에 「바로 전액 받기」가 뜬다.</b> 판을 막지는 않는다 —
        /// 굴리고 싶은 사람은 굴리고, 귀찮은 날은 한 번에 끝낸다.
        /// </summary>
        public bool AdFree
        {
            get => adFree;
            set
            {
                if (adFree == value)
                    return;

                adFree = value;
                OJ.DI.GameContainer.SaveService?.SaveAll();
                OnChanged?.Invoke();
            }
        }

        // ── 고기 멤버십 ────────────────────────────────────────────────

        /// <summary>지금 멤버십이 살아 있는가. 시각은 부를 때마다 새로 읽는다 — 켜 둔 채 만료되는 경우가 있다.</summary>
        public bool MeatMembershipActive => MembershipRules.IsActive(membershipExpiryUtcTicks, DateTime.UtcNow.Ticks);

        /// <summary>화면에 적는 남은 일수(올림). 끝났으면 0.</summary>
        public int MeatMembershipRemainingDays => MembershipRules.RemainingDays(membershipExpiryUtcTicks, DateTime.UtcNow.Ticks);

        /// <summary>다시 살 수 있는가 — 없거나 만료 <paramref name="rebuyWindowDays"/> 일 전부터.</summary>
        public bool CanRebuyMeatMembership(int rebuyWindowDays)
        {
            return MembershipRules.CanRebuy(membershipExpiryUtcTicks, DateTime.UtcNow.Ticks, rebuyWindowDays);
        }

        /// <summary>
        /// 멤버십을 <paramref name="days"/> 일 준다. 살아 있으면 만료일 뒤로 붙는다.
        /// <b>결제는 부르는 쪽이 이미 지났다</b>(<c>ShopPurchaseManager.TryBuyCashProduct</c>).
        /// </summary>
        public void GrantMeatMembership(int days)
        {
            membershipExpiryUtcTicks = MembershipRules.Extend(membershipExpiryUtcTicks, DateTime.UtcNow.Ticks, days);
            OJ.DI.GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }

        /// <summary>스테이지 입장료. 멤버십이면 0.</summary>
        public int StageEntryCost(int baseCost)
        {
            return MembershipRules.StageEntryCost(baseCost, MeatMembershipActive);
        }

        // ── 무료 소탕 한도 ─────────────────────────────────────────────

        private static string TodayKey => MembershipRules.DayKey(DateTime.Now);

        /// <summary>오늘 돈 소탕 횟수. 날이 바뀌었으면 0.</summary>
        public int SweepsUsedToday => MembershipRules.UsedToday(sweepUsageDate, sweepUsedCount, TodayKey);

        /// <summary>오늘 더 돌 수 있는 횟수. 멤버십이면 <see cref="MembershipRules.Unlimited"/>.</summary>
        public int SweepsLeftToday(int dailyLimit)
        {
            return MembershipRules.SweepsLeftToday(dailyLimit, SweepsUsedToday, MeatMembershipActive);
        }

        /// <summary>
        /// 소탕을 돈 뒤 센다. <b>멤버십 중에도 센다</b> — 만료되는 날 한도가 그날 쓴 만큼에서
        /// 바로 맞물리게 하려는 것이다. 저장은 부르는 쪽의 지급 경로가 이미 한다.
        /// </summary>
        public void RecordSweeps(int count)
        {
            if (count <= 0)
                return;

            string today = TodayKey;
            sweepUsedCount = MembershipRules.UsedToday(sweepUsageDate, sweepUsedCount, today) + count;
            sweepUsageDate = today;
            OJ.DI.GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }

        public void WriteTo(SaveState state)
        {
            if (state == null)
                return;

            state.AdFree = adFree;
            state.Membership.ExpiryUtcTicks = membershipExpiryUtcTicks;
            state.Membership.SweepUsageDate = sweepUsageDate ?? string.Empty;
            state.Membership.SweepUsedCount = sweepUsedCount;
        }

        public void ReadFrom(SaveState state)
        {
            if (state == null)
                return;

            // 여기서 OnChanged 를 쏘지 않는다. 로드는 아직 다른 매니저가 자기 몫을 읽기
            // 전이고, 구독자가 그 상태를 보면 절반만 로드된 게임을 그리게 된다.
            adFree = state.AdFree;
            membershipExpiryUtcTicks = state.Membership.ExpiryUtcTicks;
            sweepUsageDate = state.Membership.SweepUsageDate ?? string.Empty;
            sweepUsedCount = state.Membership.SweepUsedCount;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>개발용: 멤버십을 지운다(만료 상태로).</summary>
        public void DevClearMembership()
        {
            membershipExpiryUtcTicks = MembershipRules.NoExpiry;
            OJ.DI.GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }

        /// <summary>개발용: 만료까지 <paramref name="days"/> 일 남게 맞춘다(재구매 구간 확인용).</summary>
        public void DevSetMembershipRemaining(double days)
        {
            membershipExpiryUtcTicks = DateTime.UtcNow.Ticks + TimeSpan.FromDays(days).Ticks;
            OJ.DI.GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }

        /// <summary>개발용: 오늘 소탕 사용량을 비운다.</summary>
        public void DevResetSweepUsage()
        {
            sweepUsedCount = 0;
            OJ.DI.GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }
#endif
    }
}
