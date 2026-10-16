using System.Collections.Generic;

namespace OJ.Dice
{
    /// <summary>신화 하나가 지금 어떤 상태인가.</summary>
    public enum MythicOptionState
    {
        /// <summary>보유했고 재료도 보드에 있다 — 소환할 수 있다.</summary>
        Craftable = 0,

        /// <summary>보유했지만 재료가 모자란다.</summary>
        MissingMaterials = 1,

        /// <summary>보유하지 않았다 — 구매(해금)해야 한다.</summary>
        NotOwned = 2,
    }

    /// <summary>신화 목록의 한 줄. 모자란 재료가 어느 쪽인지까지 담는다.</summary>
    public readonly struct MythicOption
    {
        public readonly DiceRecipe Recipe;
        public readonly MythicOptionState State;

        /// <summary>주재료(레어)가 모자란가. <see cref="MythicOptionState.MissingMaterials"/> 일 때만 뜻이 있다.</summary>
        public readonly bool NeedMain;

        /// <summary>보조 재료(기본 1성)가 모자란가.</summary>
        public readonly bool NeedSub;

        public MythicOption(DiceRecipe recipe, MythicOptionState state, bool needMain, bool needSub)
        {
            Recipe = recipe;
            State = state;
            NeedMain = needMain;
            NeedSub = needSub;
        }
    }

    /// <summary>
    /// 신화 소환 카드를 눌렀을 때 보여 줄 목록. 엔진을 쓰지 않는다.
    ///
    /// <b>정렬이 규칙이다.</b> 소환할 수 있는 것이 맨 위, 보유했지만 재료가 모자란 것이 그다음,
    /// 보유하지 않은 것이 맨 아래다. 같은 묶음 안에서는 조합식 표의 순서를 지킨다.
    /// </summary>
    public static class MythicList
    {
        public static List<MythicOption> Build(IShopContext context)
        {
            var craftable = new List<MythicOption>(5);
            var missing = new List<MythicOption>(5);
            var notOwned = new List<MythicOption>(5);

            IReadOnlyList<DiceRecipe> recipes = DiceRecipes.Mythic;
            for (int i = 0; i < recipes.Count; i++)
            {
                DiceRecipe recipe = recipes[i];

                if (!context.IsOwned(recipe.Result))
                {
                    notOwned.Add(new MythicOption(recipe, MythicOptionState.NotOwned, false, false));
                    continue;
                }

                bool needMain = context.CountOnBoard(recipe.MainType, recipe.MainStar) < 1;
                bool needSub = context.CountOnBoard(recipe.SubType, recipe.SubStar) < 1;

                if (!needMain && !needSub)
                    craftable.Add(new MythicOption(recipe, MythicOptionState.Craftable, false, false));
                else
                    missing.Add(new MythicOption(recipe, MythicOptionState.MissingMaterials, needMain, needSub));
            }

            var result = new List<MythicOption>(recipes.Count);
            result.AddRange(craftable);
            result.AddRange(missing);
            result.AddRange(notOwned);
            return result;
        }
    }
}
