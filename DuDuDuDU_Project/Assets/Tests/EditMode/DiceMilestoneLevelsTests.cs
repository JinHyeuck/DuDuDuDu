using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 마일스톤 레벨 상수를 잠근다. 효과 코드의 `level >= N` 이 전부 이 상수를 보므로
    /// 값이 조용히 바뀌면 서른 곳이 한꺼번에 움직인다 — 바꿀 거면 이 테스트를 같이 고쳐서 의도를 남길 것.
    /// </summary>
    public class DiceMilestoneLevelsTests
    {
        [Test]
        public void MilestonesAreTenTwentyTwentyFiveThirty()
        {
            Assert.AreEqual(10, DiceMilestoneLevels.First);
            Assert.AreEqual(20, DiceMilestoneLevels.Second);
            Assert.AreEqual(25, DiceMilestoneLevels.Third);
            Assert.AreEqual(30, DiceMilestoneLevels.Final);
        }

        [Test]
        public void MilestonesAreStrictlyIncreasing()
        {
            Assert.Less(DiceMilestoneLevels.First, DiceMilestoneLevels.Second);
            Assert.Less(DiceMilestoneLevels.Second, DiceMilestoneLevels.Third);
            Assert.Less(DiceMilestoneLevels.Third, DiceMilestoneLevels.Final);
        }

        [Test]
        public void DoubleHitOpensAtThirdMilestone()
        {
            Assert.IsFalse(CriticalFormula.IsDoubleHitLevel(DiceMilestoneLevels.Third - 1));
            Assert.IsTrue(CriticalFormula.IsDoubleHitLevel(DiceMilestoneLevels.Third));
        }
    }
}
