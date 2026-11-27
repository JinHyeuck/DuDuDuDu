using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using OJ.Point;

namespace OJ.Pinball
{
    /// <summary>재화 한 덩이. <c>PointRewardEntry</c> 는 직렬화 속성이 없어 여기서 따로 둔다.</summary>
    [Serializable]
    public struct PinballReward
    {
        public PointType pointType;
        [Min(1)] public int amount;

        /// <summary>
        /// 지급 확률(%). <b>0 은 "항상"으로 읽는다</b> — 이 필드가 생기기 전에 만든 에셋은 값이 0 이고,
        /// 그것이 100% 여야 옛 표가 그대로 돈다. 칸 경품·핀 경품에서는 "이 한 줄을 줄지"의 확률이고,
        /// 랜덤 1종 핀(<c>pickOneAtRandom</c>)에서는 "목록 중 무엇을 줄지"의 <b>가중치</b>다.
        /// </summary>
        [Range(0, 100)] public int chancePercent;

        public PinballReward(PointType pointType, int amount, int chancePercent = 0)
        {
            this.pointType = pointType;
            this.amount = Mathf.Max(1, amount);
            this.chancePercent = Mathf.Clamp(chancePercent, 0, 100);
        }

        /// <summary>항상 주는가(확률 표기가 없거나 100).</summary>
        public bool IsGuaranteed => chancePercent <= 0 || chancePercent >= 100;

        /// <summary>실제로 쓰는 확률(1~100).</summary>
        public int EffectiveChance => IsGuaranteed ? 100 : chancePercent;

        public PointRewardEntry ToPointRewardEntry()
        {
            return new PointRewardEntry(pointType, amount);
        }
    }

    /// <summary>칸 하나의 경품.</summary>
    [Serializable]
    public sealed class PinballSlotReward
    {
        [Tooltip("화면·로그에 쓸 이름. 비워도 동작한다.")]
        public string label;

        public List<PinballReward> rewards = new List<PinballReward>();
    }

    /// <summary>
    /// 특수 핀 하나의 누적 보상. <c>PinballBoard</c> 의 <c>Peg.specialTag</c> 와 태그로 짝짓는다.
    /// </summary>
    [Serializable]
    public sealed class PinballSpecialReward
    {
        [Tooltip("PinballBoard 의 Peg.specialTag 값. 0 은 '특수 핀 아님'이라 쓸 수 없다.")]
        [Min(1)] public int tag = 1;

        [Tooltip("화면·로그에 쓸 이름. 예: 황금핀")]
        public string label;

        [Tooltip("이 횟수만큼 맞으면 보상이 나가고 게이지가 0으로 돌아간다.")]
        [Min(1)] public int requiredHits = 10;

        /// <summary>
        /// 켜면 <see cref="rewards"/> 를 <b>전부 주지 않고 그 중 하나만</b> 뽑아 준다.
        ///
        /// 스크롤처럼 <b>종류가 여럿인 재화</b>를 위한 것이다. 다섯 종을 한 줄씩 나열해 두고
        /// 켜면 "다이스 스크롤 랜덤 1종"이 된다 — 풀을 코드에 박지 않아도 되고, 무엇이
        /// 나올 수 있는지가 에셋에 그대로 보인다.
        ///
        /// <b>임계치를 여러 번 넘으면 넘은 횟수만큼 따로 뽑는다.</b> 그래야 1배 N번과
        /// N배 1번이 같아진다(정책 5.2의 압축 불변식) — 한 번 뽑아 N배로 주면 고배율일수록
        /// 종류가 덜 갈려서 같은 티켓으로 다른 결과가 나온다.
        /// </summary>
        [Tooltip("켜면 목록에서 하나만 뽑아 준다. 스크롤처럼 종류가 여럿인 재화용.")]
        public bool pickOneAtRandom;

        public List<PinballReward> rewards = new List<PinballReward>();
    }

    /// <summary>
    /// 배율 한 단계와 그것을 열어 주는 보유 티켓 수.
    ///
    /// <b>조건이 소모량이 아니라 보유량인 이유.</b> 배율은 클릭 수를 줄이는 장치다.
    /// 아무나 쓰면 유저가 시행 횟수를 한꺼번에 녹여 버리고 컨텐츠가 몇 분 만에 끝난다.
    /// 쌓아 둔 사람에게만 열면 그 속도가 적당히 유지된다.
    /// </summary>
    [Serializable]
    public sealed class PinballMultiplierTier
    {
        [Min(1)] public int multiplier = 1;

