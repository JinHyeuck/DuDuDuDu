using System.Collections.Generic;

namespace OJ.Dice
{
    /// <summary>
    /// 카드 모드의 조합식 한 줄. <b>재료는 주재료 하나 + 보조재료 하나</b>다.
    ///
    /// <list type="bullet">
    /// <item>레어: 주재료 = 기본 다이스 2성, 보조 = 기본 다이스 1성.</item>
    /// <item>신화(킹): 주재료 = 레어(특수) 1개, 보조 = 같은 계통 기본 다이스 1성.</item>
    /// </list>
    /// </summary>
    public readonly struct DiceRecipe
    {
        public readonly DiceType Result;
        public readonly DiceType MainType;
        public readonly int MainStar;
        public readonly DiceType SubType;
        public readonly int SubStar;

        public DiceRecipe(DiceType result, DiceType mainType, int mainStar, DiceType subType, int subStar)
        {
            Result = result;
            MainType = mainType;
            MainStar = mainStar;
            SubType = subType;
            SubStar = subStar;
        }
    }

    /// <summary>
    /// 카드 모드(조합식 상점)의 규칙표 — 조합식 10줄과 카드 비용. 엔진을 쓰지 않는다.
    ///
    /// <b>이것은 폐기한 옛 조합식(<c>recipeMaterials</c>)의 부활이 아니다.</b> 옛 식은 보드에서
    /// 재료가 모이면 저절로 합쳐졌고, 이것은 <b>상점 카드를 사야만</b> 일어나는 교환이다.
    /// 진화·교환의 정본(<see cref="DiceEvolution"/>)은 건드리지 않는다 — 두 경로가 따로 산다.
    ///
    /// <b>식을 짠 방식.</b> 기본 5종을 한 줄로 세워(Normal→Fire→Ice→Thunder→Poison) 레어는
    /// <c>A★2 + 다음 B★1</c> 로 순환시켰다. 신화는 <b>그 킹과 같은 계통</b>(진화 배선
    /// Normal→Tornado→KingNormal)의 레어 + 기본 1성이다. 기본 5종이 레어에서 주재료로 한 번,
    /// 보조로 한 번, 신화에서 보조로 한 번씩 쓰여 어느 한 다이스만 소비되지 않는다.
    /// 밸런스 값이라 정식 규칙으로 승격할 때는 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class DiceRecipes
    {
        // ── 카드 비용 ─────────────────────────────────────────────

        // 네 카드 모두 SP 로 치른다(마석은 쓰지 않는다). 수급이 후한 동안 맛보기 밸런스로
        // 지급·머지를 10배로 올렸다 — 수급이 정리되면 이 네 값만 만진다.

        /// <summary>다이스 지급 카드(SP).</summary>
        public const int GrantCostSp = 20;

        /// <summary>다이스 머지 카드(SP).</summary>
        public const int MergeCostSp = 40;

        /// <summary>레어 조합 카드(SP).</summary>
        public const int RareCostSp = 60;

        /// <summary>신화 조합 카드(SP).</summary>
        public const int MythicCostSp = 100;

        /// <summary>리롤(SP).</summary>
        public const int RerollCostSp = 1;

        /// <summary>한 번에 제시되는 카드 수.</summary>
        public const int CardCount = 3;

        private static readonly DiceRecipe[] rare =
        {
            new DiceRecipe(DiceType.Tornado, DiceType.Normal, 2, DiceType.Fire, 1),
            new DiceRecipe(DiceType.ArmorBreak, DiceType.Fire, 2, DiceType.Ice, 1),
            new DiceRecipe(DiceType.Wind, DiceType.Ice, 2, DiceType.Thunder, 1),
            new DiceRecipe(DiceType.Time, DiceType.Thunder, 2, DiceType.Poison, 1),
            new DiceRecipe(DiceType.Stun, DiceType.Poison, 2, DiceType.Normal, 1),
        };

        private static readonly DiceRecipe[] mythic =
        {
            new DiceRecipe(DiceType.KingNormal, DiceType.Tornado, 1, DiceType.Normal, 1),
            new DiceRecipe(DiceType.KingFire, DiceType.ArmorBreak, 1, DiceType.Fire, 1),
            new DiceRecipe(DiceType.KingIce, DiceType.Wind, 1, DiceType.Ice, 1),
            new DiceRecipe(DiceType.KingThunder, DiceType.Time, 1, DiceType.Thunder, 1),
            new DiceRecipe(DiceType.KingPoison, DiceType.Stun, 1, DiceType.Poison, 1),
        };

        public static IReadOnlyList<DiceRecipe> Rare => rare;
        public static IReadOnlyList<DiceRecipe> Mythic => mythic;

        public static bool TryGet(DiceType result, out DiceRecipe recipe)
        {
            for (int i = 0; i < rare.Length; i++)
            {
                if (rare[i].Result == result)
                {
                    recipe = rare[i];
                    return true;
                }
            }

            for (int i = 0; i < mythic.Length; i++)
            {
                if (mythic[i].Result == result)
                {
                    recipe = mythic[i];
                    return true;
                }
            }

            recipe = default;
            return false;
        }
    }
}
