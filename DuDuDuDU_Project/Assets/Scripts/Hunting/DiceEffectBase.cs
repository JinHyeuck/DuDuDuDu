using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public abstract class DiceEffectBase
    {
        public abstract DiceType DiceType { get; }
        public virtual bool ShouldApplyDamage => true;

        public virtual void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
        }

        public virtual void ApplyOnHit(AttackContent attackContent, Monster target)
        {
        }

        public virtual bool TryCastWithoutTarget(AttackContent attackContent, int shotDicePip)
        {
            return false;
        }

        protected void PlayEffectAt(DiceType diceType, Vector3 position, EffectID effectId = EffectID.S)
        {
            BulletEffect effect = BulletEffectPool.Instance.GetBullet(diceType, effectId);
            if (effect == null)
                return;

            effect.transform.position = position;
            effect.PlayEffect();
        }

        protected void PlayLineEffect(DiceType diceType, Vector3 startPos, Vector3 endPos, EffectID effectId = EffectID.S)
        {
            BulletEffect effect = BulletEffectPool.Instance.GetBullet(diceType, effectId);
            if (effect == null)
                return;

            effect.PlayLineEffect(startPos, endPos);
        }
    }
}