        [Tooltip("이 배율을 쓰려면 핀볼 티켓을 이만큼 들고 있어야 한다. 소모량이 아니라 보유량이다.")]
        [Min(0)] public int requiredTickets;
    }

    /// <summary>
    /// 핀볼의 경품표.
    ///
    /// <b>슬롯 확률은 여기 없다.</b> 정본은 <c>PinballBoard.declaredProbability</c> 하나다
    /// (그 필드의 툴팁이 "실제 지급은 이 확률표를 따른다"고 못 박는다).
    /// 확률을 여기에도 두면 정본이 둘이 되는데, CI 의
    /// <c>DeclaredProbability_SumsToOne</c> 은 보드 쪽만 검사하므로 이쪽이 어긋나도
    /// <b>아무도 잡지 못한다.</b> 확률은 Sim Lab 의 Edit Board 탭에서 편집한다.
    ///
    /// 확률만 바꾸는 것은 재베이크가 필요 없다 —
    /// <c>declaredProbability</c> 는 <c>LayoutHash()</c> 계산에서 빠져 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "PinballRewardDatabase", menuName = "OJ/Pinball Reward Database")]
    public sealed class PinballRewardDatabase : ScriptableObject
    {
        [Header("입장")]
        [Tooltip("공 1개(배율 x1)에 드는 핀볼 티켓 수.")]
        [Min(1)] public int ticketCost = 1;

        [Tooltip("한 세션에 발사할 수 있는 공의 최대 수. 다 쏘면 그 세션이 끝날 때까지 기다린다.")]
        [Min(1)] public int maxShotsPerSession = 15;

        [Header("배율 — 보유 티켓이 조건 이상이어야 열린다")]
        public List<PinballMultiplierTier> multiplierTiers = new List<PinballMultiplierTier>();

        [Header("칸 경품 — 순서가 곧 슬롯 인덱스다 (0 이 맨 왼쪽)")]
        public List<PinballSlotReward> slotRewards = new List<PinballSlotReward>();

        [Header("특수 핀 누적 보상")]
        public List<PinballSpecialReward> specialRewards = new List<PinballSpecialReward>();

        public int TicketCost => Mathf.Max(1, ticketCost);

        public int MaxShotsPerSession => Mathf.Max(1, maxShotsPerSession);

        /// <summary>레퍼런스 기준 8단계. 조건은 보유 티켓 수다.</summary>
        private static readonly int[] DefaultMultipliers = { 1, 2, 3, 5, 10, 20, 50, 100 };
        private static readonly int[] DefaultRequiredTickets = { 0, 120, 360, 600, 1200, 2400, 6000, 12000 };

        /// <summary>
        /// 배율 표만 기본값으로 채운다. <b>경품은 건드리지 않는다</b> —
        /// 배율은 나중에 생긴 필드라, 그 전에 만든 에셋을 보충할 때 밸런스까지 되돌리면 안 된다.
        /// </summary>
        public void PopulateMultiplierDefaults()
        {
            if (multiplierTiers == null)
                multiplierTiers = new List<PinballMultiplierTier>();

            multiplierTiers.Clear();
            maxShotsPerSession = Mathf.Max(1, maxShotsPerSession);

            for (int i = 0; i < DefaultMultipliers.Length; i++)
            {
                multiplierTiers.Add(new PinballMultiplierTier
                {
                    multiplier = DefaultMultipliers[i],
                    requiredTickets = DefaultRequiredTickets[i],
                });
            }
        }

        /// <summary>
        /// 코드 기본값을 채운다. 에셋 빌더와 <see cref="PinballDatabaseProvider"/> 의 폴백이 쓴다.
        ///
        /// <b>밸런스가 아니라 "화면이 비어 보이지 않게" 하는 값이다.</b>
        /// 칸 수는 현재 판(5칸)에 맞췄다 — 판이 바뀌면 <see cref="Validate"/> 가 어긋남을 말한다.
        /// 특수 핀 둘은 <c>PinballBoard.asset</c> 의 중앙 범퍼 두 개(태그 1·2)와 짝이다.
        /// </summary>
        /// <summary>
        /// 칸별 골드. <b>보기 좋은 수</b>로 잡았다 — 가장자리 10, 안쪽 셋 15. <b>잭팟 칸은 없다</b>(2026-11 사용자 결정:
        /// "대박"이라는 이름은 보너스 라운드 입구인 대박핀(상자)이 가진다). 가운데 칸도 평범한 15 이다.
        /// 다섯 칸이 모두 20% 라서 발당 기댓값 = 0.2 × (10 + 15 + 15 + 15 + 10) = 13.0(5,000발에 6.5만 골드, 0.1원/골드 → 약 6,500원).
        /// 예전 5·10·500·10·5 는 발당 12.4 였다 — 칸 확률을 고르게 하면서 장부(약 6.2만)보다 약 4% 후해졌다.
        /// </summary>
        public static readonly int[] GoldPerSlot = { 10, 15, 15, 15, 10 };

