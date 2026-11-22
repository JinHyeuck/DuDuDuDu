using System;
using NUnit.Framework;
using OJ.EventMinigame;

namespace OJ.Game.Tests
{
    /// <summary>래프팅 규칙: 룰렛 칸, 무료 칸 연쇄, 평균 거리.</summary>
    public sealed class RaftingRulesTests
    {
        private static Func<float> Rolls(params float[] values)
        {
            int index = 0;
            return () => values[Math.Min(index++, values.Length - 1)];
        }

        [Test]
        public void 칸_가중치의_합은_100이다()
        {
            int sum = 0;
            foreach (int w in RaftingRules.CellWeight)
                sum += w;

            Assert.AreEqual(100, sum);
            Assert.AreEqual(RaftingRules.CellCount, RaftingRules.CellWeight.Length);
            Assert.AreEqual(RaftingRules.CellCount, RaftingRules.CellDistance.Length);
        }

        [Test]
        public void 룰렛은_구간대로_칸을_뽑는다()
        {
            Assert.AreEqual(0, RaftingRules.Spin(0f));
            Assert.AreEqual(0, RaftingRules.Spin(0.01f));
            Assert.AreEqual(3, RaftingRules.Spin(0.4f));
            Assert.AreEqual(4, RaftingRules.Spin(0.5f));
            Assert.AreEqual(RaftingRules.FreeCell, RaftingRules.Spin(0.7f));
            Assert.AreEqual(7, RaftingRules.Spin(0.99f));
        }

        [Test]
        public void 룰렛은_범위_밖_난수를_가둔다()
        {
            Assert.AreEqual(7, RaftingRules.Spin(1f));
            Assert.AreEqual(7, RaftingRules.Spin(5f));
            Assert.AreEqual(0, RaftingRules.Spin(-1f));
        }

        [Test]
        public void 무료_칸은_거리를_주고_보트_없이_같은_배율로_이어_돈다()
        {
            int spins;
            long distance = RaftingRules.SpinChain(10, Rolls(0.7f, 0.7f, 0.99f), out spins);

            // 무료 5m + 무료 5m + 20m, 배율 10
            Assert.AreEqual(300L, distance);
            Assert.AreEqual(3, spins);
        }

        [Test]
        public void 무료가_아닌_칸이면_한_번에_끝난다()
        {
            int spins;
            long distance = RaftingRules.SpinChain(1, Rolls(0.5f), out spins);

            Assert.AreEqual(5L, distance);
            Assert.AreEqual(1, spins);
        }

        [Test]
        public void 무료_칸이_계속_나와도_상한에서_끊는다()
        {
            int spins;
            long distance = RaftingRules.SpinChain(1, () => 0.7f, out spins);

            Assert.AreEqual(RaftingRules.MaxChain, spins);
            Assert.AreEqual(5L * RaftingRules.MaxChain, distance);
        }

        [Test]
        public void 배율이_없거나_난수원이_없으면_돌지_않는다()
        {
            int spins;

            Assert.AreEqual(0L, RaftingRules.SpinChain(0, () => 0.5f, out spins));
            Assert.AreEqual(0, spins);
            Assert.AreEqual(0L, RaftingRules.SpinChain(10, null, out spins));
            Assert.AreEqual(0, spins);
        }

        /// <summary>무료 칸 재귀를 풀면 보트 1개당 평균 거리는 97/15 m 다.</summary>
        [Test]
        public void 보트_1개당_평균_거리는_97_15다()
        {
            double nonFree = 0;
            int nonFreeWeight = 0;
            for (int i = 0; i < RaftingRules.CellCount; i++)
            {
                if (RaftingRules.IsFreeCell(i))
                    continue;

                nonFree += RaftingRules.CellWeight[i] * RaftingRules.CellDistance[i];
                nonFreeWeight += RaftingRules.CellWeight[i];
            }

            double freeGain = RaftingRules.CellWeight[RaftingRules.FreeCell] * RaftingRules.CellDistance[RaftingRules.FreeCell];
            double expected = (nonFree + freeGain) / nonFreeWeight;

            Assert.That(expected, Is.EqualTo((double)RaftingRules.MetersPerBoatNumerator / RaftingRules.MetersPerBoatDenominator).Within(1e-9));
        }

        [Test]
        public void 초과_거리는_97분의_5씩_핀볼로_바뀐다()
        {
            Assert.AreEqual(0L, RaftingRules.OverflowShots(0));
            Assert.AreEqual(0L, RaftingRules.OverflowShots(-10));
            Assert.AreEqual(0L, RaftingRules.OverflowShots(19));
            Assert.AreEqual(1L, RaftingRules.OverflowShots(20));
            Assert.AreEqual(5L, RaftingRules.OverflowShots(97));
        }
    }
}
