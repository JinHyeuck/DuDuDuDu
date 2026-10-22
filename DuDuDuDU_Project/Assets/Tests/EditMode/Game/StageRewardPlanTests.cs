using System.Collections.Generic;
using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.StageReward;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 1~10스테이지 클리어 보상이 "그 레벨까지 올릴 수 있는 재화" 인지 잠근다.
    /// 장비는 <see cref="EquipmentUpgradeFormula"/>, 기본 다이스는 에셋 값(골드 130+55/레벨, 두루마리 10+20/레벨)으로 비용을 다시 계산한다.
    /// 에셋의 다이스 비용을 바꾸면 여기 상수도 같이 고친다 — 안 그러면 뉴비가 보상을 받고도 강화를 못 한다.
    /// </summary>
    public sealed class StageRewardPlanTests
    {
        private const int DiceBaseGold = 130, DiceGoldPerLevel = 55, DiceBaseScroll = 10, DiceScrollPerLevel = 20;

        // 스테이지별 목표 레벨(그 스테이지를 깬 뒤 이 레벨까지). 키 = 장비 인덱스 / 다이스 종류. 노멀 다이스는 튜토리얼이 2레벨로 시작시킨다.
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
                { PointType.NormalScroll, 2 }, { PointType.FireScroll, 1 }, { PointType.IceScroll, 1 },
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

                foreach ((PointType scroll, int level) in plan.dice)
                {
                    for (int l = diceLevel[scroll]; l < level; l++)
                    {
                        gold += DiceBaseGold + (l - 1) * DiceGoldPerLevel;
                        need[scroll] = (need.TryGetValue(scroll, out int v) ? v : 0) + DiceBaseScroll + (l - 1) * DiceScrollPerLevel;
                    }

                    diceLevel[scroll] = System.Math.Max(diceLevel[scroll], level);
                }

                Dictionary<PointType, int> got = Totals(stage);
                foreach (KeyValuePair<PointType, int> pair in need)
                {
                    Assert.IsTrue(got.TryGetValue(pair.Key, out int have), stage + "스테이지에 " + pair.Key + " 보상이 없다.");
                    Assert.AreEqual(pair.Value, have, stage + "스테이지 " + pair.Key + " 은 강화 비용과 같아야 한다.");
                }

                int kingGold = stage >= 4 ? 300 : 0;
                Assert.GreaterOrEqual(got[PointType.Gold], gold + kingGold, stage + "스테이지 골드가 강화 비용보다 적다.");
                Assert.LessOrEqual(got[PointType.Gold], gold + kingGold + 9, stage + "스테이지 골드가 비용보다 10 넘게 많다(올림 한도).");
            }
        }

        [Test]
        public void FirstThreeStages_GiveOnlyEquipmentDiceMeatTicketsAndRareStones()
        {
            var allowed = new HashSet<PointType>
            {
                PointType.Gold, PointType.Stamina, PointType.PinballTicket, PointType.RareStone,
                PointType.WeaponScroll, PointType.HelmetScroll, PointType.ArmorScroll, PointType.RingScroll,
                PointType.ShoesScroll, PointType.NecklaceScroll,
                PointType.NormalScroll, PointType.FireScroll, PointType.IceScroll, PointType.PoisonScroll, PointType.ThunderScroll,
            };

            for (int stage = 1; stage <= 3; stage++)
                foreach (PointType type in Totals(stage).Keys)
                    Assert.IsTrue(allowed.Contains(type), stage + "스테이지에 허용되지 않은 보상 " + type);
        }

        [Test]
        public void UserSpec_Stages1To3_MeatTicketsAndRareStones()
        {
            Assert.AreEqual(30, Totals(1)[PointType.Stamina]);
            Assert.IsFalse(Totals(1).ContainsKey(PointType.PinballTicket));
            Assert.AreEqual(30, Totals(2)[PointType.Stamina]);
            Assert.AreEqual(30, Totals(2)[PointType.PinballTicket]);
            Assert.AreEqual(3, Totals(2)[PointType.RareStone]);
            Assert.AreEqual(3, Totals(3)[PointType.RareStone]);
        }

        [Test]
        public void KingStonesStartAtStageFour()
        {
            Assert.IsFalse(Totals(3).ContainsKey(PointType.MythicStone));
            for (int stage = 4; stage <= 10; stage++)
                Assert.AreEqual(10, Totals(stage)[PointType.MythicStone]);
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
