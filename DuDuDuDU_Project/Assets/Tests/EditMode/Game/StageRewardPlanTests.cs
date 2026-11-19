using System.Collections.Generic;
using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.StageReward;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 1~10스테이지 클리어 보상이 "그 레벨까지 올릴 수 있는 재화" 인지 잠근다.
    /// 장비는 <see cref="EquipmentUpgradeFormula"/>(골드 + 두루마리), 다이스는 <see cref="DiceCopyCurve"/>(카피 장수만, 골드 없음)로
    /// 비용을 다시 계산한다. 카피 곡선이나 장비 규칙을 바꾸면 여기가 깨진다 — 안 그러면 뉴비가 보상을 받고도 강화를 못 한다.
    /// </summary>
    public sealed class StageRewardPlanTests
    {
        // 스테이지별 목표 레벨(그 스테이지를 깬 뒤 이 레벨까지). 키 = 장비 인덱스 / 다이스 종류.
        // 노멀 다이스는 1스테이지 5웨이브에서 다이스 가이드가 3레벨까지 올려 준다(재화를 채워 주며 직접 누르게 한다).
        // 그래서 시작 레벨을 3 으로 본다. 2스테이지의 "노멀 3" 몫은 이미 올라 있어 노멀 2장은 보너스다(필요량 0).

        // 1스테이지 15웨이브: 레어 다이스 두 개를 3레벨까지 — 한 개당 1+2 = 3장, 합 6장(랜덤 레어 카피).
        private const int RareDiceCount = 2, RareDiceCopies = 3;
        private static readonly (int[] equip, (PointType scroll, int level)[] dice)[] Plan =
        {
            (new[] { 3, 2, 2, 2, 2, 2 }, new[] { (PointType.ThunderScroll, 2), (PointType.FireScroll, 2) }),
            (new[] { 3, 3, 3, 3, 3, 3 }, new[] { (PointType.NormalScroll, 3), (PointType.PoisonScroll, 2) }),
            (new[] { 4, 3, 3, 4, 3, 3 }, new[] { (PointType.ThunderScroll, 3), (PointType.IceScroll, 2) }),
            (new[] { 4, 4, 4, 4, 3, 3 }, new[] { (PointType.FireScroll, 3), (PointType.IceScroll, 3) }),
            (new[] { 4, 4, 4, 4, 4, 4 }, new[] { (PointType.PoisonScroll, 3), (PointType.NormalScroll, 4) }),
            (new[] { 5, 4, 4, 5, 4, 4 }, new[] { (PointType.ThunderScroll, 4), (PointType.FireScroll, 4) }),
            (new[] { 5, 5, 5, 5, 4, 4 }, new[] { (PointType.IceScroll, 4), (PointType.PoisonScroll, 4) }),
            (new[] { 5, 5, 5, 5, 5, 5 }, new[] { (PointType.NormalScroll, 5), (PointType.FireScroll, 5) }),
            (new[] { 6, 5, 5, 6, 5, 5 }, new[] { (PointType.ThunderScroll, 5), (PointType.IceScroll, 5) }),
            (new[] { 6, 6, 6, 6, 5, 5 }, new[] { (PointType.PoisonScroll, 5), (PointType.ThunderScroll, 6) }),
        };

        private static readonly PointType[] EquipScroll =
        {
            PointType.WeaponScroll, PointType.HelmetScroll, PointType.ArmorScroll,
            PointType.RingScroll, PointType.ShoesScroll, PointType.NecklaceScroll,
        };

        private static Dictionary<PointType, int> Totals(int stage)
        {
            var sum = new Dictionary<PointType, int>();
            for (int tier = 0; tier < StageRewardPlan.TierCount; tier++)
                foreach (StageRewardEntry e in StageRewardPlan.Build(stage, tier))
                    sum[e.pointType] = (sum.TryGetValue(e.pointType, out int v) ? v : 0) + e.amount;
            return sum;
        }

        [Test]
        public void EveryPlannedStage_CoversTheUpgradeCostOfItsTargetLevels()
        {
            var equipLevel = new[] { 1, 1, 1, 1, 1, 1 };
            var diceLevel = new Dictionary<PointType, int>
            {
                { PointType.NormalScroll, 3 }, { PointType.FireScroll, 1 }, { PointType.IceScroll, 1 },
                { PointType.ThunderScroll, 1 }, { PointType.PoisonScroll, 1 },
            };

            for (int stage = 1; stage <= StageRewardPlan.LastPlannedStage; stage++)
            {
                var plan = Plan[stage - 1];
                var need = new Dictionary<PointType, int>();
                int gold = 0;

                for (int i = 0; i < 6; i++)
                {
                    EquipmentUpgradeFormula.EquipmentUpgradeRule rule = EquipmentUpgradeFormula.Rule(i);
                    for (int level = equipLevel[i]; level < plan.equip[i]; level++)
                    {
                        gold += EquipmentUpgradeFormula.UpgradeGoldCost(rule.baseGold, rule.goldPerLevel, level);
                        need[EquipScroll[i]] = (need.TryGetValue(EquipScroll[i], out int v) ? v : 0)
                            + EquipmentUpgradeFormula.UpgradeScrollCost(rule.baseScroll, rule.scrollPerLevel, level);
                    }

                    equipLevel[i] = System.Math.Max(equipLevel[i], plan.equip[i]);
                }

                // 다이스는 카피 장수만 든다(골드 없음). 한 레벨 비용은 "다음 레벨로 가는 값" 이다.
                foreach ((PointType scroll, int level) in plan.dice)
                {
                    for (int l = diceLevel[scroll]; l < level; l++)
                    {
                        need[scroll] = (need.TryGetValue(scroll, out int v) ? v : 0) + DiceCopyCurve.CopiesToLevelUp(l);
                    }

                    diceLevel[scroll] = System.Math.Max(diceLevel[scroll], level);
                }

                Dictionary<PointType, int> got = Totals(stage);
                foreach (KeyValuePair<PointType, int> pair in need)
                {
                    Assert.IsTrue(got.TryGetValue(pair.Key, out int have), stage + "스테이지에 " + pair.Key + " 보상이 없다.");
                    Assert.AreEqual(pair.Value, have, stage + "스테이지 " + pair.Key + " 은 강화 비용과 같아야 한다.");
                }

                // 골드는 장비 강화 비용만 맞춘다(다이스 골드 비용 삭제). 올림 한도 9.
                Assert.GreaterOrEqual(got[PointType.Gold], gold, stage + "스테이지 골드가 장비 강화 비용보다 적다.");
                Assert.LessOrEqual(got[PointType.Gold], gold + 9, stage + "스테이지 골드가 비용보다 10 넘게 많다(올림 한도).");
            }
        }

        [Test]
        public void StageOne_FollowsTheWaveMilestoneTheme()
        {
            // 5웨이브: 다이스 재료만. 장비 재료는 아직 쓸 수 없다(장비는 10웨이브에 열린다).
            foreach (StageRewardEntry e in StageRewardPlan.Build(1, 0))
                CollectionAssert.DoesNotContain(EquipScroll, e.pointType, "5웨이브 보상에 장비 재료가 있다.");

            // 10웨이브: 장비 재료.
            var tier1 = new HashSet<PointType>();
            foreach (StageRewardEntry e in StageRewardPlan.Build(1, 1))
                tier1.Add(e.pointType);
            foreach (PointType scroll in EquipScroll)
                Assert.IsTrue(tier1.Contains(scroll), "10웨이브 보상에 " + scroll + " 이 없다.");

            // 15웨이브: 랜덤 레어 카피가 레어 다이스 두 개를 3레벨까지 올릴 만큼.
            int copies = 0;
            foreach (StageRewardEntry e in StageRewardPlan.Build(1, 2))
                if (e.pointType == PointType.RandomRareCopy)
                    copies += e.amount;
            Assert.AreEqual(RareDiceCount * RareDiceCopies, copies);
            Assert.AreEqual(DiceCopyCurve.CumulativeCopies(3), RareDiceCopies, "레어 다이스 하나를 Lv3 까지 올리는 장수");
        }

        [Test]
        public void FirstThreeStages_GiveOnlyEquipmentDiceMeatTicketsAndRareCopies()
        {
            var allowed = new HashSet<PointType>
            {
                PointType.Gold, PointType.Stamina, PointType.PinballTicket, PointType.RandomRareCopy,
                PointType.WeaponScroll, PointType.HelmetScroll, PointType.ArmorScroll, PointType.RingScroll,
                PointType.ShoesScroll, PointType.NecklaceScroll,
                PointType.NormalScroll, PointType.FireScroll, PointType.IceScroll, PointType.PoisonScroll, PointType.ThunderScroll,
            };

            for (int stage = 1; stage <= 3; stage++)
                foreach (PointType type in Totals(stage).Keys)
                    Assert.IsTrue(allowed.Contains(type), stage + "스테이지에 허용되지 않은 보상 " + type);
        }

        [Test]
        public void UserSpec_Stages1To3_MeatTicketsAndRareCopies()
        {
            Assert.AreEqual(30, Totals(1)[PointType.Stamina]);
            Assert.IsFalse(Totals(1).ContainsKey(PointType.PinballTicket));
            Assert.AreEqual(30, Totals(2)[PointType.Stamina]);
            Assert.AreEqual(30, Totals(2)[PointType.PinballTicket]);
            Assert.AreEqual(1, Totals(2)[PointType.RandomRareCopy]);
            Assert.AreEqual(1, Totals(3)[PointType.RandomRareCopy]);
        }

        [Test]
        public void KingCopiesStartAtStageFour()
        {
            Assert.IsFalse(Totals(3).ContainsKey(PointType.RandomKingCopy));
            for (int stage = 4; stage <= 10; stage++)
                Assert.AreEqual(2, Totals(stage)[PointType.RandomKingCopy]);
        }

        /// <summary>옛 등급 공용 재화(레어석·신화석)는 보상표에서 완전히 빠졌다.</summary>
        [Test]
        public void NoLegacyStonesRemainInThePlan()
        {
            for (int stage = 1; stage <= StageRewardPlan.LastPlannedStage; stage++)
            {
                Dictionary<PointType, int> totals = Totals(stage);
                Assert.IsFalse(totals.ContainsKey(PointType.RareStone), stage + "스테이지에 레어석이 남았다.");
                Assert.IsFalse(totals.ContainsKey(PointType.MythicStone), stage + "스테이지에 신화석이 남았다.");
            }
        }

        [Test]
        public void NoTierShowsMoreThanTenIcons()
        {
            for (int stage = 1; stage <= 10; stage++)
                for (int tier = 0; tier < StageRewardPlan.TierCount; tier++)
                    Assert.LessOrEqual(StageRewardPlan.Build(stage, tier).Count, 10, stage + "스테이지 " + tier + "칸");
        }
    }
}
