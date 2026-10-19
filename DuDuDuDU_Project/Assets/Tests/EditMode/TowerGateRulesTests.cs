using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class TowerGateRulesTests
    {
        [TestCase(1, 0)]
        [TestCase(5, 0)]
        [TestCase(6, 1)]
        [TestCase(9, 4)]
        [TestCase(10, 5)]
        [TestCase(23, 18)]
        [TestCase(0, 0)]
        [TestCase(-3, 0)]
        public void RequiredTowerFloor_TowerFollowsStageByOffset(int stage, int expected)
        {
            Assert.AreEqual(expected, TowerGateRules.RequiredTowerFloor(stage));
        }

        [TestCase(5, 0, false)]
        [TestCase(6, 0, true)]
        [TestCase(6, 1, false)]
        [TestCase(10, 4, true)]
        [TestCase(10, 5, false)]
        [TestCase(23, 17, true)]
        [TestCase(23, 18, false)]
        [TestCase(23, 30, false)]
        public void IsBottomRowLocked_OnlyWhenTowerLags(int stage, int cleared, bool expected)
        {
            Assert.AreEqual(expected, TowerGateRules.IsBottomRowLocked(stage, cleared));
        }

        [TestCase(5, 0, true)]   // 튜토리얼: 6 부터 탑 1층을 요구한다
        [TestCase(5, 1, false)]
        [TestCase(4, 0, false)]  // 6 이전은 요구가 없다
        [TestCase(6, 0, false)]  // 이미 막혀 있는 중이라 반복하지 않는다
        [TestCase(6, 1, true)]   // 따라왔다가 다시 뒤처지는 순간
        [TestCase(10, 5, true)]
        [TestCase(10, 4, false)]
        public void ShouldPlaySeal_OnlyWhenNewlyLocked(int clearedStage, int towerFloor, bool expected)
        {
            Assert.AreEqual(expected, TowerGateRules.ShouldPlaySeal(clearedStage, towerFloor));
        }

        [TestCase(6, 0, 1, true)]
        [TestCase(6, 1, 2, false)]  // 이미 풀려 있었다
        [TestCase(6, 0, 0, false)]
        [TestCase(10, 3, 5, true)]
        [TestCase(10, 1, 3, false)] // 올랐지만 아직 모자라다
        [TestCase(5, 0, 3, false)]  // 애초에 막혀 있지 않았다
        public void ReleasedByTowerClear_NeedsLockedThenOpen(int stage, int before, int after, bool expected)
        {
            Assert.AreEqual(expected, TowerGateRules.ReleasedByTowerClear(stage, before, after));
        }

        [TestCase(13, false)]
        [TestCase(14, true)]
        [TestCase(20, true)]
        [TestCase(21, false)]
        [TestCase(-1, false)]
        public void IsBottomRowSlot_SevenByThree(int slot, bool expected)
        {
            Assert.AreEqual(expected, TowerGateRules.IsBottomRowSlot(slot, 21, 7));
        }

        [Test]
        public void IsBottomRowSlot_RejectsBadGrid()
        {
            Assert.IsFalse(TowerGateRules.IsBottomRowSlot(0, 21, 0));
            Assert.IsFalse(TowerGateRules.IsBottomRowSlot(0, 3, 7));
        }
    }
}
