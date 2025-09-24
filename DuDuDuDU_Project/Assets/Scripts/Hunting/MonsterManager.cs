using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class MonsterManager : MonoBehaviour
    {
        public static MonsterManager Instance;
        public List<Monster> activeMonsters = new List<Monster>();

        void Awake() { Instance = this; }

        public void RegisterMonster(Monster monster)
        {
            if (!activeMonsters.Contains(monster))
                activeMonsters.Add(monster);
        }

        public void UnregisterMonster(Monster monster)
        {
            if (activeMonsters.Contains(monster))
                activeMonsters.Remove(monster);

            GameManager.Instance.RemoveMonsterDeadCount();
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
