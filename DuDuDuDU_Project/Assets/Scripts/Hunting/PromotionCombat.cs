using System;
using System.Collections.Generic;
using UnityEngine;
using OJ.Core;
using OJ.DI;
using OJ.Dice;

namespace OJ.Hunting
{
    /// <summary>
    /// 승급 효과 중 <b>여러 다이스 효과 파일이 함께 쓰는 것</b>의 자리 — 몬스터가 죽는 순간의 훅과 근처 대상 찾기.
    /// 각 다이스 고유의 효과는 그 다이스의 <c>*DiceEffect</c> 파일에 있고, 여기는 공통 도구만 둔다.
    ///
    /// 단계(T1~T3)는 <see cref="PromotionManager.TierOf"/> 로 읽는다. 단계가 0 이면 여기 코드는 아무 일도 하지 않는다.
    /// </summary>
    public static class PromotionCombat
    {
        /// <summary>킹썬더가 마지막으로 낸 타 대미지. 감전 표식 재전이(T1)가 죽은 자리에서 쓴다.</summary>
        public static int LastKingThunderDamage { get; set; }

        private static int Tier(DiceType diceType)
        {
            return PromotionManager.TierOf(diceType);
        }

        /// <summary>
        /// 반경 안에서 가장 가까운 살아 있는 다른 몬스터. 없으면 null.
        /// <paramref name="filter"/> 가 있으면 통과하는 것만 본다.
        /// </summary>
        public static Monster Nearest(IBattleRefs battle, Vector3 position, float radius, Monster exclude, Predicate<Monster> filter = null)
        {
            List<Monster> active = battle != null && battle.Monsters != null ? battle.Monsters.activeMonsters : null;
            if (active == null)
                return null;

            Monster best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < active.Count; i++)
            {
                Monster candidate = active[i];
                if (candidate == null || candidate == exclude || !candidate.gameObject.activeInHierarchy || !candidate.IsAlive)
                    continue;

                if (filter != null && !filter(candidate))
                    continue;

                float sqr = ((Vector2)candidate.transform.position - (Vector2)position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>반경 안의 살아 있는 다른 몬스터 전부.</summary>
        public static List<Monster> Around(IBattleRefs battle, Vector3 position, float radius, Monster exclude)
        {
            var result = new List<Monster>();
            List<Monster> active = battle != null && battle.Monsters != null ? battle.Monsters.activeMonsters : null;
            if (active == null)
                return result;

            float sqrRadius = radius * radius;
            for (int i = 0; i < active.Count; i++)
            {
                Monster candidate = active[i];
                if (candidate == null || candidate == exclude || !candidate.gameObject.activeInHierarchy || !candidate.IsAlive)
                    continue;

                if (((Vector2)candidate.transform.position - (Vector2)position).sqrMagnitude <= sqrRadius)
                    result.Add(candidate);
            }

            return result;
        }

        /// <summary>
        /// 몬스터가 죽는 순간. <c>Monster.TakeDamage</c> 의 사망 분기에서 부른다.
        /// 이 시점에 상태(기절·둔화·중독 등)는 아직 지워지지 않았다.
        /// </summary>
        public static void OnMonsterKilled(Monster dead, Vector3 position, IBattleRefs battle)
        {
            if (dead == null || battle == null)
                return;

            // 스턴 T1: 기절한 적이 죽으면 주변 적 1명 기절. T3: 그때 자기 쿨타임 즉시 초기화.
            if (dead.StunSourceDice != null && dead.IsStunnedNow)
            {
                int stunTier = Tier(DiceType.Stun);
                if (stunTier >= 1)
                {
                    Monster next = Nearest(battle, position, PromotionEffectRules.StunChainRadius, dead);
                    if (next != null)
                    {
                        next.ApplyStun(StunDiceEffectDuration(stunTier), DiceType.Stun);
                        next.StunSourceDice = dead.StunSourceDice;
                    }
                }

                if (stunTier >= 3)
                    battle.Player?.ResetCooldown(dead.StunSourceDice);
            }

            // 방깎 T2: 방깎 상태의 적이 죽으면 주변 적에게 방깎 전이.
            if (dead.IsDefenseDown && dead.DefenseDownSource == DiceType.ArmorBreak && Tier(DiceType.ArmorBreak) >= 2)
            {
                List<Monster> around = Around(battle, position, PromotionEffectRules.ArmorBreakChainRadius, dead);
                for (int i = 0; i < around.Count; i++)
                    around[i].ApplyDefenseDown(4f, dead.DefenseDownPercent, DiceType.ArmorBreak);
            }

            // 킹아이스 T1: 둔화된 적이 죽으면 주변 적에게 둔화 전이.
            if (dead.IsSlowed() && dead.SlowSource == DiceType.KingIce && Tier(DiceType.KingIce) >= 1)
            {
                List<Monster> around = Around(battle, position, PromotionEffectRules.KingIceSpreadRadius, dead);
                float duration = DiceMetaDataProvider.GetSlowDuration(DiceType.KingIce, 1);
                for (int i = 0; i < around.Count; i++)
                    around[i].ApplySlow(duration, 0.8f, DiceType.KingIce);
            }

            // 킹포이즌 T1: 중독된 적이 죽으면 주변 적의 중독이 갱신되고 남은 시간이 늘어난다.
            if (dead.IsPoisoned() && dead.PoisonSource == DiceType.KingPoison && Tier(DiceType.KingPoison) >= 1)
            {
                List<Monster> around = Around(battle, position, PromotionEffectRules.KingPoisonSpreadRadius, dead);
                float duration = DiceMetaDataProvider.GetPoisonDuration(DiceType.KingPoison) + 2f;
                for (int i = 0; i < around.Count; i++)
                {
                    if (around[i].IsPoisoned())
                        around[i].ApplyPoison(duration, 1f, DiceType.KingPoison);
                }
            }

            // 킹썬더 T1: 감전 표식을 가진 적이 죽으면 번개가 근처 적에게 다시 전이된다(표식도 옮겨 간다).
            if (dead.IsThunderMarked && Tier(DiceType.KingThunder) >= 1 && LastKingThunderDamage > 0)
            {
                Monster next = Nearest(battle, position, PromotionEffectRules.StunChainRadius, dead);
                if (next != null)
                {
                    next.MarkThunder(PromotionEffectRules.KingThunderMarkSeconds);
                    battle.Attack.HitMonster(next, DiceType.KingThunder, LastKingThunderDamage);
                }
            }
        }

        /// <summary>스턴 다이스의 기절 시간(기본 1.1초). T2 부터 1.5배.</summary>
        public static float StunDiceEffectDuration(int tier)
        {
            const float baseDuration = 1.1f;
            return tier >= 2 ? baseDuration * PromotionEffectRules.StunDurationMultiplierT2 : baseDuration;
        }
    }
}
