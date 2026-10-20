using System;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>고기 축제 가이드가 고기를 채워 둘 때 쓰는 시작 시각 계산.</summary>
    public sealed class MeatGuideFillTests
    {
        private const double Interval = 2d * 60d * 60d;
        private static readonly long Now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc).Ticks;

        [Test]
        public void EmptyTimer_IsMovedBackOneSet()
        {
            long start = IdleRewardFormula.MeatStartTicksForAtLeast(Now, Now, Interval, 1);
            double elapsed = IdleRewardFormula.ElapsedSeconds(start, Now);
            Assert.That(IdleRewardFormula.StoredMeatSetCount(elapsed, Interval, 60), Is.EqualTo(1));
        }

        [Test]
        public void AlreadyEnough_IsLeftAlone()
        {
            long threeSetsAgo = Now - TimeSpan.FromSeconds(Interval * 3).Ticks;
            Assert.That(IdleRewardFormula.MeatStartTicksForAtLeast(threeSetsAgo, Now, Interval, 1), Is.EqualTo(threeSetsAgo));
        }

        [Test]
        public void NoSavedStart_IsTreatedAsEmpty()
        {
            long start = IdleRewardFormula.MeatStartTicksForAtLeast(0L, Now, Interval, 1);
            Assert.That(start, Is.GreaterThan(0L));
            Assert.That(start, Is.LessThan(Now));
        }

        [Test]
        public void NothingRequested_ChangesNothing()
        {
            Assert.That(IdleRewardFormula.MeatStartTicksForAtLeast(Now, Now, Interval, 0), Is.EqualTo(Now));
        }
    }
}
