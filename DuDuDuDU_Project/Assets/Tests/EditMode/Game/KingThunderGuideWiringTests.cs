using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.Dice;

namespace OJ.Game.Tests
{
    /// <summary>킹 썬더 유도의 상수가 진화 규칙(<see cref="DiceEvolution"/>)과 갈라지지 않게 잠근다.</summary>
    public sealed class KingThunderGuideWiringTests
    {
        [Test]
        public void ScriptedStar_MatchesEvolveRequirement()
        {
            Assert.AreEqual(DiceEvolution.EvolveRequiredStar, KingThunderGuide.ScriptedStar);
        }

        /// <summary>3성이 나오는 처음 두 머지만 고정한다. 2성 머지는 세지 않고, 셋째 3성부터는 평소처럼 굴린다.</summary>
        [Test]
        public void GuideMerges_AreTheFirstTwoThreeStarResultsOnly()
        {
            var run = new OJ.Tutorial.TutorialRunManager();
            run.SetKingThunderGuide(true);

            Assert.IsFalse(run.TryTakeGuideMerge(2), "1→2성 머지는 평소대로");
            Assert.IsTrue(run.TryTakeGuideMerge(KingThunderGuide.ScriptedStar));
            Assert.IsFalse(run.TryTakeGuideMerge(2));
            Assert.IsTrue(run.TryTakeGuideMerge(KingThunderGuide.ScriptedStar));
            Assert.IsFalse(run.TryTakeGuideMerge(KingThunderGuide.ScriptedStar), "셋째 3성부터는 평소대로");
        }

        [Test]
        public void GuideMerges_DoNothingWhenGuideIsOff()
        {
            var run = new OJ.Tutorial.TutorialRunManager();
            run.SetKingThunderGuide(false);
            Assert.IsFalse(run.TryTakeGuideMerge(KingThunderGuide.ScriptedStar));
        }

        [Test]
        public void ChainStones_IsThunderToTimeToKing()
        {
            int chain = DiceEvolution.GetEvolveCost(DiceType.Thunder) + DiceEvolution.GetEvolveCost(DiceType.Time);
            Assert.AreEqual(chain, KingThunderGuide.ChainStones);
        }
    }
}
