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

        /// <summary>
        /// 시너지 티켓. 시너지 옵션 리롤에 쓴다(<c>OJ.Synergy.SynergyManager</c>). 핀볼 베이스에서 들어온다.
        /// <see cref="PinballTicket"/> 과 같은 이유로 <see cref="Max"/> 앞이어야 한다.
        /// </summary>
        SynergyTicket = 7,

        NormalScroll = 100,
        FireScroll = 101,
        IceScroll = 102,
        PoisonScroll = 103,
        ThunderScroll = 104,
        /// <summary>
        /// 신화석. <b>레거시다.</b> 다이스 강화 재료였지만 성장이 카피 방식으로 바뀌며 사용처가 없다
        /// (킹 다이스는 이제 다이스별 카피 <see cref="KingNormalCopy"/> 등을 쓴다). 보상처를 정리하면 제거한다.
        /// 번호는 구 세이브와 에셋이 들고 있으니 재사용하지 말 것.
        /// </summary>
        MythicStone = 105,

        /// <summary>레어석. 신화석과 같은 사정의 레거시다 (레어 다이스는 <see cref="TornadoCopy"/> 등을 쓴다).</summary>
        RareStone = 106,

        /// <summary>
        /// 일반 보석 상자 열쇠(예전 이름 뽑기권). 일반 보석 상자 1회를 연다. 값어치 500원.
        ///
        /// <b>레어석·신화석과는 다른 재화다.</b> 저쪽은 지금 다이스 강화 재료(<c>ToScrollType</c>)이고,
        /// 다이스 성장이 카피 방식으로 바뀌면(작업 3) 걷어낼 예정이다. 번호도 그래서 새로 땄다 —
        /// 기존 세이브와 에셋이 105·106 을 강화석으로 들고 있으니 재사용하지 말 것.
        /// </summary>
        NormalGemTicket = 107,

        /// <summary>최고급 보석 상자 열쇠(예전 이름 뽑기권). 최고급 보석 상자 1회를 연다. 값어치 1,800원.</summary>
        PremiumGemTicket = 108,

        // ── 다이스 카피 ─────────────────────────────────────────────────
        //
        // <b>다이스마다 하나씩이다.</b> 선택권이 "고른다"는 뜻을 가지려면 카피가 다이스별이어야 한다.
        // 기본 5종은 새로 만들지 않고 기존 속성 스크롤(NormalScroll~ThunderScroll, 100~104)을 그대로
        // 카피로 쓴다 — 번호가 세이브와 에셋에 박혀 있고, 레벨업 재료라는 역할이 이미 같다.
        // 레벨업 비용·초기화 환급·킹 해금 가격이 모두 <c>PointManager.ToScrollType</c> 을 따라가므로
        // 그 함수가 이 값들을 돌려주면 나머지는 손대지 않는다.

        /// <summary>토네이도 카피.</summary>
        TornadoCopy = 110,
        /// <summary>스턴 카피.</summary>
        StunCopy = 111,
        /// <summary>방깎 카피.</summary>
        ArmorBreakCopy = 112,
        /// <summary>윈드 카피.</summary>
        WindCopy = 113,
        /// <summary>타임 카피.</summary>
        TimeCopy = 114,

        /// <summary>킹 노말 카피.</summary>
        KingNormalCopy = 115,
        /// <summary>킹 파이어 카피.</summary>
        KingFireCopy = 116,
        /// <summary>킹 아이스 카피.</summary>
        KingIceCopy = 117,
        /// <summary>킹 포이즌 카피.</summary>
        KingPoisonCopy = 118,
        /// <summary>킹 썬더 카피.</summary>
        KingThunderCopy = 119,

        /// <summary>
        /// 선택권. 레어·킹 다이스 중 하나를 골라 카피 1장으로 바꾼다. 값어치 5,250원(카피의 1.5배).
        /// 쓰는 화면은 아직 없다 — 지급·판매·교환 UI 는 후속 작업이다.
        /// </summary>
        SelectTicket = 120,

        // ── 랜덤 카피 (지급용) ──────────────────────────────────────────
        //
        // <b>보관되지 않는 재화다.</b> 보상표에 "레어 카피 3장" 처럼 적기 위한 표지이고, 지급하는 순간
        // (<c>PointManager.Add</c>) 후보 다이스의 구체적인 카피로 풀려서 들어간다 — 이 값 자체의
        // 보유량은 늘 0 이다. 보상 미리보기에는 이 이름이 그대로 보이고(가챠 미리보기와 같다),
        // 후보는 <c>DiceCopyRewards</c> 가 정한다.

        /// <summary>랜덤 기본 카피. 기본 5종 중 하나씩 무작위로.</summary>
        RandomBasicCopy = 130,
        /// <summary>랜덤 레어 카피. 레어 5종 중 하나씩 무작위로.</summary>
        RandomRareCopy = 131,
        /// <summary>랜덤 킹 카피. 킹 5종 중 하나씩 무작위로.</summary>
        RandomKingCopy = 132,
        /// <summary>랜덤 레어·킹 카피. 레어·킹 10종 중 하나씩 무작위로(핀볼 보상표용).</summary>
        RandomRareKingCopy = 133,

        // ── 이벤트 재화 ─────────────────────────────────────────────────
        //
        // 이벤트 3종이 3일씩 돌아가며 열린다(9일 주기). 재화는 이벤트마다 따로이고 "열린 이벤트의 것만" 받는다.
        // 이벤트 시스템 자체는 아직 없다 — 타입과 지급 배선만 먼저 있다. 이름은 이벤트가 정해지면 바꾼다
        // (숫자 값은 세이브에 박히니 그대로 둘 것).

        /// <summary>이벤트 재화 1. 값어치 20원.</summary>
        EventCurrency1 = 140,
        /// <summary>이벤트 재화 2.</summary>
        EventCurrency2 = 141,
        /// <summary>이벤트 재화 3.</summary>
        EventCurrency3 = 142,

        /// <summary>
        /// 열린 이벤트 재화. <b>지급용 표지다 — 보관되지 않는다.</b> 지급 순간 그날 열린 이벤트의 재화로 풀린다
        /// (<c>PointManager.Add</c>, 규칙은 <c>EventCurrencyRules</c>). 핀볼 아래 칸·Bonus 풀이 쓴다.
        /// </summary>
        OpenEventCurrency = 134,
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

        /// <summary>
        /// 다이스 최대 레벨. <b>카피 곡선(<c>DiceCopyCurve</c>)의 끝인 30 이다.</b>
        ///
        /// 승급 T3(Lv30)와 마지막 마일스톤이 모두 이 안에서 끝난다. 상한이 없던 시절에는
        /// 재화만 있으면 무한히 올라갔고, 그러면 "무엇을 목표로 키우는가"가 사라진다.
        /// Lv31 이상의 카피 규칙은 <c>DiceCopyCurve</c> 에 확장용으로만 들어 있다 —
        /// 상한을 올릴 때 이 상수만 바꾸면 된다.
        /// </summary>
        public const int MaxDiceLevel = 30;

        /// <summary>
        /// 장비 최대 레벨. <b>보석 슬롯이 마지막으로 열리는 레벨</b>이다.
        ///
        /// <see cref="EquipmentSlotUnlockLevels"/> 의 끝값을 그대로 쓴다 — 표를 늘리면
        /// 상한도 같이 따라간다. 상수로 40 을 또 적으면 표와 어긋나는 날이 온다.
        /// </summary>
        public static int MaxEquipmentLevel
        {
            get
            {
                int max = 1;
                for (int i = 0; i < EquipmentSlotUnlockLevels.Length; i++)
                {
                    if (EquipmentSlotUnlockLevels[i] > max)
                        max = EquipmentSlotUnlockLevels[i];
                }

                return max;
            }
        }
    }

}
