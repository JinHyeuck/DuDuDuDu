using System.Collections.Generic;
using OJ.DI;
using UnityEngine;
using OJ.Dice;

namespace OJ.Hunting
{
    public class KingThunderDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 만드는 순수 C# 클래스라
        //       [Inject] 필드를 쓸 수 없다. 그래서 창구를 생성자로 넘겨받는다.
        //       창구 필드는 기반(DiceEffectBase)이 protected 로 이미 들고 있으므로
        //       여기서 새로 만들지 않고 base 로 넘기기만 한다 — 필드가 둘이면 기반의
        //       PlayEffectAt / PlayLineEffect 가 보는 창구와 여기서 보는 창구가 갈라진다.
        public KingThunderDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.KingThunder;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            int thunderTargets = attackContent.GetThunderTargetCount(DiceType.Thunder) + 2;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            if (level >= 3)
                thunderTargets += 2;
            // 이 효과는 전투 중에만 만들어지고 불리므로 창구 뒤의 매니저는 null 이 될 수 없다.
            // 그래서 battle.Monsters 에 새 ?. 를 붙이지 않는다 — 비어 있으면 조용히 넘어가는 대신 울어야 한다.
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

                    // 아래 null 검사는 풀이 비어 돌려줄 이펙트가 없을 때를 막는 것이지 창구를 막는 것이 아니다.
                    // 그래서 그대로 둔다 — 지우면 풀 고갈 시 동작이 바뀐다.
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

            Vector3 center = target.transform.position;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            if (level >= 12 && target.gameObject.activeInHierarchy)
                target.ApplyThunderDamageTakenBonus(15, 5f);

            if (level >= 9 && UnityEngine.Random.value <= 0.3f)
            {
                List<Monster> nearby = attackContent.GetRedHitTarget(
                    center,
                    IFFType.IFF_Friend,
                    1.2f,
                    1,
                    null);

                if (nearby.Count > 0 && nearby[0] != null && nearby[0] != target)
                {
                    PlayLineEffect(DiceType, center, nearby[0].transform.position, EffectID.C1);
                    attackContent.HitMonster(nearby[0], DiceType, Mathf.Max(1, Mathf.RoundToInt(attackContent.CurrentDamage * 0.5f)));
                }
            }
        }
    }
}
