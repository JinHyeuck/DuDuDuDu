using System.Collections.Generic;

namespace OJ
{
    public class KingNormalDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.KingNormal;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            List<Monster> bonusTargets = new List<Monster>();
            for (int i = 0; i < hitMonsters.Count; i++)
            {
                Monster target = hitMonsters[i];
                if (target == null)
                    continue;

                List<Monster> nearby = attackContent.GetRedHitTarget(
                    target.transform.position,
                    IFFType.IFF_Friend,
                    1.3f,
                    3,
                    target);

                for (int n = 0; n < nearby.Count; n++)
                    bonusTargets.Add(nearby[n]);
            }

            hitMonsters.AddRange(bonusTargets);
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null)
                return;

            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
