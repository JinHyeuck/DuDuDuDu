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
    ///
    /// <b><see cref="Max"/> 를 뺀 모든 항목에 값이 명시돼 있다. 그 상태를 유지할 것.</b>
    /// 세이브는 이름으로 읽지만 <b>에셋은 이 enum 을 정수로 직렬화한다</b> —
    /// <c>ShopDatabase</c> · <c>StageRewardDatabase</c> · <c>PointMetadataDatabase</c> ·
    /// <c>PinballRewardDatabase</c> 가 전부 그렇다. 값이 암묵이면 항목 하나를 빼는 순간
    /// 뒤가 한 칸씩 밀리고, 에셋에 박힌 정수는 그대로라 <b>조용히 다른 재화가 된다</b>
    /// (실제로 <c>ShopDatabase</c> 의 유료젬 IAP 팩이 유물권으로 바뀔 뻔했다).
    /// 값을 박아 두면 무엇을 빼도 그 자리에 구멍만 남는다. 빠진 번호는 재사용하지 말 것 —
    /// 재사용하면 구 에셋이 새 재화를 가리킨다.
    ///
    /// <see cref="Max"/> 만 예외로 둔다. 어디에도 직렬화되지 않는 sentinel 이라
    /// 번호에 뜻이 없고, 박아 두면 뒤에 항목을 늘릴 때 충돌만 부른다.
    /// </summary>
    public enum PointType
    {
        Gold = 0,

        /// <summary>무료젬. 게임 플레이 보상과 유료젬 교환으로만 들어온다.</summary>
        FreeGem = 1,
        Stamina = 2,

        // 3 번은 비어 있다. 전투 강화석 자리였는데 BattlePointType.EnhanceStone 으로
        // 옮겼다(판마다 0 으로 밀리는 값이라 영구 재화가 아니었다).
        // 재사용하지 말 것 — 구 세이브와 구 에셋이 이 번호를 강화석으로 들고 있다.

        RelicTicket = 4,

        /// <summary>유료젬. 현금(IAP)으로만 들어온다. 10원 = 1개.</summary>
        PaidGem = 5,

        /// <summary>
        /// 핀볼 티켓. 1개 = 핀볼 1회 플레이(<c>OJ.Pinball.PinballManager</c>).
        ///
        /// <b><see cref="Max"/> 앞이어야 한다</b> —
        /// <c>UIShopGridCard.Bind</c> 가 <c>Max</c> 를 "현금 결제" sentinel 로 쓴다.
        /// </summary>
        PinballTicket = 6,

        NormalScroll = 100,
        FireScroll = 101,
        IceScroll = 102,
        PoisonScroll = 103,
        ThunderScroll = 104,
        /// <summary>신화석. 다이스석 상점(기획서 8.6)이 파는 것이 이것이다.</summary>
        MythicStone = 105,

        /// <summary>레어석. 신화석과 짝을 이루는 하위 등급이다.</summary>
        RareStone = 106,
        WeaponScroll = 200,
        HelmetScroll = 201,
        ArmorScroll = 202,
        RingScroll = 203,
        ShoesScroll = 204,
        NecklaceScroll = 205,

        Max
    }

    /// <summary>
    /// <b>전투 재화.</b> 한 판(스테이지 1회 도전) 동안만 살고 판이 끝나면 사라진다.
    /// 값은 <c>OJ.Core.RunState</c> 가 들고, <c>OJ.Battle.BattlePointManager</c> 가 창구다.
    ///
    /// <b><see cref="PointType"/> 과 갈라 둔 이유.</b> 영구 재화와 섞여 있으면 둘의 차이가
    /// 타입에 안 남아서, 로비 컨텐츠가 전투 재화를 보상으로 주는 사고를 아무도 못 막는다.
    /// 실제로 두 번 났다 — 탑의 구간 보상과 핀볼의 폭탄핀이 강화석을 줬고, 둘 다
    /// <b>받자마자 다음 판 시작에 0 으로 밀려</b> 주는 척만 하는 보상이었다.
    /// 지금은 그런 코드가 컴파일되지 않는다.
    ///
    /// <b>여기 오는 조건은 "판마다 0 으로 돌아가는가" 하나다.</b> 세이브에 남아야 하는 것은
    /// <see cref="PointType"/> 으로 간다 — 이쪽에는 저장 경로가 아예 없다.
    ///
    /// 예전 <c>OJ.Bounty.BountyRewardKind</c> 자리다. 현상금이 전투 재화를 데이터로
    /// 지급해야 해서 혼자 만들어 쓰던 것을 여기로 올렸다. <b>값(0·1)을 그대로 둔 것이
    /// 중요하다</b> — <c>BountyDatabase.asset</c> 이 정수로 들고 있어서, 바꾸면
    /// 5등급의 보상 종류가 통째로 뒤바뀐다.
    /// </summary>
    public enum BattlePointType
    {
        /// <summary>소환에 쓰는 SP. 관리 단계에서만 쓴다.</summary>
        [InspectorName("SP (소환 포인트)")]
        SummonPoint = 0,

        /// <summary>전투 강화석. 속성 강화·머지·진화의 재료다.</summary>
        [InspectorName("전투 강화석")]
        EnhanceStone = 1,

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
