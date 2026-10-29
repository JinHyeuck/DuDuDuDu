using NUnit.Framework;
using OJ.Hunting;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 딜미터기(기여도)에 시너지 몫이 다이스와 같은 전체 안에 한 줄로 서는지 잠근다.
    /// 시너지의 고정 피해가 성장한 본체 대비 얼마나 약한지(똥옵션인지)가 막대 길이로 보이게 하려는 것이다.
    /// </summary>
    public sealed class DamageContributionSynergyTests
    {
        [Test]
        public void 시너지_피해는_다이스와_같은_전체에서_비율을_받는다()
        {
            var tracker = new DamageContributionTracker();
            tracker.Record(DiceType.Fire, 900);
            tracker.RecordSynergy(100);

            var shares = tracker.BuildShares();

            Assert.AreEqual(2, shares.Count);
            Assert.AreEqual(DiceType.Fire, shares[0].DiceType);
            Assert.IsFalse(shares[0].IsSynergy);
            Assert.AreEqual(0.9f, shares[0].Ratio, 0.0001f);

            Assert.IsTrue(shares[1].IsSynergy);
            Assert.AreEqual(100, shares[1].Damage);
            Assert.AreEqual(0.1f, shares[1].Ratio, 0.0001f);
        }

        [Test]
        public void 시너지가_더_세면_맨_위에_선다()
        {
            var tracker = new DamageContributionTracker();
            tracker.Record(DiceType.Normal, 50);
            tracker.RecordSynergy(500);

            Assert.IsTrue(tracker.BuildShares()[0].IsSynergy);
        }

        [Test]
        public void 시너지만_때려도_한_줄로_나온다()
        {
            var tracker = new DamageContributionTracker();
            tracker.RecordSynergy(300);

            var shares = tracker.BuildShares();
            Assert.AreEqual(1, shares.Count);
            Assert.AreEqual(1f, shares[0].Ratio, 0.0001f);
        }

        [Test]
        public void 시너지_몫도_웨이브가_바뀌면_비워진다()
        {
            var tracker = new DamageContributionTracker();
            tracker.RecordSynergy(300);
            tracker.BeginWave();

            Assert.IsEmpty(tracker.BuildShares());
        }

        [Test]
        public void 막혀서_0인_시너지_피해는_세지_않는다()
        {
            var tracker = new DamageContributionTracker();
            tracker.RecordSynergy(0);
            tracker.RecordSynergy(-5);

            Assert.IsEmpty(tracker.BuildShares());
        }
    }
}
