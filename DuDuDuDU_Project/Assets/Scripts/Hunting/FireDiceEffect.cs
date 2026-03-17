using System.Collections.Generic;

namespace OJ
{
    public class FireDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Fire;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            List<Monster> fireTargets = new List<Monster>();
            float explosionRange = 1f;
            int fireHitTargetCount = 10;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;

            explosionRange *= DiceMetaDataProvider.GetFireExplosionRangeMultiplier(level);

            if (EquipmentManager.Instance != null)
            {
                explosionRange *= (1f + EquipmentManager.Instance.GetFireExplosionRangeBonus(DiceType));
                fireHitTargetCount += EquipmentManager.Instance.GetFireExplosionExtraTargetCount(DiceType);
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
                    fireTargets.Add(monsters[hitIdx]);

                if (level >= 6 && UnityEngine.Random.value <= 0.2f)
                {
                    for (int hitIdx = 0; hitIdx < monsters.Count; ++hitIdx)
                        fireTargets.Add(monsters[hitIdx]);
                }
            }

            hitMonsters.AddRange(fireTargets);
        }
    }
}
