using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class WaveThemeTests
    {
        // 첫 바퀴(1~10)
        [TestCase(1, WaveInterlude.SoulBonus)]
        [TestCase(2, WaveInterlude.LuckyDice)]
        [TestCase(3, WaveInterlude.BountyChallenge)]
        [TestCase(4, WaveInterlude.DiceGiftBasic)]
        [TestCase(5, WaveInterlude.SlotsOrSoul)]
        [TestCase(6, WaveInterlude.BountyChallenge)]
        [TestCase(7, WaveInterlude.WallHeal)]
        [TestCase(8, WaveInterlude.BossChallenge)]
        [TestCase(9, WaveInterlude.BountyChallenge)]
        [TestCase(10, WaveInterlude.BossReward)]
        // 둘째 바퀴(11~20): 이벤트 자리의 배치가 다르다.
        [TestCase(11, WaveInterlude.DiceGiftRare)]
        [TestCase(12, WaveInterlude.SoulBonus)]
        [TestCase(13, WaveInterlude.BountyChallenge)]
        [TestCase(14, WaveInterlude.WallHeal)]
        [TestCase(15, WaveInterlude.SlotsOrSoul)]
        [TestCase(17, WaveInterlude.LuckyDice)]
        [TestCase(18, WaveInterlude.BossChallenge)]
        [TestCase(19, WaveInterlude.BountyChallenge)]
        [TestCase(20, WaveInterlude.BossReward)]
        // 셋째 바퀴부터는 첫 바퀴 배치로 돌아온다.
        [TestCase(21, WaveInterlude.SoulBonus)]
        [TestCase(25, WaveInterlude.SlotsOrSoul)]
        [TestCase(30, WaveInterlude.BossReward)]
        public void FollowsTheTenWaveCycle(int wave, WaveInterlude expected)
        {
            Assert.AreEqual(expected, WaveTheme.After(wave));
        }

        /// <summary>영혼 보너스가 한 바퀴에 한 번만 나온다(너무 많이 나온다는 지적으로 이벤트를 나눴다).</summary>
        [Test]
        public void SoulBonusAppearsOncePerEventCycle()
        {
            for (int cycle = 0; cycle < 4; cycle++)
            {
                int count = 0;
                for (int pos = 1; pos <= WaveTheme.Cycle; pos++)
                {
                    if (WaveTheme.After(cycle * WaveTheme.Cycle + pos) == WaveInterlude.SoulBonus)
                        count++;
                }

                Assert.AreEqual(1, count, "바퀴 " + cycle);
            }
        }

        /// <summary>고정표(계획이 없을 때의 폴백)는 한 바퀴의 이벤트 자리 다섯에 서로 다른 이벤트를 놓는다.</summary>
        [Test]
        public void DefaultTableUsesFiveDifferentEventsPerCycle()
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                var seen = new System.Collections.Generic.HashSet<WaveInterlude>();
                for (int pos = 1; pos <= WaveTheme.Cycle; pos++)
                {
                    int wave = cycle * WaveTheme.Cycle + pos;
                    if (WaveTheme.IsEventSlot(wave))
                        Assert.IsTrue(seen.Add(WaveTheme.After(wave)), "바퀴 " + cycle + " 웨이브 " + wave + " 이벤트가 겹쳤다");
                }

                Assert.AreEqual(5, seen.Count, "바퀴 " + cycle);
            }
        }

        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(4, true)]
        [TestCase(5, false)]
        [TestCase(6, false)]
        [TestCase(7, true)]
        [TestCase(8, true)]
        [TestCase(9, false)]
        [TestCase(10, false)]
        [TestCase(11, true)]
        public void EventSlotsAreTheFiveOfTheCycle(int wave, bool expected)
        {
            Assert.AreEqual(expected, WaveTheme.IsEventSlot(wave));
        }

        // ── 이벤트 계획(판마다 무작위) ─────────────────────────────────

        /// <summary>이벤트 목록은 최소 15종(원래 5종 + 새로 10종 이상)이고 겹치는 항목이 없다.</summary>
        [Test]
        public void EventPoolHasAtLeastFifteenDistinctEvents()
        {
            Assert.GreaterOrEqual(WaveEventPlan.Pool.Length, 15);
            CollectionAssert.AllItemsAreUnique(WaveEventPlan.Pool);
            CollectionAssert.DoesNotContain(WaveEventPlan.Pool, WaveInterlude.None);
            CollectionAssert.DoesNotContain(WaveEventPlan.Pool, WaveInterlude.BountyChallenge);
            CollectionAssert.DoesNotContain(WaveEventPlan.Pool, WaveInterlude.SlotsOrSoul);
            CollectionAssert.DoesNotContain(WaveEventPlan.Pool, WaveInterlude.BossReward);
        }

        [Test]
        public void PlanFillsEveryEventSlotAndLeavesTheFixedOnesAlone()
        {
            WaveEventPlan plan = WaveEventPlan.Build(30, 12345);
            for (int wave = 1; wave <= 30; wave++)
            {
                if (WaveTheme.IsEventSlot(wave))
                    CollectionAssert.Contains(WaveEventPlan.Pool, plan.After(wave), "웨이브 " + wave);
                else
                    Assert.AreEqual(WaveTheme.FixedAfter(wave), plan.After(wave), "웨이브 " + wave);
            }

            Assert.AreEqual(15, plan.EventCount);
        }

        /// <summary>20웨이브 판의 이벤트 자리 10곳에는 같은 이벤트가 겹치지 않는다(목록이 자리보다 많으니).</summary>
        [Test]
        public void TwentyWavesNeverRepeatAnEvent()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                WaveEventPlan plan = WaveEventPlan.Build(20, seed);
                var seen = new System.Collections.Generic.HashSet<WaveInterlude>();
                for (int wave = 1; wave <= 20; wave++)
                {
                    if (WaveTheme.IsEventSlot(wave))
                        Assert.IsTrue(seen.Add(plan.After(wave)), "시드 " + seed + " 웨이브 " + wave + " 에서 겹쳤다");
                }

                Assert.AreEqual(10, seen.Count);
            }
        }

        [Test]
        public void SameSeedSamePlanDifferentSeedsDiffer()
        {
            WaveEventPlan a = WaveEventPlan.Build(20, 7);
            WaveEventPlan b = WaveEventPlan.Build(20, 7);
            for (int wave = 1; wave <= 20; wave++)
                Assert.AreEqual(a.After(wave), b.After(wave), "웨이브 " + wave);

            bool anyDifferent = false;
            for (int seed = 8; seed < 40 && !anyDifferent; seed++)
            {
                WaveEventPlan c = WaveEventPlan.Build(20, seed);
                for (int wave = 1; wave <= 20; wave++)
                    anyDifferent |= c.After(wave) != a.After(wave);
            }

            Assert.IsTrue(anyDifferent, "시드를 바꿔도 계획이 같다");
        }

        /// <summary>자리가 목록보다 많으면(긴 스테이지) 다 쓴 뒤 다시 섞어 이어 간다 — 어떤 자리도 비지 않는다.</summary>
        [Test]
        public void LongStagesReshuffleInsteadOfRunningOut()
        {
            WaveEventPlan plan = WaveEventPlan.Build(100, 3);
            for (int wave = 1; wave <= 100; wave++)
            {
                if (WaveTheme.IsEventSlot(wave))
                    CollectionAssert.Contains(WaveEventPlan.Pool, plan.After(wave), "웨이브 " + wave);
            }
        }

        // ── 다음 웨이브 효과 ──────────────────────────────────────────

        [Test]
        public void BuffsStartNeutralAndResetBackToIt()
        {
            WaveBuffs.Reset();
            Assert.IsFalse(WaveBuffs.IsActive);
            Assert.AreEqual(1f, WaveBuffs.CooldownMultiplier);

            WaveBuffs.AddAttackSpeedPercent(30);
            WaveBuffs.AddMonsterSlowPercent(30);
            WaveBuffs.AddMonsterHpPercent(-25);
            Assert.IsTrue(WaveBuffs.IsActive);
            Assert.AreEqual(1f / 1.3f, WaveBuffs.CooldownMultiplier, 0.0001f);
            Assert.AreEqual(0.7f, WaveBuffs.MonsterSpeedMultiplier, 0.0001f);
            Assert.AreEqual(0.75f, WaveBuffs.MonsterHpMultiplier, 0.0001f);

            WaveBuffs.Reset();
            Assert.IsFalse(WaveBuffs.IsActive);
        }

        [Test]
        public void MonsterHpBuffNeverGoesBelowATenth()
        {
            WaveBuffs.Reset();
            for (int i = 0; i < 20; i++)
                WaveBuffs.AddMonsterHpPercent(-50);

            Assert.GreaterOrEqual(WaveBuffs.MonsterHpMultiplier, 0.1f);
            WaveBuffs.Reset();
        }

        [Test]
        public void OnlyEveryTenthWaveIsBoss()
        {
            for (int w = 1; w <= 30; w++)
                Assert.AreEqual(w % 10 == 0, WaveTheme.IsBossWave(w), "웨이브 " + w);
        }

        [Test]
        public void BossWaveMatchesTheSoloBossIntervalOfTen()
        {
            for (int w = 1; w <= 30; w++)
                Assert.AreEqual(WaveTheme.IsBossWave(w), SoloBossRules.IsSoloBossWave(WaveTheme.Cycle, 30, w), "웨이브 " + w);
        }

        [TestCase(1, 10)]
        [TestCase(10, 10)]
        [TestCase(11, 20)]
        public void CycleIndexCountsTens(int wave, int expectedUpper)
        {
            Assert.AreEqual((expectedUpper / WaveTheme.Cycle) - 1, WaveTheme.CycleIndex(wave));
        }

        [TestCase(100, 10)]
        [TestCase(95, 10)]
        [TestCase(10, 1)]
        [TestCase(1, 1)]
        [TestCase(0, 0)]
        public void WallHealRoundsUpAndNeverZeroForARealWall(int maxHp, int expected)
        {
            Assert.AreEqual(expected, WaveTheme.WallHealAmount(maxHp));
        }

        [Test]
        public void EventBossGetsHarderEachCycleButNeverPastGradeFive()
        {
            Assert.AreEqual(2, WaveTheme.EventBossGrade(8));
            Assert.AreEqual(3, WaveTheme.EventBossGrade(18));
            Assert.AreEqual(5, WaveTheme.EventBossGrade(48));
            Assert.AreEqual(5, WaveTheme.EventBossGrade(98));
        }

        [Test]
        public void LuckyEventOddsSumToHundredAndStayInRange()
        {
            int sum = 0;
            for (int stones = LuckyDiceRules.EventMinStones; stones <= LuckyDiceRules.EventMaxStones; stones++)
                sum += LuckyDiceRules.EventChance(stones);

            Assert.AreEqual(100, sum);
            Assert.AreEqual(0, LuckyDiceRules.EventChance(LuckyDiceRules.EventMinStones - 1));
            Assert.AreEqual(0, LuckyDiceRules.EventChance(LuckyDiceRules.EventMaxStones + 1));
            Assert.AreEqual(LuckyDiceRules.EventMinStones, LuckyDiceRules.RollEvent(0));
            Assert.AreEqual(LuckyDiceRules.EventMaxStones, LuckyDiceRules.RollEvent(99));
        }

        [Test]
        public void LuckyEventRollFollowsTheTable()
        {
            var counts = new int[LuckyDiceRules.EventMaxStones + 1];
            for (int roll = 0; roll < 100; roll++)
                counts[LuckyDiceRules.RollEvent(roll)]++;

            for (int stones = LuckyDiceRules.EventMinStones; stones <= LuckyDiceRules.EventMaxStones; stones++)
                Assert.AreEqual(LuckyDiceRules.EventChance(stones), counts[stones], stones + "개");
        }
    }
}
