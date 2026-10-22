using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class KingHintRulesTests
    {
        [Test]
        public void ShowsOnlyAfterALossFromStageFourWithKingOwnedButNotOnBoard()
        {
            Assert.IsTrue(KingHintRules.ShouldShow(false, false, false, 4, true, false));
            Assert.IsTrue(KingHintRules.ShouldShow(false, false, false, 7, true, false));
        }

        [TestCase(true, false, false, 4, true, false, TestName = "이겼다")]
        [TestCase(false, true, false, 4, true, false, TestName = "튜토리얼")]
        [TestCase(false, false, true, 4, true, false, TestName = "탑")]
        [TestCase(false, false, false, 3, true, false, TestName = "4스테이지 전")]
        [TestCase(false, false, false, 4, false, false, TestName = "킹 없음")]
        [TestCase(false, false, false, 4, true, true, TestName = "킹이 보드에 있었다")]
        public void StaysQuiet(bool win, bool tutorial, bool tower, int stage, bool owns, bool onBoard)
        {
            Assert.IsFalse(KingHintRules.ShouldShow(win, tutorial, tower, stage, owns, onBoard));
        }
    }
}
