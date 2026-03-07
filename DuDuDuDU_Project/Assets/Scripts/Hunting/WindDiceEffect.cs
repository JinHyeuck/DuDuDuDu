using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class WindDiceEffect : DiceEffectBase
    {
        private const float BoxHalfLength = 2.8f;
        private const float BoxHalfWidth = 0.7f;
        private const float PushDistance = 0.25f;
        private const float Duration = 1.2f;
        private const float TravelDistance = 4.5f;
        private const int MaxTargetsPerTick = 24;

        public override DiceType DiceType => DiceType.Wind;

        public override void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
            if (attackContent == null || rootTarget == null || hitMonsters == null)
                return;

            Vector2 origin = attackContent.transform.position;
            if (PlayerController.Instance != null && PlayerController.Instance.firePoint != null)
                origin = PlayerController.Instance.firePoint.position;

            Vector2 direction = ((Vector2)rootTarget.transform.position - origin).normalized;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector2.down;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            float duration = Duration + Mathf.Max(0, level - 1) * 0.03f;
            float travelDistance = TravelDistance + Mathf.Max(0, level - 1) * 0.2f;
            float pushDistance = PushDistance + Mathf.Max(0, level - 1) * 0.02f;
            float halfLength = BoxHalfLength + Mathf.Max(0, level - 1) * 0.05f;

            List<Monster> initialHits = attackContent.GetMonstersInOrientedBox(
                rootTarget.transform.position,
                direction,
                halfLength,
                BoxHalfWidth,
                MaxTargetsPerTick,
                rootTarget);

            for (int i = 0; i < initialHits.Count; i++)
            {
                Monster hit = initialHits[i];
                if (hit == null || hitMonsters.Contains(hit))
                    continue;

                hitMonsters.Add(hit);
            }

            SpawnWindGust(rootTarget.transform.position, direction, halfLength, duration, travelDistance, pushDistance);
            PlayEffectAt(DiceType, rootTarget.transform.position);
        }

        private void SpawnWindGust(Vector2 start, Vector2 direction, float halfLength, float duration, float travelDistance, float pushDistance)
        {
            GameObject gustObj = new GameObject("WindGustArea");
            WindGustArea gust = gustObj.AddComponent<WindGustArea>();
            gust.Init(start, direction, halfLength, BoxHalfWidth, duration, travelDistance, pushDistance * 10f);
        }
    }
}
