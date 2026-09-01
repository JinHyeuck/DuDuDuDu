using System.Collections.Generic;
using UnityEngine;
using OJ.DI;
using OJ.Dice;
using OJ.Relic;

namespace OJ.Hunting
{
    public class WindDiceEffect : DiceEffectBase
    {
        private const float BasePushDistance = 0.7f;

        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스(DiceEffectBase)가 protected 로
        // 들고 있으니 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 여기서 따로 필드를
        // 두면 기반의 것을 가려서(CS0108) 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public WindDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.Wind;
        public override bool ShouldApplyDamage => false;

        public override bool TryCastWithoutTarget(AttackContent attackContent, int shotDicePip)
        {
            // 8.3b: MonsterManager 는 전투 씬 안에서 null 이 될 수 없으므로 매니저 자체를
            // 검사하던 부분은 지운다. activeMonsters 는 매니저가 들고 있는 데이터라 그대로 본다.
            if (attackContent == null || battle.Monsters.activeMonsters == null)
                return false;

            // 8.3b: GameManager 는 살아 있는 게 보장되지만 wall 은 스테이지 상태에 따라
            // 비어 있을 수 있는 데이터 필드라 null 검사를 남긴다.
            Wall wall = battle.Game.wall;
            if (wall == null)
                return false;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            int targetCount = DiceMetaDataProvider.GetWindTargetCount(level);
            float chancePercent = DiceMetaDataProvider.GetWindPushChancePercent(DiceType, level);
            if (RelicManager.Instance != null)
                chancePercent += RelicManager.Instance.GetWindPushChanceBonusPercent();
            float distance = BasePushDistance * DiceMetaDataProvider.GetWindDistanceMultiplier(level);

            Vector2 areaCenter = GetWallFrontAreaCenter(wall);
            Vector3 effectPosition = new Vector3(areaCenter.x, areaCenter.y, wall.transform.position.z);
            List<Monster> candidates = GetWallFrontTargets(attackContent, wall, targetCount * 3);

            if (candidates.Count == 0)
                return false;

            Shuffle(candidates);
            int castCount = Mathf.Min(targetCount, candidates.Count);
            bool pushedAny = false;
            for (int i = 0; i < castCount; i++)
            {
                Monster monster = candidates[i];
                if (Random.value * 100f > chancePercent)
                    continue;

                monster.PushBy(Vector2.up, distance);
                if (level >= 6)
                    monster.ApplyWindDamageTakenBonus(10, 3f);
                if (RelicManager.Instance != null)
                    monster.ApplyRelicDamageTakenBonus(RelicManager.Instance.GetWindDamageTakenBonusPercent(), 3f);
                PlayEffectAt(DiceType, effectPosition);
                pushedAny = true;
            }

            return pushedAny;
        }

        private List<Monster> GetWallFrontTargets(AttackContent attackContent, Wall wall, int maxTargets)
        {
            Collider2D wallCollider = wall != null ? wall.GetComponent<Collider2D>() : null;
            Bounds bounds = wallCollider != null ? wallCollider.bounds : new Bounds(wall.transform.position, new Vector3(3f, 1f, 0f));
            Vector2 origin = GetWallFrontAreaCenter(wall);
            float halfLength = 0.6f;
            float halfWidth = Mathf.Max(0.8f, bounds.extents.x * 0.6f);

            attackContent.SetWindRangeGizmo(bounds.min.x, bounds.max.x, origin.y, halfLength);
            List<Monster> targets = attackContent.GetMonstersInOrientedBox(
                origin,
                Vector2.up,
                halfLength,
                halfWidth,
                maxTargets,
                null);

            return targets;
        }

        private static Vector2 GetWallFrontAreaCenter(Wall wall)
        {
            if (wall == null)
                return Vector2.zero;

            Collider2D wallCollider = wall.GetComponent<Collider2D>();
            if (wallCollider == null)
                return wall.transform.position;

            Bounds bounds = wallCollider.bounds;
            return new Vector2(bounds.center.x, bounds.max.y + 0.15f);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
