using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public enum IFFType
    {
        IFF_None = 0,
        IFF_Friend, // �츮��
        IFF_Foe, // �����
    }

    public enum DiceType : int
    {
        Normal = 0,
        Fire = 1,
        Ice = 2,
        Poison = 3,
        Thunder = 4,

        KingNormal = 5,
        KingFire = 6,
        KingIce = 7,
        KingPoison = 8,
        KingThunder = 9,
        KingMixed = 10,

        Max,
    }

    public enum EffectID : int
    {
        S = 0,
        C1,
        C2,
        C3,
    }

    public enum CharacterState
    {
        None = 0,
        Idle = 11,
        Attack,
        Run,
        Hit,
        Dead
    }

    public enum InGameState
    {
        None = 0,
        Setting,
        Wave,
    }

    public enum PointType
    {
        Gold = 0,
        Dia,
        Stamina,
        NormalScroll,
        FireScroll,
        IceScroll,
        PoisonScroll,
        ThunderScroll,
        MythicScroll,

        Max
    }

    public static class Define
    {

    }

}
