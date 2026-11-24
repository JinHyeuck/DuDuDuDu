using System.Collections.Generic;
using NUnit.Framework;
using OJ;
using OJ.Bag;

namespace OJ.Game.Tests
{
    /// <summary>가방에 어떤 재화가 칸을 얻는지. 보유량 0 인 기본 재화는 늘 보이고 나머지는 가졌을 때만 보인다.</summary>
    public sealed class BagItemRulesTests
    {
        private static readonly PointType[] Always =
        {
            PointType.Gold,
            PointType.FreeGem,
            PointType.PaidGem,
            PointType.Stamina,
            PointType.NormalGemTicket,
            PointType.PremiumGemTicket,
        };

        [Test]
        public void EmptyBagStillShowsTheSixCoreCurrencies()
        {
            List<PointType> shown = BagItemRules.Collect(_ => 0);

            CollectionAssert.AreEqual(Always, shown);
        }

        [Test]
        public void BoxKeysAreAlwaysShownEvenAtZero()
        {
            List<PointType> shown = BagItemRules.Collect(_ => 0);

            CollectionAssert.Contains(shown, PointType.NormalGemTicket);
            CollectionAssert.Contains(shown, PointType.PremiumGemTicket);
        }

        [Test]
        public void OwnedExtraAppearsAfterTheCoreSix()
        {
            List<PointType> shown = BagItemRules.Collect(t => t == PointType.PinballTicket ? 3 : 0);

            Assert.AreEqual(Always.Length + 1, shown.Count);
            Assert.AreEqual(PointType.PinballTicket, shown[Always.Length]);
        }

        [Test]
        public void ExtraAtZeroOrNegativeIsHidden()
        {
            List<PointType> shown = BagItemRules.Collect(t => t == PointType.FireScroll ? -1 : 0);

            CollectionAssert.DoesNotContain(shown, PointType.FireScroll);
        }

        [Test]
        public void LegacyStonesAndMarkersNeverAppear()
        {
            List<PointType> shown = BagItemRules.Collect(_ => 99);

            CollectionAssert.DoesNotContain(shown, PointType.MythicStone);
            CollectionAssert.DoesNotContain(shown, PointType.RareStone);
            CollectionAssert.DoesNotContain(shown, PointType.Max);
        }

        [Test]
        public void NothingIsListedTwice()
        {
            List<PointType> shown = BagItemRules.Collect(_ => 99);

            CollectionAssert.AllItemsAreUnique(shown);
        }

        [Test]
        public void NullLookupFallsBackToCoreSix()
        {
            CollectionAssert.AreEqual(Always, BagItemRules.Collect(null));
        }
    }
}
