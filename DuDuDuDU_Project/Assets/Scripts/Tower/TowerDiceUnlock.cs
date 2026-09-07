using System;
using UnityEngine;

namespace OJ.Tower
{
    /// <summary>
    /// 층을 깨서 열리는 다이스 하나. 기획서 5.2 목표 배너의 "30층 · KingFire 해금" 이다.
    /// </summary>
    [Serializable]
    public sealed class TowerDiceUnlock
    {
        [Tooltip("이 다이스를 열어 주는 층. 그 층을 클리어하면 해금된다.")]
        [Min(1)] public int floor = 10;

        [Tooltip("열리는 다이스. 기본 다이스 5종은 처음부터 쓸 수 있으므로 여기 넣지 않는다.")]
        public DiceType diceType = DiceType.Tornado;
    }
}
