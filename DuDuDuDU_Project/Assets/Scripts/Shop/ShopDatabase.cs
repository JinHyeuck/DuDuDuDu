using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ.Shop
{
    /// <summary>
    /// 상점 7개 섹션의 수치 정본. (기획서 <c>Docs/ShopPackageDesign.md</c> 8장)
    ///
    /// <b>왜 SO 인가.</b> AGENTS.md 확정 결정 3번 — 밸런스 수치는 SO 로 올린다. 상점은
    /// 그 결정이 가장 먼저 필요한 자리다. 가격 하나 고치자고 빌드를 다시 굽게 되면
    /// 기획서 10장의 "미확정" 열아홉 줄이 전부 코드 수정 요청으로 바뀐다.
    ///
    /// <b>여기 있는 수치는 전부 임시값이다.</b> 기획서가 "미정"이라고 적은 것을 0 으로 두면
    /// 화면이 빈 칸으로 구워져 레이아웃 검증이 안 된다. 그래서 기획서에 예시로 적힌 값
    /// (유료젬 5종·스타트 패키지 등)은 그대로 옮기고, 예시조차 없는 것은 자릿수만 맞춘
    /// 그럴듯한 값을 넣었다. <b>확정 전까지 이 값으로 밸런스를 논하지 말 것.</b>
    /// </summary>
    [CreateAssetMenu(fileName = "ShopDatabase", menuName = "OJ/Shop Database")]
    public sealed class ShopDatabase : ScriptableObject
    {
        /// <summary>재화 한 덩이. <c>PointRewardEntry</c> 는 직렬화 속성이 없어 여기서 따로 둔다.</summary>
        [Serializable]
        public struct Reward
        {
            public PointType pointType;
            public int amount;

            public Reward(PointType pointType, int amount)
            {
                this.pointType = pointType;
                this.amount = amount;
            }
        }

        // ── 8.1 성장 패키지 ────────────────────────────────────────────
        [Serializable]
        public sealed class GrowthPackage
        {
            public string id = "start";
            public string title = "스타트 패키지";

            /// <summary>노출 조건 한 줄. 기획서 8.1 상품 예시의 "노출 조건" 열이다.</summary>
            public string conditionText = "최초 진입 후";

            public int priceWon = 3000;

            /// <summary>남은 시간(시). 0 이면 기간 제한 없음 — 타이머 줄을 숨긴다.</summary>
            public int durationHours = 48;

            public Sprite art;
            public List<Reward> contents = new List<Reward>();
        }

        // ── 8.2 보석뽑기 ──────────────────────────────────────────────
        [Serializable]
        public sealed class GemBox
        {
            public string title = "일반 보석 상자";

            /// <summary>
            /// 등장 등급 구간. 기획서 8.2 설계규칙 1번대로 기존 <see cref="Rarity"/> 를 그대로 쓴다.
            /// 두 상자가 한 칸 겹치는 것(2번)이 의도이므로 검사로 막지 않는다.
            /// </summary>
            public Rarity minRarity = Rarity.Common;
            public Rarity maxRarity = Rarity.Rare;

            public PointType costType = PointType.FreeGem;
            public int costSingle = 300;

            /// <summary>10회 비용. 기획서 8.2 3번 "10회 할인 여부는 미정" — 지금은 정가 10배다.</summary>
            public int costTen = 3000;

            /// <summary>
            /// 등급별 가중치. <see cref="minRarity"/> 부터 차례로 대응한다
            /// (구간이 일반~레어면 [일반, 노말, 레어]).
            ///
            /// <b>비워 두면 균등이다.</b> 기획서 10장이 "보석뽑기 등급별 확률 미정"으로
            /// 남긴 항목이라, 여기에 코드가 임의의 곡선을 박으면 그것이 곧 기획이 된다.
            /// 값을 넣는 것은 기획이 정해진 뒤다.
            /// </summary>
            public List<int> rarityWeights = new List<int>();
        }

        // 8.3 일일상점은 삭제했다. 핀볼이 들어오면서 역할이 8.6 다이스석 상점 ·
        // 8.7 골드 상점과 겹쳤고, 유지비(일일 리셋 · 슬롯 랜덤 · 갱신 2종 · 할인 슬롯)만
        // 남았다. 근거는 Docs/CurrencyPolicy.md 6.3 에 있다.

        // ── 8.4 유료젬 상점 ───────────────────────────────────────────
        [Serializable]
        public sealed class PaidGemOffer
        {
            public int gemAmount = 100;
            public int priceWon = 1000;

            /// <summary>
            /// 같은 금액의 멤버십 상품을 이 줄에 작게 병기한다(기획서 8.4 3번).
            /// 비어 있으면 아무것도 안 붙는다.
            /// </summary>
            public string pairedProductLabel;
        }

        // ── 8.6 다이스석 상점 ─────────────────────────────────────────

        /// <summary>
        /// 다이스석 한 칸. 스샷 레퍼런스대로 <b>수량 티어를 3칸 나열</b>한다(5 / 50 / 150 꼴).
        ///
        /// <b>일일 한도도 누진도 없다.</b> 기획서 8.6 은 한도를 요구했지만 개발 중 판단으로
        /// 뺐다 — 지금은 로직을 눌러 보는 것이 먼저이고, 한도가 있으면 세 번 사고 나면
        /// 더 못 눌러 본다. <b>되살릴 때는 기획서 8.6-1 의 근거(보유량이 그대로 스펙이 된다)를
        /// 다시 읽을 것.</b>
        /// </summary>
        [Serializable]
        public sealed class StoneOffer
        {
            public PointType stoneType = PointType.MythicStone;
            public int amountPerPurchase = 5;
            public int costFreeGem = 500;
        }

        // ── 8.7 골드 상점 ─────────────────────────────────────────────
        [Serializable]
        public sealed class GoldOffer
        {
            public int goldAmount = 1000000;
            public int baseCostFreeGem = 100;

            /// <summary>
            /// 구매 1회마다 오르는 단가. 기획서 8.7 2번은 <b>누진을 확정</b>했다(한도가 없으므로
            /// 이것이 유일한 제동이다). 0 으로 두면 무료젬이 골드로만 빠져나간다.
            /// </summary>
            public int costIncreasePerPurchase = 20;
        }

        // ── 5 멤버십 (광고제거권 · 고기 멤버십) ───────────────────────
        /// <summary>
        /// 멤버십 창의 두 상품과 그것이 푸는 제한. (기획서 ShopPackageDesign 5장 · BMDesign 4·5장)
        ///
        /// <b>무료 소탕 한도가 여기 있는 이유.</b> 이 한도는 고기 멤버십이 풀라고 있는 것이다 —
        /// 상품과 그것이 푸는 제한이 다른 에셋에 있으면 한쪽만 고쳐져 멤버십이 아무것도 안 푸는
        /// 날이 온다. 기본값 72 는 <c>SweepEconomy.DailySweepClears</c>(고기 유입으로 하루에 돌 수 있는
        /// 소탕 수)와 같다 — 기획서 5.3 의 "Day 3 부터 체증" 곡선은 아직 미정이라 고정값이다.
        /// </summary>
        [Serializable]
        public sealed class MembershipOffers
        {
            [Tooltip("광고제거권 가격(원). 기획서 4장: 3,000원, 무제한.")]
            public int adRemovalPriceWon = 3000;

            [Tooltip("고기 멤버십 가격(원). 기획서 5장: 5,000원.")]
            public int meatMembershipPriceWon = 5000;

            [Tooltip("고기 멤버십 기간(일). 산 시점부터 흐르고, 남은 중에 사면 만료일 뒤로 붙는다.")]
            [Min(1)] public int meatMembershipDays = 30;

            [Tooltip("만료 며칠 전부터 재구매를 연다. 기획서 5.2-4: 3일.")]
            [Min(0)] public int rebuyWindowDays = 3;

            [Tooltip("멤버십이 없을 때 하루 소탕 한도. 멤버십은 이것을 풀어 준다.")]
            [Min(1)] public int freeDailySweepLimit = 72;
        }

        [Header("5 멤버십")]
        [SerializeField] private MembershipOffers membership = new MembershipOffers();

        public MembershipOffers Membership => membership ?? (membership = new MembershipOffers());

        // ── 4 특별한 7일 ─────────────────────────────────────────────
        /// <summary>
        /// 특별한 7일 출석. (기획서 BMDesign 6장 · ShopPackageDesign 4장)
        ///
        /// <b>1일차 300 · 3일차 500 유료젬은 확정이다</b> — 각각 광고제거권·고기 멤버십 값과 정확히
        /// 같아야 "8,000원을 내고 둘을 공짜로 얻었다" 가 유저 머릿속에서 즉시 성립한다(6.3).
        /// 나머지 다섯 날은 기획서가 "미정, 기분 좋은 수준" 으로 남겼다. 지금 값은 하루 공급량
        /// (Docs/CurrencyPolicy.md 7.1)의 1~2.5일치로 잡은 <b>임시값</b>이다.
        /// </summary>
        /// <summary>하루치 보상. 줄 하나에 아이콘+이름으로 나란히 그린다(레퍼런스는 하루 2개).</summary>
        [Serializable]
        public sealed class DayRewards
        {
            public List<Reward> rewards = new List<Reward>();

            public DayRewards() { }

            public DayRewards(params Reward[] items)
            {
                rewards = new List<Reward>(items);
            }
        }

        [Serializable]
        public sealed class SpecialSevenDaysOffer
        {
            [Tooltip("가격(원). 기획서 6장: 8,000원(800유료젬).")]
            public int priceWon = 8000;

            [Tooltip("일차별 보상. 앞에서부터 1일차다. 줄 하나에 2개까지 그린다.")]
            public List<DayRewards> days = new List<DayRewards>
            {
                new DayRewards(new Reward(PointType.PaidGem, 300), new Reward(PointType.Gold, 5000)),
                new DayRewards(new Reward(PointType.Gold, 10000), new Reward(PointType.Stamina, 180)),
                new DayRewards(new Reward(PointType.PaidGem, 500), new Reward(PointType.RareStone, 50)),
                new DayRewards(new Reward(PointType.Stamina, 360), new Reward(PointType.RareStone, 50)),
                new DayRewards(new Reward(PointType.RareStone, 100), new Reward(PointType.Gold, 10000)),
                new DayRewards(new Reward(PointType.Gold, 15000), new Reward(PointType.MythicStone, 40)),
                new DayRewards(new Reward(PointType.MythicStone, 100), new Reward(PointType.Gold, 20000)),
            };

            [Tooltip("테두리를 강조할 일차(4.2-2 — 회수 구조의 핵심인 젬 줄).")]
            public List<int> highlightDays = new List<int> { 1, 3 };

            private static readonly List<Reward> None = new List<Reward>();

            /// <summary><paramref name="day"/> 일차 보상(1부터). 없으면 빈 목록.</summary>
            public IReadOnlyList<Reward> RewardsFor(int day)
            {
                return days != null && day >= 1 && day <= days.Count && days[day - 1]?.rewards != null
                    ? days[day - 1].rewards
                    : None;
            }

            public bool IsHighlighted(int day)
            {
                return highlightDays != null && highlightDays.Contains(day);
            }
        }

        [Header("4 특별한 7일")]
        [SerializeField] private SpecialSevenDaysOffer specialSevenDays = new SpecialSevenDaysOffer();

        public SpecialSevenDaysOffer SpecialSevenDays => specialSevenDays ?? (specialSevenDays = new SpecialSevenDaysOffer());

        [Header("8.1 성장 패키지")]
        [Tooltip("동시 노출 최대 2개 (기획서 8.1 설계규칙 2번).")]
        [SerializeField] private List<GrowthPackage> growthPackages = new List<GrowthPackage>();

        [Header("8.2 보석뽑기")]
        [SerializeField] private List<GemBox> gemBoxes = new List<GemBox>();





        [Header("8.4 유료젬 상점")]
        [SerializeField] private List<PaidGemOffer> paidGemOffers = new List<PaidGemOffer>();

        [Header("8.5 무료젬 상점")]
        [Tooltip("유료젬 1개로 받는 무료젬 수. 단일 고정값 — 구간별 우대 교환비를 넣지 않는다.")]
        [SerializeField] private int freeGemPerPaidGem = 1;

        [Tooltip("한 번에 교환할 수 있는 단위. 화면에 버튼으로 그대로 나온다.")]
        [SerializeField] private List<int> freeGemExchangeUnits = new List<int>();

        [Header("8.6 다이스석 상점")]
        [SerializeField] private List<StoneOffer> stoneOffers = new List<StoneOffer>();

        [Header("8.7 골드 상점")]
        [SerializeField] private List<GoldOffer> goldOffers = new List<GoldOffer>();

        public IReadOnlyList<GrowthPackage> GrowthPackages => growthPackages;
        public IReadOnlyList<GemBox> GemBoxes => gemBoxes;
        public IReadOnlyList<PaidGemOffer> PaidGemOffers => paidGemOffers;
        public int FreeGemPerPaidGem => Mathf.Max(1, freeGemPerPaidGem);
        public IReadOnlyList<int> FreeGemExchangeUnits => freeGemExchangeUnits;
        public IReadOnlyList<StoneOffer> StoneOffers => stoneOffers;
        public IReadOnlyList<GoldOffer> GoldOffers => goldOffers;


        /// <summary>
        /// 기획서의 예시 수치로 채운다. <b>에셋이 없을 때의 폴백이자 새 에셋의 초기값</b>이다.
        ///
        /// 값이 둘로 갈리지 않게 폴백과 초기값이 <b>같은 함수</b>를 쓴다 —
        /// <c>DiceMetaDataProvider.MergeMeta</c> 가 코드 표로 에셋을 덮어 생긴 문제
        /// (AGENTS.md "수치 정본이 코드다")를 여기서 되풀이하지 않으려는 것이다.
        /// 이 함수는 <b>채우기만</b> 하고 에셋 값을 덮지 않는다.
        /// </summary>
        public void PopulateDefaults()
        {
            growthPackages = new List<GrowthPackage>
            {
                new GrowthPackage
                {
                    id = "start",
                    title = "스타트 패키지",
                    conditionText = "최초 진입 후",
                    priceWon = 3000,
                    durationHours = 48,
                    contents = new List<Reward>
                    {
                        new Reward(PointType.PaidGem, 300),
                    },
                },
                new GrowthPackage
                {
                    id = "mythic3",
                    title = "3렙 신화 패키지",
                    conditionText = "신화 다이스 3레벨 도달",
                    priceWon = 5000,
                    durationHours = 0,
                    contents = new List<Reward>
                    {
                        new Reward(PointType.PaidGem, 250),
                        new Reward(PointType.MythicStone, 15),
                    },
                },
            };

            gemBoxes = new List<GemBox>
            {
                new GemBox
                {
                    title = "일반 보석 상자",
                    minRarity = Rarity.Common,
                    maxRarity = Rarity.Rare,
                    costType = PointType.FreeGem,
                    costSingle = 300,
                    costTen = 3000,
                    rarityWeights = new List<int> { 60, 30, 10 },
                },
                new GemBox
                {
                    title = "최고급 보석 상자",
                    minRarity = Rarity.Normal,
                    maxRarity = Rarity.Epic,
                    costType = PointType.FreeGem,
                    costSingle = 900,
                    costTen = 9000,
                    rarityWeights = new List<int> { 55, 35, 10 },
                },
            };


            // 기획서 8.4 예시 5종. 10원 = 1젬이 한 줄도 어긋나지 않아야 한다 — 묶음 할인이 없다는
            // 것이 이 표의 내용 전부다.
            // 3열 그리드라 6칸이 2줄로 딱 떨어진다. 기획서 8.4 예시 5종에 5,000 젬을 더했다 —
            // 5칸이면 둘째 줄에 빈칸이 둘 생겨 "품절된 칸" 처럼 보인다.
            paidGemOffers = new List<PaidGemOffer>
            {
                new PaidGemOffer { gemAmount = 100, priceWon = 1000 },
                new PaidGemOffer { gemAmount = 300, priceWon = 3000, pairedProductLabel = "광고제거권과 같은 금액" },
                new PaidGemOffer { gemAmount = 500, priceWon = 5000, pairedProductLabel = "고기 멤버십과 같은 금액" },
                new PaidGemOffer { gemAmount = 1000, priceWon = 10000 },
                new PaidGemOffer { gemAmount = 3000, priceWon = 30000 },
                new PaidGemOffer { gemAmount = 5000, priceWon = 50000 },
            };


            freeGemPerPaidGem = 1;
            freeGemExchangeUnits = new List<int> { 100, 500, 1000 };

            // 스샷 레퍼런스대로 종류마다 3티어. 그리드가 3열이라 신화석 한 줄, 레어석 한 줄로 앉는다.
            stoneOffers = new List<StoneOffer>
            {
                new StoneOffer { stoneType = PointType.MythicStone, amountPerPurchase = 5, costFreeGem = 500 },
                new StoneOffer { stoneType = PointType.MythicStone, amountPerPurchase = 50, costFreeGem = 5000 },
                new StoneOffer { stoneType = PointType.MythicStone, amountPerPurchase = 150, costFreeGem = 15000 },
                new StoneOffer { stoneType = PointType.RareStone, amountPerPurchase = 5, costFreeGem = 500 },
                new StoneOffer { stoneType = PointType.RareStone, amountPerPurchase = 50, costFreeGem = 5000 },
                new StoneOffer { stoneType = PointType.RareStone, amountPerPurchase = 150, costFreeGem = 15000 },
            };

            goldOffers = new List<GoldOffer>
            {
                new GoldOffer { goldAmount = 500000, baseCostFreeGem = 500, costIncreasePerPurchase = 50 },
                new GoldOffer { goldAmount = 5000000, baseCostFreeGem = 5000, costIncreasePerPurchase = 500 },
                new GoldOffer { goldAmount = 15000000, baseCostFreeGem = 15000, costIncreasePerPurchase = 1500 },
            };
        }

        /// <summary>
        /// 에셋이 성한지 본다. <b>기획서가 못 박은 것만</b> 검사한다 —
        /// "미정"인 수치에 범위를 걸면 확정될 때마다 검사를 같이 고쳐야 한다.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();

            if (growthPackages.Count > 2)
                problems.Add("성장 패키지가 " + growthPackages.Count + "개다. 동시 노출은 최대 2개다 (기획서 8.1-2).");


            for (int i = 0; i < paidGemOffers.Count; i++)
            {
                PaidGemOffer offer = paidGemOffers[i];

                // 10원 = 1유료젬. 이 한 줄이 BM 문서 2장의 환산 기준이고, 어긋나면
                // 출석 보상 300젬이 광고제거권 값으로 안 읽힌다.
                if (offer.priceWon != offer.gemAmount * 10)
                    problems.Add("유료젬 " + i + "번: " + offer.gemAmount + "젬 / " + offer.priceWon +
                                 "원 은 10원=1젬 이 아니다 (기획서 8.4).");
            }

            // 3열 그리드라 칸 수가 3의 배수가 아니면 마지막 줄에 빈칸이 남는다.
            // 빈칸은 "품절" 로 읽히므로 사고는 아니지만 화면이 어색해진다.
            if (paidGemOffers.Count % 3 != 0)
                problems.Add("유료젬이 " + paidGemOffers.Count + "칸이다. 3열 그리드라 3의 배수여야 줄이 안 빈다.");

            if (freeGemExchangeUnits.Count % 3 != 0)
                problems.Add("무료젬 교환 단위가 " + freeGemExchangeUnits.Count + "칸이다. 3의 배수여야 한다.");

            if (stoneOffers.Count % 3 != 0)
                problems.Add("다이스석이 " + stoneOffers.Count + "칸이다. 3의 배수여야 한다.");

            for (int i = 0; i < goldOffers.Count; i++)
            {
                if (goldOffers[i].costIncreasePerPurchase <= 0)
                    problems.Add("골드 " + i + "번: 누진 증가분이 0 이다. 골드 상점은 한도가 없어 " +
                                 "누진이 유일한 제동이다 (기획서 8.7-2).");
            }

            return problems;
        }
    }
}
