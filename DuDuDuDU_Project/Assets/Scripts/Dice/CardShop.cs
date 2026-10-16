using System;
using System.Collections.Generic;

namespace OJ.Dice
{
    public enum ShopCardKind
    {
        /// <summary>빈 칸(산 뒤, 또는 아직 안 뽑음).</summary>
        None = 0,

        /// <summary>랜덤 기본 1성 다이스 1개를 준다.</summary>
        GrantDice,

        /// <summary>같은 종류 1성 둘을 소모해 2성 하나로.</summary>
        MergeDice,

        /// <summary>기본 2성 + 기본 1성 → 레어.</summary>
        CraftRare,

        /// <summary>
        /// 신화 다이스 소환. <b>어느 신화인지는 카드가 정하지 않는다</b> — 눌러서 목록에서 고른다
        /// (<see cref="MythicList"/>). 값(SP)은 목록에서 고른 뒤에야 치른다.
        /// </summary>
        MythicSummon,
    }

    /// <summary>
    /// 상점 카드 한 장. <b>무엇을 내고 무엇을 받는지가 카드 안에 다 적혀 있다</b> —
    /// UI 는 이 값만 읽어 그리고, 구매는 이 값만 보고 실행한다.
    /// </summary>
    public struct ShopCard
    {
        public ShopCardKind Kind;

        /// <summary>받는 다이스.</summary>
        public DiceType Result;
        public int ResultStar;

        /// <summary>주재료. <see cref="MainCount"/> 가 0 이면 없다(지급 카드).</summary>
        public DiceType MainType;
        public int MainStar;
        public int MainCount;

        /// <summary>보조 재료. <see cref="SubCount"/> 가 0 이면 없다.</summary>
        public DiceType SubType;
        public int SubStar;
        public int SubCount;

        public bool Locked;

        public bool IsEmpty => Kind == ShopCardKind.None;

        /// <summary>신화 소환 카드인가(주황 카드, 눌러서 목록을 연다).</summary>
        public bool IsMythic => Kind == ShopCardKind.MythicSummon;

        public int Cost
        {
            get
            {
                switch (Kind)
                {
                    case ShopCardKind.GrantDice: return DiceRecipes.GrantCostSp;
                    case ShopCardKind.MergeDice: return DiceRecipes.MergeCostSp;
                    case ShopCardKind.CraftRare: return DiceRecipes.RareCostSp;
                    case ShopCardKind.MythicSummon: return DiceRecipes.MythicCostSp;
                    default: return 0;
                }
            }
        }

        public static ShopCard Grant(DiceType type)
        {
            return new ShopCard
            {
                Kind = ShopCardKind.GrantDice,
                Result = type,
                ResultStar = 1,
            };
        }

        public static ShopCard Merge(DiceType type)
        {
            return new ShopCard
            {
                Kind = ShopCardKind.MergeDice,
                Result = type,
                ResultStar = 2,
                MainType = type,
                MainStar = 1,
                MainCount = 2,
            };
        }

        /// <summary>신화 소환 카드. 결과 다이스는 비어 있다(목록에서 고른다).</summary>
        public static ShopCard Mythic()
        {
            return new ShopCard
            {
                Kind = ShopCardKind.MythicSummon,
                Result = DiceType.Max,
                ResultStar = 1,
            };
        }

        /// <summary>레어 조합 카드. 신화는 <see cref="Mythic"/> 이다.</summary>
        public static ShopCard Craft(DiceRecipe recipe)
        {
            return new ShopCard
            {
                Kind = ShopCardKind.CraftRare,
                Result = recipe.Result,
                ResultStar = 1,
                MainType = recipe.MainType,
                MainStar = recipe.MainStar,
                MainCount = 1,
                SubType = recipe.SubType,
                SubStar = recipe.SubStar,
                SubCount = 1,
            };
        }

        /// <summary>같은 카드인가(잠금은 보지 않는다). 한 번에 같은 카드가 겹치지 않게 거를 때 쓴다.</summary>
        public bool SameOffer(ShopCard other)
        {
            return Kind == other.Kind && Result == other.Result;
        }
    }

    /// <summary>
    /// 카드를 뽑는 데 필요한 보드의 사실. 구현은 보드(<c>UIBoard</c>)가 하고, 테스트는 가짜를 쓴다.
    /// </summary>
    public interface IShopContext
    {
        /// <summary>보드에 (type, star) 다이스가 몇 개 있는가.</summary>
        int CountOnBoard(DiceType type, int star);

        /// <summary>이 다이스를 보유(해금)했는가. 미보유 킹은 카드로 나오지 않는다.</summary>
        bool IsOwned(DiceType type);
    }

