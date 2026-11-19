using UnityEngine;
using OJ.Dice;
using OJ.Relic;

using OJ.DI;

namespace OJ.Hunting
{
    public class ArmorBreakDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public ArmorBreakDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        private const float DefenseDownDuration = 4f;

        public override DiceType DiceType => DiceType.ArmorBreak;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            int percent = DiceMetaDataProvider.GetArmorBreakPercent(level);
            float duration = DefenseDownDuration;
            if (RelicManager.Instance != null)
            {
                percent += RelicManager.Instance.GetArmorBreakPercentBonus();
                duration += RelicManager.Instance.GetArmorBreakDurationBonus();
            }
            // 승급 T3: 보스에게는 방깎 효과 2배.
            int armorTier = OJ.Dice.PromotionManager.TierOf(DiceType);
            if (armorTier >= 3 && target.IsStageBoss)
                percent *= OJ.Core.PromotionEffectRules.ArmorBreakBossMultiplier;

            bool wasBroken = target.IsDefenseDown;
            target.ApplyDefenseDown(duration, percent, DiceType);

            // 승급 T1: 방깎이 걸린 적에게 명중하면 20% 확률로 방어 무시 추가 일격(피해 50%).
            if (wasBroken && armorTier >= 1 && attackContent != null && UnityEngine.Random.value <= OJ.Core.PromotionEffectRules.ArmorBreakExtraChance)
                attackContent.HitMonsterFixed(target, DiceType, UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(attackContent.CurrentDamage * OJ.Core.PromotionEffectRules.ArmorBreakExtraRatio)));
            if (level >= OJ.Core.DiceMilestoneLevels.Third)
                target.ApplyArmorBreakDamageTakenBonus(10, duration);
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
