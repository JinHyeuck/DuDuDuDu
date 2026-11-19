using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OJ;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>
    /// "랜덤 카피" 지급용 재화가 구체적인 다이스 카피로 풀리는 규칙을 잠근다.
    /// 난수는 인자로 받는 static 이라 고정값을 넣어 두드린다(헤드리스에서 <c>UnityEngine.Random</c> 은 안 돈다).
    /// </summary>
    public sealed class DiceCopyRewardsTests
    {
        [Test]
        public void 지급용_표지만_랜덤으로_본다()
        {
            Assert.IsTrue(DiceCopyRewards.IsRandom(PointType.RandomBasicCopy));
            Assert.IsTrue(DiceCopyRewards.IsRandom(PointType.RandomRareCopy));
            Assert.IsTrue(DiceCopyRewards.IsRandom(PointType.RandomKingCopy));
            Assert.IsTrue(DiceCopyRewards.IsRandom(PointType.RandomRareKingCopy));

            Assert.IsFalse(DiceCopyRewards.IsRandom(PointType.KingFireCopy));
            Assert.IsFalse(DiceCopyRewards.IsRandom(PointType.NormalScroll));
            Assert.IsFalse(DiceCopyRewards.IsRandom(PointType.Gold));
        }

        [Test]
        public void 후보는_등급별_다섯_종이고_레어킹은_열_종이다()
        {
            Assert.AreEqual(5, DiceCopyRewards.Candidates(PointType.RandomBasicCopy).Count);
            Assert.AreEqual(5, DiceCopyRewards.Candidates(PointType.RandomRareCopy).Count);
            Assert.AreEqual(5, DiceCopyRewards.Candidates(PointType.RandomKingCopy).Count);
            Assert.AreEqual(10, DiceCopyRewards.Candidates(PointType.RandomRareKingCopy).Count);
            Assert.AreEqual(0, DiceCopyRewards.Candidates(PointType.Gold).Count);
        }

        /// <summary>후보는 실제 <c>ToScrollType</c> 이 돌려주는 카피와 같은 집합이어야 한다 — 다르면 지급해도 못 쓰는 카피가 된다.</summary>
        [Test]
        public void 후보는_다이스의_실제_카피_재화와_같다()
        {
            var basic = new[] { DiceType.Normal, DiceType.Fire, DiceType.Ice, DiceType.Poison, DiceType.Thunder };
            var rare = new[] { DiceType.Tornado, DiceType.Stun, DiceType.ArmorBreak, DiceType.Wind, DiceType.Time };
            var king = new[] { DiceType.KingNormal, DiceType.KingFire, DiceType.KingIce, DiceType.KingPoison, DiceType.KingThunder };

            CollectionAssert.AreEquivalent(
                basic.Select(PointManager.ToScrollType), DiceCopyRewards.Candidates(PointType.RandomBasicCopy));
            CollectionAssert.AreEquivalent(
                rare.Select(PointManager.ToScrollType), DiceCopyRewards.Candidates(PointType.RandomRareCopy));
            CollectionAssert.AreEquivalent(
                king.Select(PointManager.ToScrollType), DiceCopyRewards.Candidates(PointType.RandomKingCopy));
            CollectionAssert.AreEquivalent(
                rare.Concat(king).Select(PointManager.ToScrollType), DiceCopyRewards.Candidates(PointType.RandomRareKingCopy));
        }

        [Test]
        public void 풀어도_합계는_그대로다()
        {
            int tick = 0;
            Dictionary<PointType, int> result =
                DiceCopyRewards.Distribute(PointType.RandomRareKingCopy, 37, n => tick++ % n);

            Assert.AreEqual(37, result.Values.Sum());
            foreach (PointType key in result.Keys)
                CollectionAssert.Contains(DiceCopyRewards.Candidates(PointType.RandomRareKingCopy).ToList(), key);
        }

        [Test]
        public void 난수가_정한_후보로_들어간다()
        {
            // 항상 첫 후보를 고르는 난수 — 전부 첫 번째 킹(King Normal 카피)으로 들어가야 한다.
            Dictionary<PointType, int> result = DiceCopyRewards.Distribute(PointType.RandomKingCopy, 4, n => 0);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(4, result[PointType.KingNormalCopy]);
        }

        [Test]
        public void 범위를_벗어난_난수는_끝으로_붙인다()
        {
            Dictionary<PointType, int> high = DiceCopyRewards.Distribute(PointType.RandomRareCopy, 2, n => 99);
            Dictionary<PointType, int> low = DiceCopyRewards.Distribute(PointType.RandomRareCopy, 2, n => -5);

            Assert.AreEqual(2, high[PointType.TimeCopy]);
            Assert.AreEqual(2, low[PointType.TornadoCopy]);
        }

        [Test]
        public void 표지가_아니거나_장수가_없으면_빈_사전이다()
        {
            Assert.AreEqual(0, DiceCopyRewards.Distribute(PointType.Gold, 5, n => 0).Count);
            Assert.AreEqual(0, DiceCopyRewards.Distribute(PointType.RandomRareCopy, 0, n => 0).Count);
            Assert.AreEqual(0, DiceCopyRewards.Distribute(PointType.RandomRareCopy, -3, n => 0).Count);
            Assert.AreEqual(0, DiceCopyRewards.Distribute(PointType.RandomRareCopy, 3, null).Count);
        }
    }
}