    /// <summary>
    /// 롤토체스식 카드 상점의 상태와 규칙. 엔진을 쓰지 않는다 — 난수는 호출부가 준다
    /// (<see cref="DiceEvolution"/> 과 같은 이유: 테스트에서 두드릴 수 있어야 한다).
    ///
    /// <b>갱신은 잠긴 칸을 건드리지 않는다.</b> 리롤이든 다음 웨이브의 자동 갱신이든 같다.
    /// 산 카드는 빈 칸이 되고 잠금도 풀린다.
    ///
    /// <b>카드는 보드 보유를 기준으로 뽑는다.</b> 머지 카드는 1성이 둘 이상 있는 종류만,
    /// 조합 카드는 재료를 둘 다 가진 식만 나오고, 후보가 하나도 없으면 지급 카드가 나온다.
    /// 뽑은 뒤에 보드가 바뀌어 재료가 사라질 수 있으므로 <b>구매 시점에 다시 검사한다</b>
    /// (<c>MergeSystem.TryBuyShopCard</c>).
    /// </summary>
    public sealed class CardShop
    {
        // 종류별 뽑힐 가중치. 지급이 가장 흔하고 신화가 가장 드물다.
        private const int GrantWeight = 4;
        private const int MergeWeight = 3;
        private const int RareWeight = 2;
        private const int MythicWeight = 1;

        // 같은 카드가 겹치지 않도록 다시 뽑는 횟수. 후보가 모자라면 겹침을 허용하고 만다.
        private const int DuplicateRetry = 8;

        private readonly ShopCard[] cards = new ShopCard[DiceRecipes.CardCount];

        /// <summary>카드가 바뀌었다(갱신·구매·잠금). UI 가 다시 그린다.</summary>
        public event Action Changed;

        public int Count => cards.Length;

        public ShopCard this[int index] => cards[index];

        /// <summary>잠기지 않은 칸을 새로 뽑는다. 잠긴 칸은 그대로다.</summary>
        public void Refresh(IShopContext context, Func<int, int> nextInt)
        {
            RollUnlocked(context, nextInt);
            PlaceMythicIfReady(context);
            Changed?.Invoke();
        }

        /// <summary>
        /// 소환할 수 있는 신화가 있는데 카드가 없으면 한 장을 신화 카드로 채운다. 바뀌었으면 true.
        ///
        /// <b>뽑기 확률에 맡기지 않는다.</b> 가중치로만 뽑으면 재료를 다 모아 놓고도 신화 카드가
        /// 몇 판씩 안 나올 수 있다 — 재료는 준비 단계 중에 모이는데 카드는 그때그때가 아니라
        /// 갱신 때만 뽑기 때문이다. 그래서 갱신 직후와 <b>재료가 갖춰진 순간</b>(창이 주기적으로 부른다)
        /// 둘 다 이 규칙을 건다.
        /// </summary>
        public bool EnsureMythic(IShopContext context)
        {
            if (!PlaceMythicIfReady(context))
                return false;

            Changed?.Invoke();
            return true;
        }

