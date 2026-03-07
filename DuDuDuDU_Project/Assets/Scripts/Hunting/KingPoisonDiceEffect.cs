using System.Collections.Generic;

namespace OJ
{
    public class KingPoisonDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.KingPoison;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            List<Monster> poisonTargets = new List<Monster>();
            for (int i = 0; i < hitMonsters.Count; i++)
            {
                Monster target = hitMonsters[i];
                if (target == null)
                    continue;

                List<Monster> nearby = attackContent.GetRedHitTarget(
                    target.transform.position,
                    IFFType.IFF_Friend,
                    1.1f,
                    2,
                    target);

                for (int n = 0; n < nearby.Count; n++)
                    poisonTargets.Add(nearby[n]);
            }

            hitMonsters.AddRange(poisonTargets);
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            target.ApplyPoison();
            target.ApplySlow();
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
