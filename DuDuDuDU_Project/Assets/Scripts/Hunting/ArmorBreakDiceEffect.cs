using UnityEngine;
using OJ.Dice;
using OJ.Relic;

using OJ.DI;

namespace OJ.Hunting
{
    public class ArmorBreakDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public ArmorBreakDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        private const float DefenseDownDuration = 4f;

        public override DiceType DiceType => DiceType.ArmorBreak;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            int percent = DiceMetaDataProvider.GetArmorBreakPercent(level);
            float duration = DefenseDownDuration;
            if (RelicManager.Instance != null)
            {
                percent += RelicManager.Instance.GetArmorBreakPercentBonus();
                duration += RelicManager.Instance.GetArmorBreakDurationBonus();
            }
            target.ApplyDefenseDown(duration, percent);
            if (level >= 12)
                target.ApplyArmorBreakDamageTakenBonus(10, duration);
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
