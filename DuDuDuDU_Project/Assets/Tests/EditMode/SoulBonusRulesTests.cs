using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>영혼 보너스의 순수 규칙(목표 열·SP 보상·마석 보상)을 잠근다.</summary>
    public sealed class SoulBonusRulesTests
    {
        [TestCase(0, 10)]
        [TestCase(1, 15)]
        [TestCase(2, 20)]
        [TestCase(3, 25)]
        [TestCase(10, 60)]
        [TestCase(-4, 10)]
        public void Goal_StartsAtTenAndGrowsByFive(int index, int expected)
        {
            Assert.AreEqual(expected, SoulBonusRules.GoalFor(index));
        }

        [Test]
        public void Goal_DoesNotOverflowOnHugeIndex()
        {
            Assert.AreEqual(int.MaxValue, SoulBonusRules.GoalFor(int.MaxValue));
        }

        [TestCase(100, 115, 15, 1825)]
        [TestCase(100, 80, 15, 1300)]
        [TestCase(0, 0, 0, 0)]
        [TestCase(-5, 115, 15, 1725)]
        public void StageTotal_IsInitialPlusWaveClearTimesWaves(int initial, int waveClear, int waves, int expected)
        {
            Assert.AreEqual(expected, SoulBonusRules.StageSpTotal(initial, waveClear, waves));
        }

        [Test]
        public void Sp_IsThreeToFivePercentOfTheStageTotal()
        {
            // 1~10스테이지 총량 1,825 SP → 54 ~ 91 SP. 양 끝 난수가 정확히 그 값을 낸다.
            Assert.AreEqual(54, SoulBonusRules.RollSp(1825, 0));
            Assert.AreEqual(91, SoulBonusRules.RollSp(1825, SoulBonusRules.SpMaxPermille - SoulBonusRules.SpMinPermille));

            for (int roll = 0; roll < 100; roll++)
                Assert.That(SoulBonusRules.RollSp(1825, roll), Is.InRange(54, 91), "roll " + roll);
        }

        [Test]
        public void Sp_SmallStageTotalIsLiftedToTheFloor()
        {
            // 튜토리얼 판은 총량이 작아 3~5% 가 한 자리 수다 — 하한이 받쳐 준다.
            Assert.AreEqual(SoulBonusRules.MinSp, SoulBonusRules.RollSp(226, 0));
            Assert.AreEqual(SoulBonusRules.MinSp, SoulBonusRules.RollSp(0, 7));
        }

        [Test]
        public void Stones_CoverFourToThirteenEvenly()
        {
            var counts = new int[SoulBonusRules.MaxStones + 1];
            for (int roll = 0; roll < 100; roll++)
                counts[SoulBonusRules.RollStones(roll)]++;

            for (int stones = SoulBonusRules.MinStones; stones <= SoulBonusRules.MaxStones; stones++)
                Assert.AreEqual(10, counts[stones], stones + "개");

            Assert.AreEqual(0, counts[SoulBonusRules.MinStones - 1]);
        }

        [Test]
        public void Rolls_NegativeAndHugeValuesWrapIntoRange()
        {
            Assert.That(SoulBonusRules.RollStones(-7), Is.InRange(SoulBonusRules.MinStones, SoulBonusRules.MaxStones));
            Assert.That(SoulBonusRules.RollStones(int.MaxValue), Is.InRange(SoulBonusRules.MinStones, SoulBonusRules.MaxStones));
            Assert.That(SoulBonusRules.RollSp(1825, -300), Is.InRange(54, 91));
            Assert.That(SoulBonusRules.RollSp(1825, int.MaxValue), Is.InRange(54, 91));
        }
    }
}
