using System.Collections.Generic;

namespace OJ
{
    public class KingIceDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.KingIce;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            List<Monster> iceTargets = new List<Monster>();
            for (int i = 0; i < hitMonsters.Count; i++)
            {
                Monster target = hitMonsters[i];
                if (target == null)
                    continue;

                List<Monster> nearby = attackContent.GetRedHitTarget(
                    target.transform.position,
                    IFFType.IFF_Friend,
                    1.2f,
                    3,
                    target);

                for (int n = 0; n < nearby.Count; n++)
                    iceTargets.Add(nearby[n]);
            }

            hitMonsters.AddRange(iceTargets);
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            target.ApplySlow();
            target.ApplySlow();
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
