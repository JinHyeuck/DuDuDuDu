using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class MonsterManager : MonoBehaviour
    {
        public static MonsterManager Instance;
        public List<Monster> activeMonsters = new List<Monster>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RegisterMonster(Monster monster)
        {
            if (!activeMonsters.Contains(monster))
                activeMonsters.Add(monster);
        }

        public void UnregisterMonster(Monster monster, bool countAsKill = true)
        {
            bool removed = activeMonsters.Remove(monster);

            if (removed && countAsKill)
                GameManager.Instance?.RemoveMonsterDeadCount();
        }

        public Monster GetClosestMonster(Vector3 position)
        {
            Monster closest = null;
            float minDist = Mathf.Infinity;

            foreach (Monster m in activeMonsters)
            {
                float dist = Vector2.Distance(position, m.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = m;
                }
            }
            return closest;
        }
    }

}
