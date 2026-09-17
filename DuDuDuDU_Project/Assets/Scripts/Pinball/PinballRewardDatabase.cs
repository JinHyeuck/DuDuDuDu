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

            int[] golds = { 1000, 2000, 5000, 2000, 1000 };
            for (int i = 0; i < golds.Length; i++)
            {
                var slot = new PinballSlotReward { label = golds[i] + " 골드" };
                slot.rewards.Add(new PinballReward(PointType.Gold, golds[i]));
                slotRewards.Add(slot);
            }

            var gold = new PinballSpecialReward { tag = 1, label = "황금핀", requiredHits = 10 };
            gold.rewards.Add(new PinballReward(PointType.FreeGem, 10));
            specialRewards.Add(gold);

            var bomb = new PinballSpecialReward { tag = 2, label = "폭탄핀", requiredHits = 15 };
            bomb.rewards.Add(new PinballReward(PointType.BattleEnhanceStone, 5));
            specialRewards.Add(bomb);
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
