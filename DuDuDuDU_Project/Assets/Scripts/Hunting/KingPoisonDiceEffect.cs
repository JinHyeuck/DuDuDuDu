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
                {
                    Monster splashTarget = nearby[n];
                    if (splashTarget == null || splashTarget == target)
                        continue;

                    poisonTargets.Add(splashTarget);
                }
            }

            hitMonsters.AddRange(poisonTargets);
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            target.ApplyPoison(DiceMetaDataProvider.GetPoisonDuration(DiceType), 1f);
            target.ApplySlow(DiceMetaDataProvider.GetSlowDuration(DiceType, level));
            if (level >= 9 && UnityEngine.Random.value <= 0.3f && attackContent != null)
            {
                List<Monster> nearby = attackContent.GetRedHitTarget(
                    target.transform.position,
                    IFFType.IFF_Friend,
                    1.1f,
                    1,
                    target);

                if (nearby.Count > 0 && nearby[0] != null)
                    nearby[0].ApplyPoison(DiceMetaDataProvider.GetPoisonDuration(DiceType), 1f);
            }
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
