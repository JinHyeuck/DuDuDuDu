using UnityEngine;

namespace OJ
{
    public class ArmorBreakDiceEffect : DiceEffectBase
    {
        private const float DefenseDownDuration = 4f;
        private const int DefenseDownAmount = 20;

        public override DiceType DiceType => DiceType.ArmorBreak;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float duration = DefenseDownDuration + Mathf.Max(0, level - 1) * 0.1f;
            int amount = DefenseDownAmount + Mathf.Max(0, level / 3);

            target.ApplyDefenseDown(duration, amount);
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