        /// <summary>
        /// 칸별 시너지 티켓. 칸 확률이 20% 라 정수 한 장이 두 칸에 <b>확정</b>으로 걸리면
        /// 발당 0.4 로 장부(0.12)의 3배가 넘는다. 그래서 <b>확률로 준다</b> — 2·4번 칸에 1장씩, 30% 지급이면
        /// 0.2 × 2 × 0.30 = 0.12 (5,000발에 600장, 하루 2,016발이면 약 242장).
        /// 0 은 그 칸이 주지 않는다는 뜻이다.
        /// </summary>
        public static readonly int[] SynergyTicketsPerSlot = { 0, 1, 0, 1, 0 };
        public const int SynergyTicketChancePercent = 30;

        /// <summary>
        /// 칸별 고기. <b>장부 밖 값이다</b> — 고기는 가치 장부에 없고 고기 루프(정책 4장 r)가 따로 잡는다.
        ///
        /// 클리어당 티켓을 1장에서 12장으로 올리면서(<c>StageRewardCalculator.PinballTicketPerClear</c>, 하루 2,016발)
        /// 발당 고기를 1.24 → 0.11 로 내렸다. 그대로 두면 r = 168 × 12 × 1.24 ÷ 720 ≈ 3.5 로 고기가 불어난다.
        /// r = 하루 발수 × 발당 고기 ÷ 하루 소탕 고기(720) = 2,016 × 0.11 ÷ 720 ≈ 0.31 (목표 0.3, 상한 0.35).
        /// 하루 소탕 고기 720 은 고기 축제(2분마다 1개)가 기준이다.
        /// 정수 1 을 확정으로 걸면 발당 1.0 이라 r 이 상한을 넘는다. 그래서 <b>모든 칸이 1개를 11% 확률로</b> 준다 —
        /// 칸 확률의 합이 1 이니 발당 기댓값은 어느 칸에 얼마를 걸든 0.11 이다(예전엔 가운데 1% 칸이 11개를 몰아 줘서 같은 0.11 을 냈다).
        /// 고기 "잭팟"은 없다 — 한 번에 받는 고기는 늘 1개이고, 하루 변동은 2,016발에 걸친 이항 분포라 작다.
        /// </summary>
        public static readonly int[] StaminaPerSlot = { 1, 1, 1, 1, 1 };
        public const int StaminaChancePercent = 11;

        /// <summary>
        /// 칸별 열린 이벤트 재화. 장부 값 발당 0.09(5,000발에 450개, 20원 → 0.9만 원). 가장자리 1·5번 칸에 1개씩
        /// <b>22% 확률</b>로 준다 — 0.2 × 2 × 0.22 = 0.088. 지급 순간 그날 열린 이벤트의 재화로 풀린다(<c>EventCurrencyRules</c>).
        /// </summary>
        public static readonly int[] EventCurrencyPerSlot = { 1, 0, 0, 0, 1 };
        public const int EventCurrencyChancePercent = 22;

