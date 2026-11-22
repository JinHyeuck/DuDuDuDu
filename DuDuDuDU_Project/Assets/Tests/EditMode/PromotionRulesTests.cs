using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 승급 규칙. 요구량 표·다이스별 고정 C 미션·레벨 게이트·교체 잠금·환급이 계획서(1.3)와 같은지 잠근다.
    /// 값을 바꾸려면 이 테스트를 같이 고쳐서 의도를 남길 것.
    /// </summary>
    public class PromotionRulesTests
    {
        private static readonly long Hour = TimeSpan.FromHours(1).Ticks;

        [Test]
        public void RequiredLevelsAreFiveFifteenThirty()
        {
            Assert.AreEqual(5, PromotionRules.RequiredDiceLevel(1));
            Assert.AreEqual(15, PromotionRules.RequiredDiceLevel(2));
            Assert.AreEqual(30, PromotionRules.RequiredDiceLevel(3));
            Assert.AreEqual(0, PromotionRules.RequiredDiceLevel(0));
            Assert.AreEqual(0, PromotionRules.RequiredDiceLevel(4));
        }

        [Test]
        public void BasicDiceStopAtTierOne()
        {
            Assert.AreEqual(1, PromotionRules.MaxTier(true));
            Assert.AreEqual(3, PromotionRules.MaxTier(false));
        }

        [TestCase(PromotionMission.PinballShots, 1, 300)]
        [TestCase(PromotionMission.PinballShots, 2, 3000)]
        [TestCase(PromotionMission.PinballShots, 3, 15000)]
        [TestCase(PromotionMission.SynergyTickets, 1, 50)]
        [TestCase(PromotionMission.SynergyTickets, 2, 500)]
        [TestCase(PromotionMission.SynergyTickets, 3, 2500)]
        [TestCase(PromotionMission.Sweeps, 1, 10)]
        [TestCase(PromotionMission.Sweeps, 2, 100)]
        [TestCase(PromotionMission.Sweeps, 3, 500)]
        [TestCase(PromotionMission.TowerChallenges, 1, 5)]
        [TestCase(PromotionMission.TowerChallenges, 2, 0)]
        [TestCase(PromotionMission.StageChallenges, 1, 3)]
        [TestCase(PromotionMission.StageChallenges, 2, 30)]
        [TestCase(PromotionMission.StageChallenges, 3, 150)]
        [TestCase(PromotionMission.MonsterKills, 1, 50)]
        [TestCase(PromotionMission.MonsterKills, 3, 0)]
        [TestCase(PromotionMission.BossKills, 1, 3)]
        [TestCase(PromotionMission.BossKills, 2, 0)]
        [TestCase(PromotionMission.EventCurrency, 1, 0)]
        [TestCase(PromotionMission.EventCurrency, 2, 1000)]
        [TestCase(PromotionMission.EventCurrency, 3, 3000)]
        public void BaseRequirementTableMatchesPlan(PromotionMission mission, int tier, int expected)
        {
            Assert.AreEqual(expected, PromotionRules.BaseRequirement(mission, tier));
        }

        [Test]
        public void MissionKeysAreDistinctNames()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < PromotionRules.MissionCount; i++)
                Assert.IsTrue(seen.Add(PromotionRules.Key((PromotionMission)i)));
        }

        // 칸 번호: 기본 0~4, 레어 5~9, 킹 10~14. 아래 테스트는 불(1) 과 토네이도(5) 를 쓴다.
        private const int Fire = 1;
        private const int Tornado = 5;

        [Test]
        public void EveryAssignedMissionIsUsableInItsTier()
        {
            // 고정표가 그 단계에서 요구량이 0인 미션을 가리키면 그 다이스는 영영 승급할 수 없다.
            for (int slot = 0; slot < PromotionRules.SlotCount; slot++)
            {
                bool basic = slot < 5;
                int maxTier = PromotionRules.MaxTier(basic);
                for (int tier = 1; tier <= maxTier; tier++)
                {
                    PromotionMission mission = PromotionRules.AssignedMission(slot, tier);
                    Assert.AreNotEqual(PromotionMission.EventCurrency, mission, "slot " + slot + " T" + tier);
                    Assert.Greater(PromotionRules.Requirement(slot, mission, tier), 0, "slot " + slot + " T" + tier);
                }
            }
        }

        [Test]
        public void OnlyTheAssignedParticipationMissionIsRequired()
        {
            // 불 T1 은 핀볼 발수 하나만 요구한다. 나머지 C 미션은 제외다.
            Assert.AreEqual(PromotionMission.PinballShots, PromotionRules.AssignedMission(Fire, 1));
            Assert.AreEqual(300, PromotionRules.Requirement(Fire, PromotionMission.PinballShots, 1));

            for (int i = 0; i < PromotionRules.MissionCount; i++)
            {
                var m = (PromotionMission)i;
                if (m != PromotionMission.PinballShots)
                    Assert.AreEqual(0, PromotionRules.Requirement(Fire, m, 1), m.ToString());
            }
        }

        [Test]
        public void AddStopsAtTheRequirement()
        {
            var progress = new Dictionary<string, int>();

            // 토네이도 T2 의 C 는 소탕 100회다.
            Assert.AreEqual(PromotionMission.Sweeps, PromotionRules.AssignedMission(Tornado, 2));
            Assert.AreEqual(80, PromotionRules.Add(progress, Tornado, PromotionMission.Sweeps, 2, 80));
            Assert.AreEqual(20, PromotionRules.Add(progress, Tornado, PromotionMission.Sweeps, 2, 50));
            Assert.AreEqual(0, PromotionRules.Add(progress, Tornado, PromotionMission.Sweeps, 2, 5));
            Assert.AreEqual(100, PromotionRules.Progress(progress, Tornado, PromotionMission.Sweeps, 2));
        }

        [Test]
        public void UnassignedMissionIsIgnored()
        {
            var progress = new Dictionary<string, int>();

            // 불 T1 의 C 는 핀볼뿐이라 소탕·몬스터 처치는 세지 않는다.
            Assert.AreEqual(0, PromotionRules.Add(progress, Fire, PromotionMission.Sweeps, 1, 10));
            Assert.AreEqual(0, PromotionRules.Add(progress, Fire, PromotionMission.MonsterKills, 1, 50));
            Assert.AreEqual(0, progress.Count);
        }

        [Test]
        public void TierOneNeedsOnlyTheAssignedMission()
        {
            var progress = new Dictionary<string, int>();
            Assert.IsFalse(PromotionRules.AllMissionsMet(progress, Fire, 1));

            PromotionRules.Add(progress, Fire, PromotionMission.PinballShots, 1, 299);
            Assert.IsFalse(PromotionRules.AllMissionsMet(progress, Fire, 1));

            PromotionRules.Add(progress, Fire, PromotionMission.PinballShots, 1, 1);
            Assert.IsTrue(PromotionRules.AllMissionsMet(progress, Fire, 1));
        }

        [Test]
        public void TierTwoNeedsOneParticipationMissionAndEventCurrency()
        {
            var progress = new Dictionary<string, int>();

            // 토네이도 T2: 소탕 100회 + 이벤트 재화 1,000개.
            PromotionRules.Add(progress, Tornado, PromotionMission.Sweeps, 2, 100);
            Assert.IsFalse(PromotionRules.AllMissionsMet(progress, Tornado, 2));

            PromotionRules.Add(progress, Tornado, PromotionMission.EventCurrency, 2, 1000);
            Assert.IsTrue(PromotionRules.AllMissionsMet(progress, Tornado, 2));
        }

        [Test]
        public void EventCurrencyAloneIsNotEnoughAndNeitherIsParticipationAlone()
        {
            var progress = new Dictionary<string, int>();
            PromotionRules.Add(progress, Tornado, PromotionMission.EventCurrency, 3, 3000);
            Assert.IsFalse(PromotionRules.AllMissionsMet(progress, Tornado, 3));

            PromotionRules.Add(progress, Tornado, PromotionMission.PinballShots, 3, 15000);
            Assert.IsTrue(PromotionRules.AllMissionsMet(progress, Tornado, 3));
        }

        [Test]
        public void RegisterNeedsTheTierLevel()
        {
            Assert.AreEqual(PromotionRegisterResult.LevelTooLow,
                PromotionRules.CanRegister(0, false, 4, false, false, 0, 0));
            Assert.AreEqual(PromotionRegisterResult.Ok,
                PromotionRules.CanRegister(0, false, 5, false, false, 0, 0));
            Assert.AreEqual(PromotionRegisterResult.LevelTooLow,
                PromotionRules.CanRegister(1, false, 14, false, false, 0, 0));
            Assert.AreEqual(PromotionRegisterResult.Ok,
                PromotionRules.CanRegister(2, false, 30, false, false, 0, 0));
        }

        [Test]
        public void BasicDiceCannotGoPastTierOne()
        {
            Assert.AreEqual(PromotionRegisterResult.MaxTier,
                PromotionRules.CanRegister(1, true, 30, false, false, 0, 0));
        }

        [Test]
        public void MaxTierDiceCannotRegisterAgain()
        {
            Assert.AreEqual(PromotionRegisterResult.MaxTier,
                PromotionRules.CanRegister(3, false, 30, false, false, 0, 0));
        }

        [Test]
        public void SameDiceCannotBeQueuedTwice()
        {
            Assert.AreEqual(PromotionRegisterResult.AlreadyQueued,
                PromotionRules.CanRegister(0, false, 5, true, false, 0, Hour));
        }

        [Test]
        public void ReplacingIsLockedForTwentyFourHours()
        {
            long registered = 100 * Hour;

            Assert.AreEqual(PromotionRegisterResult.ReplaceLocked,
                PromotionRules.CanRegister(0, false, 5, false, true, registered, registered + 23 * Hour));
            Assert.AreEqual(PromotionRegisterResult.Ok,
                PromotionRules.CanRegister(0, false, 5, false, true, registered, registered + 24 * Hour));
        }

        [Test]
        public void EmptyQueueHasNoLock()
        {
            // 승급이 끝난 직후 곧바로 다음 다이스를 올릴 수 있다.
            Assert.AreEqual(PromotionRegisterResult.Ok,
                PromotionRules.CanRegister(0, false, 5, false, false, 100 * Hour, 100 * Hour));
        }

        [Test]
        public void BackwardsClockDoesNotLockForever()
        {
            Assert.IsFalse(PromotionRules.IsReplaceLocked(100 * Hour, 50 * Hour));
        }

        [Test]
        public void LockRemainingCountsDown()
        {
            Assert.AreEqual(TimeSpan.FromHours(20), PromotionRules.ReplaceLockRemaining(0, 4 * Hour));
            Assert.AreEqual(TimeSpan.Zero, PromotionRules.ReplaceLockRemaining(0, 24 * Hour));
        }

        [Test]
        public void EventCurrencyIsCappedByRemainingNeedAndHeld()
        {
            Assert.AreEqual(300, PromotionRules.AcceptableEventCurrency(700, 1000, 5000, 5000));
            Assert.AreEqual(40, PromotionRules.AcceptableEventCurrency(0, 1000, 40, 5000));
            Assert.AreEqual(10, PromotionRules.AcceptableEventCurrency(0, 1000, 5000, 10));
            Assert.AreEqual(0, PromotionRules.AcceptableEventCurrency(1000, 1000, 5000, 5000));
            Assert.AreEqual(0, PromotionRules.AcceptableEventCurrency(0, 0, 5000, 5000));
        }

        [TestCase(0, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 1)]
        [TestCase(8, 2)]
        [TestCase(600, 200)]
        [TestCase(-5, 0)]
        public void RefundIsThreeCurrencyPerShotRoundedDown(int leftover, int expected)
        {
            Assert.AreEqual(expected, PromotionRules.RefundShots(leftover));
        }
    }
}
