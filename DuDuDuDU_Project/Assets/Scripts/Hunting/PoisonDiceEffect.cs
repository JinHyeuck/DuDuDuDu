using System.Collections.Generic;

namespace OJ
{
    public class PoisonDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Poison;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            target.ApplyPoison(
                DiceMetaDataProvider.GetPoisonDuration(DiceType),
                DiceMetaDataProvider.GetPoisonDamageMultiplier(DiceType, level));
            if (level >= 12)
                target.ApplyPoisonDamageTakenBonus(10);
            PlayEffectAt(DiceType, target.transform.position);

            if (level < 9 || UnityEngine.Random.value > 0.4f)
                return;

            List<Monster> around = attackContent.GetRedHitTarget(
                target.transform.position,
                IFFType.IFF_Friend,
                1.1f,
                -1,
                target);

            for (int i = 0; i < around.Count; i++)
            {
                Monster splashTarget = around[i];
                if (splashTarget == null || splashTarget.gameObject.activeInHierarchy == false)
                    continue;

                attackContent.HitMonster(splashTarget, DiceType, attackContent.CurrentDamage);
                splashTarget.ApplyPoison(
                    DiceMetaDataProvider.GetPoisonDuration(DiceType),
                    DiceMetaDataProvider.GetPoisonDamageMultiplier(DiceType, level));
                if (level >= 12)
                    splashTarget.ApplyPoisonDamageTakenBonus(10);
            }
        }
    }
}
