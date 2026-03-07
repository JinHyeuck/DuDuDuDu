using System.Collections;
using UnityEngine;

namespace OJ
{
    public class WindDiceEffect : DiceEffectBase
    {
        private const float PushDuration = 0.5f;
        private const float PushDistance = 0.7f;
        private const float WallYOffset = 1.0f;
        private const float BandHalfHeight = 0.5f;

        public override DiceType DiceType => DiceType.Wind;
        public override bool ShouldApplyDamage => false;

        public override bool TryCastWithoutTarget(AttackContent attackContent, int shotDicePip)
        {
            if (attackContent == null)
                return false;

            Wall wall = Object.FindFirstObjectByType<Wall>();
            if (wall == null || wall.gameObject == null || wall.gameObject.activeInHierarchy == false)
                return false;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float duration = PushDuration;
            float totalPushDistance = PushDistance + Mathf.Max(0, level - 1) * 0.04f;
            float pushPerSecond = totalPushDistance / Mathf.Max(0.01f, duration);
            float bandHalfHeight = BandHalfHeight + Mathf.Max(0, level - 1) * 0.02f;

            Vector2 wallPos = wall.transform.position;
            float minX;
            float maxX;
            ResolveWallXRange(wall, wallPos.x, out minX, out maxX);

            float centerY = wallPos.y + WallYOffset;
            Vector3 lineStart = new Vector3(minX, centerY, 0f);
            Vector3 lineEnd = new Vector3(maxX, centerY, 0f);
            PlayLineEffect(DiceType, lineStart, lineEnd);
            attackContent.SetWindRangeGizmo(minX, maxX, centerY, bandHalfHeight);

            attackContent.StartCoroutine(CoPushBand(duration, minX, maxX, centerY, bandHalfHeight, pushPerSecond));
            return true;
        }

        private static void ResolveWallXRange(Wall wall, float defaultCenterX, out float minX, out float maxX)
        {
            Collider2D wallCollider = wall.GetComponent<Collider2D>();
            if (wallCollider != null)
            {
                minX = wallCollider.bounds.min.x;
                maxX = wallCollider.bounds.max.x;
                return;
            }

            SpriteRenderer wallRenderer = wall.GetComponent<SpriteRenderer>();
            if (wallRenderer != null)
            {
                minX = wallRenderer.bounds.min.x;
                maxX = wallRenderer.bounds.max.x;
                return;
            }

            minX = defaultCenterX - 3f;
            maxX = defaultCenterX + 3f;
        }

        private IEnumerator CoPushBand(
            float duration,
            float minX,
            float maxX,
            float centerY,
            float bandHalfHeight,
            float pushPerSecond)
        {
            float elapsed = 0f;
            float minY = centerY - Mathf.Max(0.01f, bandHalfHeight);
            float maxY = centerY + Mathf.Max(0.01f, bandHalfHeight);

            while (elapsed < duration)
            {
                if (MonsterManager.Instance != null && MonsterManager.Instance.activeMonsters != null)
                {
                    for (int i = 0; i < MonsterManager.Instance.activeMonsters.Count; i++)
                    {
                        Monster monster = MonsterManager.Instance.activeMonsters[i];
                        if (monster == null || monster.gameObject.activeInHierarchy == false)
                            continue;

                        Vector3 pos = monster.transform.position;
                        if (pos.x < minX || pos.x > maxX)
                            continue;

                        if (pos.y < minY || pos.y > maxY)
                            continue;

                        monster.PushBy(Vector2.up, pushPerSecond * Time.deltaTime);
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
