using System;
using System.Collections.Generic;
using NUnit.Framework;
using OJ.Dice;

namespace OJ.Game.Tests
{
    /// <summary>조합식 표와 카드 상점의 규칙. 보드는 가짜로 대신한다.</summary>
    public sealed class CardShopTests
    {
        private sealed class FakeContext : IShopContext
        {
            public readonly Dictionary<(DiceType, int), int> Board = new Dictionary<(DiceType, int), int>();
            public readonly HashSet<DiceType> NotOwned = new HashSet<DiceType>();

            public int CountOnBoard(DiceType type, int star)
            {
                return Board.TryGetValue((type, star), out int n) ? n : 0;
            }

            public bool IsOwned(DiceType type) => !NotOwned.Contains(type);
        }

        private static Func<int, int> Seeded(int seed)
        {
            var rng = new Random(seed);
            return max => rng.Next(max);
        }

        // ── 조합식 표 ────────────────────────────────────────────

        [Test]
        public void Recipes_CoverEverySpecialAndKingExactlyOnce()
        {
            var results = new HashSet<DiceType>();
            foreach (DiceRecipe r in DiceRecipes.Rare) Assert.IsTrue(results.Add(r.Result));
            foreach (DiceRecipe r in DiceRecipes.Mythic) Assert.IsTrue(results.Add(r.Result));

            foreach (DiceType t in DiceEvolution.SpecialTypes) Assert.Contains(t, new List<DiceType>(results));
            foreach (DiceType t in DiceEvolution.KingTypes) Assert.Contains(t, new List<DiceType>(results));
            Assert.AreEqual(10, results.Count);
        }

        [Test]
        public void RareRecipes_UseBaseMainStar2AndBaseSubStar1()
        {
            foreach (DiceRecipe r in DiceRecipes.Rare)
            {
                Assert.AreEqual(DiceTier.Special, DiceEvolution.GetTier(r.Result));
                Assert.AreEqual(DiceTier.Base, DiceEvolution.GetTier(r.MainType));
                Assert.AreEqual(2, r.MainStar);
                Assert.AreEqual(DiceTier.Base, DiceEvolution.GetTier(r.SubType));
                Assert.AreEqual(1, r.SubStar);
                Assert.AreNotEqual(r.MainType, r.SubType);
            }
        }

        [Test]
        public void MythicRecipes_UseSameLineageSpecialAndBase()
        {
            foreach (DiceRecipe r in DiceRecipes.Mythic)
            {
                Assert.AreEqual(DiceTier.King, DiceEvolution.GetTier(r.Result));
                Assert.AreEqual(DiceTier.Special, DiceEvolution.GetTier(r.MainType));
                Assert.AreEqual(DiceTier.Base, DiceEvolution.GetTier(r.SubType));

                // 계통: 재료 레어도 보조 기본도 그 킹으로 가는 진화 줄기 위에 있다.
                Assert.IsTrue(DiceEvolution.TryGetEvolveTarget(r.MainType, out DiceType king));
                Assert.AreEqual(r.Result, king);
                Assert.IsTrue(DiceEvolution.TryGetEvolveTarget(r.SubType, out DiceType special));
                Assert.AreEqual(r.MainType, special);
            }
        }

        [Test]
        public void Recipes_SpreadBaseDiceEvenly()
        {
            var uses = new Dictionary<DiceType, int>();
            foreach (DiceType t in DiceEvolution.BaseTypes) uses[t] = 0;

            foreach (DiceRecipe r in DiceRecipes.Rare) { uses[r.MainType]++; uses[r.SubType]++; }
            foreach (DiceRecipe r in DiceRecipes.Mythic) uses[r.SubType]++;

            foreach (KeyValuePair<DiceType, int> kv in uses)
                Assert.AreEqual(3, kv.Value, kv.Key.ToString());
        }

        // ── 카드 상점 ────────────────────────────────────────────

        [Test]
        public void EmptyBoard_OffersOnlyGrantCards()
        {
            var shop = new CardShop();
            shop.Refresh(new FakeContext(), Seeded(1));

            for (int i = 0; i < shop.Count; i++)
                Assert.AreEqual(ShopCardKind.GrantDice, shop[i].Kind);
        }

        [Test]
        public void MergeCard_NeedsTwoOneStarOfTheSameType()
        {
            var ctx = new FakeContext();
            ctx.Board[(DiceType.Fire, 1)] = 2;

            bool sawMerge = false;
            for (int seed = 0; seed < 50 && !sawMerge; seed++)
            {
                var shop = new CardShop();
                shop.Refresh(ctx, Seeded(seed));
                for (int i = 0; i < shop.Count; i++)
                {
                    if (shop[i].Kind != ShopCardKind.MergeDice)
                        continue;

                    sawMerge = true;
                    Assert.AreEqual(DiceType.Fire, shop[i].MainType);
                    Assert.AreEqual(2, shop[i].MainCount);
                    Assert.AreEqual(DiceType.Fire, shop[i].Result);
                    Assert.AreEqual(2, shop[i].ResultStar);
                }
            }

            Assert.IsTrue(sawMerge);
        }

