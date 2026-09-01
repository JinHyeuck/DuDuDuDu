using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OJ.Dice;
using OJ.DI;
using OJ.Relic;

namespace OJ.Hunting
{
    public class TornadoDiceEffect : DiceEffectBase
    {
        private const float PullRange = 1.8f;
        private const int MaxTargets = 10;
        private const float PullDistancePerHit = 1.2f;
        private const float MinimumLoopTime = 0.05f;

        /// <summary>
        /// 이 클래스는 컨테이너가 만들지 않고 <c>AttackContent</c> 가 <c>new</c> 하는 순수 C# 이다.
        /// 그래서 <c>[Inject]</c> 필드가 아니라 생성자로 창구를 받아 기반 클래스에 넘긴다.
        ///
        /// 창구 필드를 여기서 새로 만들지 않는 이유: <c>DiceEffectBase</c> 가 이미
        /// <c>protected readonly IBattleRefs battle</c> 을 들고 있다. 같은 이름을 다시 선언하면
        /// 기반 필드를 가리기만 할 뿐 얻는 게 없다 — 그냥 물려받은 것을 쓴다.
        /// </summary>
        public TornadoDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

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
                if (RelicManager.Instance != null)
                {
                    monster.ApplyRelicDamageTakenBonus(
                        RelicManager.Instance.GetTornadoDamageTakenBonusPercent(),
                        RelicManager.Instance.GetTornadoDamageTakenBonusDuration());
                }
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
            // 창구에는 ?. 를 붙이지 않는다. 이 효과는 AttackContent 가 BattleScene 안에서만 만들고
            // 그 수명 동안 battle.BulletEffects 는 살아 있다 — 비어 있다면 조용히 넘어갈 게 아니라 울어야 한다.
            // 반면 아래 null 검사는 풀이 고갈돼 빌려줄 이펙트가 없는 경우를 막는 것이라 그대로 둔다.
            BulletEffect effect = battle.BulletEffects.GetBullet(DiceType, effectId);
            if (effect == null)
                return null;

            effect.transform.position = center;
            return effect;
        }
    }
}
