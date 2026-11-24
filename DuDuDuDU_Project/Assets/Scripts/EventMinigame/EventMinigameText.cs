using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 이벤트 화면이 쓰는 문구. 화면 코드에 글자를 흩어 놓으면 같은 말이 화면마다 다르게 적히므로 한 곳에 모았다.
    /// UnityEngine 을 쓰지 않아 헤드리스 테스트가 문구를 잠근다.
    /// </summary>
    public static class EventMinigameText
    {
        public static string EventName(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Fishing: return "낚시";
                case EventKind.TreasureHunt: return "보물찾기";
                default: return "래프팅";
            }
        }

        /// <summary>이벤트 재화의 이름(소모품). 낚싯대 / 곡괭이 / 보트.</summary>
        public static string CurrencyName(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Fishing: return "낚싯대";
                case EventKind.TreasureHunt: return "곡괭이";
                default: return "보트";
            }
        }

        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 0: return "일반";
                case 1: return "희귀";
                case 2: return "영웅";
                default: return "전설";
            }
        }

        public static string TimingName(FishTiming timing)
        {
            switch (timing)
            {
                case FishTiming.Perfect: return "퍼펙트!";
                case FishTiming.Good: return "굿";
                default: return "미스";
            }
        }

        /// <summary>타이밍을 눌렀을 때 화면에 터지는 글자. 미스(범위 밖)도 "Cool!" 이다 — 실패 문구를 띄우지 않는 것이 레퍼런스 연출이다.</summary>
        public static string TimingBurst(FishTiming timing)
        {
            switch (timing)
            {
                case FishTiming.Perfect: return "Perfect!";
                case FishTiming.Good: return "Great!";
                default: return "Cool!";
            }
        }

        /// <summary>3자리마다 쉼표를 찍은 정수. 문화권을 타지 않는다.</summary>
        public static string Number(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>무게. 1kg 미만은 g, 이상은 소수 둘째 자리까지 kg.</summary>
        public static string Weight(long grams)
        {
            if (grams < 1000)
                return grams + "g";

            long hundredths = grams / 10;
            return (hundredths / 100) + "." + (hundredths % 100).ToString("00", CultureInfo.InvariantCulture) + "kg";
        }

        /// <summary>남은 시간. 하루 이상이면 "N일 H시간", 한 시간 이상이면 "H시간 M분", 그 아래는 "M분".</summary>
        public static string Remaining(TimeSpan span)
        {
            if (span <= TimeSpan.Zero)
                return "0분";

            if (span.TotalDays >= 1)
                return span.Days + "일 " + span.Hours + "시간";

            if (span.TotalHours >= 1)
                return span.Hours + "시간 " + span.Minutes + "분";

            return Math.Max(1, span.Minutes) + "분";
        }

        /// <summary>보상 묶음을 한 줄로. 0인 항목은 뺀다. 비어 있으면 "-".</summary>
        public static string Describe(EventBundle bundle)
        {
            var parts = new List<string>();

            if (bundle.PinballShots > 0) parts.Add("핀볼 " + Number(bundle.PinballShots) + "발");
            if (bundle.SynergyTickets > 0) parts.Add("시너지 티켓 " + Number(bundle.SynergyTickets) + "장");
            if (bundle.Gold > 0) parts.Add("골드 " + Number(bundle.Gold));
            if (bundle.NormalGemTickets > 0) parts.Add("일반 뽑기권 " + Number(bundle.NormalGemTickets) + "장");
            if (bundle.PremiumGemTickets > 0) parts.Add("최고급 뽑기권 " + Number(bundle.PremiumGemTickets) + "장");
            if (bundle.RelicTickets > 0) parts.Add("유물권 " + Number(bundle.RelicTickets) + "장");
            if (bundle.BasicCopies > 0) parts.Add("기본 카피 " + Number(bundle.BasicCopies) + "장");
            if (bundle.RareKingCopies > 0) parts.Add("레어·킹 카피 " + Number(bundle.RareKingCopies) + "장");

            return parts.Count == 0 ? "-" : string.Join(" · ", parts);
        }

        /// <summary>보물찾기 룰렛 칸 이름. 무료 칸은 "무료 5m".</summary>
        public static string RaftingCell(int cell)
        {
            int c = Math.Max(0, Math.Min(RaftingRules.CellCount - 1, cell));
            string meters = RaftingRules.CellDistance[c] + "m";
            return RaftingRules.IsFreeCell(c) ? "무료 " + meters : meters;
        }

        /// <summary>낚시 도감 한 줄. 아직 못 낚았으면 "???".</summary>
        public static string CodexLine(int tier, int species, long bestGrams)
        {
            string name = FishingRules.SpeciesNames[tier][species];
            return "[" + TierName(tier) + "] " + name + "  " + (bestGrams > 0 ? Weight(bestGrams) : "???");
        }

        /// <summary>낚시 한 번의 결과를 두세 줄로.</summary>
        public static string FishingResultLine(FishingCastResult result)
        {
            var text = new StringBuilder();
            text.Append('[').Append(TierName(result.Tier)).Append("] ")
                .Append(FishingRules.SpeciesNames[result.Tier][result.Species]).Append("  ")
                .Append(Weight(result.WeightGrams)).Append("  ")
                .Append(TimingName(result.Timing)).Append('\n')
                .Append('+').Append(Number(result.CastScore)).Append("점");

            if (result.Multiplier > 1)
                text.Append("  (x").Append(result.Multiplier).Append(')');

            if (result.NewRecord)
                text.Append("\n신기록!  이 크기는 상위 ").Append(PercentFromPermille(result.TopPermille)).Append("%");

            if (result.Baits > 0)
                text.Append("\n특별 떡밥 발견! 행운 ").Append(result.LuckStacks).Append("단계");

            if (result.RodLevelAfter > result.RodLevelBefore)
                text.Append("\n낚싯대가 Lv.").Append(result.RodLevelAfter).Append(" 이 됐어요");

            return text.ToString();
        }

        /// <summary>‰ 를 % 로. 소수 첫째 자리가 0 이면 정수로("5%"), 아니면 "22.5%" 처럼.</summary>
        public static string PercentFromPermille(int permille)
        {
            int whole = permille / 10;
            int tenth = permille % 10;
            return tenth == 0 ? whole.ToString(CultureInfo.InvariantCulture) : whole + "." + tenth;
        }
    }
}
