using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public enum TargetProjectileState
    {
        None = 0,
        Eject,
        Shoot,
        Hit,
    }

    public enum IFFType
    {
        IFF_None = 0,
        IFF_Friend, // 우리팀
        IFF_Foe, // 상대팀
    }

    public enum DiceType : int
    {
        Normal = 0,
        Fire = 1,
        Ice = 2,
        Poison = 3,
        Thunder = 4,

        Max,
        // 필요하면 추가
    }

    public enum CharacterState
    {
        None = 0,
        Idle = 11,
        Attack,
        Hit,
        Dead
    }

    public static class Define
    {

    }

}
