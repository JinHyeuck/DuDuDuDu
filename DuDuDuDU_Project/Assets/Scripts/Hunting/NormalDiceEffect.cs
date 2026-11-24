using OJ.DI;

namespace OJ.Hunting
{
    public class NormalDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public NormalDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.Normal;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null)
                return;

            PlayEffectAt(DiceType, target.transform.position);

            // 승급 T1: 공격이 대상을 관통해 근처의 적 1명에게 50% 피해.
            if (attackContent != null && target == attackContent.CurrentRootTarget && OJ.Dice.PromotionManager.TierOf(DiceType) >= 1)
            {
                Monster behind = PromotionCombat.Nearest(battle, target.transform.position, OJ.Core.PromotionEffectRules.NormalPierceRadius, target);
                if (behind != null)
                    attackContent.HitMonster(behind, DiceType, UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(attackContent.CurrentDamage * OJ.Core.PromotionEffectRules.NormalPierceRatio)));
            }
        }
    }
}
