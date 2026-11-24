using NUnit.Framework;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 승급 효과의 순수 규칙. 전투 코드(Hunting)는 헤드리스에서 돌지 않아 여기서 수치와 산술만 잠근다.
    /// 효과가 실제로 맞게 터지는지는 플레이로 본다.
    /// </summary>
    public class PromotionEffectRulesTests
    {
        [TestCase(0, 1f)]
        [TestCase(1, 1.2f)]
        [TestCase(2, 1.44f)]
        [TestCase(3, 1.728f)]
        public void DamageMultiplierGrowsTwentyPercentPerTier(int tier, float expected)
        {
            Assert.AreEqual(expected, PromotionEffectRules.DamageMultiplier(tier), 0.0001f);
        }

        [Test]
        public void DamageMultiplierStopsAtTierThree()
        {
            Assert.AreEqual(PromotionEffectRules.DamageMultiplier(3), PromotionEffectRules.DamageMultiplier(9), 0.0001f);
            Assert.AreEqual(1f, PromotionEffectRules.DamageMultiplier(-2), 0.0001f);
        }

        [TestCase(0, 0, 0)]
        [TestCase(0, 1, 3)]
        [TestCase(0, 5, 15)]
        [TestCase(21, 3, 30)]
        [TestCase(9, 100, 30)]
        [TestCase(12, 0, 12)]
        [TestCase(-5, 1, 3)]
        public void KingFireStackAddsThreePerTargetUpToThirty(int current, int hits, int expected)
        {
            Assert.AreEqual(expected, PromotionEffectRules.KingFireStackAfter(current, hits));
        }

        [TestCase(1, 5, false)]
        [TestCase(4, 5, false)]
        [TestCase(5, 5, true)]
        [TestCase(6, 5, false)]
        [TestCase(10, 5, true)]
        [TestCase(0, 5, false)]
        [TestCase(5, 0, false)]
        public void EveryNthTriggersOnMultiples(int count, int n, bool expected)
        {
            Assert.AreEqual(expected, PromotionEffectRules.IsEveryNth(count, n));
        }

        [Test]
        public void KeyNumbersMatchThePlan()
        {
            Assert.AreEqual(0.5f, PromotionEffectRules.NormalPierceRatio);
            Assert.AreEqual(5, PromotionEffectRules.FireBigExplosionMinTargets);
            Assert.AreEqual(1.3f, PromotionEffectRules.FireBigExplosionMultiplier);
            Assert.AreEqual(0.2f, PromotionEffectRules.IceFreezeChance);
            Assert.AreEqual(0.5f, PromotionEffectRules.IceFreezeSeconds);
            Assert.AreEqual(20, PromotionEffectRules.TornadoCenterDamageTakenPercent);
            Assert.AreEqual(1.5f, PromotionEffectRules.TornadoSlowAfterSeconds);
            Assert.AreEqual(5, PromotionEffectRules.TornadoMassPullCount);
            Assert.AreEqual(0.3f, PromotionEffectRules.TornadoMassPullCooldownCut);
            Assert.AreEqual(0.2f, PromotionEffectRules.ArmorBreakExtraChance);
            Assert.AreEqual(0.5f, PromotionEffectRules.ArmorBreakExtraRatio);
            Assert.AreEqual(2, PromotionEffectRules.WindSplashCount);
            Assert.AreEqual(1.3f, PromotionEffectRules.TimeNextShotMultiplier);
            Assert.AreEqual(3, PromotionEffectRules.KingIceChainTargetsBase);
            Assert.AreEqual(5, PromotionEffectRules.KingIceChainTargetsT2);
            Assert.AreEqual(5, PromotionEffectRules.KingThunderEveryNth);
            Assert.AreEqual(3, PromotionEffectRules.KingThunderBonusTargets);
        }
    }
}
