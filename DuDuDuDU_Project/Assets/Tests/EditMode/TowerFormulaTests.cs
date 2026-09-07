using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 무한의 탑 산술. <b>골든 기준선에 얹지 않는다</b> — 기준선은 기존 동작을 박제하는
    /// 파일이고 이것은 새 규칙이라, 섞으면 "기준선이 바뀐 것"과 "새 값이 들어온 것"을
    /// 구별할 수 없게 된다. (<c>BountyFormulaTests</c> 와 같은 판단이다.)
    /// </summary>
    public sealed class TowerFormulaTests
    {
        // ── 층 ↔ 구간 ────────────────────────────────────────────────

        [TestCase(1, 1)]
        [TestCase(5, 1)]
        [TestCase(6, 2)]
        [TestCase(20, 4)]
        [TestCase(21, 5)]
        [TestCase(300, 60)]
        public void BandOf_FiveFloorsPerBand(int floor, int expectedBand)
        {
            Assert.AreEqual(expectedBand, TowerFormula.BandOf(floor));
        }

        [TestCase(1, 1)]
        [TestCase(5, 5)]
        [TestCase(6, 1)]
        [TestCase(300, 5)]
        public void FloorInBand_WrapsEveryFiveFloors(int floor, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.FloorInBand(floor));
        }

        [Test]
        public void BandCount_CoversAllFloors()
        {
            Assert.AreEqual(60, TowerFormula.BandCount);
            Assert.AreEqual(296, TowerFormula.BandStartFloor(TowerFormula.BandCount));
        }

        /// <summary>
        /// 저장된 진행도가 깨져도 배열 색인 사고로 번지지 않아야 한다.
        /// 여기서 막지 않으면 <c>TowerDatabase.GetPlan</c> 이 없는 구간을 찾는다.
        /// </summary>
        [TestCase(0, 1)]
        [TestCase(-5, 1)]
        [TestCase(301, 300)]
        [TestCase(99999, 300)]
        public void ClampFloor_KeepsFloorInRange(int floor, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.ClampFloor(floor));
        }

        // ── 구간 보상 층 ─────────────────────────────────────────────

        [TestCase(5, true)]
        [TestCase(10, true)]
        [TestCase(4, false)]
        [TestCase(1, false)]
        [TestCase(300, true)]
        public void IsBandLastFloor_MarksEveryFifthFloor(int floor, bool expected)
        {
            Assert.AreEqual(expected, TowerFormula.IsBandLastFloor(floor));
        }

        [TestCase(1, 4)]
        [TestCase(4, 1)]
        [TestCase(5, 0)]
        public void FloorsUntilBandReward_CountsDownWithinBand(int floor, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.FloorsUntilBandReward(floor));
        }

        // ── 성장 곡선 ────────────────────────────────────────────────
        //
        // 값 자체를 박제한다. <b>이 숫자가 곧 화면에 뜨는 체력이다</b> —
        // 곡선을 바꾸면 여기가 먼저 깨져야 하고, 깨지지 않고 바뀌면 그건 사고다.

        // <b>값을 손으로 계산해 적지 않았다.</b> AGENTS 의 부동소수 절 참고 —
        // Mono 는 float 식의 중간 결과를 더 높은 정밀도로 들고 가다 대입 시점에 접어서,
        // 재구현으로 뽑은 기대값이 실제와 1 씩 어긋난다. 아래 숫자는 전부
        // <b>Mono 러너가 실제로 내놓은 값</b>이다.
        [TestCase(1, 8)]
        [TestCase(5, 19)]
        [TestCase(10, 35)]
        [TestCase(20, 73)]
        [TestCase(50, 225)]
        [TestCase(100, 564)]
        [TestCase(300, 2619)]
        public void BaseMonsterHp_FollowsGrowthCurve(int floor, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.BaseMonsterHp(floor));
        }

        [Test]
        public void BaseMonsterHp_NeverDecreases()
        {
            // 곡선이 어디서도 꺾여 내려가면 "위층이 더 쉬운" 구간이 생긴다.
            // 지수를 만질 때 실수로 그렇게 되기 쉬워서 전 구간을 훑는다.
            int previous = 0;
            for (int floor = 1; floor <= TowerFormula.TotalFloors; floor++)
            {
                int hp = TowerFormula.BaseMonsterHp(floor);
                Assert.GreaterOrEqual(hp, previous, floor + "층에서 체력이 줄었다.");
                previous = hp;
            }
        }

        /// <summary>
        /// 방어력 곡선의 상한을 못 박는다. <b>이 수치가 곧 유효 체력의 배수다</b> —
        /// <c>IncomingDamageFormula.DefenseMultiplier</c> 가 100/(100+armor) 이므로
        /// 394 는 감쇄 0.20, 즉 본편 30스테이지 15웨이브 일반 몬스터와 같은 자리다.
        /// 여기를 올리면 고방어 구간에서 아머브레이크가 선택이 아니라 필수가 된다.
        ///
        /// 값은 Mono 러너가 실제로 내놓은 것이다(위 체력 곡선 주석 참조).
        /// </summary>
        [TestCase(1, 4)]
        [TestCase(20, 25)]
        [TestCase(100, 125)]
        [TestCase(300, 394)]
        public void BaseMonsterDefense_StaysUnderMainGameBoss(int floor, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.BaseMonsterDefense(floor));
        }

        [Test]
        public void BaseMonsterDefense_NeverDecreases()
        {
            int previous = -1;
            for (int floor = 1; floor <= TowerFormula.TotalFloors; floor++)
            {
                int defense = TowerFormula.BaseMonsterDefense(floor);
                Assert.GreaterOrEqual(defense, previous, floor + "층에서 방어력이 줄었다.");
                previous = defense;
            }
        }

        [Test]
        public void MonsterHp_AppliesConceptMultiplier()
        {
            // 물량(×0.45)과 정예(×5)가 같은 층에서 갈리는지.
            Assert.AreEqual(254, TowerFormula.MonsterHp(100, 0.45f));
            Assert.AreEqual(2820, TowerFormula.MonsterHp(100, 5f));
        }

        [Test]
        public void MonsterHp_NeverZero()
        {
            // 배수를 아주 작게 넣어도 체력 0 짜리 몬스터가 나오면 안 된다 —
            // 그 몬스터는 태어나는 프레임에 죽어 층이 통째로 사라진 것처럼 보인다.
            Assert.AreEqual(1, TowerFormula.MonsterHp(1, 0.0001f));
        }

        [Test]
        public void MonsterDefense_CanBeZero()
        {
            // 방어력 0 은 정상값이다. 하한을 1 로 올리면 "방어력 없음" 을 표현할 수 없다.
            Assert.AreEqual(0, TowerFormula.MonsterDefense(1, 0f));
        }

        // ── 마리 수 ──────────────────────────────────────────────────

        [TestCase(1, 14, 2, 14)]
        [TestCase(2, 14, 2, 16)]
        [TestCase(5, 14, 2, 22)]
        [TestCase(6, 11, 4, 11)]
        [TestCase(11, 17, 0, 17)]
        public void MonsterCount_GrowsWithinBand(int floor, int bandCount, int step, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.MonsterCount(floor, bandCount, step));
        }

        [Test]
        public void MonsterCount_NeverZero()
        {
            Assert.AreEqual(1, TowerFormula.MonsterCount(1, 0, 0));
            Assert.AreEqual(1, TowerFormula.MonsterCount(1, -5, 0));
        }

        // ── 목표 처치 수 ─────────────────────────────────────────────
        //
        // 웨이브 종료 판정이 여기 걸린다. 어긋나면 층이 안 끝나거나 일찍 끝난다.

        [TestCase(10, 0, 10)]
        [TestCase(10, 2, 30)]
        [TestCase(1, 3, 4)]
        public void WaveKillTarget_CountsSplitChildren(int count, int split, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.WaveKillTarget(count, split));
        }

        [Test]
        public void WaveKillTarget_IgnoresNegativeSplit()
        {
            Assert.AreEqual(10, TowerFormula.WaveKillTarget(10, -3));
        }

        // ── 등장 간격 ────────────────────────────────────────────────

        [Test]
        public void SpawnInterval_KeepsTotalSpawnTimeBounded()
        {
            // 45마리 물량 층이 등장에만 90초를 쓰면 "30~60초 안에 끝난다"는 전제가
            // 무너진다. 마리 수가 몇이든 총 등장 시간이 비슷해야 한다.
            float swarm = TowerFormula.SpawnInterval(45);
            Assert.Less(swarm * 45f, 15f);

            // 한 마리짜리 층에서 그 한 마리를 오래 기다리게 두지 않는다.
            Assert.AreEqual(1.2f, TowerFormula.SpawnInterval(1), 0.0001f);
        }

        [Test]
        public void SpawnInterval_HasFloor()
        {
            // 하한이 없으면 마리 수가 많은 층에서 한 프레임에 여러 마리가 겹쳐 쏟아진다.
            Assert.AreEqual(0.08f, TowerFormula.SpawnInterval(1000), 0.0001f);
        }

        // ── 보상 ─────────────────────────────────────────────────────

        [TestCase(1, 68)]
        [TestCase(10, 180)]
        [TestCase(25, 340)]
        [TestCase(100, 1260)]
        [TestCase(300, 3660)]
        public void FirstClearGold_HasTenFloorSteps(int floor, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.FirstClearGold(floor));
        }

        [Test]
        public void FirstClearGold_NeverDecreases()
        {
            int previous = 0;
            for (int floor = 1; floor <= TowerFormula.TotalFloors; floor++)
            {
                int gold = TowerFormula.FirstClearGold(floor);
                Assert.GreaterOrEqual(gold, previous);
                previous = gold;
            }
        }

        [TestCase(5, 10)]
        [TestCase(10, 12)]
        [TestCase(300, 128)]
        public void BandRewardDia_GrowsWithBand(int floor, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.BandRewardDia(floor));
        }

        [TestCase(5, 12)]
        [TestCase(10, 15)]
        [TestCase(300, 189)]
        public void BandRewardEnhanceStone_GrowsWithBand(int floor, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.BandRewardEnhanceStone(floor));
        }

        // ── 진행 ─────────────────────────────────────────────────────

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(25, 26)]
        [TestCase(300, 300)]
        public void HighestUnlockedFloor_IsNextAfterCleared(int cleared, int expected)
        {
            Assert.AreEqual(expected, TowerFormula.HighestUnlockedFloor(cleared));
        }

        [Test]
        public void IsFloorRevealed_ShowsEveryFloorUpToTheCurrentOne()
        {
            Assert.IsTrue(TowerFormula.IsFloorRevealed(1, 0));
            Assert.IsFalse(TowerFormula.IsFloorRevealed(2, 0));

            // 이미 깬 층도 정보는 보여 준다 — 목록이 "올라온 길" 이기 때문이다.
            Assert.IsTrue(TowerFormula.IsFloorRevealed(1, 25));
            Assert.IsTrue(TowerFormula.IsFloorRevealed(26, 25));
            Assert.IsFalse(TowerFormula.IsFloorRevealed(27, 25));

            // 범위 밖은 언제나 false. 저장된 진행도가 깨져도 없는 층이 열리면 안 된다.
            Assert.IsFalse(TowerFormula.IsFloorRevealed(0, 25));
            Assert.IsFalse(TowerFormula.IsFloorRevealed(301, 300));
        }

        /// <summary>
        /// <b>도전 가능한 층은 언제나 정확히 하나다.</b> 반복 플레이가 없으므로
        /// 이미 깬 층은 다시 들어갈 수 없다 — 이 규칙이 깨지면 낮은 층을 반복해
        /// 보상을 다시 받는 경로가 열린다.
        /// </summary>
        [Test]
        public void IsFloorChallengeable_OnlyTheNextFloor()
        {
            Assert.IsTrue(TowerFormula.IsFloorChallengeable(1, 0));
            Assert.IsFalse(TowerFormula.IsFloorChallengeable(2, 0));

            Assert.IsFalse(TowerFormula.IsFloorChallengeable(25, 25), "이미 깬 층");
            Assert.IsTrue(TowerFormula.IsFloorChallengeable(26, 25));
            Assert.IsFalse(TowerFormula.IsFloorChallengeable(27, 25));

            Assert.IsFalse(TowerFormula.IsFloorChallengeable(0, 25));
            Assert.IsFalse(TowerFormula.IsFloorChallengeable(301, 300));
        }

        [Test]
        public void IsFloorChallengeable_IsTrueForExactlyOneFloor()
        {
            // 진행도가 어떻든 목록에서 버튼이 붙는 카드는 한 장뿐이어야 한다.
            foreach (int cleared in new[] { 0, 1, 25, 299, 300 })
            {
                int count = 0;
                for (int floor = 1; floor <= TowerFormula.TotalFloors; floor++)
                {
                    if (TowerFormula.IsFloorChallengeable(floor, cleared))
                        count++;
                }

                Assert.AreEqual(1, count, "클리어 " + cleared + "층에서 도전 가능한 층이 " + count + "개다.");
            }
        }

        /// <summary>
        /// 꼭대기를 깨도 300층이 계속 "도전 가능" 으로 남는다. 그 자리에서 멈추는 것이
        /// 맞다 — 301층은 없고, 반복이 없다고 300층을 잠그면 마지막 카드가 사라진다.
        /// </summary>
        [Test]
        public void IsFloorChallengeable_StaysAtTopFloor()
        {
            Assert.IsTrue(TowerFormula.IsFloorChallengeable(300, 300));
        }

        [Test]
        public void UnlockProgress01_IsRatioOfClearedToUnlockFloor()
        {
            Assert.AreEqual(0f, TowerFormula.UnlockProgress01(0, 30), 0.0001f);
            Assert.AreEqual(0.5f, TowerFormula.UnlockProgress01(15, 30), 0.0001f);
            Assert.AreEqual(1f, TowerFormula.UnlockProgress01(30, 30), 0.0001f);

            // 넘어가도 1 을 넘지 않는다. 게이지가 트랙 밖으로 나가면 화면이 깨진다.
            Assert.AreEqual(1f, TowerFormula.UnlockProgress01(99, 30), 0.0001f);
        }

        [Test]
        public void UnlockProgress01_TreatsMissingUnlockAsComplete()
        {
            // 해금 조건이 없는데 0 을 돌려주면 게이지가 "영영 안 열리는 것" 처럼 보인다.
            Assert.AreEqual(1f, TowerFormula.UnlockProgress01(0, 0), 0.0001f);
        }

        // ── 편성 슬롯 ────────────────────────────────────────────────

        [Test]
        public void SlotCounts_MatchDesignDocument()
        {
            // 기획서 4.1 — "최대 7개 = 기본 4(고정) + 스페셜 2 + 신화 1".
            // 이 합이 깨지면 편성 화면의 슬롯 바와 규칙이 갈린다.
            Assert.AreEqual(1, TowerFormula.MythicSlotCount);
            Assert.AreEqual(2, TowerFormula.SpecialSlotCount);
            Assert.AreEqual(4, TowerFormula.BaseSlotCount);
            Assert.AreEqual(7, TowerFormula.TotalSlotCount);
            Assert.AreEqual(4, TowerFormula.MaxBaseStar);
        }
    }
}
