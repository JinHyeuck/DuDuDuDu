using System.Collections.Generic;

namespace OJ
{
    public class KingNormalDiceEffect : DiceEffectBase
    {
        public const float SplashRadius = 1.3f;
        public const int AdditionalTargetCount = 3;
        public const int TotalHitCount = 4;
        public const float MultiHitInterval = 0.1f;

        public override DiceType DiceType => DiceType.KingNormal;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null || rootTarget == null)
                return;

            hitMonsters.Clear();
            hitMonsters.Add(rootTarget);

            List<Monster> nearby = attackContent.GetRedHitTarget(
                rootTarget.transform.position,
                IFFType.IFF_Friend,
                SplashRadius,
                AdditionalTargetCount,
                null);

            HashSet<Monster> uniqueTargets = new HashSet<Monster>(hitMonsters);
            for (int i = 0; i < nearby.Count; i++)
            {
                Monster splashTarget = nearby[i];
                if (splashTarget == null)
                    continue;

                if (uniqueTargets.Add(splashTarget))
                    hitMonsters.Add(splashTarget);
            }
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
        }

        public void PlayImpactEffect(UnityEngine.Vector3 position)
        {
            PlayEffectAt(DiceType, position);
        }
    }
}
