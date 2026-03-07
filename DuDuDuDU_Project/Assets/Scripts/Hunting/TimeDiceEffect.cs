using UnityEngine;

namespace OJ
{
    public class TimeDiceEffect : DiceEffectBase
    {
        private const float CooldownReduceSeconds = 2f;
        private int _lastCastFrame = -1;

        public override DiceType DiceType => DiceType.Time;
        public override bool ShouldApplyDamage => false;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (_lastCastFrame == Time.frameCount)
                return;

            _lastCastFrame = Time.frameCount;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float reduce = CooldownReduceSeconds + (level >= 9 ? 1f : 0f) + Mathf.Max(0, level - 1) * 0.05f;
            PlayerController.Instance?.ReduceCooldownForOtherDice(reduce);

            if (target != null)
                PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