        /// <summary>
        /// 중간 핀의 "몇 번 맞으면 보상이 나가는가". 공 1발당 적중 횟수를 시뮬로 쟀다
        /// (2026-11, 아트 PSD 배치 + 아치 판 셋을 일반 벽으로 둘러 상자를 감싼 판. 칸 확률 0.2 × 5 로 칸을 뽑고
        /// 그 칸의 <b>시드표 풀</b>(칸마다 2,000개)에서 공을 뽑는다 — 실제 게임이 하는 그대로, 풀 전체를 다시 돌려 평균을 냈다):
        /// 카피핀(1번) 0.3612 / 뽑기권핀(2번) 0.3627 / 대박핀(3번) 0.6186. 판이나 칸 확률을 바꾸면 다시 재서 고칠 것.
        /// (예전 칸 확률 0.2475/0.2475/0.01/0.2475/0.2475 에서는 0.370 / 0.369 / 0.690 이었다.)
        /// 시드표는 재미있는 궤적을 골라 담아서 날것 궤적보다 특수 핀을 자주 맞힌다(날것으로 재면 대박핀 0.192). 장부는 실제 게임 기준으로 맞춘다.
        ///
        /// 5,000발 기준 완주 횟수 = 발당 적중 × 5,000 ÷ 필요 적중:
        ///   1번  11회  → 164.2번 (완주마다 카피 1장: 레어·킹 17% / 기본 83% → 레어·킹 27.9장 + 기본 136.3장; 장부 27.5 / 140)
        ///   2번 151회  → 12.0번 (일반 보석 뽑기권 12장; 장부 12)
        ///   3번 309회  → 10.0번 (Bonus 입장 10회, 500발당 1회; 장부 10)
        ///
        /// 카피핀은 <b>자주 나오고 한 번에 1장</b>이다(예전엔 40회마다 1장+5장을 몰아 줬다 — 2026-11 사용자 결정).
        /// 무엇이 나올지는 확률이다(랜덤 1종 핀의 가중치).
        ///
        /// 3번 대박핀은 아치 판 셋(좌·우·아래)에 둘러싸여 맞히기 어렵다(공 한 발이 한 번이라도 맞힐 확률 약 25%).
        /// 한 번 들어가면 틀 안에서 여러 번 튀기 때문에 맞힌 공은 평균 2.7번 맞힌다. 게이지는 34/67/100% 에서 판이 하나씩 켜진다.
        /// 보너스 라운드의 입구는 이 핀 하나뿐이고 완주 보상도 그 라운드 하나다(확정 보상을 더 얹지 않는다 — 2026-11 사용자 결정).
        /// </summary>
        public const int CopyPinRequiredHits = 11;
        public const int CopyPinRareKingPercent = 17;
        public const int CopyPinBasicPercent = 83;
        public const int GemTicketPinRequiredHits = 151;
        public const int BonusPinRequiredHits = 309;

        /// <summary>
        /// 칸별 경품 — <b>가치 장부(2026-11)의 5,000발 값에서 거꾸로 잡았다.</b> 에셋과 폴백이 같은 표를 쓰도록 static 으로 뺐다.
        /// 판의 declaredProbability 는 다섯 칸 모두 0.2 다(2026-11 사용자 결정: 가운데 칸도 20%).
        ///
        /// 발당 기댓값 = 칸 확률 가중 평균:
        ///   골드        13.0   (5,000발에 6.5만)      = 10·15·15·15·10 (보기 좋은 수 — 장부 6.2만보다 약 4% 후하다)
        ///   시너지 티켓 0.12   (5,000발에 600)        = 2·4번 칸 1장 × 30% 확률
        ///   고기        0.11   (고기 루프의 몫 — 위 StaminaPerSlot 주석)  = 모든 칸 1개 × 11% 확률
        ///   이벤트 재화 0.088  (5,000발에 440)        = 1·5번 칸 1개 × 22% 확률 (지급 순간 열린 이벤트의 재화로 풀린다)
        ///
        /// 확률 지급(<c>chancePercent</c>)을 쓰는 이유: 정수 1 을 확정으로 걸면 20% 칸에서 발당 0.2 라 장부의 2~4배가 된다.
        /// </summary>
        public static List<PinballSlotReward> BuildDefaultSlotRewards()
        {
            var result = new List<PinballSlotReward>(GoldPerSlot.Length);
            for (int i = 0; i < GoldPerSlot.Length; i++)
            {
                var slot = new PinballSlotReward
                {
                    label = GoldPerSlot[i].ToString("N0") + " 골드" +
                            (StaminaPerSlot[i] > 0 ? " + 고기 " + StaminaPerSlot[i] + " (" + StaminaChancePercent + "%)" : string.Empty) +
                            (SynergyTicketsPerSlot[i] > 0 ? " + 시너지 티켓 " + SynergyTicketsPerSlot[i] + " (" + SynergyTicketChancePercent + "%)" : string.Empty) +
                            (EventCurrencyPerSlot[i] > 0 ? " + 이벤트 재화 " + EventCurrencyPerSlot[i] + " (" + EventCurrencyChancePercent + "%)" : string.Empty),
                };
                slot.rewards.Add(new PinballReward(PointType.Gold, GoldPerSlot[i]));
                if (StaminaPerSlot[i] > 0)
                    slot.rewards.Add(new PinballReward(PointType.Stamina, StaminaPerSlot[i], StaminaChancePercent));
                if (SynergyTicketsPerSlot[i] > 0)
                    slot.rewards.Add(new PinballReward(PointType.SynergyTicket, SynergyTicketsPerSlot[i], SynergyTicketChancePercent));
                if (EventCurrencyPerSlot[i] > 0)
                    slot.rewards.Add(new PinballReward(PointType.OpenEventCurrency, EventCurrencyPerSlot[i], EventCurrencyChancePercent));
                result.Add(slot);
            }

            return result;
        }

