using System;
using System.Collections.Generic;
using OJ.Point;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 이벤트 보상의 값어치 장부. 단위는 <b>0.1원(데시원)</b> 정수다 — 골드 한 닢이 0.1원이라
    /// 원 단위로는 정수가 안 떨어진다. 값은 <c>Docs/BMDesign.md</c> 2.2 의 장부와 같다
    /// (핀볼 1발은 환급 규칙 3재화 = 1발 에 맞춰 60원으로 둔다).
    ///
    /// <b>기대 보상은 쓴 재화 가치의 10배</b>(사용자 결정, 2026-11-23)라서 재화 1개를 쓰면
    /// 평균 <see cref="ReturnPerCurrency"/> 데시원어치가 돌아온다. 보상표는 이 값으로 역산하고
    /// <c>EventLedgerTests</c> 가 표의 누적 가치를 곡선과 맞춰 본다.
    /// </summary>
    public static class EventLedger
    {
        public const int PinballShot = 600;
        public const int SynergyTicket = 500;
        public const int Gold = 1;
        public const int NormalGemTicket = 5000;
        public const int PremiumGemTicket = 18000;
        public const int RelicTicket = 30000;
        public const int BasicCopy = 7000;
        public const int RareKingCopy = 35000;

        /// <summary>이벤트 재화 1개의 값어치(20원).</summary>
        public const int CurrencyValue = 200;

        public const int ReturnMultiplier = 10;

        /// <summary>재화 1개를 쓸 때 평균으로 돌려받는 가치. 2,000데시원 = 200원.</summary>
        public const int ReturnPerCurrency = CurrencyValue * ReturnMultiplier;
    }

    /// <summary>
    /// 마일스톤·스테이지·상자 한 칸의 보상 묶음. 항목별 수량만 들고 있어 가치 계산이 정수로 떨어진다.
    /// 핀볼은 "발" 단위이고, <see cref="ToRewards"/> 가 티켓 수(발 × 한 발의 티켓 값)로 바꾼다.
    /// </summary>
    public readonly struct EventBundle
    {
        public readonly int PinballShots;
        public readonly int SynergyTickets;
        public readonly int Gold;
        public readonly int NormalGemTickets;
        public readonly int PremiumGemTickets;
        public readonly int RelicTickets;
        public readonly int BasicCopies;
        public readonly int RareKingCopies;

        public EventBundle(int pinballShots, int synergyTickets, int gold, int normalGemTickets,
            int premiumGemTickets, int relicTickets, int basicCopies, int rareKingCopies)
        {
            PinballShots = pinballShots;
            SynergyTickets = synergyTickets;
            Gold = gold;
            NormalGemTickets = normalGemTickets;
            PremiumGemTickets = premiumGemTickets;
            RelicTickets = relicTickets;
            BasicCopies = basicCopies;
            RareKingCopies = rareKingCopies;
        }

        public bool IsEmpty
        {
            get { return ValueDeci == 0; }
        }

        /// <summary>장부 값어치(0.1원 단위).</summary>
        public long ValueDeci
        {
            get
            {
                return (long)PinballShots * EventLedger.PinballShot
                       + (long)SynergyTickets * EventLedger.SynergyTicket
                       + (long)Gold * EventLedger.Gold
                       + (long)NormalGemTickets * EventLedger.NormalGemTicket
                       + (long)PremiumGemTickets * EventLedger.PremiumGemTicket
                       + (long)RelicTickets * EventLedger.RelicTicket
                       + (long)BasicCopies * EventLedger.BasicCopy
                       + (long)RareKingCopies * EventLedger.RareKingCopy;
            }
        }

        /// <summary>
        /// 지급용 목록. 카피는 "랜덤 카피" 지급 표지로 적는다(<c>PointManager.Add</c> 가 지급 순간 다이스별로 푼다).
        /// <paramref name="pinballTicketCost"/> 는 핀볼 한 발에 드는 티켓 수 — 환급 규칙(<c>PromotionManager</c>)과 같다.
        /// </summary>
        public List<PointRewardEntry> ToRewards(int pinballTicketCost)
        {
            int cost = pinballTicketCost < 1 ? 1 : pinballTicketCost;
            var list = new List<PointRewardEntry>();
            Add(list, PointType.PinballTicket, (long)PinballShots * cost);
            Add(list, PointType.SynergyTicket, SynergyTickets);
            Add(list, PointType.Gold, Gold);
            Add(list, PointType.NormalGemTicket, NormalGemTickets);
            Add(list, PointType.PremiumGemTicket, PremiumGemTickets);
            Add(list, PointType.RelicTicket, RelicTickets);
            Add(list, PointType.RandomBasicCopy, BasicCopies);
            Add(list, PointType.RandomRareKingCopy, RareKingCopies);
            return list;
        }

        private static void Add(List<PointRewardEntry> list, PointType type, long amount)
        {
            if (amount <= 0)
                return;

            list.Add(new PointRewardEntry(type, amount > int.MaxValue ? int.MaxValue : (int)amount));
        }

        /// <summary>
        /// 예산(데시원)을 비율(‰)로 쪼개 묶음을 만든다. 큰 단위부터 비율만큼 담고(단위가 안 되면 못 담고 넘어간다),
        /// 골드는 지정 비율, <b>남은 것은 핀볼로</b> 채운다. 핀볼 한 발보다 작은 나머지만 골드로 간다.
        /// 그래서 값은 예산에서 10데시원(1원) 넘게 모자라지 않는다.
        /// </summary>
        public static EventBundle Compose(long budget, int premiumPm, int relicPm, int basicPm, int rareKingPm,
            int normalGemPm, int synergyPm, int goldPm)
        {
            if (budget <= 0)
                return default(EventBundle);

            long left = budget;
            int premium = Take(ref left, budget * premiumPm / 1000, EventLedger.PremiumGemTicket);
            int relic = Take(ref left, budget * relicPm / 1000, EventLedger.RelicTicket);
            int rareKing = Take(ref left, budget * rareKingPm / 1000, EventLedger.RareKingCopy);
            int basic = Take(ref left, budget * basicPm / 1000, EventLedger.BasicCopy);
            int normal = Take(ref left, budget * normalGemPm / 1000, EventLedger.NormalGemTicket);
            int synergy = Take(ref left, budget * synergyPm / 1000, EventLedger.SynergyTicket);

            long goldWant = Math.Min(left, budget * goldPm / 1000) / 10 * 10;
            left -= goldWant;

            int shots = (int)(left / EventLedger.PinballShot);
            left -= (long)shots * EventLedger.PinballShot;

            long gold = goldWant + left / 10 * 10;

            return new EventBundle(shots, synergy, (int)Math.Min(gold, int.MaxValue), normal, premium, relic, basic, rareKing);
        }

        private static int Take(ref long left, long want, int unit)
        {
            long n = Math.Min(want, left) / unit;
            if (n <= 0)
                return 0;

            left -= n * unit;
            return (int)n;
        }
    }
}
