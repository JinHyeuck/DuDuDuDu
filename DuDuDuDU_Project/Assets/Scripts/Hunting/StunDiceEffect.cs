using UnityEngine;

namespace OJ
{
    public class StunDiceEffect : DiceEffectBase
    {
        private const float StunDuration = 1.1f;

        public override DiceType DiceType => DiceType.Stun;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float chancePercent = DiceMetaDataProvider.GetStunChancePercent(level);
            if (RelicManager.Instance != null)
                chancePercent += RelicManager.Instance.GetStunChanceBonusPercent();
            if (Random.value * 100f > chancePercent)
                return;

            float duration = StunDuration;
            target.ApplyStun(duration);
            if (level >= 12)
                target.ApplyStunDamageTakenBonus(20, duration);
            if (RelicManager.Instance != null)
                target.ApplyRelicDamageTakenBonus(RelicManager.Instance.GetStunDamageTakenBonusPercent(), duration);
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
