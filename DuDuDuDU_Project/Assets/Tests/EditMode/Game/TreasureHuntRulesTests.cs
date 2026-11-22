using System.Collections.Generic;
using NUnit.Framework;
using OJ.EventMinigame;

namespace OJ.Game.Tests
{
    /// <summary>보물찾기 규칙: 판 생성, 칸 파기, 스테이지·열쇠·상자, 보상 가치.</summary>
    public sealed class TreasureHuntRulesTests
    {
        private static int ShapeSize(int shape)
        {
            return TreasureHuntRules.ShapeSilhouette(shape).Length / 2;
        }

        [Test]
        public void 모양마다_방향과_칸_수가_맞다()
        {
            int[] expectedCells = { 2, 3, 3, 4, 4, 4, 4, 4, 5 };

            for (int shape = 0; shape < TreasureHuntRules.ShapeCount; shape++)
                Assert.AreEqual(expectedCells[shape], ShapeSize(shape), "shape " + shape);
        }

        [Test]
        public void 모든_단계와_여러_시드에서_보물이_겹치지_않고_칸_수가_맞다()
        {
            for (int tier = 1; tier <= TreasureHuntRules.Tiers.Length; tier++)
            {
                TreasureTier data = TreasureHuntRules.Tiers[tier - 1];

                for (int seed = 0; seed < 60; seed++)
                {
                    TreasureBoard board = TreasureHuntRules.Generate(tier, seed);

                    Assert.AreEqual(data.Width, board.Width);
                    Assert.AreEqual(data.Height, board.Height);
                    Assert.AreEqual(data.Shapes.Length, board.TreasureCount);

                    for (int t = 0; t < board.TreasureCount; t++)
                        Assert.AreEqual(ShapeSize(data.Shapes[t]), board.CellCount(t), "tier " + tier + " seed " + seed + " treasure " + t);

                    int occupied = 0;
                    foreach (int owner in board.Owner)
                    {
                        if (owner >= 0)
                            occupied++;
                    }

                    int expectedTotal = 0;
                    foreach (int shape in data.Shapes)
                        expectedTotal += ShapeSize(shape);

                    Assert.AreEqual(expectedTotal, occupied, "tier " + tier + " seed " + seed);
                }
            }
        }

        [Test]
        public void 같은_시드는_같은_판이고_시드가_다르면_판이_달라진다()
        {
            TreasureBoard a = TreasureHuntRules.Generate(3, 1234);
            TreasureBoard b = TreasureHuntRules.Generate(3, 1234);
            CollectionAssert.AreEqual(a.Owner, b.Owner);

            bool anyDifferent = false;
            for (int seed = 1; seed <= 10; seed++)
            {
                TreasureBoard other = TreasureHuntRules.Generate(3, 1234 + seed);
                if (!System.Linq.Enumerable.SequenceEqual(a.Owner, other.Owner))
                    anyDifferent = true;
            }

            Assert.IsTrue(anyDifferent);
        }

        [Test]
        public void 범위_밖_단계는_가장_가까운_단계로_가둔다()
        {
            Assert.AreEqual(TreasureHuntRules.Generate(1, 7).Width, TreasureHuntRules.Generate(0, 7).Width);
            Assert.AreEqual(TreasureHuntRules.Generate(8, 7).Width, TreasureHuntRules.Generate(99, 7).Width);
        }

        [Test]
        public void 빈칸을_파면_보물도_클리어도_없다()
        {
            TreasureBoard board = TreasureHuntRules.Generate(1, 1);
            bool[] revealed = new bool[board.Width * board.Height];

            for (int i = 0; i < board.Owner.Length; i++)
            {
                if (board.Owner[i] >= 0)
                    continue;

                DigResult result = TreasureHuntRules.Dig(board, revealed, i % board.Width, i / board.Width);
                Assert.IsTrue(result.Valid);
                Assert.IsFalse(result.IsHit);
                Assert.AreEqual(-1, result.Treasure);
                Assert.IsFalse(result.StageCleared);
            }
        }

        [Test]
        public void 이미_판_칸과_범위_밖은_아무_일도_없다()
        {
            TreasureBoard board = TreasureHuntRules.Generate(1, 1);
            bool[] revealed = new bool[board.Width * board.Height];

            Assert.IsTrue(TreasureHuntRules.Dig(board, revealed, 0, 0).Valid);
            Assert.IsFalse(TreasureHuntRules.Dig(board, revealed, 0, 0).Valid);
            Assert.IsFalse(TreasureHuntRules.Dig(board, revealed, -1, 0).Valid);
            Assert.IsFalse(TreasureHuntRules.Dig(board, revealed, 0, board.Height).Valid);
            Assert.IsFalse(TreasureHuntRules.Dig(null, revealed, 0, 0).Valid);
            Assert.IsFalse(TreasureHuntRules.Dig(board, new bool[3], 0, 0).Valid);
            Assert.IsFalse(TreasureHuntRules.Dig(board, null, 0, 0).Valid);
        }

