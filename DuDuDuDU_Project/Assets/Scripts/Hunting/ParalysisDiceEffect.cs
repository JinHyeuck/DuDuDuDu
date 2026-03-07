using UnityEngine;

namespace OJ
{
    public class ParalysisDiceEffect : DiceEffectBase
    {
        private const float ParalysisDuration = 1.1f;

        public override DiceType DiceType => DiceType.Paralysis;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float duration = ParalysisDuration + Mathf.Max(0, level - 1) * 0.02f;
            target.ApplyParalysis(duration);
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
