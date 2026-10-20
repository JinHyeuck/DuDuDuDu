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

        [Test]
        public void ChainStones_IsThunderToTimeToKing()
        {
            int chain = DiceEvolution.GetEvolveCost(DiceType.Thunder) + DiceEvolution.GetEvolveCost(DiceType.Time);
            Assert.AreEqual(chain, KingThunderGuide.ChainStones);
        }
    }
}
