using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 튜토리얼 판의 SP 계산과 건너뛰기 판정을 잠근다.
    ///
    /// <b>SP 가 정확해야 하는 이유.</b> 첫 단계는 "SP 를 다 쓰면 딤드가 풀린다" 다.
    /// 한 점이라도 남으면 영영 안 풀리고, 모자라면 여섯 번을 다 하기 전에 막힌다 —
    /// 둘 다 첫 판의 첫 화면에서 멈추는 사고다.
    /// </summary>
    public sealed class TutorialRulesTests
    {
        /// <summary>두 번마다 1씩 오른다: 10,10,11,11,12,12.</summary>
        [Test]
        public void SummonCostSumFollowsTwoStepIncrease()
        {
            Assert.AreEqual(66, TutorialRules.SummonCostSum(10, 0, 2, 6));
        }

        /// <summary>이어서 세 번: 13,13,14.</summary>
        [Test]
        public void SummonCostSumContinuesFromState()
        {
            Assert.AreEqual(40, TutorialRules.SummonCostSum(13, 0, 2, 3));

            int nine = TutorialRules.SummonCostSum(10, 0, 2, 9);
            int six = TutorialRules.SummonCostSum(10, 0, 2, 6);
            Assert.AreEqual(40, nine - six);
        }

        /// <summary>카운터가 이미 하나 쌓여 있으면 첫 소환 뒤에 바로 오른다.</summary>
        [Test]
        public void SummonCostSumRespectsPartialCounter()
        {
            Assert.AreEqual(10 + 11, TutorialRules.SummonCostSum(10, 1, 2, 2));
        }

        /// <summary>소환 시스템의 Mathf.Max(1, …) 와 같다 — 0 이하는 "매번 오른다" 다.</summary>
        [Test]
        public void NonPositiveIncreaseStepIsOne()
        {
            Assert.AreEqual(10 + 11 + 12, TutorialRules.SummonCostSum(10, 0, 0, 3));
        }

        [Test]
        public void ZeroCountCostsNothing()
        {
            Assert.AreEqual(0, TutorialRules.SummonCostSum(10, 0, 2, 0));
        }

        // ── 건너뛰기 ───────────────────────────────────────────────────

        [Test]
        public void FreshSaveDoesNotSkip()
        {
            Assert.IsFalse(TutorialRules.ShouldSkip(false, 1, 0));
        }

        [Test]
        public void CompletedFlagSkips()
        {
            Assert.IsTrue(TutorialRules.ShouldSkip(true, 1, 0));
        }

        /// <summary>플래그가 생기기 전부터 플레이하던 세이브는 첫 판으로 돌려보내지 않는다.</summary>
        [Test]
        public void ExistingProgressSkipsWithoutFlag()
        {
            Assert.IsTrue(TutorialRules.ShouldSkip(false, 2, 0));
            Assert.IsTrue(TutorialRules.ShouldSkip(false, 1, 1));
        }
    }
}