        public void PopulateDefaults()
        {
            ticketCost = 1;
            maxShotsPerSession = 15;
            slotRewards.Clear();
            specialRewards.Clear();
            PopulateMultiplierDefaults();
            slotRewards.AddRange(BuildDefaultSlotRewards());

            // 중간 핀 셋 (기본판의 태그 1·2·3). 필요 적중 수는 위 상수의 시뮬 측정에서 왔다.
            //
            // 1번 핀 = 카피핀: 완주 한 번에 <b>카피 1장</b> — 랜덤 레어·킹 카피 17% / 랜덤 기본 카피 83%(랜덤 1종 핀, chancePercent 가 가중치).
            //   5,000발에 170번 완주 → 레어·킹 29.0장(3,500원) + 기본 141.5장(700원) ≈ 20.1만 원.
            var copyPin = new PinballSpecialReward
            {
                tag = 1, label = "카피핀", requiredHits = CopyPinRequiredHits, pickOneAtRandom = true,
            };
            copyPin.rewards.Add(new PinballReward(PointType.RandomRareKingCopy, 1, CopyPinRareKingPercent));
            copyPin.rewards.Add(new PinballReward(PointType.RandomBasicCopy, 1, CopyPinBasicPercent));
            specialRewards.Add(copyPin);

            // 2번 핀 = 뽑기권핀: 일반 보석 뽑기권 1장. 5,000발에 12장(500원) = 0.6만 원.
            //   (예전 장비핀이 있던 자리다 — 장비 두루마리의 핀볼 수급은 없어졌고 스테이지 클리어가 남는다.)
            var gemPin = new PinballSpecialReward
            {
                tag = 2, label = "뽑기권핀", requiredHits = GemTicketPinRequiredHits, pickOneAtRandom = false,
            };
            gemPin.rewards.Add(new PinballReward(PointType.NormalGemTicket, 1));
            specialRewards.Add(gemPin);

            // 3번 핀 = 대박핀: 보상을 걸지 않는다 — 가득 차면 보너스 라운드가 열린다(BonusDiceDatabase.pinballTriggerTag).
            //   5,000발에 10번(500발당 1회). 아치 판 셋이 둘러싼 상자 핀이라 맞히기 어렵고, 그래서 이 핀이 대박이다.
            specialRewards.Add(new PinballSpecialReward
            {
                tag = 3, label = "대박핀", requiredHits = BonusPinRequiredHits, pickOneAtRandom = false,
            });
        }

        /// <summary>칸 하나의 경품. 범위를 벗어나면 null.</summary>
        public PinballSlotReward GetSlot(int slot)
        {
            if (slotRewards == null || slot < 0 || slot >= slotRewards.Count)
                return null;

            return slotRewards[slot];
        }

        /// <summary>태그에 걸린 누적 보상. 없으면 null — 그 태그는 게이지가 오르지 않는다.</summary>
        public PinballSpecialReward GetSpecial(int tag)
        {
            if (specialRewards == null)
                return null;

            for (int i = 0; i < specialRewards.Count; i++)
            {
                PinballSpecialReward rule = specialRewards[i];
                if (rule != null && rule.tag == tag)
                    return rule;
            }

            return null;
        }

        /// <summary>
        /// 판과 짝이 맞는지 본다. <paramref name="slotCount"/> 에는
        /// <c>PinballBoard.SlotCount</c> 를 넘긴다.
        /// </summary>
        public bool Validate(int slotCount, out string error)
        {
            if (!Validate(slotRewards, specialRewards, slotCount, out string slotError))
            {
                error = slotError;
                return false;
            }

            return ValidateMultipliers(multiplierTiers, out error);
        }

        /// <summary>그 배율의 조건. 목록에 없으면 null.</summary>
        public PinballMultiplierTier GetTier(int multiplier)
        {
            if (multiplierTiers == null)
                return null;

            for (int i = 0; i < multiplierTiers.Count; i++)
            {
                PinballMultiplierTier tier = multiplierTiers[i];
                if (tier != null && tier.multiplier == multiplier)
                    return tier;
            }

            return null;
        }

