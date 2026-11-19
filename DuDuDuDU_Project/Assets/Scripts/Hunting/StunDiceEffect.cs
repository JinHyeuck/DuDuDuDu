using UnityEngine;
using OJ.Dice;
using OJ.Relic;

using OJ.DI;

namespace OJ.Hunting
{
    public class StunDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public StunDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        private const float StunDuration = 1.1f;

        public override DiceType DiceType => DiceType.Stun;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float chancePercent = DiceMetaDataProvider.GetStunChancePercent(level);
            if (RelicManager.Instance != null)
                chancePercent += RelicManager.Instance.GetStunChanceBonusPercent();
            if (Random.value * 100f > chancePercent)
                return;

            // 승급 T2: 기절 지속시간 +50%.
            float duration = PromotionCombat.StunDiceEffectDuration(OJ.Dice.PromotionManager.TierOf(DiceType));
            target.ApplyStun(duration, DiceType);
            // 이 기절을 건 다이스를 적어 둔다 — 죽을 때 주변 전이(T1)와 쿨타임 초기화(T3)가 읽는다.
            target.StunSourceDice = attackContent != null ? attackContent.CurrentSourceDice : null;
            if (level >= OJ.Core.DiceMilestoneLevels.Third)
                target.ApplyStunDamageTakenBonus(20, duration);
            if (RelicManager.Instance != null)
                target.ApplyRelicDamageTakenBonus(RelicManager.Instance.GetStunDamageTakenBonusPercent(), duration);
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
