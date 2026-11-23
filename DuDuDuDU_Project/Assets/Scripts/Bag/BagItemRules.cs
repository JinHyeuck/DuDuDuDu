using System;
using System.Collections.Generic;

namespace OJ.Bag
{
    /// <summary>
    /// 가방에 어떤 재화를 어떤 순서로 보여 줄지.
    ///
    /// <b>보유량이 0 이어도 늘 보이는 것</b>은 유저가 "이걸 모으는구나" 를 알아야 하는 기본 재화다.
    /// 나머지는 <b>하나라도 있을 때만</b> 보인다 — 카피·강화권을 전부 0 으로 깔면 서른 칸이 비어 가방이 허전하고,
    /// 정작 가진 것은 스크롤 아래로 밀린다.
    ///
    /// <b>제외</b>: 레거시 석(신화석·레어석)은 쓸 곳이 없고, 랜덤 카피·열린 이벤트 표지는 지급하는 순간
    /// 구체 카피로 풀려 보유량이 늘 0 인 표지다. 보여 줘 봐야 없는 물건이다.
    ///
    /// static 이고 게임 상태를 직접 읽지 않는다 — 보유량을 함수로 받는다. EditMode 테스트가 돈다.
    /// </summary>
    public static class BagItemRules
    {
        private static readonly PointType[] AlwaysShown =
        {
            PointType.Gold,
            PointType.FreeGem,
            PointType.PaidGem,
            PointType.Stamina,
            PointType.NormalGemTicket,
            PointType.PremiumGemTicket,
        };

        private static readonly PointType[] ShownWhenOwned =
        {
            PointType.PinballTicket,
            PointType.RelicTicket,
            PointType.SynergyTicket,
            PointType.SelectTicket,

            PointType.NormalScroll,
            PointType.FireScroll,
            PointType.IceScroll,
            PointType.PoisonScroll,
            PointType.ThunderScroll,

            PointType.TornadoCopy,
            PointType.StunCopy,
            PointType.ArmorBreakCopy,
            PointType.WindCopy,
            PointType.TimeCopy,

            PointType.KingNormalCopy,
            PointType.KingFireCopy,
            PointType.KingIceCopy,
            PointType.KingPoisonCopy,
            PointType.KingThunderCopy,

            PointType.WeaponScroll,
            PointType.HelmetScroll,
            PointType.ArmorScroll,
            PointType.RingScroll,
            PointType.ShoesScroll,
            PointType.NecklaceScroll,

            PointType.EventCurrency1,
            PointType.EventCurrency2,
            PointType.EventCurrency3,
        };

        /// <summary>가방에 칸을 만들 재화를 보여 줄 순서대로 돌려준다.</summary>
        public static List<PointType> Collect(Func<PointType, int> getAmount)
        {
            var result = new List<PointType>(AlwaysShown.Length + ShownWhenOwned.Length);

            for (int i = 0; i < AlwaysShown.Length; i++)
                result.Add(AlwaysShown[i]);

            for (int i = 0; i < ShownWhenOwned.Length; i++)
            {
                if (getAmount != null && getAmount(ShownWhenOwned[i]) > 0)
                    result.Add(ShownWhenOwned[i]);
            }

            return result;
        }
    }
}
