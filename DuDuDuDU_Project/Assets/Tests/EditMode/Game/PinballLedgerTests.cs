using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OJ.Pinball;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 핀볼 보상표가 <b>가치 장부(2026-11)</b> 의 5,000발 값에서 크게 벗어나지 않는지 잠근다.
    ///
    /// 입력은 코드의 상수(칸별 양, 중간 핀 필요 적중 수, 기본 보상 풀)이고, 공 1발당 특수 핀 적중 횟수만 시뮬로 잰
    /// 값을 여기 적는다(<c>PinballRewardDatabase</c> 주석의 측정치 — 판이나 칸 확률을 바꾸면 다시 잴 것).
    /// </summary>
    public sealed class PinballLedgerTests
    {
        // 칸 확률: 판의 declaredProbability.
        private static readonly double[] SlotProb = { 0.2475, 0.2475, 0.01, 0.2475, 0.2475 };

        // 기본판(PSD 배치 + 아치 판 셋)의 공 1발당 특수 핀 적중 횟수. 시드표 칸별 풀 × 표기 확률로 20만 표본을 쟀다 —
        // 실제 게임이 공을 뽑는 방식 그대로다(날것 궤적이 아니라 시드표의 재미있는 궤적).
        private const double Tag1HitsPerBall = 0.370, Tag2HitsPerBall = 0.369, Tag3HitsPerBall = 0.690;

        private const int Balls = 5000;

        private static double Expect(int[] perSlot)
        {
            double sum = 0;
            for (int i = 0; i < SlotProb.Length; i++)
                sum += SlotProb[i] * perSlot[i];
            return sum;
        }

        [Test]
        public void 칸_확률은_합이_1이다()
        {
            Assert.That(SlotProb.Sum(), Is.EqualTo(1.0).Within(1e-9));
        }

        /// <summary>골드는 보기 좋은 수(5·10·500·10·5)로 잡았다 — 발당 12.4, 5,000발에 약 6.2만 골드(0.1원/골드 → 약 6,200원).</summary>
        [Test]
        public void 아래칸_골드는_5천발에_6만2천_골드_근처다()
        {
            double perBall = Expect(PinballRewardDatabase.GoldPerSlot);

            Assert.That(perBall * Balls, Is.EqualTo(62125).Within(200));
        }

        /// <summary>시너지 티켓은 발당 0.12 — 5,000발에 약 600장(50원 → 3만 원). 확률 지급이라 기댓값은 양 × 확률이다.</summary>
        [Test]
        public void 아래칸_시너지_티켓은_5천발에_600장_근처다()
        {
            double perBall = Expect(PinballRewardDatabase.SynergyTicketsPerSlot)
                             * PinballRewardDatabase.SynergyTicketChancePercent / 100.0;

            Assert.That(perBall * Balls, Is.EqualTo(600).Within(10));
        }

        /// <summary>시너지 티켓은 가운데 칸에 걸지 않고, 가장자리 칸에는 <b>확률</b>로만 준다 — 확정 1장이면 발당 0.2475 로 장부의 두 배가 된다.</summary>
        [Test]
        public void 시너지_티켓은_가장자리_칸에_확률로_준다()
        {
            int[] amounts = PinballRewardDatabase.SynergyTicketsPerSlot;

            Assert.AreEqual(0, amounts[2]);
            Assert.Greater(amounts[1] + amounts[3], 0);
            Assert.Less(PinballRewardDatabase.SynergyTicketChancePercent, 100);
        }

        /// <summary>카피핀: 5,000발에 완주 약 168번, 완주마다 카피 1장(레어·킹 17% / 기본 83%) → 레어·킹 28.6장 + 기본 139.6장(장부 27.5 / 140).</summary>
        [Test]
        public void 카피핀은_장부의_카피_수에_맞는다()
        {
            double completions = Tag1HitsPerBall * Balls / PinballRewardDatabase.CopyPinRequiredHits;

            Assert.That(completions * PinballRewardDatabase.CopyPinRareKingPercent / 100.0, Is.EqualTo(27.5).Within(2.0), "레어·킹 카피");
            Assert.That(completions * PinballRewardDatabase.CopyPinBasicPercent / 100.0, Is.EqualTo(140).Within(10), "기본 카피");
        }

        [Test]
        public void 카피핀_확률은_합이_100이다()
        {
            Assert.AreEqual(100, PinballRewardDatabase.CopyPinRareKingPercent + PinballRewardDatabase.CopyPinBasicPercent);
        }

        // ── 확률 지급 규칙 ──────────────────────────────────────────

        [Test]
        public void 확률이_0_이하거나_100_이상이면_전부_성공이다()
        {
            Assert.AreEqual(7, PinballRules.CountHits(0, 7, () => 0.99f));
            Assert.AreEqual(7, PinballRules.CountHits(100, 7, () => 0.99f));
            Assert.AreEqual(0, PinballRules.CountHits(50, 0, () => 0f));
        }

        [Test]
        public void 확률_지급은_시도마다_따로_굴린다()
        {
            // 난수열 0.1, 0.9, 0.2, 0.8, 0.3 → 40% 미만은 0.1, 0.2, 0.3 세 번
            float[] rolls = { 0.1f, 0.9f, 0.2f, 0.8f, 0.3f };
            int i = 0;

            Assert.AreEqual(3, PinballRules.CountHits(40, 5, () => rolls[i++]));
        }

        [Test]
        public void 확률_지급의_비율은_확률을_따른다()
        {
            const int n = 100000;
            int i = 0;

            int hits = PinballRules.CountHits(24, 1000, () => ((i++ * 7919) % 1000 + 0.5f) / 1000f);
            Assert.That(hits, Is.EqualTo(240).Within(2));
            Assert.That(PinballRules.CountHits(24, n, () => 0f), Is.EqualTo(24000), "시도가 많으면 기댓값으로 줄인다");
        }

        [Test]
        public void 뽑기권핀은_5천발에_12장_근처다()
        {
            double completions = Tag2HitsPerBall * Balls / PinballRewardDatabase.GemTicketPinRequiredHits;

            Assert.That(completions, Is.EqualTo(12).Within(0.5));
        }

        /// <summary>대박핀(상자): 5,000발에 10번 = 500발당 1회 입장.</summary>
        [Test]
        public void 대박핀은_500발에_한_번_보너스를_연다()
        {
            double entries = Tag3HitsPerBall * Balls / PinballRewardDatabase.BonusPinRequiredHits;

            Assert.That(entries, Is.EqualTo(10).Within(0.3));
        }

        // ── 보너스 보상 풀 ──────────────────────────────────────────

        [Test]
        public void 기본_풀의_가중치_합은_1000이다()
        {
            Assert.AreEqual(1000, BonusDiceDatabase.BuildDefaultPool().Sum(d => d.weight));
        }

        /// <summary>하루 4번 열리고 핀이 6개라 하루 24번 뽑는다 — 장부의 하루 개수와 맞는지.</summary>
        [Test]
        public void 기본_풀의_하루_기대_개수가_장부와_맞는다()
        {
            List<BonusRewardDraw> pool = BonusDiceDatabase.BuildDefaultPool();
            const double draws = 24.0;

            double Daily(PointType type) =>
                pool.Where(d => d.rewards.Any(r => r.pointType == type))
                    .Sum(d => draws * d.weight / 1000.0 * d.rewards.First(r => r.pointType == type).amount);

            Assert.That(Daily(PointType.RandomRareKingCopy), Is.EqualTo(4).Within(0.1), "레어·킹 카피");
            Assert.That(Daily(PointType.RandomBasicCopy), Is.EqualTo(4).Within(0.1), "기본 카피");
            Assert.That(Daily(PointType.NormalGemTicket), Is.EqualTo(2).Within(0.1), "일반 보석 뽑기권");
            Assert.That(Daily(PointType.SelectTicket), Is.EqualTo(1).Within(0.1), "선택권");
            Assert.That(Daily(PointType.RelicTicket), Is.EqualTo(1).Within(0.1), "유물권");
            Assert.That(Daily(PointType.PremiumGemTicket), Is.EqualTo(1).Within(0.1), "최고급 보석 뽑기권");
        }

        [Test]
        public void 풀의_모든_줄이_뭔가를_준다()
        {
            foreach (BonusRewardDraw draw in BonusDiceDatabase.BuildDefaultPool())
            {
                Assert.That(draw.rewards, Is.Not.Empty, draw.label);
                Assert.Greater(draw.weight, 0, draw.label);
            }
        }

        // ── 가중치 추첨 규칙 ────────────────────────────────────────

        [Test]
        public void 가중치_추첨은_누적_구간으로_고른다()
        {
            var weights = new[] { 10, 30, 60 };

            Assert.AreEqual(0, BonusDiceRules.PickWeightedIndex(weights, 0.00f));
            Assert.AreEqual(0, BonusDiceRules.PickWeightedIndex(weights, 0.099f));
            Assert.AreEqual(1, BonusDiceRules.PickWeightedIndex(weights, 0.10f));
            Assert.AreEqual(1, BonusDiceRules.PickWeightedIndex(weights, 0.399f));
            Assert.AreEqual(2, BonusDiceRules.PickWeightedIndex(weights, 0.40f));
            Assert.AreEqual(2, BonusDiceRules.PickWeightedIndex(weights, 0.999f));
        }

        [Test]
        public void 가중치_추첨은_범위를_벗어난_난수에도_죽지_않는다()
        {
            var weights = new[] { 1, 1 };

            Assert.AreEqual(1, BonusDiceRules.PickWeightedIndex(weights, 1.0f));
            Assert.AreEqual(1, BonusDiceRules.PickWeightedIndex(weights, 5f));
            Assert.AreEqual(0, BonusDiceRules.PickWeightedIndex(weights, -1f));
        }

        [Test]
        public void 가중치가_0_이하인_줄은_뽑히지_않는다()
        {
            var weights = new[] { 0, 5, -3, 5 };

            for (float roll = 0f; roll < 1f; roll += 0.05f)
            {
                int index = BonusDiceRules.PickWeightedIndex(weights, roll);
                Assert.That(index == 1 || index == 3, $"roll {roll} → {index}");
            }
        }

        [Test]
        public void 뽑을_표가_없으면_마이너스_일이다()
        {
            Assert.AreEqual(-1, BonusDiceRules.PickWeightedIndex(null, 0.5f));
            Assert.AreEqual(-1, BonusDiceRules.PickWeightedIndex(new int[0], 0.5f));
            Assert.AreEqual(-1, BonusDiceRules.PickWeightedIndex(new[] { 0, 0 }, 0.5f));
        }

        /// <summary>충분히 뽑으면 비율이 가중치에 수렴한다 — 결정적 난수열로 본다.</summary>
        [Test]
        public void 가중치_추첨의_비율은_가중치를_따른다()
        {
            var weights = new[] { 167, 167, 83, 42, 42, 42, 150, 307 };
            var counts = new int[weights.Length];
            const int n = 100000;

            for (int i = 0; i < n; i++)
                counts[BonusDiceRules.PickWeightedIndex(weights, (i + 0.5f) / n)]++;

            for (int i = 0; i < weights.Length; i++)
                Assert.That(counts[i] / (double)n, Is.EqualTo(weights[i] / 1000.0).Within(0.002), "줄 " + i);
        }
    }
}
