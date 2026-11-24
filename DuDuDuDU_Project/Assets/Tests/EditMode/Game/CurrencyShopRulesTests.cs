using NUnit.Framework;
using OJ.Shop;

namespace OJ.Game.Tests
{
    /// <summary>재화 상점 규칙: 하루 한도, 이벤트 재화 노출, 콘텐츠별 판매 목록, 기본 표(가치 장부).</summary>
    public sealed class CurrencyShopRulesTests
    {
        [TestCase(2, 0, false)]
        [TestCase(2, 1, false)]
        [TestCase(2, 2, true)]
        [TestCase(2, 5, true)]
        [TestCase(0, 99, false)]
        public void 하루_한도는_다_쓰면_막는다(int limit, int bought, bool reached)
        {
            Assert.AreEqual(reached, CurrencyShopRules.IsLimitReached(limit, bought));
        }

        [TestCase(2, 0, 2)]
        [TestCase(2, 1, 1)]
        [TestCase(2, 7, 0)]
        [TestCase(0, 3, -1)]
        public void 남은_횟수(int limit, int bought, int left)
        {
            Assert.AreEqual(left, CurrencyShopRules.RemainingToday(limit, bought));
        }

        [Test]
        public void 이벤트_재화는_열린_이벤트가_있을_때만_보인다()
        {
            Assert.IsTrue(CurrencyShopRules.IsVisible(PointType.OpenEventCurrency, true));
            Assert.IsFalse(CurrencyShopRules.IsVisible(PointType.OpenEventCurrency, false));
            Assert.IsTrue(CurrencyShopRules.IsVisible(PointType.Gold, false));
        }

        [Test]
        public void 콘텐츠별_판매_재화()
        {
            Assert.IsTrue(CurrencyShopRules.SellsIn(ContentPass.Pinball, PointType.PinballTicket));
            Assert.IsTrue(CurrencyShopRules.SellsIn(ContentPass.DiceGrowth, PointType.SelectTicket));
            Assert.IsTrue(CurrencyShopRules.SellsIn(ContentPass.Synergy, PointType.SynergyTicket));
            Assert.IsTrue(CurrencyShopRules.SellsIn(ContentPass.Relic, PointType.RelicTicket));
            Assert.IsTrue(CurrencyShopRules.SellsIn(ContentPass.Gem, PointType.PremiumGemTicket));
            Assert.IsFalse(CurrencyShopRules.SellsIn(ContentPass.Relic, PointType.SelectTicket));
        }

        [Test]
        public void 기본_표는_가치_장부_가격이다()
        {
            var offers = ShopDatabase.BuildDefaultCurrencyOffers();

            ShopDatabase.CurrencyOffer Find(PointType type, int amount)
            {
                foreach (var o in offers)
                    if (o.pointType == type && o.amount == amount)
                        return o;
                return null;
            }

            Assert.AreEqual(990, Find(PointType.PinballTicket, 1000).priceGem);
            Assert.AreEqual(2, Find(PointType.PinballTicket, 1000).dailyLimit);
            Assert.AreEqual(525, Find(PointType.SelectTicket, 1).priceGem);
            Assert.AreEqual(300, Find(PointType.RelicTicket, 1).priceGem);
            Assert.AreEqual(180, Find(PointType.PremiumGemTicket, 1).priceGem);
            Assert.AreEqual(50, Find(PointType.NormalGemTicket, 1).priceGem);
            Assert.AreEqual(500, Find(PointType.SynergyTicket, 100).priceGem);
            Assert.AreEqual(500, Find(PointType.OpenEventCurrency, 250).priceGem);
            Assert.AreEqual(100, Find(PointType.Gold, 10000).priceGem);
            Assert.AreEqual(500, Find(PointType.Gold, 50000).priceGem);
            Assert.AreEqual(1000, Find(PointType.Gold, 100000).priceGem);
        }

        /// <summary>핀볼 하루 2회, 이벤트 재화 하루 1회(쓰면 10배가 돌아오니 한도가 곧 보상 상한). 나머지는 한도 없음.</summary>
        [Test]
        public void 한도는_핀볼과_이벤트_재화_상품에만_있다()
        {
            foreach (var o in ShopDatabase.BuildDefaultCurrencyOffers())
            {
                int expected = 0;
                if (o.pointType == PointType.PinballTicket)
                    expected = 2;
                else if (o.pointType == PointType.OpenEventCurrency)
                    expected = 1;

                Assert.AreEqual(expected, o.dailyLimit, o.pointType.ToString());
            }
        }
    }
}
