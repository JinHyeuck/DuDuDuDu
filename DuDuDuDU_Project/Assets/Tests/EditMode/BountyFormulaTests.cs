using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 현상금 산술. <b>골든 기준선에 얹지 않는다</b> — 기준선은 기존 동작을 박제하는
    /// 파일이고 이것은 새 규칙이라, 섞으면 "기준선이 바뀐 것"과 "새 값이 들어온 것"을
    /// 구별할 수 없게 된다.
    /// </summary>
    public sealed class BountyFormulaTests
    {
        // ── ReferenceWave ────────────────────────────────────────────

        [TestCase(15, 0.10f, 2)]
        [TestCase(15, 0.30f, 5)]
        [TestCase(15, 0.50f, 8)]
        [TestCase(15, 0.75f, 12)]
        [TestCase(15, 1.00f, 15)]
        [TestCase(10, 0.10f, 1)]
        [TestCase(10, 0.50f, 5)]
        [TestCase(20, 0.30f, 6)]
        public void ReferenceWave_ScalesWithStageLength(int totalWaves, float ratio, int expected)
        {
            Assert.AreEqual(expected, BountyFormula.ReferenceWave(totalWaves, ratio));
        }

        /// <summary>
        /// 비율이 아주 작아도 0 웨이브가 나오면 안 된다. 0 이 나오면
        /// <c>StageGrowthFormula.MonsterHp</c> 의 <c>Max(1, ...)</c> 가 그것을 삼켜
        /// 비율을 잘못 넣은 사실이 화면에서 사라진다.
        /// </summary>
        [Test]
        public void ReferenceWave_NeverZero()
        {
            Assert.AreEqual(1, BountyFormula.ReferenceWave(15, 0f));
            Assert.AreEqual(1, BountyFormula.ReferenceWave(15, 0.0001f));
            Assert.AreEqual(1, BountyFormula.ReferenceWave(0, 0.5f));
        }

        [Test]
        public void ReferenceWave_ClampsRatioAboveOne()
        {
            Assert.AreEqual(15, BountyFormula.ReferenceWave(15, 2f));
            Assert.AreEqual(1, BountyFormula.ReferenceWave(15, -1f));
        }

        // ── Hp ───────────────────────────────────────────────────────

        /// <summary>
        /// 1스테이지(baseMonsterHp 7 / lin 0.145 / quad 0.018 / 15웨이브)의 다섯 등급.
        /// <b>이 값이 곧 화면에 뜨는 숫자다</b> — 밸런스를 바꾸면 여기가 먼저 깨져야 한다.
        /// </summary>
        [TestCase(0.10f, 5f, 40)]
        [TestCase(0.30f, 6f, 78)]
        [TestCase(0.50f, 8f, 160)]
        [TestCase(0.75f, 10f, 330)]
        [TestCase(1.00f, 14f, 644)]
        public void Hp_Stage1(float ratio, float multiplier, int expected)
        {
            int wave = BountyFormula.ReferenceWave(15, ratio);
            int monsterHp = StageGrowthFormula.MonsterHp(7, 0.145f, 0.018f, wave);

            Assert.AreEqual(expected, BountyFormula.Hp(monsterHp, multiplier));
        }

        [Test]
        public void Hp_NeverBelowOne()
        {
            Assert.AreEqual(1, BountyFormula.Hp(1, 0.1f));
            Assert.AreEqual(1, BountyFormula.Hp(0, 5f));
        }

        // ── WallDamage ───────────────────────────────────────────────

        [TestCase(100, 33)]
        [TestCase(67, 22)]
        [TestCase(45, 15)]
        [TestCase(3, 1)]
        [TestCase(2, 1)]
        [TestCase(1, 0)]
        [TestCase(0, 0)]
        public void WallDamage_IsOneThirdOfCurrent(int currentHp, int expected)
        {
            Assert.AreEqual(expected, BountyFormula.WallDamage(currentHp));
        }

        /// <summary>
        /// <b>이 시스템의 핵심 성질이다.</b> 현상금은 벽을 몇 번 때려도 0 으로 만들지
        /// 못한다 — 곁가지 시스템이 판을 끝내면 안 되기 때문이다. 대가는 클리어 등급으로
        /// 치른다. 식을 "최대 체력의 1/3" 으로 바꾸면 이 테스트가 먼저 깨진다.
        /// </summary>
        [Test]
        public void WallDamage_NeverKillsTheWall()
        {
            int hp = 100;
            for (int i = 0; i < 200; i++)
            {
                hp -= BountyFormula.WallDamage(hp);
                Assert.Greater(hp, 0, i + "번째 타격에서 벽이 죽었다.");
            }
        }

        // ── 해금 ─────────────────────────────────────────────────────

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(4, 5)]
        [TestCase(5, 5)]
        public void HighestSelectableGrade_IsOneAboveDefeated(int defeated, int expected)
        {
            Assert.AreEqual(expected, BountyFormula.HighestSelectableGrade(defeated));
        }

        [Test]
        public void IsSelectable_NoneIsAlwaysAvailable()
        {
            Assert.IsTrue(BountyFormula.IsSelectable(BountyFormula.NoneGrade, 0));
            Assert.IsTrue(BountyFormula.IsSelectable(BountyFormula.NoneGrade, 5));
        }

        [Test]
        public void IsSelectable_LocksBeyondNextGrade()
        {
            Assert.IsTrue(BountyFormula.IsSelectable(1, 0));
            Assert.IsFalse(BountyFormula.IsSelectable(2, 0));

            Assert.IsTrue(BountyFormula.IsSelectable(2, 1));
            Assert.IsFalse(BountyFormula.IsSelectable(3, 1));
        }

        [Test]
        public void IsSelectable_RejectsOutOfRange()
        {
            Assert.IsFalse(BountyFormula.IsSelectable(-1, 5));
            Assert.IsFalse(BountyFormula.IsSelectable(6, 5));
        }

        // ── 웨이브 판정 ──────────────────────────────────────────────

        /// <summary>
        /// 보스 웨이브(마지막)에는 나오지 않는다. SP·강화석을 그때 받아 봐야 쓸 곳이 없다.
        /// </summary>
        [Test]
        public void CanSpawnOnWave_ExcludesBossWave()
        {
            Assert.IsTrue(BountyFormula.CanSpawnOnWave(1, 15));
            Assert.IsTrue(BountyFormula.CanSpawnOnWave(14, 15));
            Assert.IsFalse(BountyFormula.CanSpawnOnWave(15, 15));
            Assert.IsFalse(BountyFormula.CanSpawnOnWave(16, 15));
        }

        /// <summary>웨이브 0 은 아직 시작 전이다(<c>RunState.WaveIndex</c> 주석).</summary>
        [Test]
        public void CanSpawnOnWave_RejectsBeforeStart()
        {
            Assert.IsFalse(BountyFormula.CanSpawnOnWave(0, 15));
        }
    }

    public sealed class ShortNumberFormatTests
    {
        [TestCase(0, "0")]
        [TestCase(40, "40")]
        [TestCase(644, "644")]
        [TestCase(999, "999")]
        [TestCase(1000, "1.000K")]
        [TestCase(5550, "5.550K")]
        [TestCase(197400, "197.400K")]
        [TestCase(750000, "750.000K")]
        // 1000.000K 로 올라가지 않는다 — 999999/1000f 가 999.999 로 딱 떨어져
        // 소수 세 자리에서 반올림이 일어나지 않는다. M 으로 넘어가는 경계는 1000000 이다.
        [TestCase(999999, "999.999K")]
        [TestCase(1000000, "1.000M")]
        [TestCase(8085000, "8.085M")]
        [TestCase(231000000, "231.000M")]
        public void Format_MatchesScreenshotStyle(int value, string expected)
        {
            Assert.AreEqual(expected, ShortNumberFormat.Format(value));
        }

        [Test]
        public void Format_HandlesNegative()
        {
            Assert.AreEqual("-40", ShortNumberFormat.Format(-40));
            Assert.AreEqual("-5.550K", ShortNumberFormat.Format(-5550));
        }
    }
}
