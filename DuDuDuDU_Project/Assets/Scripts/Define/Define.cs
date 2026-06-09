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
        Fire,
        Ice,
        Thunder,
        Poison,

        Tornado = 100,
        Stun,
        ArmorBreak,
        Wind,
        Time,

        KingNormal = 200,
        KingFire,
        KingIce,
        KingThunder,
        KingPoison,

        Max,
    }

    public enum ElementType : int
    {
        Normal = 0,
        Fire = 1,
        Water = 2,
        Light = 3,
        Dark = 4,
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
        Coin,
        NormalScroll = 100,
        FireScroll,
        IceScroll,
        PoisonScroll,
        ThunderScroll,
        MythicScroll,
        WeaponScroll = 200,
        HelmetScroll,
        ArmorScroll,
        RingScroll,
        ShoesScroll,
        NecklaceScroll,

        Max
    }

    public enum Rarity
    {
        Uncommon = 0,
        Common,
        Normal,
        Rare,
        Epic,
        Mythic,

    }

    public enum EquipmentType
    {
        Weapon,
        Helmet,
        Armor,
        Ring,
        Shoes,
        Necklace,
    }

    public enum GemStatType
    {
        AttackPercent = 0,
        AttackFlat,
        CooldownReducePercent,
        FirstNWavesDamageFlat,
        FireExplosionRangePercent,
        WellHpOnKill,
        FinalDamagePercent,
        FireExplosionTargetCountFlat,
        ThunderChainCountFlat,
        GoldOnKill,
    }

    public static class Define
    {
        public const int MaxEquipmentSlot = 5;
        public static readonly int[] EquipmentSlotUnlockLevels = { 1, 10, 20, 30, 40};
    }

}
