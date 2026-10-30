using System;
using System.Collections.Generic;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    public sealed class SynergyRulesTests
    {
        private static readonly IReadOnlyList<int> SomeTargets = new[] { 0, 1, 2 };

        private static IReadOnlyList<int> Targets(SynergyTargetKind kind) => SomeTargets;

        /// <summary>정해 둔 수열을 차례로 돌려준다. 다 쓰면 0. 범위를 넘는 값은 n-1 로 자른다.</summary>
        private static Func<int, int> Sequence(params int[] values)
        {
            int index = 0;
            return n =>
            {
                int value = index < values.Length ? values[index] : 0;
                index++;
                return Math.Min(value, n - 1);
            };
        }

        // ── 비용 ──────────────────────────────────────────────

        [Test]
        public void Cost_FollowsTheAgreedFormula()
        {
            Assert.AreEqual(10, SynergyRules.RerollCost(false, 0));
            Assert.AreEqual(20, SynergyRules.RerollCost(false, 1));
            Assert.AreEqual(30, SynergyRules.RerollCost(false, 2));
            Assert.AreEqual(20, SynergyRules.RerollCost(true, 0));

            // 사용자가 고른 식: (10 + 20) × 2 = 60. 10 × 2 + 20 = 40 이 아니다.
            Assert.AreEqual(60, SynergyRules.RerollCost(true, 2));
        }

        [Test]
        public void Cost_IgnoresLocksOnLinesThatAreNotOpen()
        {
            var preset = new SynergyPreset();
            preset.Effects[2].Locked = true;

            Assert.AreEqual(10, SynergyRules.RerollCost(preset, 2));
            Assert.AreEqual(20, SynergyRules.RerollCost(preset, 3));
        }

        [Test]
        public void Reroll_IsBlockedWhenEveryOpenLineIsLocked()
        {
            var preset = new SynergyPreset();
            preset.Condition.Locked = true;
            preset.Effects[0].Locked = true;

            Assert.IsFalse(SynergyRules.CanReroll(preset, 1));
            Assert.IsTrue(SynergyRules.CanReroll(preset, 2));

            int spent = 0;
            SynergyRerollResult result = SynergyRules.Reroll(preset, SynergyCategory.Dice, 1, 40, ref spent, Sequence(), Targets);
            Assert.IsFalse(result.Rolled);
            Assert.AreEqual(0, spent);
        }

        // ── Best 보정 ─────────────────────────────────────────

        [Test]
        public void BestChance_GrowsPerTicketAndStopsAtThirtyPercent()
        {
            Assert.AreEqual(0, SynergyRules.BestChanceBp(0));
            Assert.AreEqual(50, SynergyRules.BestChanceBp(10));
            Assert.AreEqual(3000, SynergyRules.BestChanceBp(600));
            Assert.AreEqual(3000, SynergyRules.BestChanceBp(100000));
            Assert.AreEqual(600, SynergyRules.TicketsToCap);
            Assert.AreEqual("0.50%", SynergyRules.FormatBp(50));
            Assert.AreEqual("30.00%", SynergyRules.FormatBp(3000));
        }

        [Test]
        public void Reroll_AddsTheCostBeforeJudgingBest()
        {
            var preset = new SynergyPreset();
            int spent = 590;

            // 굴림 수열: 조건(옵션·값) → 효과(옵션·대상·값) → Best 판정. 판정값 2999 는 누적 600 일 때만 당첨된다.
            SynergyRerollResult result = SynergyRules.Reroll(preset, SynergyCategory.Combat, 1, 10, ref spent,
                Sequence(0, 0, 0, 0, 2999, 0), Targets);

            Assert.IsTrue(result.BestHit);
            Assert.AreEqual(0, spent, "당첨되면 누적이 0 으로 돌아간다");
        }

        [Test]
        public void Reroll_BestLandsOnExactlyOneRolledLine()
        {
            var preset = new SynergyPreset();
            int spent = 600;

            // 조건 + 효과 2줄을 굴리고, Best 는 두 번째로 굴린 줄(효과 0번)에 붙인다.
            SynergyRerollResult result = SynergyRules.Reroll(preset, SynergyCategory.Combat, 2, 10, ref spent,
                Sequence(0, 5, 0, 0, 0, 0, 0, 1), Targets);

            Assert.IsTrue(result.BestHit);
            Assert.AreEqual(0, result.BestLine);
            Assert.IsTrue(SynergyRules.IsBest(preset.Effects[0], false));
            Assert.IsFalse(SynergyRules.IsBest(preset.Condition, true));
        }

        [Test]
        public void Reroll_LockedLinesAreNeverRolledNorPickedForBest()
        {
            var preset = new SynergyPreset();
            preset.Condition.OptionId = (int)SynergyConditionId.WaveEnter;
            preset.Condition.Value = 3;
            preset.Condition.Locked = true;
            int spent = 600;

            SynergyRerollResult result = SynergyRules.Reroll(preset, SynergyCategory.Combat, 1, 20, ref spent,
                Sequence(0, 0, 0, 0, 0), Targets);

            Assert.AreEqual(3, preset.Condition.Value);
            Assert.AreEqual(0, result.BestLine);
        }

        [Test]
        public void Reroll_WithoutBestKeepsAccumulating()
        {
            var preset = new SynergyPreset();
            int spent = 0;

            SynergyRules.Reroll(preset, SynergyCategory.Dice, 1, 10, ref spent, Sequence(0, 0, 0, 0, 0, 9999), Targets);
            Assert.AreEqual(10, spent);
        }

        // ── 줄 굴림 ───────────────────────────────────────────

        [Test]
        public void RollLine_StaysInsideWorstToBestForBothDirections()
        {
            var random = new Random(7);
            Func<int, int> next = random.Next;

            foreach (SynergyCategory category in Enum.GetValues(typeof(SynergyCategory)))
            {
                foreach (bool conditions in new[] { true, false })
                {
                    List<SynergyOption> pool = SynergyCatalog.Pool(category, conditions);
                    Assert.IsNotEmpty(pool, category + (conditions ? " 조건" : " 효과"));

                    for (int i = 0; i < 200; i++)
                    {
                        var line = new SynergyLine();
                        SynergyRules.RollLine(line, pool, next, Targets);
                        SynergyOption option = SynergyRules.Resolve(line, conditions);

                        Assert.IsNotNull(option);
                        Assert.That(line.Value, Is.InRange(option.Low, option.High));
                        Assert.AreEqual(option.Target == SynergyTargetKind.None, line.Target < 0);
                    }
                }
            }
        }

        [Test]
        public void Catalog_ConditionsGetBetterAsNShrinks_EffectsAsValueGrows()
        {
            foreach (SynergyOption option in SynergyCatalog.All)
            {
                if (option.IsCondition)
                    Assert.LessOrEqual(option.Best, option.Worst, "조건 " + option.Id);
                else
                    Assert.GreaterOrEqual(option.Best, option.Worst, "효과 " + option.Id);
            }
        }

        [Test]
        public void Catalog_ResourceGrantsLiveOnlyInCombat()
        {
            SynergyEffectId[] grants =
            {
                SynergyEffectId.GrantSp, SynergyEffectId.GrantSpPercent,
                SynergyEffectId.GrantStone, SynergyEffectId.GrantStonePercent,
            };

            foreach (SynergyEffectId id in grants)
            {
                SynergyOption option = SynergyCatalog.GetEffect(id);
                Assert.IsTrue(option.BelongsTo(SynergyCategory.Combat), id.ToString());
                Assert.IsFalse(option.BelongsTo(SynergyCategory.Action), id.ToString());
                Assert.IsFalse(option.BelongsTo(SynergyCategory.Dice), id.ToString());
                Assert.IsFalse(option.BelongsTo(SynergyCategory.Unified), id.ToString());
            }
        }

        /// <summary>맨 앞 적에게 200~400 고정 피해 5발 장전(1초에 한 발). 다이스계에만 있고 버프가 아니다.</summary>
        [Test]
        public void Catalog_RepeatFixedDamage_IsADiceEffectWithFiveLoadedBullets()
        {
            SynergyOption option = SynergyCatalog.GetEffect(SynergyEffectId.RepeatFixedDamage);
            Assert.IsNotNull(option);
            Assert.IsTrue(option.BelongsTo(SynergyCategory.Dice));
            Assert.IsFalse(option.BelongsTo(SynergyCategory.Combat));
            Assert.IsFalse(option.IsBuff);
            Assert.AreEqual(200, option.Worst);
            Assert.AreEqual(400, option.Best);
            Assert.AreEqual(0f, option.Seconds);
            Assert.AreEqual(5, option.Hits);
            Assert.AreEqual("맨 앞 적에게 300 고정 피해 5발 장전", SynergyRules.Describe(option, 300, string.Empty));
        }

        /// <summary>로비 가이드: 노멀 속성 다이스 11회 공격 시 → 5초간 5회 고정 피해(357). 무작위가 아니고 Best 도 안 터진다.</summary>
        [Test]
        public void GuideReroll_FillsTheAgreedConditionAndEffect()
        {
            var preset = new SynergyPreset();
            preset.Effects[1].OptionId = (int)SynergyEffectId.AttackBuff;
            preset.Effects[1].Value = 10;

            int spent = 0;
            SynergyRerollResult result = SynergyRules.GuideReroll(preset, SynergyRules.BaseRerollCost, ref spent);

            Assert.IsTrue(result.Rolled);
            Assert.IsFalse(result.BestHit);
            Assert.AreEqual((int)SynergyConditionId.ElementAttack, preset.Condition.OptionId);
            Assert.AreEqual(0, preset.Condition.Target, "ElementType.Normal");
            Assert.AreEqual(11, preset.Condition.Value);
            Assert.AreEqual((int)SynergyEffectId.RepeatFixedDamage, preset.Effects[0].OptionId);
            Assert.AreEqual(SynergyRules.GuideEffectValue, preset.Effects[0].Value);
            Assert.IsTrue(preset.Effects[1].IsEmpty, "나머지 효과 줄은 비운다");
            Assert.AreEqual(SynergyRules.BaseRerollCost, spent);

            // 값이 범위 안이어야 저장·불러오기에서 잘리지 않는다.
            SynergyOption condition = SynergyRules.Resolve(preset.Condition, true);
            SynergyOption effect = SynergyRules.Resolve(preset.Effects[0], false);
            Assert.That(preset.Condition.Value, Is.InRange(condition.Low, condition.High));
            Assert.That(preset.Effects[0].Value, Is.InRange(effect.Low, effect.High));
        }

        // ── 개방 ──────────────────────────────────────────────

        [Test]
        public void EffectLines_OpenAtStageTwentyAndSixty()
        {
            Assert.AreEqual(1, SynergyRules.OpenEffectLines(0));
            Assert.AreEqual(1, SynergyRules.OpenEffectLines(19));
            Assert.AreEqual(2, SynergyRules.OpenEffectLines(20));
            Assert.AreEqual(2, SynergyRules.OpenEffectLines(59));
            Assert.AreEqual(3, SynergyRules.OpenEffectLines(60));
        }

        [Test]
        public void Categories_OpenInTheAgreedOrder()
        {
            Assert.IsTrue(SynergyRules.IsCategoryOpen(SynergyCategory.Dice, 0, 0, 0));
            Assert.IsFalse(SynergyRules.IsCategoryOpen(SynergyCategory.Combat, 29, 0, 0));
            Assert.IsTrue(SynergyRules.IsCategoryOpen(SynergyCategory.Combat, 30, 0, 0));
            Assert.IsFalse(SynergyRules.IsCategoryOpen(SynergyCategory.Action, 0, 1999, 0));
            Assert.IsTrue(SynergyRules.IsCategoryOpen(SynergyCategory.Action, 0, 2000, 0));
            Assert.IsFalse(SynergyRules.IsCategoryOpen(SynergyCategory.Unified, 0, 0, 99));
            Assert.IsTrue(SynergyRules.IsCategoryOpen(SynergyCategory.Unified, 0, 0, 100));
        }

        // ── 카운터 ────────────────────────────────────────────

        [Test]
        public void Counter_CarriesTheRemainderAndCapsBurstFires()
        {
            var counter = new SynergyCounter(300);
            Assert.AreEqual(0, counter.Add(250));
            Assert.AreEqual(1, counter.Add(100));
            Assert.AreEqual(50, counter.Progress);

            Assert.AreEqual(SynergyRules.MaxFiresPerNotify, counter.Add(300 * 20));
            Assert.Less(counter.Progress, counter.Threshold);
        }

        // ── 세이브 ────────────────────────────────────────────

        [Test]
        public void Save_RoundTripsPresetsAndCounters()
        {
            var original = new SaveState();
            original.Synergy.TotalFires = 42;
            var category = new SynergyCategorySave { EquippedPreset = 2, BestSpent = 130, Rerolls = 7 };
            var preset = new SynergyPresetSave
            {
                Condition = new SynergyLineSave { Option = "ElementAttack", Target = "Fire", Value = 12, Locked = true },
            };
            preset.Effects.Add(new SynergyLineSave { Option = "GrantSp", Value = 30 });
            preset.Effects.Add(new SynergyLineSave());
            category.Presets.Add(preset);
            original.Synergy.Categories["Dice"] = category;

            SaveState restored = SaveSerializer.Deserialize(SaveSerializer.Serialize(original));

            Assert.AreEqual(42, restored.Synergy.TotalFires);
            SynergyCategorySave back = restored.Synergy.Categories["Dice"];
            Assert.AreEqual(2, back.EquippedPreset);
            Assert.AreEqual(130, back.BestSpent);
            Assert.AreEqual(7, back.Rerolls);
            Assert.AreEqual("ElementAttack", back.Presets[0].Condition.Option);
            Assert.AreEqual("Fire", back.Presets[0].Condition.Target);
            Assert.AreEqual(12, back.Presets[0].Condition.Value);
            Assert.IsTrue(back.Presets[0].Condition.Locked);
            Assert.AreEqual(2, back.Presets[0].Effects.Count, "빈 줄도 자리를 지킨다");
            Assert.AreEqual("GrantSp", back.Presets[0].Effects[0].Option);
            Assert.AreEqual(string.Empty, back.Presets[0].Effects[1].Option);
        }

        [Test]
        public void Describe_FillsEveryPlaceholder()
        {
            SynergyOption buff = SynergyCatalog.GetEffect(SynergyEffectId.ElementBuffStack);
            string text = SynergyRules.Describe(buff, 12, "불");
            Assert.AreEqual("8초간 불 속성 공격력 12% 증가(5회 중첩)", text);
        }
    }
}
