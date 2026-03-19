using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class FireDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Fire;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            List<Monster> fireTargets = new List<Monster>();
            float explosionRange = 1f;
            int fireHitTargetCount = 10;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;

            explosionRange *= DiceMetaDataProvider.GetFireExplosionRangeMultiplier(level);

            if (EquipmentManager.Instance != null)
            {
                explosionRange *= (1f + EquipmentManager.Instance.GetFireExplosionRangeBonus(DiceType));
                fireHitTargetCount += EquipmentManager.Instance.GetFireExplosionExtraTargetCount(DiceType);
            }

            for (int i = 0; i < hitMonsters.Count; ++i)
            {
                Monster target = hitMonsters[i];
                if (target == null)
                    continue;

                List<Monster> monsters = attackContent.GetRedHitTarget(
                    target.transform.position,
                    IFFType.IFF_Friend,
                    explosionRange,
                    fireHitTargetCount,
                    target);

                PlayEffectAt(DiceType, target.transform.position);

                for (int hitIdx = 0; hitIdx < monsters.Count; ++hitIdx)
                {
                    Monster splashTarget = monsters[hitIdx];
                    if (splashTarget == null || splashTarget == target)
                        continue;

                    fireTargets.Add(splashTarget);
                }
            }

            hitMonsters.AddRange(fireTargets);
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (attackContent == null || target == null || target.gameObject.activeInHierarchy == false)
                return;

            if (target != attackContent.CurrentRootTarget)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            if (level < 6 || Random.value > 0.2f)
                return;

            attackContent.StartCoroutine(CoDelayedExplosion(attackContent, target.transform.position, attackContent.CurrentDamage, level));
        }

        private IEnumerator CoDelayedExplosion(AttackContent attackContent, Vector3 center, int damage, int level)
        {
            yield return new WaitForSeconds(0.5f);

            if (attackContent == null)
                yield break;

            float explosionRange = 1f * DiceMetaDataProvider.GetFireExplosionRangeMultiplier(level);
            int fireHitTargetCount = 10;
            if (EquipmentManager.Instance != null)
            {
                explosionRange *= (1f + EquipmentManager.Instance.GetFireExplosionRangeBonus(DiceType));
                fireHitTargetCount += EquipmentManager.Instance.GetFireExplosionExtraTargetCount(DiceType);
            }

            List<Monster> monsters = attackContent.GetRedHitTarget(
                center,
                IFFType.IFF_Friend,
                explosionRange,
                fireHitTargetCount,
                null);

            PlayEffectAt(DiceType, center);

            for (int i = 0; i < monsters.Count; i++)
            {
                Monster splashTarget = monsters[i];
                if (splashTarget == null || splashTarget.gameObject.activeInHierarchy == false)
                    continue;

                attackContent.HitMonster(splashTarget, DiceType, damage);
            }
        }
    }
}
