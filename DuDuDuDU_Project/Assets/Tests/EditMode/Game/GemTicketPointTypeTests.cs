using NUnit.Framework;
using OJ;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 보석 뽑기권 두 재화의 번호가 굳어 있는지. 세이브와 에셋이 enum 을 <b>숫자</b>로 들고 있어서
    /// 번호가 한 번 나가면 바꿀 수 없다 — 이 테스트가 그 사실을 박아 둔다.
    /// </summary>
    public sealed class GemTicketPointTypeTests
    {
        [Test]
        public void GemTicketNumbersAreStable()
        {
            Assert.AreEqual(107, (int)PointType.NormalGemTicket);
            Assert.AreEqual(108, (int)PointType.PremiumGemTicket);
        }

        [Test]
        public void GemTicketsDoNotReuseDiceStoneNumbers()
        {
            // 105·106 은 옛 세이브가 다이스 강화석으로 들고 있다.
            Assert.AreEqual(105, (int)PointType.MythicStone);
            Assert.AreEqual(106, (int)PointType.RareStone);
            Assert.AreNotEqual((int)PointType.MythicStone, (int)PointType.PremiumGemTicket);
            Assert.AreNotEqual((int)PointType.RareStone, (int)PointType.NormalGemTicket);
        }

        [Test]
        public void GemTicketsComeBeforeMax()
        {
            // UIShopGridCard 가 Max 를 "현금 결제" 표지로 쓴다. 새 재화는 그 앞이어야 한다.
            Assert.Less((int)PointType.NormalGemTicket, (int)PointType.Max);
            Assert.Less((int)PointType.PremiumGemTicket, (int)PointType.Max);
        }
    }
}
