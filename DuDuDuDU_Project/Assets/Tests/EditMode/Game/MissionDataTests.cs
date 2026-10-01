using System;
using System.Collections.Generic;
using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.Mission;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 미션 데이터와 하위 키 규칙을 잠근다.
    ///
    /// <b>SO 인스턴스는 만들지 않는다.</b> 헤드리스 러너 안에서
    /// <c>ScriptableObject.CreateInstance</c> 가 돌지 않기 때문이고(<see cref="AssemblyPilotTests"/>),
    /// 그래서 <c>DailyMissionDatabase</c>·<c>AchievementDatabase</c> 의 규칙은 전부
    /// 목록을 받는 <c>static</c> 으로 빼 두었다 — 여기서 두드리는 것이 그것이다.
    /// </summary>
    public sealed class MissionDataTests
    {
        // ── 다이스 등급 하위 키 ────────────────────────────────────────

        /// <summary>
        /// <b>표를 둘로 둔 값을 치르는 테스트다.</b> <c>MissionSubKeys.ForDice</c> 는 번호
        /// 구간으로 등급을 가르고 <c>PointManager.ToScrollType</c> 은 다이스마다 강화 재화를
        /// 적는다. 둘이 어긋나면 "신화 다이스 레벨업" 업적이 특수 다이스에 올라가는데,
        /// 그 사고는 화면에서 숫자가 조금 더 빨리 오르는 것으로만 보인다.
        /// </summary>
        [Test]
        public void DiceGradeMatchesUpgradeCurrency()
        {
            foreach (DiceType diceType in Enum.GetValues(typeof(DiceType)))
            {
                if (diceType == DiceType.Max)
                    continue;

                PointType scroll = PointManager.ToScrollType(diceType);

                string expected =
                    scroll == PointType.MythicStone ? MissionSubKeys.DiceMythic :
                    scroll == PointType.RareStone ? MissionSubKeys.DiceRare :
                    MissionSubKeys.DiceNormal;

                Assert.AreEqual(expected, MissionSubKeys.ForDice(diceType), diceType.ToString());
            }
        }

        [Test]
        public void DiceLevelUpKeyCarriesTheGrade()
        {
            Assert.AreEqual("DiceLevelUp:Normal", MissionSubKeys.DiceLevelUpKey(DiceType.Fire));
            Assert.AreEqual("DiceLevelUp:Rare", MissionSubKeys.DiceLevelUpKey(DiceType.Tornado));
            Assert.AreEqual("DiceLevelUp:Mythic", MissionSubKeys.DiceLevelUpKey(DiceType.KingFire));
        }

        [Test]
        public void RaritySubKeyIsTheEnumName()
        {
            Assert.AreEqual("Rare", MissionSubKeys.ForRarity(Rarity.Rare));
            Assert.AreEqual("Epic", MissionSubKeys.ForRarity(Rarity.Epic));
        }

        // ── 일일 미션 기본값 ───────────────────────────────────────────

        [Test]
        public void DefaultDailyMissionsPassValidation()
        {
            List<string> problems = DailyMissionDatabase.Validate(
                DailyMissionDatabase.BuildDefaultMissions(),
                DailyMissionDatabase.BuildDefaultTiers());

            CollectionAssert.IsEmpty(problems, string.Join(" / ", problems));
        }

        /// <summary>기획의 11종이 한 종류씩 다 들어 있어야 한다.</summary>
        [Test]
        public void DefaultDailyMissionsCoverEveryPlannedAction()
        {
            var expected = new[]
            {
                MissionAction.Login,
                MissionAction.GamePlay,
                MissionAction.PinballShot,
                MissionAction.IapPurchase,
                MissionAction.DiceLevelUp,
                MissionAction.EquipmentLevelUp,
                MissionAction.GemDraw,
                MissionAction.GemMerge,
                MissionAction.AdWatch,
                MissionAction.MonsterKill,
                MissionAction.ShopPurchase,
            };

            List<DailyMissionDefinition> missions = DailyMissionDatabase.BuildDefaultMissions();
            Assert.AreEqual(expected.Length, missions.Count, "일일 미션 개수");

            var seen = new List<MissionAction>();
            for (int i = 0; i < missions.Count; i++)
                seen.Add(missions[i].action);

            CollectionAssert.AreEquivalent(expected, seen);
        }

        /// <summary>일일 미션은 종류를 가르지 않는다 — 하위 키가 붙으면 업적용 카운터를 보게 된다.</summary>
        [Test]
        public void DefaultDailyMissionsUsePlainCounters()
        {
            foreach (DailyMissionDefinition definition in DailyMissionDatabase.BuildDefaultMissions())
            {
                Assert.AreEqual(MissionRules.CounterKey(definition.action), definition.CounterKey,
                    definition.id);
            }
        }

        /// <summary>
        /// 문턱이 켜진 미션 수를 넘으면 그 보상은 영원히 안 열린다.
        /// 기본값에서는 11개가 전부 켜져 있으므로 10개 문턱까지 닿는다.
        /// </summary>
        [Test]
        public void TierThresholdsAreReachableWithDefaultMissions()
        {
            List<DailyMissionDefinition> missions = DailyMissionDatabase.BuildDefaultMissions();
            List<DailyMissionTier> tiers = DailyMissionDatabase.BuildDefaultTiers();

            int active = DailyMissionDatabase.GetActiveMissions(missions).Count;

            for (int i = 0; i < tiers.Count; i++)
                Assert.LessOrEqual(tiers[i].requiredClearCount, active, i + "번 문턱");
        }

        [Test]
        public void ValidationCatchesUnreachableTier()
        {
            List<DailyMissionDefinition> missions = DailyMissionDatabase.BuildDefaultMissions();

            // 광고·결제를 끄면 달성 가능한 미션이 9개가 되어 10개 문턱이 닫힌다.
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i].action == MissionAction.AdWatch ||
                    missions[i].action == MissionAction.IapPurchase)
                {
                    missions[i].enabled = false;
                }
            }

            List<string> problems = DailyMissionDatabase.Validate(
                missions, DailyMissionDatabase.BuildDefaultTiers());

            Assert.IsNotEmpty(problems, "닫힌 문턱을 잡아내지 못했다.");
        }

        [Test]
        public void ValidationCatchesDuplicateIds()
        {
            List<DailyMissionDefinition> missions = DailyMissionDatabase.BuildDefaultMissions();
            missions[1].id = missions[0].id;

            List<string> problems = DailyMissionDatabase.Validate(
                missions, DailyMissionDatabase.BuildDefaultTiers());

            Assert.IsNotEmpty(problems);
        }

        [Test]
        public void ValidationCatchesMissingReward()
        {
            List<DailyMissionDefinition> missions = DailyMissionDatabase.BuildDefaultMissions();
            missions[0].rewards.Clear();

            List<string> problems = DailyMissionDatabase.Validate(
                missions, DailyMissionDatabase.BuildDefaultTiers());

            Assert.IsNotEmpty(problems);
        }

        [Test]
        public void DefaultTierThresholdsFollowThePlan()
        {
            CollectionAssert.AreEqual(
                new[] { 3, 5, 7, 10 },
                DailyMissionDatabase.GetTierThresholds(DailyMissionDatabase.BuildDefaultTiers()));
        }

        // ── 업적 기본값 ────────────────────────────────────────────────

        [Test]
        public void DefaultAchievementsPassValidation()
        {
            List<string> problems = AchievementDatabase.Validate(AchievementDatabase.BuildDefaults());
            CollectionAssert.IsEmpty(problems, string.Join(" / ", problems));
        }

        /// <summary>기획의 업적 종류가 계열로 다 들어 있어야 한다. 보석 합성은 재료 등급 4종이다.</summary>
        [Test]
        public void DefaultAchievementsCoverEveryPlannedSeries()
        {
            var expected = new[]
            {
                "GamePlay",
                "PinballShot",
                "IapPurchase",
                "DiceLevelUp:Normal",
                "DiceLevelUp:Rare",
                "DiceLevelUp:Mythic",
                "EquipmentLevelUp",
                "GemDraw",
                "GemMerge:Common",
                "GemMerge:Normal",
                "GemMerge:Rare",
                "GemMerge:Epic",
                "AdWatch",
                "ShopPurchase",
                "MonsterKill",
                "BountyKill",
                "DiceMerge",
            };

            List<AchievementSeries> series = AchievementDatabase.BuildSeries(
                AchievementDatabase.BuildDefaults());

            var keys = new List<string>();
            for (int i = 0; i < series.Count; i++)
                keys.Add(series[i].CounterKey);

            CollectionAssert.AreEquivalent(expected, keys);
        }

        /// <summary>
        /// 계열 안쪽은 <b>요구 횟수 오름차순</b>이어야 한다. 화면이 "아직 안 받은 가장 낮은
        /// 단계" 를 그 순서에서 집어 오므로, 뒤섞이면 100회가 10회보다 먼저 뜬다.
        /// </summary>
        [Test]
        public void SeriesTiersAreSortedAscending()
        {
            foreach (AchievementSeries series in AchievementDatabase.BuildSeries(
                         AchievementDatabase.BuildDefaults()))
            {
                for (int i = 1; i < series.Tiers.Count; i++)
                {
                    Assert.Less(series.Tiers[i - 1].requiredCount, series.Tiers[i].requiredCount,
                        series.CounterKey);
                }
            }
        }

        /// <summary>목록이 뒤섞여 들어와도 정렬된다 — 인스펙터에서 끌어 옮긴 상태가 그렇다.</summary>
        [Test]
        public void SeriesSortsUnorderedInput()
        {
            var source = new List<AchievementDefinition>
            {
                MakeTier("b", MissionAction.GamePlay, 100),
                MakeTier("a", MissionAction.GamePlay, 10),
                MakeTier("c", MissionAction.GamePlay, 50),
            };

            List<AchievementSeries> series = AchievementDatabase.BuildSeries(source);

            Assert.AreEqual(1, series.Count);
            CollectionAssert.AreEqual(
                new[] { 10, 50, 100 },
                new[]
                {
                    series[0].Tiers[0].requiredCount,
                    series[0].Tiers[1].requiredCount,
                    series[0].Tiers[2].requiredCount,
                });
        }

        /// <summary>계열의 순서는 목록에 처음 나온 순서여야 한다 — 그것이 화면의 줄 순서다.</summary>
        [Test]
        public void SeriesOrderFollowsFirstAppearance()
        {
            var source = new List<AchievementDefinition>
            {
                MakeTier("x1", MissionAction.DiceMerge, 10),
                MakeTier("y1", MissionAction.GamePlay, 10),
                MakeTier("x2", MissionAction.DiceMerge, 100),
            };

            List<AchievementSeries> series = AchievementDatabase.BuildSeries(source);

            Assert.AreEqual("DiceMerge", series[0].CounterKey);
            Assert.AreEqual("GamePlay", series[1].CounterKey);
        }

        [Test]
        public void ValidationCatchesDuplicateRequiredCountInSeries()
        {
            var source = new List<AchievementDefinition>
            {
                MakeTier("a", MissionAction.GamePlay, 10),
                MakeTier("b", MissionAction.GamePlay, 10),
            };

            List<string> problems = AchievementDatabase.Validate(source);
            Assert.IsNotEmpty(problems);
        }

        /// <summary>
        /// 업적 id 는 영구 세이브 키다. 요구 횟수를 담아 두면 단계를 중간에 끼워 넣어도
        /// 뒤 단계의 id 가 밀리지 않는다 — 밀리면 이미 받은 사람이 다시 받게 된다.
        /// </summary>
        [Test]
        public void AchievementIdsEmbedTheRequiredCount()
        {
            foreach (AchievementDefinition definition in AchievementDatabase.BuildDefaults())
            {
                StringAssert.EndsWith("_" + definition.requiredCount, definition.id);
            }
        }

        private static AchievementDefinition MakeTier(string id, MissionAction action, int required)
        {
            return new AchievementDefinition
            {
                id = id,
                action = action,
                subKey = string.Empty,
                requiredCount = required,
                titleFormat = "{0}회",
                rewards = new List<MissionReward> { new MissionReward(PointType.Gold, 1) },
            };
        }

        // ── 표기 ───────────────────────────────────────────────────────

        [Test]
        public void TitleFillsTheRequiredCount()
        {
            Assert.AreEqual("게임 플레이 2회", MissionText.Title("게임 플레이 {0}회", 2));
        }

        /// <summary>서식이 틀려도 목록 전체를 못 그리게 되면 안 된다.</summary>
        [Test]
        public void BadTitleFormatFallsBackInsteadOfThrowing()
        {
            Assert.AreEqual("{1}회", MissionText.Title("{1}회", 3));
        }

        [Test]
        public void ProgressDoesNotOvershootTheRequirement()
        {
            Assert.AreEqual("0/2", MissionText.Progress(0, 2));
            Assert.AreEqual("1/2", MissionText.Progress(1, 2));
            Assert.AreEqual("2/2", MissionText.Progress(2, 2));
            Assert.AreEqual("2/2", MissionText.Progress(7, 2));
            Assert.AreEqual("10/100", MissionText.Progress(10, 100));
        }

        [Test]
        public void ResetCountdownDropsSecondsAndHoursWhenZero()
        {
            Assert.AreEqual("14시간 49분", MissionText.ResetCountdown(new TimeSpan(14, 49, 30)));
            Assert.AreEqual("49분", MissionText.ResetCountdown(new TimeSpan(0, 49, 30)));
            Assert.AreEqual("0분", MissionText.ResetCountdown(TimeSpan.Zero));

            // 기기 시계를 뒤로 돌린 경우. 음수 시간을 그대로 찍지 않는다.
            Assert.AreEqual("0분", MissionText.ResetCountdown(TimeSpan.FromMinutes(-5)));
        }
    }
}
