using NUnit.Framework;
using OJ.Stage;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 고기 루프의 회수율 <b>r</b> 을 잠근다. (<c>Docs/CurrencyPolicy.md</c> 4.3)
    ///
    /// <b>이 파일이 유일한 방어선이다.</b> 소탕에 일일 상한을 두지 않기로 했으므로
    /// (정책 4.2 — 상방이 닫혀 있지 않다는 느낌이 핀볼의 동력이라서) r 이 새는 것을
    /// 막는 장치가 이것 하나뿐이다.
    ///
    /// <b>r 은 코드 한 곳에 적혀 있지 않다.</b> 소탕 비용 · 클리어당 티켓 · 티켓당 발수 ·
    /// 발당 고기의 곱인데, 넷이 서로 다른 파일에 있고 서로 다른 이유로 만져진다.
    /// 그래서 넷 중 <b>무엇을 고쳐도</b> 이 테스트가 반응한다.
    ///
    /// 터졌다면 "테스트가 낡았다"가 아니라 <b>경제 승수가 바뀌었다</b>는 뜻이다.
    /// 1/(1-r) 은 위로 갈수록 급격해서, r 0.3 → 0.6 은 승수 1.43 → 2.5 다.
    /// </summary>
    public sealed class SweepEconomyTests
    {
        [Test]
        public void 회수율이_상한을_넘지_않는다()
        {
            float r = SweepEconomy.RecoveryRate();

            Assert.LessOrEqual(
                r, SweepEconomy.MaxRecoveryRate,
                $"루프 회수율이 {r:0.###} 로 상한 {SweepEconomy.MaxRecoveryRate} 를 넘었다. " +
                $"경제 승수가 x{SweepEconomy.Multiplier(r):0.##} 가 된다. " +
                "소탕 비용 / 클리어당 티켓 / 핀볼 칸 고기 중 무엇을 고쳤는지 확인할 것.");
        }

        /// <summary>
        /// 목표는 0.3 이다. 위뿐 아니라 <b>아래로도</b> 본다 — r 이 너무 낮으면
        /// "핀볼이 소탕의 연료"라는 인과(정책 4.2)가 유저에게 안 읽힌다.
        /// </summary>
        [Test]
        public void 회수율이_목표_0_3_근처다()
        {
            float r = SweepEconomy.RecoveryRate();
            Assert.That(r, Is.EqualTo(0.30f).Within(0.05f), $"실제 r = {r:0.###}");
        }

        [Test]
        public void 발산하지_않는다()
        {
            float r = SweepEconomy.RecoveryRate();

            Assert.Less(r, 1f, "r 이 1 이상이면 고기가 무한히 늘어난다.");
            Assert.Greater(SweepEconomy.Multiplier(r), 1f);
            Assert.Less(SweepEconomy.Multiplier(r), 2f, "승수가 2 를 넘으면 설계 의도(x1.43)를 벗어난다.");
        }

        /// <summary>
        /// 승수 표. 1/(1-r) 이 위로 갈수록 급격하다는 것을 숫자로 남긴다 —
        /// "조금 올리는 것"이 왜 위험한지가 여기서 보인다.
        /// </summary>
        [TestCase(0.3f, 1.43f)]
        [TestCase(0.5f, 2.00f)]
        [TestCase(0.7f, 3.33f)]
        [TestCase(0.9f, 10.0f)]
        public void 승수는_1_나누기_1빼기r_이다(float r, float expected)
        {
            Assert.That(SweepEconomy.Multiplier(r), Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void 회수율이_1이상이면_승수가_0으로_막힌다()
        {
            Assert.AreEqual(0f, SweepEconomy.Multiplier(1f));
            Assert.AreEqual(0f, SweepEconomy.Multiplier(1.5f));
        }

        /// <summary>
        /// 하루 발수 = (방치 24 + 소탕 72) x 클리어당 티켓 / 발당 티켓.
        /// 티켓 수급처가 클리어 하나뿐이라(StageRewardCalculator.PinballTicketPerClear)
        /// 이 식이 곧 핀볼 시행 횟수다.
        /// </summary>
        [Test]
        public void 하루_발수는_클리어_수에서_나온다()
        {
            Assert.AreEqual(96, SweepEconomy.DailyShots(24, 72, 1, 1));
            Assert.AreEqual(192, SweepEconomy.DailyShots(24, 72, 2, 1), "티켓을 2장씩 주면 두 배다.");
            Assert.AreEqual(48, SweepEconomy.DailyShots(24, 72, 1, 2), "발당 티켓이 2장이면 절반이다.");
        }

        [Test]
        public void 하루_고기_소모는_소탕_횟수에서_나온다()
        {
            Assert.AreEqual(360, SweepEconomy.DailyStaminaSpent(72, 5));
            Assert.AreEqual(
                SweepEconomy.DailySweepClears * SweepRules.StaminaCostPerSweep,
                SweepEconomy.DailyStaminaSpent(SweepEconomy.DailySweepClears, SweepRules.StaminaCostPerSweep));
        }

        /// <summary>
        /// 발당 고기는 칸 확률의 가중 평균이다. 바닥값이 0 이 아니어야 한다 —
        /// 잭팟이 안 터져도 조금씩 쌓여야 한다는 정책 5.1 이다.
        /// </summary>
        [Test]
        public void 발당_고기는_칸_확률의_가중평균이다()
        {
            float perShot = SweepEconomy.StaminaPerShot();
            Assert.That(perShot, Is.EqualTo(1.24f).Within(0.001f));

            foreach (int stamina in SweepEconomy.DeclaredSlotStamina)
                Assert.Greater(stamina, 0, "바닥값을 0 으로 두면 꽝 칸이 생긴다 (정책 5.1).");
        }

        [Test]
        public void 칸_확률의_합은_1이다()
        {
            float sum = 0f;
            foreach (float p in SweepEconomy.DeclaredSlotProbability)
                sum += p;

            Assert.That(sum, Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>
        /// 잭팟 칸은 <b>확률이 가장 낮고 고기가 가장 많아야</b> 한다.
        ///
        /// 이 둘이 뒤집히면 정책 5.1 의 "평소 조금 + 가끔 왕창"이 "자주 왕창"이 되고,
        /// 회수율이 바로 상한을 넘는다 — 잭팟 25개를 확률 0.2475 칸에 두면
        /// 기댓값이 1.24 에서 6.9 로 뛰어 r 이 1.8 이 된다(발산).
        /// </summary>
        [Test]
        public void 잭팟_칸은_가장_드물고_가장_크다()
        {
            int jp = SweepEconomy.JackpotSlotIndex;
            float[] probability = SweepEconomy.DeclaredSlotProbability;
            int[] stamina = SweepEconomy.DeclaredSlotStamina;

            for (int i = 0; i < probability.Length; i++)
            {
                if (i == jp)
                    continue;

                Assert.Less(probability[jp], probability[i], $"{i}번 칸보다 확률이 높다.");
                Assert.Greater(stamina[jp], stamina[i], $"{i}번 칸보다 고기가 적다.");
            }
        }

        /// <summary>
        /// 잭팟 한 방이 <b>소탕 몇 회분</b>인지 잠근다. 이것이 체감의 크기다 —
        /// 소탕 1회가 고기 5 이므로 25개면 5회분이고, 하루 96발에 1% 면 약 하루 한 번이다.
        /// </summary>
        [Test]
        public void 잭팟_한_방이_소탕_5회분이다()
        {
            int jackpotStamina = SweepEconomy.DeclaredSlotStamina[SweepEconomy.JackpotSlotIndex];
            Assert.AreEqual(5, jackpotStamina / SweepRules.StaminaCostPerSweep);
        }

        [Test]
        public void 칸_수와_고기표_길이가_같다()
        {
            Assert.AreEqual(
                SweepEconomy.DeclaredSlotProbability.Length,
                SweepEconomy.DeclaredSlotStamina.Length,
                "판이 바뀌면 고기표도 같이 늘려야 한다.");
        }

        // ── 상위 재화 페이스 ─────────────────────────────────────────

        /// <summary>
        /// 상위 재화 4종의 일일 페이스를 잠근다.
        ///
        /// <b>이것이 게임 전체의 성장 속도다.</b> 티어 4~6 재화의 반복 수급처가
        /// Pinball_Bonus 하나뿐이라(정책 3.2), 보너스 라운드 지급량 하나가 곧
        /// 무료젬·레어석·신화석·유물권의 하루치 전부다.
        ///
        /// 근거는 각각 이렇다.
        /// <list type="bullet">
        /// <item>무료젬 300 — 보석뽑기 1회 값(상점 8.2)</item>
        /// <item>레어석 150 — 레어 다이스 1종 +3렙 (lv20 부근 50/렙)</item>
        /// <item>신화석 80 — 킹 다이스 1종 +1렙 (lv20 부근 75/렙)</item>
        /// </list>
        /// </summary>
        [Test]
        public void 상위_재화_일일_페이스가_고정돼_있다()
        {
            Assert.AreEqual(300, SweepEconomy.DailyFromBonus(SweepEconomy.BonusFreeGemPerRound), "무료젬");
            Assert.AreEqual(150, SweepEconomy.DailyFromBonus(SweepEconomy.BonusRareStonePerRound), "레어석");
            Assert.AreEqual(80, SweepEconomy.DailyFromBonus(SweepEconomy.BonusMythicStonePerRound), "신화석");
            Assert.AreEqual(4, SweepEconomy.DailyFromBonus(SweepEconomy.BonusRelicTicketPerRound), "유물권");
        }

        /// <summary>
        /// 무료젬 하루치가 보석뽑기 1회(300)를 넘지 않는다.
        ///
        /// 무료젬은 1:1 로 유료젬과 같은 값이라(정책 6.1) 하루 300은 현금 3,000원어치다.
        /// 여기가 넘어가면 패키지 가격 앵커(10원 = 1젬)가 흔들린다.
        /// </summary>
        [Test]
        public void 무료젬_하루치가_뽑기_한_번을_넘지_않는다()
        {
            const int GemBoxSingleCost = 300;
            Assert.LessOrEqual(SweepEconomy.DailyFromBonus(SweepEconomy.BonusFreeGemPerRound), GemBoxSingleCost);
        }

        [Test]
        public void 보너스_라운드_가정값이_1회_이상이다()
        {
            Assert.GreaterOrEqual(
                SweepEconomy.AssumedBonusRoundsPerDay, 1,
                "0 이면 상위 재화의 반복 수급이 통째로 멈춘다.");
        }
    }
}
