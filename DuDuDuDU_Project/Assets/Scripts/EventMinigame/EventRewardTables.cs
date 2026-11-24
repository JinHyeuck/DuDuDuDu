using System.Collections.Generic;

namespace OJ.EventMinigame
{
    /// <summary>마일스톤 한 줄. 선택 상자면 <see cref="Options"/> 가 셋이고 하나를 고른다.</summary>
    public sealed class EventMilestone
    {
        /// <summary>이 줄이 열리는 누적 진행도(낚시 점수, 래프팅 거리 m).</summary>
        public readonly int Threshold;

        public readonly EventBundle[] Options;

        public EventMilestone(int threshold, params EventBundle[] options)
        {
            Threshold = threshold;
            Options = options;
        }

        public bool IsChoice
        {
            get { return Options.Length > 1; }
        }

        /// <summary>선택하지 않았을 때(회차가 끝날 때) 자동으로 주는 묶음 = 첫 번째.</summary>
        public EventBundle Default
        {
            get { return Options[0]; }
        }
    }

    /// <summary>
    /// 낚시·래프팅의 마일스톤 표. 줄마다 가치를 <b>(진행도 차이) × (재화 1개 평균 진행도당 200원 × 10배)</b> 로 역산해
    /// <see cref="EventBundle.Compose"/> 로 묶음을 만든다 — 숫자를 손으로 적지 않으므로 장부를 바꾸면 표가 따라온다.
    /// 보물찾기는 스테이지 보상이라 <see cref="TreasureHuntRules"/> 가 따로 만든다.
    ///
    /// 이벤트별 구성(사용자 결정): 낚시는 카피를 넣지 않는다 — 핀볼·시너지·골드·뽑기권. 래프팅은 큰 줄을 3택1 로 준다.
    /// </summary>
    public static class EventRewardTables
    {
        /// <summary>낚시 마일스톤(점수). 낚싯대 레벨업 점수(1만/3만/7만)가 줄에 들어 있다. 평균 낚싯대 1개 = 100점.</summary>
        private static readonly int[] FishingThresholds =
        {
            500, 1000, 2000, 3500, 5000, 7500, 10000, 15000, 20000, 25000,
            30000, 35000, 40000, 50000, 60000, 70000, 80000, 90000, 100000, 120000,
            140000, 160000, 180000, 200000, 230000, 260000, 300000, 340000, 400000,
        };

        /// <summary>래프팅 마일스톤(거리 m). 평균 보트 1개 = 97/15 m.</summary>
        private static readonly int[] RaftingThresholds =
        {
            50, 100, 200, 350, 500, 750, 1000, 1400, 1800, 2200,
            2500, 3000, 3500, 4000, 4500, 5000, 5600, 6200, 6800, 7500,
            8200, 9000, 10000, 11000, 12000, 13500, 15000, 17000, 19000, 20000,
        };

        /// <summary>선택 상자가 되는 래프팅 줄.</summary>
        private static readonly int[] RaftingChoiceThresholds = { 1000, 2500, 5000, 10000, 15000, 20000 };

        public static readonly EventMilestone[] Fishing = BuildFishing();

        public static readonly EventMilestone[] Rafting = BuildRafting();

        public static int FishingLastThreshold
        {
            get { return Fishing[Fishing.Length - 1].Threshold; }
        }

        public static int RaftingLastThreshold
        {
            get { return Rafting[Rafting.Length - 1].Threshold; }
        }

        /// <summary>
        /// 마지막 마일스톤을 넘긴 진행도를 핀볼 발수로 바꾼다(재화 3개 상당 = 1발, 소수점 버림).
        /// 낚시는 300점당 1발, 래프팅은 97/5 m 당 1발, 보물찾기는 표가 끝없이 이어지므로 없다.
        /// </summary>
        public static long OverflowShots(EventKind kind, long progress)
        {
            switch (kind)
            {
                case EventKind.Fishing:
                    return EventMinigameRules.OverflowToShots(progress - FishingLastThreshold, 3L * FishingRules.PointsPerCurrency);
                case EventKind.Rafting:
                    return RaftingRules.OverflowShots(progress - RaftingLastThreshold);
                default:
                    return 0;
            }
        }

        /// <summary>일반 줄 묶음. 4줄 주기로 핀볼 / 시너지 / 뽑기권 / 큰 덩어리(최고급 뽑기권)가 돌아간다.</summary>
        private static EventBundle NormalBundle(int index, long budget)
        {
            switch (index % 4)
            {
                case 0:
                    return EventBundle.Compose(budget, 0, 0, 0, 0, 0, 0, 400);
                case 1:
                    return EventBundle.Compose(budget, 0, 0, 0, 0, 0, 400, 100);
                case 2:
                    return EventBundle.Compose(budget, 0, 0, 0, 0, 400, 200, 0);
                default:
                    return EventBundle.Compose(budget, 400, 0, 0, 0, 200, 100, 0);
            }
        }

        private static EventMilestone[] BuildFishing()
        {
            long deciPerPoint = EventLedger.ReturnPerCurrency / FishingRules.PointsPerCurrency;
            var list = new EventMilestone[FishingThresholds.Length];

            long previous = 0;
            for (int i = 0; i < FishingThresholds.Length; i++)
            {
                long budget = (FishingThresholds[i] - previous) * deciPerPoint;
                list[i] = new EventMilestone(FishingThresholds[i], NormalBundle(i, budget));
                previous = FishingThresholds[i];
            }

            return list;
        }

        private static EventMilestone[] BuildRafting()
        {
            var choices = new HashSet<int>(RaftingChoiceThresholds);
            var list = new EventMilestone[RaftingThresholds.Length];

            long previous = 0;
            for (int i = 0; i < RaftingThresholds.Length; i++)
            {
                // 1m 의 가치 = 재화 1개 가치 / (보트 1개당 평균 거리 97/15 m).
                long budget = (RaftingThresholds[i] - previous) * EventLedger.ReturnPerCurrency
                              * RaftingRules.MetersPerBoatDenominator / RaftingRules.MetersPerBoatNumerator;

                if (choices.Contains(RaftingThresholds[i]))
                {
                    list[i] = new EventMilestone(
                        RaftingThresholds[i],
                        EventBundle.Compose(budget, 0, 0, 0, 0, 0, 0, 100),          // 핀볼 중심
                        EventBundle.Compose(budget, 0, 0, 0, 0, 0, 650, 300),        // 시너지 + 골드
                        EventBundle.Compose(budget, 350, 150, 0, 0, 300, 0, 0));      // 뽑기권·유물권
                }
                else
                {
                    list[i] = new EventMilestone(RaftingThresholds[i], NormalBundle(i, budget));
                }

                previous = RaftingThresholds[i];
            }

            return list;
        }
    }
}
