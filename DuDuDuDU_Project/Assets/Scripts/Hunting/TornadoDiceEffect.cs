using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class TornadoDiceEffect : DiceEffectBase
    {
        private const float PullRange = 1.8f;
        private const int MaxTargets = 10;
        private const float PullDistancePerHit = 1.2f;
        private const float MinimumLoopTime = 0.05f;

        public override DiceType DiceType => DiceType.Tornado;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (attackContent == null || target == null)
                return;

            Vector2 center = target.transform.position;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float pullRange = PullRange * (level >= 3 ? 1.1f : 1f);
            int maxTargets = MaxTargets + Mathf.Max(0, level / 8);
            float pullDistance = PullDistancePerHit + Mathf.Max(0, level - 1) * 0.02f;

            List<Monster> around = attackContent.GetRedHitTarget(
                center,
                IFFType.IFF_Friend,
                pullRange,
                maxTargets,
                target);

            float pullDuration = level >= 6 ? 2f : attackContent.TornadoPullDuration;
            for (int i = 0; i < around.Count; i++)
            {
                Monster monster = around[i];
                if (monster == null || monster.gameObject.activeInHierarchy == false)
                    continue;

                monster.AddSmoothPull(center, pullDistance, pullDuration);
            }

            attackContent.StartCoroutine(CoPlayTornadoSequence(center, pullDuration));
        }

        private IEnumerator CoPlayTornadoSequence(Vector2 center, float pullDuration)
        {
            float startDuration = PlayEffect(center, EffectID.S);
            if (startDuration > 0.0001f)
                yield return new WaitForSeconds(startDuration);

            BulletEffect loopEffect = null;
            float loopDuration = Mathf.Max(0f, pullDuration - startDuration);
            if (loopDuration >= MinimumLoopTime)
            {
                loopEffect = GetEffect(center, EffectID.C1);
                loopEffect?.PlayEffect();
                yield return new WaitForSeconds(loopDuration);
            }

            if (loopEffect != null)
                loopEffect.ForceRelease();

            PlayEffect(center, EffectID.C2);
        }

        private float PlayEffect(Vector2 center, EffectID effectId)
        {
            BulletEffect effect = GetEffect(center, effectId);
            if (effect == null)
                return 0f;

            effect.PlayEffect();
            return effect.Duration;
        }

        private BulletEffect GetEffect(Vector2 center, EffectID effectId)
        {
            BulletEffect effect = BulletEffectPool.Instance.GetBullet(DiceType, effectId);
            if (effect == null)
                return null;

            effect.transform.position = center;
            return effect;
        }
    }
}
