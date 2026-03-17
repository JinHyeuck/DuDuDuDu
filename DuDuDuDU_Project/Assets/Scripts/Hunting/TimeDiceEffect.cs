using UnityEngine;

namespace OJ
{
    public class TimeDiceEffect : DiceEffectBase
    {
        private int _lastCastFrame = -1;

        public override DiceType DiceType => DiceType.Time;
        public override bool ShouldApplyDamage => false;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (_lastCastFrame == Time.frameCount)
                return;

            _lastCastFrame = Time.frameCount;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float reducePercent = DiceMetaDataProvider.GetTimeCooldownReducePercent(level);
            int targetCount = DiceMetaDataProvider.GetTimeTargetCount(level);
            PlayerController.Instance?.ReduceRemainingCooldownPercentForOtherDice(reducePercent, targetCount);

            if (target != null)
                PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
