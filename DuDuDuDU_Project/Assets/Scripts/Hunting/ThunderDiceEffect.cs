using System.Collections.Generic;
using OJ.DI;
using UnityEngine;

namespace OJ.Hunting
{
    public class ThunderDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 만드는 순수 C# 클래스라
        //       [Inject] 필드를 쓸 수 없다. 그래서 창구를 생성자로 넘겨받는다.
        //       창구 필드(battle)는 기반 클래스 DiceEffectBase 가 protected 로 이미 들고 있으므로
        //       여기서는 base 로 넘기기만 한다 — 같은 이름의 필드를 다시 선언하면 기반 필드를 가려서
        //       기반의 PlayEffectAt / PlayLineEffect 와 서로 다른 창구를 보게 된다.
        //       이 효과는 전투 중에만 만들어지고 불리므로 창구 뒤의 매니저는 null 이 될 수 없다.
        //       따라서 battle.Monsters / battle.BulletEffects 에 새 ?. 를 붙이지 않는다.
        public ThunderDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.Thunder;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            int thunderTargets = attackContent.GetThunderTargetCount(DiceType);
            Dictionary<Monster, List<Monster>> chainedByTarget = attackContent.GetNPerTarget_NoGlobalDup(
                battle.Monsters.activeMonsters,
                hitMonsters,
                thunderTargets);

            foreach (var pair in chainedByTarget)
            {
                PlayEffectAt(DiceType, pair.Key.transform.position);

                for (int i = 0; i < pair.Value.Count; ++i)
                {
                    Monster chained = pair.Value[i];

                    // 풀이 반환하는 BulletEffect 는 여분이 없으면 null 일 수 있으므로 이 검사는 유지한다.
                    // (창구가 null 인지 보는 검사가 아니다)
                    BulletEffect chain = battle.BulletEffects.GetBullet(DiceType, EffectID.C1);
                    if (chain != null)
                        chain.PlayLineEffect(pair.Key.transform.position, chained.transform.position);

                    PlayEffectAt(DiceType, chained.transform.position);
                    hitMonsters.Add(chained);
                }
            }
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (attackContent == null || target == null)
                return;

            if (attackContent.CurrentDiceLevel < 12)
                return;

            Vector3 center = target.transform.position;
            List<Monster> nearby = attackContent.GetRedHitTarget(
                center,
                IFFType.IFF_Friend,
                1.2f,
                1,
                null);

            if (nearby.Count <= 0 || nearby[0] == null || nearby[0] == target)
                return;

            int splashDamage = Mathf.Max(1, Mathf.RoundToInt(attackContent.CurrentDamage * 0.5f));
            PlayLineEffect(DiceType, center, nearby[0].transform.position, EffectID.C1);
            attackContent.HitMonster(nearby[0], DiceType, splashDamage);
            PlayEffectAt(DiceType, nearby[0].transform.position);
        }
    }
}
