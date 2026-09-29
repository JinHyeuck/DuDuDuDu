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

        public PinballReward(PointType pointType, int amount)
        {
            this.pointType = pointType;
            this.amount = Mathf.Max(1, amount);
        }

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
        public void PopulateDefaults()
        {
            ticketCost = 1;
            maxShotsPerSession = 15;
            slotRewards.Clear();
            specialRewards.Clear();
            PopulateMultiplierDefaults();

            // 칸별 경품. <b>가운데 칸이 잭팟이다</b> — 판의 declaredProbability 가
            // 0.2475 / 0.2475 / <b>0.01</b> / 0.2475 / 0.2475 라 100발에 한 번 걸린다.
            //
            // <b>확률을 깎는 것이 잭팟의 핵심이다.</b> 예전에는 다섯 칸이 0.16~0.22 로 고르고
            // 금액이 1000~5000 이었다. 그 모양으로는 무엇을 얹어도 대박이 안 나온다 —
            // 고기는 특히 그렇다. 발당 최소 1개씩만 줘도 96발이면 96개인데 회수율 상한
            // (r = 0.35, 하루 126개)이 거의 다 차서 잭팟에 쓸 몫이 안 남는다.
            // 한 칸을 1% 로 깎으면 나머지 99% 가 1개씩이어도 그 칸에 25개를 넣을 수 있다.
            //
            //   고기 기댓값 = 0.99 x 1 + 0.01 x 25 = 1.24  ->  하루 119개, r = 0.331
            //   골드 기댓값 = 248.5                        ->  하루 23,856 (수요의 2.6배)
            //
            // 골드는 1000/2000/5000 -> 100/200/10000 -> 지금 값으로 두 번 내렸다.
            // 마지막 인하는 <b>골드를 병목으로 만들기 위한 것</b>이다(StageRewardFormula
            // .GuaranteedNormalGold 주석). 발당 21.375 로 하루 2,052 다.
            int[] golds = { 10, 15, 900, 15, 10 };
            int[] stamina = { 1, 1, 25, 1, 1 };
            for (int i = 0; i < golds.Length; i++)
            {
                bool jackpot = stamina[i] > 1;
                var slot = new PinballSlotReward
                {
                    label = (jackpot ? "대박! " : string.Empty) +
                            golds[i].ToString("N0") + " 골드 + 고기 " + stamina[i],
                };
                slot.rewards.Add(new PinballReward(PointType.Gold, golds[i]));
                slot.rewards.Add(new PinballReward(PointType.Stamina, stamina[i]));
                slotRewards.Add(slot);
            }

            // <b>무료젬 10 이었다.</b> 무료젬은 티어 6 이라 여기 있을 물건이 아니다 —
            // Base 는 티어 3 공급처이고, 상위 재화는 Pinball_Bonus 한 곳에서만 나와야
            // 성장 페이스가 한 손잡이에 모인다(정책 3.2 · 5.1).
            //
            // 다이스 스크롤은 티어 3 이라 Base 와 티어가 맞고, 고기가 아니라서
            // 루프 회수율 r 을 건드리지 않는다.
            //
            // 10장인 근거: 소탕이 종당 하루 115장을 주므로(정책 1.5) 이 핀이 하루
            // 다섯 번쯤 차면 종당 10장, 약 9% 다. 그 이상이면 "Scroll 이 강화 게이트"
            // (정책 7.1)가 핀볼로 우회된다 — Gold 를 게이트로 못 쓰는 이유와 같은 논리다.
            var scrollPin = new PinballSpecialReward
            {
                tag = 1, label = "스크롤핀", requiredHits = 10, pickOneAtRandom = true,
            };
            scrollPin.rewards.Add(new PinballReward(PointType.NormalScroll, 10));
            scrollPin.rewards.Add(new PinballReward(PointType.FireScroll, 10));
            scrollPin.rewards.Add(new PinballReward(PointType.IceScroll, 10));
            scrollPin.rewards.Add(new PinballReward(PointType.PoisonScroll, 10));
            scrollPin.rewards.Add(new PinballReward(PointType.ThunderScroll, 10));
            specialRewards.Add(scrollPin);

            // <b>전투 재화를 주면 안 된다.</b> 핀볼은 로비 컨텐츠라, 판이 시작될 때 0 으로
            // 밀리는 재화(SP·강화석)를 여기서 주면 받자마자 사라진다 — 탑이 먼저 같은
            // 함정에 빠졌고 같은 이유로 신화석으로 갈아탔다(TowerRunManager.BuildClearRewards).
            //
            // <b>강화석 → 레어석 → 장비 스크롤로 두 번 바뀌었다.</b> 레어석은 티어 4 라
            // 대표 수급처가 Pinball_Bonus 이고(정책 2장), 거기 레어석 핀(75)이 생긴 지금은
            // Base 가 1개씩 흘리는 것이 원칙만 새게 한다. 당시 근거였던 "핀볼에 다이스 성장
            // 축이 비어 있다"도 보너스 라운드가 채웠다.
            //
            // 10장인 근거: 소탕이 종당 하루 48장을 주므로(정책 1.5) 이 핀이 하루 세 번쯤
            // 차면 종당 약 5장, 11% 다. 스크롤핀(tag 1)과 같은 비율이다.
            var equipPin = new PinballSpecialReward
            {
                tag = 2, label = "장비핀", requiredHits = 15, pickOneAtRandom = true,
            };
            equipPin.rewards.Add(new PinballReward(PointType.WeaponScroll, 10));
            equipPin.rewards.Add(new PinballReward(PointType.HelmetScroll, 10));
            equipPin.rewards.Add(new PinballReward(PointType.ArmorScroll, 10));
            equipPin.rewards.Add(new PinballReward(PointType.RingScroll, 10));
            equipPin.rewards.Add(new PinballReward(PointType.ShoesScroll, 10));
            equipPin.rewards.Add(new PinballReward(PointType.NecklaceScroll, 10));
            specialRewards.Add(equipPin);
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
