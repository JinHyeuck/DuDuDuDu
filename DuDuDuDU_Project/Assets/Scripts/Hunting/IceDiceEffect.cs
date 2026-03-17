using System.Collections.Generic;

namespace OJ
{
    public class IceDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Ice;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            target.ApplySlow(DiceMetaDataProvider.GetSlowDuration(DiceType, level));
            PlayEffectAt(DiceType, target.transform.position);

            if (level < 6 || UnityEngine.Random.value > 0.3f)
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
            }
        }
    }
}
