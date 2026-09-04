using System.Collections.Generic;
using OJ.Dice;

using OJ.DI;

namespace OJ.Hunting
{
    public class KingPoisonDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public KingPoisonDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.KingPoison;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null)
                return;

            var impactPosition = target.transform.position;
            PlayEffectAt(DiceType, impactPosition);

            if (target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            target.ApplyPoison(DiceMetaDataProvider.GetPoisonDuration(DiceType), 1f);
            target.ApplySlow(DiceMetaDataProvider.GetSlowDuration(DiceType, level));
            if (level >= 9 && UnityEngine.Random.value <= 0.3f && attackContent != null)
            {
                List<Monster> nearby = attackContent.GetRedHitTarget(
                    impactPosition,
                    IFFType.IFF_Friend,
                    1.1f,
                    1,
                    null);

                if (nearby.Count > 0 && nearby[0] != null && nearby[0] != target)
                    nearby[0].ApplyPoison(DiceMetaDataProvider.GetPoisonDuration(DiceType), 1f);
            }
        }
    }
}
