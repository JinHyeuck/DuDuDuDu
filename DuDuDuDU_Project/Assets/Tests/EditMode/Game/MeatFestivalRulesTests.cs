using NUnit.Framework;
using OJ.IdleReward;
using OJ.Stage;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 고기 축제의 표시 판정을 잠근다.
    ///
    /// <b>왜 테스트가 필요한가.</b> <see cref="MeatFestivalRules.IsFull"/> 이 틀리면
    /// 화면이 <b>조용히</b> 틀린다 — 가득 찬 줄 모르고 이틀을 두면 그동안 구워진 고기가
    /// 전부 버려지는데, 예외도 로그도 없다. 2시간에 한 세트라 60세트는 5일이고,
    /// 주말을 한 번 건너뛰면 닿는 거리다.
    /// </summary>
    public sealed class MeatFestivalRulesTests
    {
        [Test]
        public void 세트가_없으면_받을_수_없다()
        {
            Assert.IsFalse(MeatFestivalRules.CanClaim(0));
            Assert.IsFalse(MeatFestivalRules.CanClaim(-1));
            Assert.IsTrue(MeatFestivalRules.CanClaim(1));
        }

        [Test]
        public void 상한에_닿으면_가득이다()
        {
            Assert.IsFalse(MeatFestivalRules.IsFull(59, 60));
            Assert.IsTrue(MeatFestivalRules.IsFull(60, 60));
            Assert.IsTrue(MeatFestivalRules.IsFull(61, 60), "상한을 넘겨도 가득이다.");
        }

        [Test]
        public void 상한이_0이면_가득_판정을_하지_않는다()
        {
            Assert.IsFalse(MeatFestivalRules.IsFull(5, 0));
        }

        /// <summary>
        /// 저장 상한이 실제로 5일치인지 못 박는다. 주기나 상한을 고치면 여기가 반응한다 —
        /// 이 값이 길어질수록 "며칠 안 들어와도 손해가 없나"의 답이 바뀐다.
        /// </summary>
        [Test]
        public void 최대_저장은_고기_1800개_소탕_360회분이다()
        {
            int total = MeatFestivalRules.TotalMeat(
                IdleRewardManager.MaxMeatSetCount, IdleRewardManager.MeatPerSet);

            Assert.AreEqual(1800, total);
            Assert.AreEqual(360, MeatFestivalRules.SweepCount(total, SweepRules.StaminaCostPerSweep));
        }

        /// <summary>하루 유입은 360 이다. 2시간마다 30개 x 12번 — 소탕 72회분이다.</summary>
        [Test]
        public void 하루_유입은_고기_360개다()
        {
            int setsPerDay = (int)(24d * 60d * 60d / IdleRewardManager.MeatSetIntervalSeconds);
            Assert.AreEqual(12, setsPerDay);

            int daily = MeatFestivalRules.TotalMeat(setsPerDay, IdleRewardManager.MeatPerSet);
            Assert.AreEqual(360, daily);
            Assert.AreEqual(
                SweepEconomy.DailySweepClears,
                MeatFestivalRules.SweepCount(daily, SweepRules.StaminaCostPerSweep),
                "고기 유입과 SweepEconomy 의 하루 소탕 횟수가 어긋났다.");
        }

        [Test]
        public void 받을_것이_없으면_0이다()
        {
            Assert.AreEqual(0, MeatFestivalRules.TotalMeat(0, 30));
            Assert.AreEqual(0, MeatFestivalRules.TotalMeat(5, 0));
            Assert.AreEqual(0, MeatFestivalRules.SweepCount(0, 5));
            Assert.AreEqual(0, MeatFestivalRules.SweepCount(100, 0));
        }
    }
}
