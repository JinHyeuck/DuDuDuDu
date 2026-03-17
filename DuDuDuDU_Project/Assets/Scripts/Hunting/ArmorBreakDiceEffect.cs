using UnityEngine;

namespace OJ
{
    public class ArmorBreakDiceEffect : DiceEffectBase
    {
        private const float DefenseDownDuration = 4f;

        public override DiceType DiceType => DiceType.ArmorBreak;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            int percent = DiceMetaDataProvider.GetArmorBreakPercent(level);
            target.ApplyDefenseDown(DefenseDownDuration, percent);
            if (level >= 12)
                target.ApplyArmorBreakDamageTakenBonus(10, DefenseDownDuration);
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
