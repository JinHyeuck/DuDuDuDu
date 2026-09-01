using System.Collections.Generic;
using UnityEngine;
using VContainer;
using OJ.DI;
using OJ.Utils;

namespace OJ.Hunting
{
    public class MonsterManager : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. BattleScene 안에서는 null 이 아니다.
        // 이 매니저 자체가 BattleScene 에만 사는 것이라 창구도 항상 살아 있다.
        [Inject] private IBattleRefs battle;

        public List<Monster> activeMonsters = new List<Monster>();

        public void RegisterMonster(Monster monster)
        {
            if (!activeMonsters.Contains(monster))
                activeMonsters.Add(monster);
        }

        public void UnregisterMonster(Monster monster, bool countAsKill = true)
        {
            bool removed = activeMonsters.Remove(monster);

            // ?. 를 뗀다. countAsKill 이 true 로 들어오는 곳은 Monster.TakeDamage 의 사망
            // 처리 하나뿐이고 그것은 전투가 돌아가는 중에만 실행된다 — 그 시점에 battle.Game
            // 이 null 이면 그것은 사고이므로 조용히 넘기지 않고 울어야 한다.
            // (씬 정리 중에 도는 Monster.OnDisable 경로는 countAsKill 이 false 라 여기 닿지 않는다.)
            if (removed && countAsKill)
                battle.Game.RemoveMonsterDeadCount();
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
