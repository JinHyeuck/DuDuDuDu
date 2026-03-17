using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class ThunderDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Thunder;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            int thunderTargets = attackContent.GetThunderTargetCount(DiceType);
            Dictionary<Monster, List<Monster>> chainedByTarget = attackContent.GetNPerTarget_NoGlobalDup(
                MonsterManager.Instance.activeMonsters,
                hitMonsters,
                thunderTargets);

            foreach (var pair in chainedByTarget)
            {
                PlayEffectAt(DiceType, pair.Key.transform.position);

                for (int i = 0; i < pair.Value.Count; ++i)
                {
                    Monster chained = pair.Value[i];

                    BulletEffect chain = BulletEffectPool.Instance.GetBullet(DiceType, EffectID.C1);
                    if (chain != null)
                        chain.PlayLineEffect(pair.Key.transform.position, chained.transform.position);

                    PlayEffectAt(DiceType, chained.transform.position);
                    hitMonsters.Add(chained);
                }
            }
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (attackContent == null || target == null || target.gameObject.activeInHierarchy == false)
                return;

            if (attackContent.CurrentDiceLevel < 12)
                return;

            List<Monster> nearby = attackContent.GetRedHitTarget(
                target.transform.position,
                IFFType.IFF_Friend,
                1.2f,
                1,
                target);

            if (nearby.Count <= 0 || nearby[0] == null)
                return;

            int splashDamage = Mathf.Max(1, Mathf.RoundToInt(attackContent.CurrentDamage * 0.5f));
            attackContent.HitMonster(nearby[0], DiceType, splashDamage);
            PlayEffectAt(DiceType, nearby[0].transform.position);
        }
    }
}
