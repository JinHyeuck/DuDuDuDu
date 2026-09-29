using NUnit.Framework;
using OJ;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 다이스 강화 비용의 <b>모양</b>을 잠근다.
    ///
    /// <b>같은 등급은 같은 값이어야 한다.</b> 예전에는 기본 5종이 8~11장, 킹 5종이 250~280 골드로
    /// 조금씩 달랐다. 같은 등급끼리 값이 갈리면 유저가 이유 없이 싼 쪽만 키우고, 그건 빌드
    /// 선택이 아니라 표의 실수다.
    ///
    /// <b>리듬은 3 / 6 / 12 다.</b> 3까지는 쉽게, 6까지는 어느 정도, 12까지는 오래.
    /// 비용이 레벨마다 일정하게 늘어나므로 누적은 제곱으로 붙고, 그래서 이 리듬이 저절로 나온다 —
    /// 비용을 고정값으로 바꾸면(= per-level 0) 세 구간이 똑같이 밋밋해진다.
    ///
    /// <b>수치의 정본은 <c>DiceMetaDataDatabase.asset</c> 이다.</b> 헤드리스에서 SO 가 안 뜨므로
    /// 여기 선언값을 두고 모양만 검사한다. 에셋을 고치면 여기도 같이 고칠 것.
    /// </summary>
    public sealed class DiceUpgradeCurveTests
    {
        // (baseGold, goldPerLevel, baseScroll, scrollPerLevel) — 등급별 선언값
        private static readonly (string Name, int BaseGold, int GoldPer, int BaseScroll, int ScrollPer)[] Tiers =
        {
            ("기본", 130, 55, 10, 20),
            ("레어", 150, 62, 3, 5),
            ("킹", 265, 90, 4, 6),
        };

        /// <summary>1 → L 누적. 한 레벨 비용은 <c>base + per x (L-1)</c> 이다.</summary>
        private static int Cumulative(int baseCost, int per, int level)
        {
            int steps = level - 1;
            return steps <= 0 ? 0 : baseCost * steps + per * (steps * (steps - 1) / 2);
        }

        [Test]
        public void 상한은_12다()
        {
            Assert.AreEqual(12, Define.MaxDiceLevel);
        }

        /// <summary>
        /// lv3 까지가 전체의 10% 를 넘지 않는다 — "3까지는 쉽게".
        /// 하루 공급의 절반이면 닿는 거리여야 한다.
        /// </summary>
        [Test]
        public void 삼레벨까지는_전체의_일할_안이다()
        {
            foreach (var t in Tiers)
            {
                double ratio = Cumulative(t.BaseScroll, t.ScrollPer, 3)
                             / (double)Cumulative(t.BaseScroll, t.ScrollPer, Define.MaxDiceLevel);

                Assert.Less(ratio, 0.10, $"{t.Name}: lv3 까지가 {ratio:P1} 다. 초반이 무겁다.");
            }
        }

        /// <summary>
        /// lv6 까지가 전체의 15~35% — "6까지는 어느 정도".
        /// 너무 낮으면 중반이 비어 보이고, 너무 높으면 후반에 남는 것이 없다.
        /// </summary>
        [Test]
        public void 육레벨까지는_전체의_이삼할이다()
        {
            foreach (var t in Tiers)
            {
                double ratio = Cumulative(t.BaseScroll, t.ScrollPer, 6)
                             / (double)Cumulative(t.BaseScroll, t.ScrollPer, Define.MaxDiceLevel);

                Assert.That(ratio, Is.InRange(0.15, 0.35), $"{t.Name}: lv6 까지가 {ratio:P1} 다.");
            }
        }

        /// <summary>
        /// 후반 한 레벨이 초반 한 레벨보다 <b>훨씬</b> 비싸야 한다.
        /// 5배가 안 되면 "12까지 오래"가 레벨 수로만 만들어진 것이고, 그건 지루함이지 무게가 아니다.
        /// </summary>
        [Test]
        public void 마지막_한_레벨이_첫_레벨보다_다섯_배_넘게_비싸다()
        {
            foreach (var t in Tiers)
            {
                int first = t.BaseScroll;
                int last = t.BaseScroll + t.ScrollPer * (Define.MaxDiceLevel - 2);

                Assert.Greater(last, first * 5, $"{t.Name}: {first} → {last} 밖에 안 오른다.");
            }
        }

        /// <summary>등급이 올라갈수록 한 레벨이 비싸야 한다. 골드로 본다(세 등급이 같은 재화를 쓴다).</summary>
        [Test]
        public void 등급이_올라갈수록_골드가_비싸다()
        {
            for (int i = 1; i < Tiers.Length; i++)
            {
                int prev = Cumulative(Tiers[i - 1].BaseGold, Tiers[i - 1].GoldPer, Define.MaxDiceLevel);
                int cur = Cumulative(Tiers[i].BaseGold, Tiers[i].GoldPer, Define.MaxDiceLevel);

                Assert.Greater(cur, prev, $"{Tiers[i].Name} 가 {Tiers[i - 1].Name} 보다 싸다.");
            }
        }

        /// <summary>
        /// 만렙까지의 골드 총액. 다이스 15종 + 장비 6종이 <b>같은 골드를 나눠 쓴다</b> —
        /// 이 값이 커지면 하루 공급(6,012)으로 나눈 만렙 소요가 그대로 늘어난다.
        /// </summary>
        [Test]
        public void 다이스_전체_만렙_골드가_87000_근처다()
        {
            int total = 0;
            foreach (var t in Tiers)
                total += 5 * Cumulative(t.BaseGold, t.GoldPer, Define.MaxDiceLevel);

            Assert.That(total, Is.EqualTo(86900).Within(100), $"실제 {total:N0}");
        }
    }
}
