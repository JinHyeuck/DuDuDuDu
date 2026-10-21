using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>4스테이지 첫 진입 킹 썬더 유도의 조건을 잠근다.</summary>
    public sealed class KingThunderGuideTests
    {
        [Test]
        public void Applies_OnlyOnUnclearedStageFourMainRun()
        {
            Assert.IsTrue(KingThunderGuide.Applies(false, false, 4, 0));
            Assert.IsFalse(KingThunderGuide.Applies(false, false, 4, 1), "깬 뒤에는 꺼진다");
            Assert.IsFalse(KingThunderGuide.Applies(false, false, 3, 0));
            Assert.IsFalse(KingThunderGuide.Applies(false, false, 5, 0));
            Assert.IsFalse(KingThunderGuide.Applies(true, false, 4, 0), "튜토리얼");
            Assert.IsFalse(KingThunderGuide.Applies(false, true, 4, 0), "탑");
        }
    }
}
