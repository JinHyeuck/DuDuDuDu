using System.Collections.Generic;
using OJ.Dice;

using OJ.DI;

namespace OJ.Hunting
{
    public class KingIceDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public KingIceDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.KingIce;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            List<Monster> iceTargets = new List<Monster>();
            for (int i = 0; i < hitMonsters.Count; i++)
            {
                Monster target = hitMonsters[i];
                if (target == null)
                    continue;

                List<Monster> nearby = attackContent.GetRedHitTarget(
                    target.transform.position,
                    IFFType.IFF_Friend,
                    1.2f,
                    3,
                    target);

                for (int n = 0; n < nearby.Count; n++)
                    iceTargets.Add(nearby[n]);
            }

            hitMonsters.AddRange(iceTargets);
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null)
                return;

            PlayEffectAt(DiceType, target.transform.position);

            if (target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float duration = DiceMetaDataProvider.GetSlowDuration(DiceType, level);
            target.ApplySlow(duration);
            target.ApplySlow(duration);
            if (level >= 9)
                target.ApplyStun(1f);
        }
    }
}
