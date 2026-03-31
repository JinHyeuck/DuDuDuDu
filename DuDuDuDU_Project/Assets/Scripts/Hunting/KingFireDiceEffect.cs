using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class KingFireDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.KingFire;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            List<Monster> fireTargets = new List<Monster>();
            float explosionRange = 1.6f;
            int fireHitTargetCount = 14;

            if (EquipmentManager.Instance != null)
            {
                explosionRange *= (1f + EquipmentManager.Instance.GetFireExplosionRangeBonus(DiceType));
                fireHitTargetCount += EquipmentManager.Instance.GetFireExplosionExtraTargetCount(DiceType) + 2;
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
            if (attackContent == null || target == null)
                return;

            if (target != attackContent.CurrentRootTarget)
                return;

            Vector3 center = target.transform.position;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            if (level < 9 || Random.value > 0.3f)
                return;

            attackContent.StartCoroutine(CoDelayedExplosion(attackContent, center, attackContent.CurrentDamage));
        }

        private IEnumerator CoDelayedExplosion(AttackContent attackContent, Vector3 center, int damage)
        {
            yield return new WaitForSeconds(0.5f);

            if (attackContent == null)
                yield break;

            float explosionRange = 1.6f;
            int fireHitTargetCount = 14;
            if (EquipmentManager.Instance != null)
            {
                explosionRange *= (1f + EquipmentManager.Instance.GetFireExplosionRangeBonus(DiceType));
                fireHitTargetCount += EquipmentManager.Instance.GetFireExplosionExtraTargetCount(DiceType) + 2;
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
