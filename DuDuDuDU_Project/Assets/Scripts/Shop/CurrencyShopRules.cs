namespace OJ.Shop
{
    /// <summary>재화 상점 상품이 걸리는 콘텐츠. 각 콘텐츠 화면의 "패스 버튼"이 그 콘텐츠에 쓰는 재화만 판다.</summary>
    public enum ContentPass
    {
        Pinball = 0,
        DiceGrowth,
        Synergy,
        Relic,
        Gem,
    }

    /// <summary>
    /// 재화 상점의 순수 규칙. <b>매니저·SO 를 참조하지 않는 <c>static</c> 뿐이다</b> — 헤드리스 러너에서 돈다.
    /// </summary>
    public static class CurrencyShopRules
    {
        /// <summary>하루 한도를 다 썼는가. 한도 0(이하)은 "한도 없음"이다.</summary>
        public static bool IsLimitReached(int dailyLimit, int boughtToday)
        {
            return dailyLimit > 0 && boughtToday >= dailyLimit;
        }

        /// <summary>오늘 남은 횟수. 한도가 없으면 -1.</summary>
        public static int RemainingToday(int dailyLimit, int boughtToday)
        {
            if (dailyLimit <= 0)
                return -1;

            int left = dailyLimit - boughtToday;
            return left < 0 ? 0 : left;
        }

        /// <summary>
        /// 지금 보이는 상품인가. 이벤트 재화는 <b>열린 이벤트가 있을 때만</b> 보인다.
        /// 이벤트 시스템이 아직 없어 <c>EventCurrencyRules.IsEventOpen</c> 이 항상 true 다(임시).
        /// </summary>
        public static bool IsVisible(PointType pointType, bool eventOpen)
        {
            return pointType != PointType.OpenEventCurrency || eventOpen;
        }

        /// <summary>
        /// 이 콘텐츠의 패스 팝업이 파는 재화인가.
        /// 핀볼은 핀볼 발수와 이벤트 재화, 다이스 성장은 선택권과 이벤트 재화(승급 미션 B 가 쓴다),
        /// 시너지는 티켓과 골드(리롤), 유물은 유물권, 보석은 두 뽑기권이다.
        /// </summary>
        public static bool SellsIn(ContentPass pass, PointType pointType)
        {
            switch (pass)
            {
                case ContentPass.Pinball:
                    return pointType == PointType.PinballTicket || pointType == PointType.OpenEventCurrency;

                case ContentPass.DiceGrowth:
                    return pointType == PointType.SelectTicket || pointType == PointType.OpenEventCurrency;

                case ContentPass.Synergy:
                    return pointType == PointType.SynergyTicket || pointType == PointType.Gold;

                case ContentPass.Relic:
                    return pointType == PointType.RelicTicket;

                case ContentPass.Gem:
                    return pointType == PointType.NormalGemTicket || pointType == PointType.PremiumGemTicket;

                default:
                    return false;
            }
        }
    }
}
