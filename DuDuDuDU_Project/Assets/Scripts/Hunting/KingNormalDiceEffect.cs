using System.Collections.Generic;

using OJ.DI;

namespace OJ.Hunting
{
    public class KingNormalDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public KingNormalDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

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
