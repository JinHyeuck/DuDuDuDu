using UnityEngine;
using OJ.DI;
using OJ.Dice;

namespace OJ.Hunting
{
    public class TimeDiceEffect : DiceEffectBase
    {
        private int _lastCastFrame = -1;

        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스(DiceEffectBase)가 protected 로
        // 들고 있으니 여기서 새로 만들지 않고 base 로 넘기기만 한다.
        public TimeDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.Time;
        public override bool ShouldApplyDamage => false;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (_lastCastFrame == Time.frameCount)
                return;

            _lastCastFrame = Time.frameCount;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float reducePercent = DiceMetaDataProvider.GetTimeCooldownReducePercent(DiceType, level);
            int targetCount = DiceMetaDataProvider.GetTimeTargetCount(level);
            // 8.3b: 이 객체는 AttackContent 가 BattleScene 안에서만 new 하므로 전투 밖에는 존재하지 않는다.
            // 즉 PlayerController 는 여기서 null 이 될 수 없어 기존 ?. 를 지운다 —
            // 남겨두면 창구가 비었을 때 쿨타임 감소가 조용히 사라져 버그를 감춘다.
            battle.Player.ReduceRemainingCooldownPercentForOtherDice(reducePercent, targetCount);

            if (target != null)
                PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
