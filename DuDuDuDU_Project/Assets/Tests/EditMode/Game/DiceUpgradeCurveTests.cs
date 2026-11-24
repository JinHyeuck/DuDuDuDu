using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.Dice;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 다이스 레벨업 비용의 <b>값과 모양</b>을 잠근다. 정본은 <see cref="DiceCopyCurve"/> 다.
    ///
    /// 예전에는 에셋의 골드·스크롤 일차식 네 값을 이 파일에 선언해 모양만 검사했다. 지금은 표가
    /// 코드에 있으므로 누적값을 <b>숫자로</b> 못 박는다 — 가치 장부(카피 3,500원, Lv30 ≈ 93.5만 원)와
    /// 연동된 값이라 표가 움직이면 장부도 같이 움직여야 하기 때문이다.
    /// </summary>
    public sealed class DiceUpgradeCurveTests
    {
        [Test]
        public void 상한은_30이고_곡선이_거기까지_닿는다()
        {
            Assert.AreEqual(30, Define.MaxDiceLevel);

            for (int target = 2; target <= Define.MaxDiceLevel; target++)
                Assert.Greater(DiceCopyCurve.CopiesToReach(target), 0, $"Lv{target} 로 가는 비용이 없다.");
        }

        /// <summary>장부의 기준 지점. 레벨별 누적 카피 12 / 47 / 87 / 147 / 207 / 267.</summary>
        [TestCase(1, 0)]
        [TestCase(5, 12)]
        [TestCase(10, 47)]
        [TestCase(15, 87)]
        [TestCase(20, 147)]
        [TestCase(25, 207)]
        [TestCase(30, 267)]
        public void 누적_카피가_장부와_같다(int level, int expected)
        {
            Assert.AreEqual(expected, DiceCopyCurve.CumulativeCopies(level));
        }

        [Test]
        public void 초반은_일_이_사_오다()
        {
            Assert.AreEqual(1, DiceCopyCurve.CopiesToReach(2));
            Assert.AreEqual(2, DiceCopyCurve.CopiesToReach(3));
            Assert.AreEqual(4, DiceCopyCurve.CopiesToReach(4));
            Assert.AreEqual(5, DiceCopyCurve.CopiesToReach(5));
        }

        [Test]
        public void 구간별_한_레벨_장수는_7_8_12다()
        {
            Assert.AreEqual(7, DiceCopyCurve.CopiesToReach(6));
            Assert.AreEqual(7, DiceCopyCurve.CopiesToReach(10));
            Assert.AreEqual(8, DiceCopyCurve.CopiesToReach(11));
            Assert.AreEqual(8, DiceCopyCurve.CopiesToReach(15));
            Assert.AreEqual(12, DiceCopyCurve.CopiesToReach(16));
            Assert.AreEqual(12, DiceCopyCurve.CopiesToReach(30));
        }

        /// <summary>Lv31 이상은 15장에서 시작해 10레벨 구간마다 3장씩 늘어난다(상한 밖, 확장용).</summary>
        [Test]
        public void Lv31_이상은_열다섯에서_시작해_열_레벨마다_세_장_는다()
        {
            Assert.AreEqual(15, DiceCopyCurve.CopiesToReach(31));
            Assert.AreEqual(15, DiceCopyCurve.CopiesToReach(40));
            Assert.AreEqual(18, DiceCopyCurve.CopiesToReach(41));
            Assert.AreEqual(21, DiceCopyCurve.CopiesToReach(51));
        }

        /// <summary>한 레벨 비용은 줄어들지 않는다. 뒤 레벨이 앞 레벨보다 싸면 표의 실수다.</summary>
        [Test]
        public void 비용은_레벨이_오를수록_줄지_않는다()
        {
            int previous = 0;
            for (int target = 2; target <= 60; target++)
            {
                int cost = DiceCopyCurve.CopiesToReach(target);
                Assert.GreaterOrEqual(cost, previous, $"Lv{target} 가 Lv{target - 1} 보다 싸다.");
                previous = cost;
            }
        }

        /// <summary>마지막 한 레벨이 첫 레벨보다 다섯 배 넘게 비싸다 — 후반이 무거워야 한다.</summary>
        [Test]
        public void 마지막_한_레벨이_첫_레벨보다_다섯_배_넘게_비싸다()
        {
            int first = DiceCopyCurve.CopiesToReach(2);
            int last = DiceCopyCurve.CopiesToReach(Define.MaxDiceLevel);

            Assert.Greater(last, first * 5, $"{first} → {last} 밖에 안 오른다.");
        }

        /// <summary>
        /// 레어·킹 카피 3,500원 기준으로 Lv30 한 종이 93.5만 원 근처다 — 장부의 기준점.
        /// 한 장의 값이 바뀌면 이 숫자를 보고 곡선을 같이 손봐야 한다.
        /// </summary>
        [Test]
        public void 레어_킹_Lv30_한_종은_93만_5천_원_근처다()
        {
            int won = DiceCopyCurve.CumulativeCopies(Define.MaxDiceLevel) * 3500;

            Assert.That(won, Is.EqualTo(934500));
        }

        /// <summary>골드는 들지 않는다. 호출부 여덟 곳이 첫 칸(골드)을 그대로 읽으므로 항상 0 이어야 한다.</summary>
        [Test]
        public void 레벨업에_골드는_들지_않는다()
        {
            foreach (DiceType type in System.Enum.GetValues(typeof(DiceType)))
            {
                if (type == DiceType.Max)
                    continue;

                for (int level = 1; level < Define.MaxDiceLevel; level++)
                    Assert.AreEqual(0, DiceMetaDataProvider.GetUpgradeCost(type, level).goldCost, $"{type} Lv{level}");
            }
        }

        [Test]
        public void CopiesToLevelUp은_다음_레벨의_비용이다()
        {
            Assert.AreEqual(1, DiceCopyCurve.CopiesToLevelUp(1));
            Assert.AreEqual(5, DiceCopyCurve.CopiesToLevelUp(4));
            Assert.AreEqual(12, DiceCopyCurve.CopiesToLevelUp(29));
        }
    }
}
