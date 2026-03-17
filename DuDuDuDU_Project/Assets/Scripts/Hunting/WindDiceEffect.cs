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

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            int targetCount = DiceMetaDataProvider.GetWindTargetCount(level);
            float chancePercent = DiceMetaDataProvider.GetWindPushChancePercent(level);
            float distance = BasePushDistance * DiceMetaDataProvider.GetWindDistanceMultiplier(level);

            List<Monster> candidates = new List<Monster>();
            for (int i = 0; i < MonsterManager.Instance.activeMonsters.Count; i++)
            {
                Monster monster = MonsterManager.Instance.activeMonsters[i];
                if (monster == null || monster.gameObject.activeInHierarchy == false)
                    continue;

                candidates.Add(monster);
            }

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
                PlayEffectAt(DiceType, monster.transform.position);
                pushedAny = true;
            }

            return pushedAny;
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
