using System.Collections.Generic;

namespace OJ
{
    public class ThunderDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Thunder;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            int thunderTargets = attackContent.GetThunderTargetCount();
            Dictionary<Monster, List<Monster>> chainedByTarget = attackContent.GetNPerTarget_NoGlobalDup(
                MonsterManager.Instance.activeMonsters,
                hitMonsters,
                thunderTargets);

            foreach (var pair in chainedByTarget)
            {
                PlayEffectAt(DiceType, pair.Key.transform.position);

                for (int i = 0; i < pair.Value.Count; ++i)
                {
                    Monster chained = pair.Value[i];

                    BulletEffect chain = BulletEffectPool.Instance.GetBullet(DiceType, EffectID.C1);
                    if (chain != null)
                        chain.PlayLineEffect(pair.Key.transform.position, chained.transform.position);

                    PlayEffectAt(DiceType, chained.transform.position);
                    hitMonsters.Add(chained);
                }
            }
        }
    }
}
