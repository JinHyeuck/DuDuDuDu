using System.Linq;
using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.Tower;

namespace OJ.Game.Tests
{
    /// <summary>탑 1층 첫 클리어는 킹 썬더 카피 3장을 준다(킹 썬더 강화 유도, 2026-10-20 신화석 10 → 2026-11 카피 개편).</summary>
    public sealed class TowerFirstClearRewardTests
    {
        [Test]
        public void FirstFloorFirstClearGivesKingThunderCopies()
        {
            var rewards = TowerRunManager.BuildClearRewards(1, firstClear: true);
            var copies = rewards.Where(r => r.PointType == PointType.KingThunderCopy).ToList();

            Assert.AreEqual(1, copies.Count);
            Assert.AreEqual(3, copies[0].Amount);
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
            Assert.IsFalse(rewards.Any(r => r.PointType == PointType.KingThunderCopy));
        }
    }
}
