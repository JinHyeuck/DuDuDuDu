using System.Collections.Generic;
using NUnit.Framework;
using OJ.Point;
using OJ.Stage;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 소탕의 잠금 규칙을 잠근다.
    ///
    /// <b>왜 여기서만 테스트가 되는가.</b> 헤드리스 러너 안에서는 싱글톤도 SO 도 돌지 않는다
    /// (<see cref="AssemblyPilotTests"/> 주석). 그래서 규칙을 <see cref="SweepRules"/> 라는
    /// 상태 없는 static 으로 빼 두었고 이 파일은 그것만 두드린다.
    ///
    /// <b>보상 내용은 여기서 검사할 수 없다.</b> <see cref="SweepRules.BuildRewards"/> 는
    /// <c>StageRewardCalculator.BuildNormalClearRewards</c> 를 타고, 그 안의
    /// <c>Random.Range</c>(UnityEngine) 가 헤드리스 Mono 에서
    /// <c>cant resolve internal call to "UnityEngine.Random::RandomRangeInt"</c> 로 죽는다.
    /// 그래서 이 파일은 <b>난수를 타지 않는 경로만</b> 두드린다 —
    /// 보상 금액은 에디터에서 눌러 확인할 것.
    ///
    /// <b>무엇이 걸려 있는가.</b> 잠금 조건이 느슨해지면 한 판도 못 깬 유저가 보상을 받는다.
    /// </summary>
    public sealed class SweepRulesTests
    {
        [Test]
        public void 클리어_기록이_없으면_잠긴다()
        {
            Assert.IsFalse(SweepRules.IsUnlocked(0));
            Assert.AreEqual(SweepRules.NoTargetStage, SweepRules.ResolveTargetStageIndex(0));
        }

        /// <summary>
        /// <b>해금이 아니라 클리어가 기준이다.</b> 1스테이지는 처음부터 해금돼 있으므로
        /// (<c>highestUnlockedStageIndex = 1</c>) 해금으로 판정하면 신규 유저가 첫 판을
        /// 깨기도 전에 소탕을 돌린다. 이 테스트가 그 회귀를 잡는다.
        /// </summary>
        [Test]
        public void 한_판이라도_깼으면_열린다()
        {
            Assert.IsTrue(SweepRules.IsUnlocked(1));
            Assert.AreEqual(1, SweepRules.ResolveTargetStageIndex(1));
        }

        [Test]
        public void 대상은_마지막으로_깬_스테이지다()
        {
            Assert.AreEqual(7, SweepRules.ResolveTargetStageIndex(7));
        }

        [Test]
        public void 음수_기록도_잠긴_것으로_본다()
        {
            Assert.IsFalse(SweepRules.IsUnlocked(-1));
            Assert.AreEqual(SweepRules.NoTargetStage, SweepRules.ResolveTargetStageIndex(-1));
        }

        [Test]
        public void 횟수는_최소_한_번이다()
        {
            Assert.AreEqual(1, SweepRules.ClampCount(0));
            Assert.AreEqual(1, SweepRules.ClampCount(-5));
            Assert.AreEqual(24, SweepRules.ClampCount(24));
        }

        /// <summary>
        /// <b>소탕 1회 = 고기 5개.</b> 이 값이 루프 회수율 r 의 분모다
        /// (<c>Docs/CurrencyPolicy.md</c> 4장). 여기를 내리면 r 이 올라가므로,
        /// 이 테스트가 깨지면 경제 승수 1/(1-r) 을 다시 계산해야 한다는 신호다.
        /// </summary>
        [Test]
        public void 소탕_1회는_고기_5개다()
        {
            Assert.AreEqual(5, SweepRules.StaminaCostPerSweep);
            Assert.AreEqual(5, SweepRules.TotalStaminaCost(1));
            Assert.AreEqual(60, SweepRules.TotalStaminaCost(12));
        }

        /// <summary>0 회를 요청해도 비용은 1회분이다 — <see cref="SweepRules.ClampCount"/> 를 탄다.</summary>
        [Test]
        public void 비용은_최소_1회분이다()
        {
            Assert.AreEqual(5, SweepRules.TotalStaminaCost(0));
            Assert.AreEqual(5, SweepRules.TotalStaminaCost(-3));
        }

        /// <summary>
        /// 보유 고기로 돌 수 있는 횟수. <b>모자라면 0 이다.</b>
        ///
        /// 여기서 1 을 돌려주면 고기가 4개뿐인 유저에게 창이 "1회 가능"이라고 말하고,
        /// 누르면 <c>TrySpend</c> 에서 조용히 실패한다.
        /// </summary>
        [Test]
        public void 고기가_모자라면_최대_횟수가_0이다()
        {
            Assert.AreEqual(0, SweepRules.ResolveMaxCount(0));
            Assert.AreEqual(0, SweepRules.ResolveMaxCount(4));
            Assert.AreEqual(1, SweepRules.ResolveMaxCount(5));
        }

        [Test]
        public void 최대_횟수는_보유_고기를_비용으로_나눈_값이다()
        {
            Assert.AreEqual(12, SweepRules.ResolveMaxCount(60));
            Assert.AreEqual(12, SweepRules.ResolveMaxCount(64), "나머지는 버린다.");
            Assert.AreEqual(180, SweepRules.ResolveMaxCount(900), "고기 최대 보유량(30세트 x 30).");
        }

        [Test]
        public void 고른_횟수는_최대치로_잘린다()
        {
            Assert.AreEqual(12, SweepRules.ClampToMax(99, 12));
            Assert.AreEqual(1, SweepRules.ClampToMax(0, 12));
            Assert.AreEqual(7, SweepRules.ClampToMax(7, 12));
        }

        /// <summary>최대치가 0 이면 어떤 입력도 0 이다. 창은 이 값으로 소탕 버튼을 잠근다.</summary>
        [Test]
        public void 최대치가_0이면_고른_횟수도_0이다()
        {
            Assert.AreEqual(0, SweepRules.ClampToMax(1, 0));
            Assert.AreEqual(0, SweepRules.ClampToMax(99, 0));
        }

        /// <summary>
        /// 잠긴 상태에서는 보상 목록이 비어 있다.
        ///
        /// <b>이 경로만은 난수를 안 탄다.</b> <see cref="SweepRules.BuildRewards"/> 가
        /// <c>stageIndex &lt; 1</c> 에서 <c>StageRewardCalculator</c> 를 부르기 전에
        /// 돌아가기 때문이다. 그 조기 반환이 사라지면 이 테스트가 난수 호출에 걸려 죽는데,
        /// 그것도 회귀 신호로 맞다 — 잠긴 상태에서 보상 계산이 돌면 안 된다.
        /// </summary>
        [Test]
        public void 잠긴_상태에서는_보상이_비어_있다()
        {
            List<PointRewardEntry> rewards = SweepRules.BuildRewards(SweepRules.NoTargetStage, 10);
            Assert.IsEmpty(rewards);
        }
    }
}
