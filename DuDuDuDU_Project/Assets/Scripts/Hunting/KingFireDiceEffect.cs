using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OJ.Dice;
using OJ.Equipment;
using OJ.Relic;

using OJ.DI;

namespace OJ.Hunting
{
    public class KingFireDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스가 protected 로 들고 있으니
        // 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 따로 두면 기반의 것을 가려서
        // 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public KingFireDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.KingFire;

        // 승급 T1 스택(%) 과 그 스택이 쌓인 웨이브, 승급 T3 의 마지막 발동 웨이브. 웨이브가 바뀌면 스택은 0 부터다.
        private int stackPercent;
        private int stackWave = -1;
        private int screenWave = -1;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || hitMonsters == null)
                return;

            List<Monster> fireTargets = new List<Monster>();
            float explosionRange = 1.6f;
            int fireHitTargetCount = 14;

            if (EquipmentManager.Instance != null)
            {
                explosionRange *= (1f + EquipmentManager.Instance.GetFireExplosionRangeBonus(DiceType));
                fireHitTargetCount += EquipmentManager.Instance.GetFireExplosionExtraTargetCount(DiceType) + 2;
            }
            if (RelicManager.Instance != null)
            {
                explosionRange *= RelicManager.Instance.GetFireExplosionRangeMultiplier(DiceType);
                fireHitTargetCount += RelicManager.Instance.GetFireExplosionExtraTargetCount(DiceType);
            }

            for (int i = 0; i < hitMonsters.Count; ++i)
            {
                Monster target = hitMonsters[i];
                if (target == null)
                    continue;

                List<Monster> monsters = attackContent.GetRedHitTarget(
                    target.transform.position,
                    IFFType.IFF_Friend,
                    explosionRange,
                    fireHitTargetCount,
                    target);

                PlayEffectAt(DiceType, target.transform.position);

                for (int hitIdx = 0; hitIdx < monsters.Count; ++hitIdx)
                {
                    Monster splashTarget = monsters[hitIdx];
                    if (splashTarget == null || splashTarget == target)
                        continue;

                    fireTargets.Add(splashTarget);
                }

            }

            hitMonsters.AddRange(fireTargets);

            int kingFireTier = OJ.Dice.PromotionManager.TierOf(DiceType);
            int wave = battle.Game != null ? battle.Game.CurrentWaveIndex : 0;

            // 승급 T1: 폭발로 맞춘 적 1마리당 다음 폭발 피해 +3% (최대 +30%, 웨이브 동안 유지).
            if (kingFireTier >= 1)
            {
                if (wave != stackWave)
                {
                    stackWave = wave;
                    stackPercent = 0;
                }

                if (stackPercent > 0)
                    attackContent.ScaleShot(1f + stackPercent * 0.01f);

                stackPercent = OJ.Core.PromotionEffectRules.KingFireStackAfter(stackPercent, hitMonsters.Count);
            }

            // 승급 T2: 폭발 중심에 맞은 적은 폭발 피해 +50%.
            if (kingFireTier >= 2 && rootTarget != null)
                attackContent.ScaleTarget(rootTarget, OJ.Core.PromotionEffectRules.KingFireCenterMultiplier);
        }

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (attackContent == null || target == null)
                return;

            if (target != attackContent.CurrentRootTarget)
                return;

            Vector3 center = target.transform.position;

            // 승급 T3: 웨이브당 1회, 화면 전체 적에게 같은 피해(적마다 기존 폭발 이펙트).
            if (OJ.Dice.PromotionManager.TierOf(DiceType) >= 3)
            {
                int currentWave = battle.Game != null ? battle.Game.CurrentWaveIndex : 0;
                if (currentWave != screenWave)
                {
                    screenWave = currentWave;
                    Monster[] everyone = battle.Monsters.EngagedMonsters.ToArray();
                    for (int i = 0; i < everyone.Length; i++)
                    {
                        Monster m = everyone[i];
                        if (m == null || !m.gameObject.activeInHierarchy)
                            continue;

                        PlayEffectAt(DiceType, m.transform.position);
                        attackContent.HitMonster(m, DiceType, attackContent.CurrentDamage);
                    }
                }
            }

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            if (level < OJ.Core.DiceMilestoneLevels.Second || Random.value > 0.3f)
                return;

            attackContent.StartCoroutine(CoDelayedExplosion(attackContent, center, attackContent.CurrentDamage));
        }

        private IEnumerator CoDelayedExplosion(AttackContent attackContent, Vector3 center, int damage)
        {
            yield return new WaitForSeconds(0.5f);

            if (attackContent == null)
                yield break;

            float explosionRange = 1.6f;
            int fireHitTargetCount = 14;
            if (EquipmentManager.Instance != null)
            {
                explosionRange *= (1f + EquipmentManager.Instance.GetFireExplosionRangeBonus(DiceType));
                fireHitTargetCount += EquipmentManager.Instance.GetFireExplosionExtraTargetCount(DiceType) + 2;
            }
            if (RelicManager.Instance != null)
            {
                explosionRange *= RelicManager.Instance.GetFireExplosionRangeMultiplier(DiceType);
                fireHitTargetCount += RelicManager.Instance.GetFireExplosionExtraTargetCount(DiceType);
            }

            List<Monster> monsters = attackContent.GetRedHitTarget(
                center,
                IFFType.IFF_Friend,
                explosionRange,
                fireHitTargetCount,
                null);

            PlayEffectAt(DiceType, center);

            for (int i = 0; i < monsters.Count; i++)
            {
                Monster splashTarget = monsters[i];
                if (splashTarget == null || splashTarget.gameObject.activeInHierarchy == false)
                    continue;

                attackContent.HitMonster(splashTarget, DiceType, damage);
            }
        }
    }
}
