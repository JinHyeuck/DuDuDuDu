using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public enum IFFType
    {
        IFF_None = 0,
        IFF_Friend, // 우리팀
        IFF_Foe, // 상대팀
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

    /// <summary>
    /// 재화. <b>세이브는 이름(문자열)으로 저장된다</b>(<c>PointManager.WriteTo</c>) —
    /// 새 항목은 0 으로 시작하고 없어진 이름은 조용히 무시되므로, 여기를 고치는 것만으로
    /// 세이브가 깨지지는 않는다.
    ///
    /// <b>젬이 둘인 이유.</b> 상점 기획서(<c>Docs/ShopPackageDesign.md</c>) 7.3 의 재화 흐름은
    /// "현금 → 유료젬 → 무료젬 → 소비 재화" 한 방향이다. 되돌아가는 경로를 만들지 않는 것이
    /// 10원 = 1유료젬 환산 기준을 지탱한다. 한 종류로 두면 그 방향성을 표현할 수가 없다.
    /// 게임 플레이로 주는 젬은 전부 <see cref="FreeGem"/> 이다(예전 <c>Dia</c> 자리).
    /// </summary>
    public enum PointType
    {
        Gold = 0,

        /// <summary>무료젬. 게임 플레이 보상과 유료젬 교환으로만 들어온다.</summary>
        FreeGem,
        Stamina,
        BattleEnhanceStone,
        RelicTicket,

        /// <summary>유료젬. 현금(IAP)으로만 들어온다. 10원 = 1개.</summary>
        PaidGem,

        NormalScroll = 100,
        FireScroll,
        IceScroll,
        PoisonScroll,
        ThunderScroll,
        /// <summary>신화석. 다이스석 상점(기획서 8.6)이 파는 것이 이것이다.</summary>
        MythicScroll,

        /// <summary>레어석. 신화석과 짝을 이루는 하위 등급이다.</summary>
        SpecialDiceCore,
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
