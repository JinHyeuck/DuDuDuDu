using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class TornadoDiceEffect : DiceEffectBase
    {
        private const float PullRange = 1.8f;
        private const int MaxTargets = 10;
        private const float PullDistancePerHit = 0.55f;

        public override DiceType DiceType => DiceType.Tornado;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (attackContent == null || target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float pullRange = PullRange + (level >= 7 ? 0.3f : 0f);
            int maxTargets = MaxTargets + Mathf.Max(0, level / 8);
            float pullDistance = PullDistancePerHit + Mathf.Max(0, level - 1) * 0.02f;

            List<Monster> around = attackContent.GetRedHitTarget(
                target.transform.position,
                IFFType.IFF_Friend,
                pullRange,
                maxTargets,
                target);

            Vector2 center = target.transform.position;
            for (int i = 0; i < around.Count; i++)
            {
                Monster monster = around[i];
                if (monster == null || monster.gameObject.activeInHierarchy == false)
                    continue;

                monster.PullTowards(center, pullDistance);
            }

            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
