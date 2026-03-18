using System.Collections.Generic;

namespace OJ
{
    public class KingFireDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.KingFire;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
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

                if (level >= 9 && UnityEngine.Random.value <= 0.3f)
                {
                    for (int hitIdx = 0; hitIdx < monsters.Count; ++hitIdx)
                    {
                        Monster splashTarget = monsters[hitIdx];
                        if (splashTarget == null || splashTarget == target)
                            continue;

                        fireTargets.Add(splashTarget);
                    }
                }
            }

            hitMonsters.AddRange(fireTargets);
        }
    }
}