        /// <summary>
        /// 배율 표를 검사한다. 목록을 직접 받는 <c>static</c> 인 이유는 칸 경품 쪽과 같다 —
        /// 헤드리스 러너가 <c>ScriptableObject</c> 를 못 만든다.
        /// </summary>
        public static bool ValidateMultipliers(
            IReadOnlyList<PinballMultiplierTier> tiers, out string error)
        {
            if (tiers == null || tiers.Count == 0)
            {
                error = "배율 표가 비었다. 조건 없이 쓸 수 있는 x1 이 하나는 있어야 발사할 수 있다. ";
                return false;
            }

            var problems = new StringBuilder();
            var seen = new HashSet<int>();
            bool hasBase = false;
            int previousMultiplier = int.MinValue;
            int previousRequired = int.MinValue;

            for (int i = 0; i < tiers.Count; i++)
            {
                PinballMultiplierTier tier = tiers[i];
                if (tier == null)
                    continue;

                if (!seen.Add(tier.multiplier))
                    problems.Append("배율 x").Append(tier.multiplier).Append(" 가 중복이다. ");

                if (tier.multiplier < 1)
                    problems.Append("배율이 1 미만이다: x").Append(tier.multiplier).Append(". ");

                if (tier.multiplier == 1)
                {
                    hasBase = true;

                    // x1 을 잠그면 티켓을 처음 얻은 사람이 아무것도 못 한다.
                    if (tier.requiredTickets > 0)
                    {
                        problems.Append("x1 의 조건이 0 이 아니다(")
                                .Append(tier.requiredTickets).Append("). ");
                    }
                }

                // 높은 배율이 더 싸면 낮은 배율을 고를 이유가 없어져 표가 무의미해진다.
                if (tier.multiplier > previousMultiplier && tier.requiredTickets < previousRequired)
                {
                    problems.Append("x").Append(tier.multiplier)
                            .Append(" 의 조건이 앞 단계보다 낮다. ");
                }

                previousMultiplier = tier.multiplier;
                previousRequired = tier.requiredTickets;
            }

            if (!hasBase)
                problems.Append("x1 이 없다. 조건 없이 쓸 수 있는 배율이 하나는 있어야 한다. ");

            error = problems.ToString();
            return error.Length == 0;
        }

        /// <summary>
        /// 목록을 직접 받는 검사. <b>이 형태가 정본이다</b> — 헤드리스 EditMode 러너에서는
        /// <c>ScriptableObject.CreateInstance</c> 가 돌지 않아, 인스턴스 메서드로만 두면
        /// 이 규칙에 테스트가 닿지 못한다(<c>DiceUnlockDatabase.Validate</c> 와 같은 이유).
        ///
        /// <b>확률 합은 검사하지 않는다</b> — <c>DeclaredProbability_SumsToOne</c> 의 몫이고,
        /// 같은 것을 두 곳에서 검사하면 한쪽을 고칠 때 다른 쪽이 남는다.
        /// </summary>
        public static bool Validate(
            IReadOnlyList<PinballSlotReward> slots,
            IReadOnlyList<PinballSpecialReward> specials,
            int slotCount,
            out string error)
        {
            var problems = new StringBuilder();

            if (slots == null || slots.Count != slotCount)
            {
                problems.Append("칸 수가 판과 다르다: 경품표 ")
                        .Append(slots == null ? 0 : slots.Count)
                        .Append("칸 vs 판 ").Append(slotCount).Append("칸. ");
            }
            else
            {
                for (int i = 0; i < slots.Count; i++)
                {
                    PinballSlotReward slot = slots[i];
                    if (slot == null || slot.rewards == null || slot.rewards.Count == 0)
                        problems.Append(i).Append("번 칸에 경품이 없다. ");
                }
            }

            if (specials != null)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < specials.Count; i++)
                {
                    PinballSpecialReward rule = specials[i];
                    if (rule == null)
                        continue;

                    if (!seen.Add(rule.tag))
                        problems.Append("특수 핀 태그 ").Append(rule.tag).Append(" 가 중복이다. ");

                    if (rule.requiredHits <= 0)
                        problems.Append("특수 핀 태그 ").Append(rule.tag)
                                .Append(" 의 필요 적중 수가 0 이하다. ");
                }
            }

            error = problems.ToString();
            return error.Length == 0;
        }
    }
}