        [Test]
        public void CraftCards_AppearOnlyWhenBothMaterialsAreHeld()
        {
            var ctx = new FakeContext();
            ctx.Board[(DiceType.Normal, 2)] = 1; // Tornado 의 주재료만 있다.

            for (int seed = 0; seed < 60; seed++)
            {
                var shop = new CardShop();
                shop.Refresh(ctx, Seeded(seed));
                for (int i = 0; i < shop.Count; i++)
                    Assert.AreNotEqual(ShopCardKind.CraftRare, shop[i].Kind);
            }

            ctx.Board[(DiceType.Fire, 1)] = 1; // 보조도 갖췄다.
            bool saw = false;
            for (int seed = 0; seed < 80 && !saw; seed++)
            {
                var shop = new CardShop();
                shop.Refresh(ctx, Seeded(seed));
                for (int i = 0; i < shop.Count; i++)
                {
                    if (shop[i].Kind == ShopCardKind.CraftRare)
                    {
                        saw = true;
                        Assert.AreEqual(DiceType.Tornado, shop[i].Result);
                    }
                }
            }

            Assert.IsTrue(saw);
        }

        [Test]
        public void MythicCard_CanAppearEvenWithoutMaterials()
        {
            bool saw = false;
            for (int seed = 0; seed < 200 && !saw; seed++)
            {
                var shop = new CardShop();
                shop.Refresh(new FakeContext(), Seeded(seed));
                for (int i = 0; i < shop.Count; i++)
                {
                    if (shop[i].Kind == ShopCardKind.MythicSummon)
                    {
                        saw = true;
                        Assert.IsTrue(shop[i].IsMythic);
                        Assert.AreEqual(50, shop[i].Cost);
                    }
                }
            }

            Assert.IsTrue(saw);
        }

        [Test]
        public void MythicList_SortsCraftableThenMissingThenNotOwned()
        {
            var ctx = new FakeContext();

            // KingNormal(Tornado + Normal1): 둘 다 있다 → 소환 가능.
            ctx.Board[(DiceType.Tornado, 1)] = 1;
            ctx.Board[(DiceType.Normal, 1)] = 1;

            // KingFire(ArmorBreak + Fire1): 보조만 있다 → 주재료가 모자라다.
            ctx.Board[(DiceType.Fire, 1)] = 1;

            // KingIce: 아무것도 없다 → 둘 다 모자라다. KingThunder 는 미보유.
            ctx.NotOwned.Add(DiceType.KingThunder);
            ctx.NotOwned.Add(DiceType.KingPoison);

            System.Collections.Generic.List<MythicOption> list = MythicList.Build(ctx);

            Assert.AreEqual(5, list.Count);
            Assert.AreEqual(DiceType.KingNormal, list[0].Recipe.Result);
            Assert.AreEqual(MythicOptionState.Craftable, list[0].State);

            Assert.AreEqual(MythicOptionState.MissingMaterials, list[1].State);
            Assert.AreEqual(MythicOptionState.MissingMaterials, list[2].State);
            Assert.AreEqual(MythicOptionState.NotOwned, list[3].State);
            Assert.AreEqual(MythicOptionState.NotOwned, list[4].State);

            MythicOption fire = list[1].Recipe.Result == DiceType.KingFire ? list[1] : list[2];
            Assert.IsTrue(fire.NeedMain);
            Assert.IsFalse(fire.NeedSub);

            MythicOption ice = list[1].Recipe.Result == DiceType.KingIce ? list[1] : list[2];
            Assert.IsTrue(ice.NeedMain);
            Assert.IsTrue(ice.NeedSub);
        }

        [Test]
        public void LockedCard_SurvivesRefresh_AndConsumeClearsIt()
        {
            var ctx = new FakeContext();
            var shop = new CardShop();
            shop.Refresh(ctx, Seeded(3));

            ShopCard kept = shop[1];
            shop.ToggleLock(1);
            Assert.IsTrue(shop[1].Locked);

            for (int seed = 10; seed < 30; seed++)
            {
                shop.Reroll(ctx, Seeded(seed));
                Assert.AreEqual(kept.Result, shop[1].Result);
                Assert.IsTrue(shop[1].Locked);
            }

            shop.Consume(1);
            Assert.IsTrue(shop[1].IsEmpty);
            Assert.IsFalse(shop[1].Locked);

            shop.Refresh(ctx, Seeded(99));
            Assert.IsFalse(shop[1].IsEmpty);
        }

        [Test]
        public void Cost_FollowsTheCardKind()
        {
            Assert.AreEqual(10, ShopCard.Grant(DiceType.Normal).Cost);
            Assert.AreEqual(20, ShopCard.Merge(DiceType.Normal).Cost);
            Assert.AreEqual(30, ShopCard.Craft(DiceRecipes.Rare[0]).Cost);
            Assert.AreEqual(50, ShopCard.Mythic().Cost);
        }
    }
}
