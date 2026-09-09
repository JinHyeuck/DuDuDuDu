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

        // ── 8.3 일일상점 ──────────────────────────────────────────────
        [Serializable]
        public sealed class DailyOffer
        {
            public Reward reward = new Reward(PointType.Gold, 100000);
            public PointType costType = PointType.FreeGem;
            public int cost = 30;

            /// <summary>0 이면 정가. 기획서 8.3 "확률적으로 할인 슬롯 등장".</summary>
            [Range(0, 90)] public int discountPercent;
        }

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
            public PointType stoneType = PointType.MythicScroll;
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

        [Header("8.1 성장 패키지")]
        [Tooltip("동시 노출 최대 2개 (기획서 8.1 설계규칙 2번).")]
        [SerializeField] private List<GrowthPackage> growthPackages = new List<GrowthPackage>();

        [Header("8.2 보석뽑기")]
        [SerializeField] private List<GemBox> gemBoxes = new List<GemBox>();

        [Header("8.3 일일상점")]
        [Tooltip("슬롯 6칸 고정 (2행 x 3열).")]
        [SerializeField] private List<DailyOffer> dailyOffers = new List<DailyOffer>();

        [Tooltip("하루에 광고를 보고 갱신할 수 있는 횟수.")]
        [SerializeField] private int dailyAdRefreshPerDay = 2;

        [Tooltip("하루에 무료젬으로 갱신할 수 있는 횟수.")]
        [SerializeField] private int dailyGemRefreshPerDay = 3;

        [Tooltip("무료젬 갱신 1회 비용.")]
        [SerializeField] private int dailyGemRefreshCost = 1000;

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
        public IReadOnlyList<DailyOffer> DailyOffers => dailyOffers;
        public int DailyAdRefreshPerDay => Mathf.Max(0, dailyAdRefreshPerDay);
        public int DailyGemRefreshPerDay => Mathf.Max(0, dailyGemRefreshPerDay);
        public int DailyGemRefreshCost => Mathf.Max(0, dailyGemRefreshCost);
        public IReadOnlyList<PaidGemOffer> PaidGemOffers => paidGemOffers;
        public int FreeGemPerPaidGem => Mathf.Max(1, freeGemPerPaidGem);
        public IReadOnlyList<int> FreeGemExchangeUnits => freeGemExchangeUnits;
        public IReadOnlyList<StoneOffer> StoneOffers => stoneOffers;
        public IReadOnlyList<GoldOffer> GoldOffers => goldOffers;

        /// <summary>일일상점 슬롯 수. 기획서 8.3 "6칸 고정 (2행 × 3열)".</summary>
        public const int DailySlotCount = 6;

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
                        new Reward(PointType.MythicScroll, 15),
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

            dailyOffers = new List<DailyOffer>
            {
                new DailyOffer { reward = new Reward(PointType.NormalScroll, 5), costType = PointType.FreeGem, cost = 50 },
                new DailyOffer { reward = new Reward(PointType.Gold, 100000), costType = PointType.FreeGem, cost = 30 },
                new DailyOffer { reward = new Reward(PointType.SpecialDiceCore, 3), costType = PointType.FreeGem, cost = 200 },
                new DailyOffer { reward = new Reward(PointType.WeaponScroll, 10), costType = PointType.FreeGem, cost = 80 },
                new DailyOffer { reward = new Reward(PointType.SpecialDiceCore, 3), costType = PointType.FreeGem, cost = 150 },
                new DailyOffer { reward = new Reward(PointType.Gold, 300000), costType = PointType.FreeGem, cost = 70, discountPercent = 20 },
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

            dailyAdRefreshPerDay = 2;
            dailyGemRefreshPerDay = 3;
            dailyGemRefreshCost = 1000;

            freeGemPerPaidGem = 1;
            freeGemExchangeUnits = new List<int> { 100, 500, 1000 };

            // 스샷 레퍼런스대로 종류마다 3티어. 그리드가 3열이라 신화석 한 줄, 레어석 한 줄로 앉는다.
            stoneOffers = new List<StoneOffer>
            {
                new StoneOffer { stoneType = PointType.MythicScroll, amountPerPurchase = 5, costFreeGem = 500 },
                new StoneOffer { stoneType = PointType.MythicScroll, amountPerPurchase = 50, costFreeGem = 5000 },
                new StoneOffer { stoneType = PointType.MythicScroll, amountPerPurchase = 150, costFreeGem = 15000 },
                new StoneOffer { stoneType = PointType.SpecialDiceCore, amountPerPurchase = 5, costFreeGem = 500 },
                new StoneOffer { stoneType = PointType.SpecialDiceCore, amountPerPurchase = 50, costFreeGem = 5000 },
                new StoneOffer { stoneType = PointType.SpecialDiceCore, amountPerPurchase = 150, costFreeGem = 15000 },
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

            if (dailyOffers.Count != DailySlotCount)
                problems.Add("일일상점 슬롯이 " + dailyOffers.Count + "개다. 6칸 고정이다 (기획서 8.3).");

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