        [Test]
        public void 보물을_다_찾는_마지막_칸에서만_스테이지가_클리어된다()
        {
            TreasureBoard board = TreasureHuntRules.Generate(2, 99);
            bool[] revealed = new bool[board.Width * board.Height];

            var treasureCells = new List<int>();
            for (int i = 0; i < board.Owner.Length; i++)
            {
                if (board.Owner[i] >= 0)
                    treasureCells.Add(i);
            }

            for (int n = 0; n < treasureCells.Count; n++)
            {
                int cell = treasureCells[n];
                DigResult result = TreasureHuntRules.Dig(board, revealed, cell % board.Width, cell / board.Width);

                Assert.IsTrue(result.Valid);
                Assert.IsTrue(result.IsHit);
                Assert.AreEqual(n == treasureCells.Count - 1, result.StageCleared, "n " + n);
            }

            Assert.IsTrue(TreasureHuntRules.AllFound(board, revealed));
        }

        [Test]
        public void 보물_하나를_다_드러내면_그_보물만_완료다()
        {
            TreasureBoard board = TreasureHuntRules.Generate(2, 5);
            bool[] revealed = new bool[board.Width * board.Height];

            int dug = 0;
            DigResult last = default(DigResult);
            for (int i = 0; i < board.Owner.Length; i++)
            {
                if (board.Owner[i] != 0)
                    continue;

                last = TreasureHuntRules.Dig(board, revealed, i % board.Width, i / board.Width);
                dug++;
                if (dug < board.CellCount(0))
                    Assert.IsFalse(last.TreasureCompleted);
            }

            Assert.IsTrue(last.TreasureCompleted);
            Assert.IsFalse(last.StageCleared);
            Assert.IsTrue(TreasureHuntRules.IsTreasureFound(board, revealed, 0));
            Assert.IsFalse(TreasureHuntRules.IsTreasureFound(board, revealed, 1));
        }

        [Test]
        public void 남은_보물_목록은_찾은_보물을_뺀다()
        {
            TreasureBoard board = TreasureHuntRules.Generate(2, 5);
            bool[] revealed = new bool[board.Width * board.Height];

            Assert.AreEqual(board.TreasureCount, TreasureHuntRules.RemainingShapes(board, revealed).Count);

            for (int i = 0; i < board.Owner.Length; i++)
            {
                if (board.Owner[i] == 0)
                    TreasureHuntRules.Dig(board, revealed, i % board.Width, i / board.Width);
            }

            var expected = new List<int>(board.TreasureShapes);
            expected.RemoveAt(0);

            List<int> remaining = TreasureHuntRules.RemainingShapes(board, revealed);
            CollectionAssert.AreEquivalent(expected, remaining);
        }

        [Test]
        public void 단계는_3스테이지마다_오르고_8단계에서_멈춘다()
        {
            Assert.AreEqual(1, TreasureHuntRules.TierOfStage(0));
            Assert.AreEqual(1, TreasureHuntRules.TierOfStage(1));
            Assert.AreEqual(1, TreasureHuntRules.TierOfStage(3));
            Assert.AreEqual(2, TreasureHuntRules.TierOfStage(4));
            Assert.AreEqual(8, TreasureHuntRules.TierOfStage(22));
            Assert.AreEqual(8, TreasureHuntRules.TierOfStage(24));
            Assert.AreEqual(8, TreasureHuntRules.TierOfStage(25));
            Assert.AreEqual(8, TreasureHuntRules.TierOfStage(500));
        }

        [Test]
        public void 곡괭이_비용은_단계에_따라_1_2_3_4다()
        {
            Assert.AreEqual(1, TreasureHuntRules.DigCost(1));
            Assert.AreEqual(1, TreasureHuntRules.DigCost(6));
            Assert.AreEqual(2, TreasureHuntRules.DigCost(7));
            Assert.AreEqual(2, TreasureHuntRules.DigCost(12));
            Assert.AreEqual(3, TreasureHuntRules.DigCost(13));
            Assert.AreEqual(3, TreasureHuntRules.DigCost(18));
            Assert.AreEqual(4, TreasureHuntRules.DigCost(19));
            Assert.AreEqual(4, TreasureHuntRules.DigCost(100));
        }