        private bool PlaceMythicIfReady(IShopContext context)
        {
            if (!IsMythicCraftable(context))
                return false;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i].IsMythic)
                    return false;
            }

            // 빈 칸 → 잠기지 않은 지급 카드 → 잠기지 않은 아무 카드 순으로 자리를 잡는다.
            // 잠근 카드는 건드리지 않는다.
            int target = -1;
            for (int i = 0; i < cards.Length && target < 0; i++)
            {
                if (cards[i].IsEmpty)
                    target = i;
            }

            for (int i = 0; i < cards.Length && target < 0; i++)
            {
                if (!cards[i].Locked && cards[i].Kind == ShopCardKind.GrantDice)
                    target = i;
            }

            for (int i = 0; i < cards.Length && target < 0; i++)
            {
                if (!cards[i].Locked)
                    target = i;
            }

            if (target < 0)
                return false;

            cards[target] = ShopCard.Mythic();
            return true;
        }

        /// <summary>지금 소환할 수 있는 신화(보유 + 재료)가 하나라도 있는가.</summary>
        private static bool IsMythicCraftable(IShopContext context)
        {
            IReadOnlyList<DiceRecipe> recipes = DiceRecipes.Mythic;
            for (int i = 0; i < recipes.Count; i++)
            {
                DiceRecipe recipe = recipes[i];
                if (context.IsOwned(recipe.Result)
                    && context.CountOnBoard(recipe.MainType, recipe.MainStar) >= 1
                    && context.CountOnBoard(recipe.SubType, recipe.SubStar) >= 1)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>리롤. 규칙은 갱신과 같다 — 비용은 부르는 쪽이 낸다.</summary>
        public void Reroll(IShopContext context, Func<int, int> nextInt)
        {
            Refresh(context, nextInt);
        }

        public void ToggleLock(int index)
        {
            if (index < 0 || index >= cards.Length || cards[index].IsEmpty)
                return;

            cards[index].Locked = !cards[index].Locked;
            Changed?.Invoke();
        }

        /// <summary>산 카드를 치운다. 잠금도 같이 풀린다.</summary>
        public void Consume(int index)
        {
            if (index < 0 || index >= cards.Length)
                return;

            cards[index] = default;
            Changed?.Invoke();
        }

        private void RollUnlocked(IShopContext context, Func<int, int> nextInt)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i].Locked && !cards[i].IsEmpty)
                    continue;

                cards[i] = default;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (!cards[i].IsEmpty)
                    continue;

                ShopCard picked = Roll(context, nextInt);
                for (int retry = 0; retry < DuplicateRetry && IsDuplicate(picked); retry++)
                    picked = Roll(context, nextInt);

                cards[i] = picked;
            }
        }

        private bool IsDuplicate(ShopCard candidate)
        {
            // 지급 카드는 종류가 겹쳐도 된다(같은 다이스를 두 장 사고 싶을 수 있다).
            if (candidate.Kind == ShopCardKind.GrantDice)
                return false;

            for (int i = 0; i < cards.Length; i++)
            {
                if (!cards[i].IsEmpty && cards[i].SameOffer(candidate))
                    return true;
            }

            return false;
        }

        private static ShopCard Roll(IShopContext context, Func<int, int> nextInt)
        {
            var merges = new List<DiceType>(5);
            var rares = new List<DiceRecipe>(5);

            IReadOnlyList<DiceType> bases = DiceEvolution.BaseTypes;
            for (int i = 0; i < bases.Count; i++)
            {
                DiceType type = bases[i];
                if (context.IsOwned(type) && context.CountOnBoard(type, 1) >= 2)
                    merges.Add(type);
            }

            CollectCraftable(context, DiceRecipes.Rare, rares);

            // 신화 카드는 <b>지금 소환할 수 있는 신화가 하나라도 있을 때만</b> 나온다 —
            // 보유했고 재료도 보드에 있는 것(MythicList 의 Craftable). 그래서 이 카드를 누르면
            // 목록 맨 위에는 반드시 소환 가능한 줄이 있다.
            bool mythicReady = IsMythicCraftable(context);

            int total = GrantWeight
                + (merges.Count > 0 ? MergeWeight : 0)
                + (rares.Count > 0 ? RareWeight : 0)
                + (mythicReady ? MythicWeight : 0);

            int roll = nextInt(total);

            if (roll < GrantWeight)
                return ShopCard.Grant(PickGrantType(context, nextInt));
            roll -= GrantWeight;

            if (merges.Count > 0)
            {
                if (roll < MergeWeight)
                    return ShopCard.Merge(merges[nextInt(merges.Count)]);
                roll -= MergeWeight;
            }

            if (rares.Count > 0)
            {
                if (roll < RareWeight)
                    return ShopCard.Craft(rares[nextInt(rares.Count)]);
                roll -= RareWeight;
            }

            return ShopCard.Mythic();
        }

        private static DiceType PickGrantType(IShopContext context, Func<int, int> nextInt)
        {
            IReadOnlyList<DiceType> bases = DiceEvolution.BaseTypes;
            var owned = new List<DiceType>(bases.Count);
            for (int i = 0; i < bases.Count; i++)
            {
                if (context.IsOwned(bases[i]))
                    owned.Add(bases[i]);
            }

            // 기본 5종은 처음부터 보유라 비는 일이 없지만, 비면 첫 종류로 버틴다 — 여기서 터뜨릴 일이 아니다.
            if (owned.Count == 0)
                return bases[0];

            return owned[nextInt(owned.Count)];
        }

        private static void CollectCraftable(IShopContext context, IReadOnlyList<DiceRecipe> source, List<DiceRecipe> into)
        {
            for (int i = 0; i < source.Count; i++)
            {
                DiceRecipe recipe = source[i];
                if (!context.IsOwned(recipe.Result))
                    continue;

                if (context.CountOnBoard(recipe.MainType, recipe.MainStar) < 1)
                    continue;

                if (context.CountOnBoard(recipe.SubType, recipe.SubStar) < 1)
                    continue;

                into.Add(recipe);
            }
        }
    }
}
