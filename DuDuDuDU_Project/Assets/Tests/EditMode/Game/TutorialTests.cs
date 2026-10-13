using System.Collections.Generic;
using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.Dice;
using OJ.Stage;
using OJ.Tutorial;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 인게임 첫 튜토리얼의 대본과 수치를 잠근다. (Docs/IngameTutorialSteps.md)
    ///
    /// 디렉터 자체(딤드·대기)는 씬이 있어야 돌아서 여기서 못 두드린다. 대신 디렉터가
    /// <b>믿고 있는 전제</b>를 잠근다 — 전제가 깨지면 튜토리얼은 조용히 멈춘다
    /// (딤드가 안 풀리거나, 진화 버튼이 안 먹거나).
    /// </summary>
    public sealed class TutorialTests
    {
        private const int SummonsPerCostIncrease = 2;

        // ── 판 수치 ────────────────────────────────────────────────────

        /// <summary>시작 SP 가 첫 여섯 번 소환과 정확히 같다. 남거나 모자라면 첫 단계에서 멈춘다.</summary>
        [Test]
        public void 시작_SP_는_첫_소환_여섯_번과_정확히_같다()
        {
            StageData stage = TutorialStage.Build(SummonsPerCostIncrease);
            int expected = TutorialRules.SummonCostSum(
                TutorialStage.StartSummonCost, 0, SummonsPerCostIncrease, TutorialStage.OpeningSummons.Count);

            Assert.That(stage.initialSP, Is.EqualTo(expected));
        }

        /// <summary>
        /// 1웨이브 클리어 SP 가 "되돌린 뒤 세 번 소환" 과 정확히 같다.
        /// 되돌리기는 2웨이브 직전으로 가므로 그때 손에 있는 SP 가 이 값이다.
        /// </summary>
        [Test]
        public void 웨이브_클리어_SP_는_되돌린_뒤_소환과_정확히_같다()
        {
            StageData stage = TutorialStage.Build(SummonsPerCostIncrease);
            int opening = TutorialStage.OpeningSummons.Count;
            int expected = TutorialRules.SummonCostSum(
                TutorialStage.StartSummonCost, 0, SummonsPerCostIncrease, opening + TutorialStage.RetrySummonCount)
                - TutorialRules.SummonCostSum(TutorialStage.StartSummonCost, 0, SummonsPerCostIncrease, opening);

            Assert.That(stage.waveClearSP, Is.EqualTo(expected));
        }

        /// <summary>
        /// 현상금을 거는 웨이브와 손을 놓는 웨이브는 <b>보스 웨이브가 아니어야</b> 한다 —
        /// 마지막 웨이브에는 현상금이 안 나온다.
        /// </summary>
        [Test]
        public void 현상금_웨이브는_보스_앞이다()
        {
            StageData stage = TutorialStage.Build(SummonsPerCostIncrease);

            Assert.That(BountyFormula.CanSpawnOnWave(TutorialStage.BountyWave, stage.totalWaves), Is.True);
            Assert.That(BountyFormula.CanSpawnOnWave(stage.totalWaves - 1, stage.totalWaves), Is.True,
                "보스 바로 앞 웨이브(2단계 현상금)에도 현상금이 나와야 한다");
            Assert.That(BountyFormula.CanSpawnOnWave(stage.totalWaves, stage.totalWaves), Is.False);
        }

        // ── 대본 ───────────────────────────────────────────────────────

        /// <summary>머지 단계가 여는 두 칸에 같은 종류(노멀)가 놓인다.</summary>
        [Test]
        public void 머지_두_칸에는_노멀이_놓인다()
        {
            var bySlot = new Dictionary<int, DiceType>();
            foreach (TutorialRunManager.ScriptedSummon summon in TutorialStage.OpeningSummons)
                bySlot[summon.SlotIndex] = summon.Type;

            Assert.That(bySlot[TutorialStage.MergeSlotA], Is.EqualTo(DiceType.Normal));
            Assert.That(bySlot[TutorialStage.MergeSlotB], Is.EqualTo(DiceType.Normal));
        }

        /// <summary>대본의 칸이 겹치면 두 번째 소환이 랜덤 칸으로 새어 머지 구멍이 빈 칸을 연다.</summary>
        [Test]
        public void 대본_칸은_겹치지_않는다()
        {
            var seen = new HashSet<int>();
            foreach (TutorialRunManager.ScriptedSummon summon in TutorialStage.OpeningSummons)
                Assert.That(seen.Add(summon.SlotIndex), Is.True, "칸 " + summon.SlotIndex + " 이 두 번 나온다");
        }

        /// <summary>
        /// 원안: 노멀 3 · 불 1 · 얼음 1 · 독 1. 기획이 적은 구성 그대로다.
        /// </summary>
        [Test]
        public void 첫_소환_구성은_노멀셋_불_얼음_독이다()
        {
            var counts = new Dictionary<DiceType, int>();
            foreach (TutorialRunManager.ScriptedSummon summon in TutorialStage.OpeningSummons)
            {
                counts.TryGetValue(summon.Type, out int n);
                counts[summon.Type] = n + 1;
            }

            Assert.That(counts[DiceType.Normal], Is.EqualTo(3));
            Assert.That(counts[DiceType.Fire], Is.EqualTo(1));
            Assert.That(counts[DiceType.Ice], Is.EqualTo(1));
            Assert.That(counts[DiceType.Poison], Is.EqualTo(1));
        }

        /// <summary>
        /// 선물로 주는 4성을 진화하면 튜토리얼이 보여 주려는 다이스가 되고, 그 다이스가 곧
        /// 언락 표의 튜토리얼 보상이다. 셋 중 하나만 바뀌면 진화 버튼이 조용히 실패하거나
        /// (대상 미보유) 엉뚱한 다이스를 준다.
        /// </summary>
        [Test]
        public void 선물_진화_대상이_언락_표의_튜토리얼_보상이다()
        {
            Assert.That(DiceEvolution.TryGetEvolveTarget(TutorialStage.GiftDice, out DiceType target), Is.True);
            Assert.That(target, Is.EqualTo(TutorialStage.EvolveTarget));

            DiceType tutorialUnlock = DiceType.Max;
            foreach (DiceUnlockDefinition definition in DiceUnlockDatabase.BuildDefaults())
            {
                if (definition.tutorialReward)
                    tutorialUnlock = definition.diceType;
            }

            Assert.That(tutorialUnlock, Is.EqualTo(TutorialStage.EvolveTarget));
        }

        [Test]
        public void 머지_결과는_선물과_같은_계통이다()
        {
            Assert.That(TutorialStage.MergeResult, Is.EqualTo(TutorialStage.GiftDice));
        }

        // ── 대본 관리자 ────────────────────────────────────────────────

        /// <summary>튜토리얼 판이 아니면 대본이 있어도 꺼내 주지 않는다 — 본편이 대본을 물면 안 된다.</summary>
        [Test]
        public void 튜토리얼이_아니면_대본을_꺼내지_않는다()
        {
            var run = new TutorialRunManager();
            run.ScriptSummons(TutorialStage.OpeningSummons);
            run.ForceNextMergeType(DiceType.Thunder);

            Assert.That(run.TryTakeScriptedSummon(out _), Is.False);
            Assert.That(run.TryTakeForcedMergeType(out _), Is.False);
        }

        [Test]
        public void 대본은_순서대로_한_번씩_나온다()
        {
            var run = new TutorialRunManager();
            run.Begin();
            run.ScriptSummons(TutorialStage.OpeningSummons);

            for (int i = 0; i < TutorialStage.OpeningSummons.Count; i++)
            {
                Assert.That(run.TryTakeScriptedSummon(out TutorialRunManager.ScriptedSummon summon), Is.True);
                Assert.That(summon.SlotIndex, Is.EqualTo(TutorialStage.OpeningSummons[i].SlotIndex));
            }

            Assert.That(run.TryTakeScriptedSummon(out _), Is.False);
            Assert.That(run.PendingScriptedSummons, Is.EqualTo(0));
        }

        /// <summary>머지 결과 강제는 한 번뿐이다. 남으면 그 뒤 모든 머지가 번개가 된다.</summary>
        [Test]
        public void 머지_결과_강제는_한_번만_쓴다()
        {
            var run = new TutorialRunManager();
            run.Begin();
            run.ForceNextMergeType(DiceType.Thunder);

            Assert.That(run.TryTakeForcedMergeType(out DiceType first), Is.True);
            Assert.That(first, Is.EqualTo(DiceType.Thunder));
            Assert.That(run.TryTakeForcedMergeType(out _), Is.False);
        }

        [Test]
        public void Clear_는_대본과_결과를_모두_비운다()
        {
            var run = new TutorialRunManager();
            run.Begin();
            run.ScriptSummons(TutorialStage.OpeningSummons);
            run.NotifyFailed();
            run.Clear();

            Assert.That(run.IsActive, Is.False);
            Assert.That(run.IsFailed, Is.False);
            Assert.That(run.PendingScriptedSummons, Is.EqualTo(0));
        }
    }
}