        [Test]
        public void 누적_기대_곡괭이는_12스테이지에_약_373개_24스테이지에_약_2007개다()
        {
            Assert.AreEqual(3729L, TreasureHuntRules.CumulativePicksTenths(12));
            Assert.AreEqual(20067L, TreasureHuntRules.CumulativePicksTenths(24));
        }

        [Test]
        public void 무료_600개는_14스테이지_최대_구매_1350개는_20스테이지_근처다()
        {
            int free = 0;
            while (TreasureHuntRules.CumulativePicksTenths(free + 1) <= 6000)
                free++;

            int bought = 0;
            while (TreasureHuntRules.CumulativePicksTenths(bought + 1) <= 13500)
                bought++;

            Assert.That(free, Is.InRange(13, 15));
            Assert.That(bought, Is.InRange(19, 21));
        }

        [Test]
        public void 열쇠는_3스테이지마다_상자는_12스테이지마다다()
        {
            Assert.IsTrue(TreasureHuntRules.GrantsKey(3));
            Assert.IsTrue(TreasureHuntRules.GrantsKey(12));
            Assert.IsFalse(TreasureHuntRules.GrantsKey(4));
            Assert.IsFalse(TreasureHuntRules.GrantsKey(0));

            Assert.IsTrue(TreasureHuntRules.OpensChest(12));
            Assert.IsTrue(TreasureHuntRules.OpensChest(24));
            Assert.IsFalse(TreasureHuntRules.OpensChest(9));
            Assert.AreEqual(2, TreasureHuntRules.ChestIndexAt(24));
            Assert.AreEqual(0, TreasureHuntRules.ChestIndexAt(13));
        }

        [Test]
        public void 첫_스테이지_보상은_2만_데시원_근처의_핀볼과_골드다()
        {
            EventBundle reward = TreasureHuntRules.StageReward(1);

            Assert.That(reward.ValueDeci, Is.InRange(20510L, 20522L));
            Assert.Greater(reward.PinballShots, 0);
            Assert.Greater(reward.Gold, 0);
            Assert.AreEqual(0, reward.NormalGemTickets);
        }

        [Test]
        public void 스테이지_보상에는_카피와_시너지가_없다()
        {
            for (int stage = 1; stage <= 40; stage++)
            {
                EventBundle reward = TreasureHuntRules.StageReward(stage);

                Assert.AreEqual(0, reward.BasicCopies, "stage " + stage);
                Assert.AreEqual(0, reward.RareKingCopies, "stage " + stage);
                Assert.AreEqual(0, reward.SynergyTickets, "stage " + stage);
                Assert.AreEqual(0, reward.PremiumGemTickets, "stage " + stage);
            }
        }

        [Test]
        public void 상자에는_카피와_최고급_뽑기권이_들어_있다()
        {
            EventBundle chest = TreasureHuntRules.ChestReward(1);

            Assert.Greater(chest.PremiumGemTickets, 0);
            Assert.Greater(chest.BasicCopies, 0);
            Assert.Greater(chest.RareKingCopies, 0);
        }

        [Test]
        public void 상자는_뒤로_갈수록_커진다()
        {
            Assert.Greater(TreasureHuntRules.ChestReward(2).ValueDeci, TreasureHuntRules.ChestReward(1).ValueDeci);
            Assert.GreaterOrEqual(TreasureHuntRules.ChestReward(3).ValueDeci, TreasureHuntRules.ChestReward(2).ValueDeci);
        }

        /// <summary>12스테이지 구간의 스테이지 보상 합 + 상자 = 기대 곡괭이 소모 × 200원 × 107%.</summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void 구간_보상_합은_기대_소모의_107퍼센트다(int chest)
        {
            int first = (chest - 1) * TreasureHuntRules.StagesPerChest + 1;
            long picksTenths = 0;
            long total = TreasureHuntRules.ChestReward(chest).ValueDeci;

            for (int s = first; s < first + TreasureHuntRules.StagesPerChest; s++)
            {
                picksTenths += TreasureHuntRules.ExpectedPicksTenths(s);
                total += TreasureHuntRules.StageReward(s).ValueDeci;
            }

            long expected = picksTenths * EventLedger.ReturnPerCurrency * 107 / (10L * 100);

            // 묶음마다 10데시원 미만을 버리고, 예산도 정수로 내림한다.
            Assert.That(total, Is.EqualTo(expected).Within(300L), "chest " + chest);
        }
    }
}
