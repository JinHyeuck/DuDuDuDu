using System.Collections.Generic;
using OJ.Dice;

using OJ.DI;

namespace OJ.Hunting
{
    public class IceDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public IceDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.Ice;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            target.ApplySlow(DiceMetaDataProvider.GetSlowDuration(DiceType, level));
            PlayEffectAt(DiceType, target.transform.position);

            if (level < 6 || UnityEngine.Random.value > 0.3f)
                return;

            List<Monster> around = attackContent.GetRedHitTarget(
                target.transform.position,
                IFFType.IFF_Friend,
                1.1f,
                -1,
                target);

            for (int i = 0; i < around.Count; i++)
            {
                Monster splashTarget = around[i];
                if (splashTarget == null || splashTarget.gameObject.activeInHierarchy == false)
                    continue;

                attackContent.HitMonster(splashTarget, DiceType, attackContent.CurrentDamage);
            }
        }
    }
}
