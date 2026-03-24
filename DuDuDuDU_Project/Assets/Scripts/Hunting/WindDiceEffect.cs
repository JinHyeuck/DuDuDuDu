using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class WindDiceEffect : DiceEffectBase
    {
        private const float BasePushDistance = 0.7f;

        public override DiceType DiceType => DiceType.Wind;
        public override bool ShouldApplyDamage => false;

        public override bool TryCastWithoutTarget(AttackContent attackContent, int shotDicePip)
        {
            if (attackContent == null || MonsterManager.Instance == null || MonsterManager.Instance.activeMonsters == null)
                return false;

            Wall wall = GameManager.Instance != null ? GameManager.Instance.wall : null;
            if (wall == null)
                return false;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            int targetCount = DiceMetaDataProvider.GetWindTargetCount(level);
            float chancePercent = DiceMetaDataProvider.GetWindPushChancePercent(DiceType, level);
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
