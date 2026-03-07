using System.Collections.Generic;

namespace OJ
{
    public class KingMixedDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.KingMixed;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            int thunderTargets = attackContent.GetThunderTargetCount() + 1;
            Dictionary<Monster, List<Monster>> chainedByTarget = attackContent.GetNPerTarget_NoGlobalDup(
                MonsterManager.Instance.activeMonsters,
                hitMonsters,
                thunderTargets);

            foreach (var pair in chainedByTarget)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Monster chained = pair.Value[i];
                    BulletEffect chain = BulletEffectPool.Instance.GetBullet(DiceType.Thunder, EffectID.C1);
                    if (chain != null)
                        chain.PlayLineEffect(pair.Key.transform.position, chained.transform.position);

                    hitMonsters.Add(chained);
                }
            }

            List<Monster> mixedTargets = new List<Monster>();
            for (int i = 0; i < hitMonsters.Count; i++)
            {
                Monster target = hitMonsters[i];
                if (target == null)
                    continue;

                List<Monster> splash = attackContent.GetRedHitTarget(
                    target.transform.position,
                    IFFType.IFF_Friend,
                    1.4f,
                    12,
                    target);

                for (int s = 0; s < splash.Count; s++)
                    mixedTargets.Add(splash[s]);
            }

            hitMonsters.AddRange(mixedTargets);
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            target.ApplySlow();
            target.ApplyPoison();

            PlayEffectAt(DiceType.Normal, target.transform.position);
            PlayEffectAt(DiceType.Fire, target.transform.position);
            PlayEffectAt(DiceType.Ice, target.transform.position);
            PlayEffectAt(DiceType.Poison, target.transform.position);
            PlayEffectAt(DiceType.Thunder, target.transform.position);
        }
    }
}
