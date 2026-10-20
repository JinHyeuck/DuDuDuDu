using System.Linq;
using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.Tower;

namespace OJ.Game.Tests
{
    /// <summary>탑 1층 첫 클리어는 신화석 10개를 준다(킹 썬더 강화 유도, 2026-10-20).</summary>
    public sealed class TowerFirstClearRewardTests
    {
        [Test]
        public void FirstFloorFirstClearGivesMythicStone()
        {
            var rewards = TowerRunManager.BuildClearRewards(1, firstClear: true);
            var mythic = rewards.Where(r => r.PointType == PointType.MythicStone).ToList();

            Assert.AreEqual(1, mythic.Count);
            Assert.AreEqual(10, mythic[0].Amount);
        }

        [Test]
        public void FirstFloorRepeatGivesNothing()
        {
            Assert.AreEqual(0, TowerRunManager.BuildClearRewards(1, firstClear: false).Count);
        }

        [Test]
        public void SecondFloorHasNoFirstFloorBonus()
        {
            var rewards = TowerRunManager.BuildClearRewards(2, firstClear: true);
            Assert.IsFalse(rewards.Any(r => r.PointType == PointType.MythicStone));
        }
    }
}
