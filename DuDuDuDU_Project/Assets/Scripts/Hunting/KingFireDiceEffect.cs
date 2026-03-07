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
                    fireTargets.Add(monsters[hitIdx]);
            }

            hitMonsters.AddRange(fireTargets);
        }
    }
}
