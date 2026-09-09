using System;
using NUnit.Framework;
using OJ.Shop;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 상점 가격 규칙. <b>static 만 검사한다</b> — 헤드리스 러너에서는 <c>ScriptableObject</c>
    /// 생성과 싱글톤이 돌지 않아 <see cref="ShopDatabase"/> 나 <see cref="ShopPurchaseManager"/>
    /// 인스턴스를 만들 수 없다. 그래서 값을 결정하는 식은 전부 static 으로 빼 두었고,
    /// 여기서 그 식만 본다.
    /// </summary>
    public sealed class ShopPricingTests
    {
        // ── 누진 단가 (기획서 8.6-2 / 8.7-2) ───────────────────────────

        [Test]
        public void 첫_구매는_기본가다()
        {
            Assert.That(ShopPurchaseManager.ProgressiveCost(100, 20, 0), Is.EqualTo(100));
        }

        [TestCase(1, 120)]
        [TestCase(2, 140)]
        [TestCase(5, 200)]
        public void 구매할수록_단가가_오른다(int boughtToday, int expected)
        {
            Assert.That(ShopPurchaseManager.ProgressiveCost(100, 20, boughtToday), Is.EqualTo(expected));
        }

        /// <summary>
        /// 증가분 0 은 정액이라는 뜻이다. 골드 상점에서는 이 값이 0 이면 안 되지만
        /// (한도가 없어 누진이 유일한 제동이다), 그 판정은 <c>ShopDatabase.Validate</c> 몫이고
        /// 계산식 자체는 0 을 정상으로 다뤄야 한다 — 다이스석은 누진이 "검토(미정)"다.
        /// </summary>
        [Test]
        public void 증가분이_0_이면_정액이다()
        {
            Assert.That(ShopPurchaseManager.ProgressiveCost(500, 0, 7), Is.EqualTo(500));
        }

        [Test]
        public void 단가는_음수가_되지_않는다()
        {
            Assert.That(ShopPurchaseManager.ProgressiveCost(-50, 0, 0), Is.EqualTo(0));
        }

        // ── 할인 (기획서 8.3) ─────────────────────────────────────────

        [Test]
        public void 할인이_없으면_정가다()
        {
            Assert.That(ShopPurchaseManager.DiscountedCost(70, 0), Is.EqualTo(70));
        }

        [TestCase(100, 20, 80)]
        [TestCase(70, 20, 56)]
        [TestCase(50, 50, 25)]
        public void 할인율이_적용된다(int cost, int percent, int expected)
        {
            Assert.That(ShopPurchaseManager.DiscountedCost(cost, percent), Is.EqualTo(expected));
        }

        /// <summary>
        /// <b>올림이다.</b> 내림으로 두면 1젬짜리가 할인으로 0 이 되어 공짜가 된다.
        /// 33 의 20% 할인은 26.4 → 27.
        /// </summary>
        [Test]
        public void 할인가는_올림한다()
        {
            Assert.That(ShopPurchaseManager.DiscountedCost(33, 20), Is.EqualTo(27));
        }

        /// <summary>
        /// 100% 할인(=공짜)은 만들 수 없다. 데이터에 100 이 들어가도 90 으로 잘린다 —
        /// 상점에서 재화가 0 원이 되는 경로를 만들지 않는다.
        /// </summary>
        [Test]
        public void 할인율은_90_퍼센트에서_잘린다()
        {
            Assert.That(ShopPurchaseManager.DiscountedCost(100, 100), Is.EqualTo(10));
        }

        // ── 일일 갱신 ─────────────────────────────────────────────────

        [Test]
        public void 갱신까지_남은_시간은_다음_자정까지다()
        {
            var now = new DateTime(2026, 9, 9, 16, 47, 27);

            Assert.That(ShopPurchaseManager.TimeUntilReset(now),
                Is.EqualTo(new TimeSpan(7, 12, 33)));
        }

        /// <summary>자정 직후에는 꼬박 하루가 남는다. 0 이 나오면 화면이 "갱신 중"으로 굳는다.</summary>
        [Test]
        public void 자정_직후에는_하루가_남는다()
        {
            var now = new DateTime(2026, 9, 9, 0, 0, 0);

            Assert.That(ShopPurchaseManager.TimeUntilReset(now), Is.EqualTo(TimeSpan.FromDays(1)));
        }

        /// <summary>
        /// 날짜 키는 문화권을 안 탄다. 기기 로캘이 바뀌면 형식이 달라져 <b>매번 리셋되거나
        /// 영영 리셋되지 않는다</b> — 둘 다 그 기기에서만 재현된다.
        /// </summary>
        [Test]
        public void 날짜_키는_고정_형식이다()
        {
            Assert.That(ShopPurchaseManager.FormatDate(new DateTime(2026, 9, 9)),
                Is.EqualTo("2026-09-09"));
        }

        // ── 구매 키 ───────────────────────────────────────────────────

        /// <summary>
        /// 여러 용도가 한 표를 나눠 쓴다. 접두사가 겹치면 <b>일일상점 0번을 산 것이 골드 0번을
        /// 산 것으로 읽혀</b> SOLD 와 누진이 동시에 틀어진다.
        /// </summary>
        [Test]
        public void 구매_키는_용도끼리_안_겹친다()
        {
            string daily = ShopPurchaseManager.DailySlotKey(0);
            string gold = ShopPurchaseManager.GoldKey(0);

            Assert.That(daily, Is.EqualTo("daily:0"));
            Assert.That(gold, Is.EqualTo("gold:0"));
            Assert.That(new[] { daily, gold, ShopPurchaseManager.AdRefreshKey,
                ShopPurchaseManager.GemRefreshKey }, Is.Unique);
        }

        /// <summary>
        /// 갱신 횟수 키는 <b>슬롯 키와 다른 접두사</b>여야 한다. 같은 접두사를 쓰면
        /// 갱신이 SOLD 를 지울 때 자기 횟수까지 지워 <b>갱신이 무한</b>이 된다.
        /// </summary>
        [Test]
        public void 갱신_키는_슬롯_키와_접두사가_다르다()
        {
            Assert.That(ShopPurchaseManager.AdRefreshKey, Does.Not.StartWith("daily:"));
            Assert.That(ShopPurchaseManager.GemRefreshKey, Does.Not.StartWith("daily:"));
        }

        // ── 성장 패키지 구성품 표기 (기획서 8.1-3) ──────────────────────

        /// <summary>
        /// 유료젬이 항상 맨 앞이다. 기획서가 요구하는 "10원 = 1젬 즉시 환산"은
        /// 젬 수량이 가장 먼저 읽혀야 성립한다.
        /// </summary>
        [Test]
        public void 구성품은_유료젬이_먼저_적힌다()
        {
            var package = new ShopDatabase.GrowthPackage
            {
                contents = new System.Collections.Generic.List<ShopDatabase.Reward>
                {
                    new ShopDatabase.Reward(PointType.MythicScroll, 15),
                    new ShopDatabase.Reward(PointType.PaidGem, 250),
                },
            };

            string text = ShopText.DescribeContents(package);

            Assert.That(text, Does.StartWith("유료젬 250"));
            Assert.That(text, Does.Contain("신화석 15"));
        }

        [Test]
        public void 구성품이_없으면_빈_문자열이다()
        {
            var package = new ShopDatabase.GrowthPackage
            {
                contents = new System.Collections.Generic.List<ShopDatabase.Reward>(),
            };

            Assert.That(ShopText.DescribeContents(package), Is.Empty);
        }

        // -- 보석뽑기 등급 판정 (기획서 8.2) ---------------------------

        /// <summary>
        /// 가중치가 비면 균등이다. 데이터가 비었을 때 0번만 나오면 "확률이 이상하다" 가 아니라
        /// <b>"이 상자는 원래 이것만 준다"</b> 로 읽혀 사고가 안 드러난다.
        /// </summary>
        [TestCase(0.0f, 0)]
        [TestCase(0.5f, 1)]
        [TestCase(0.99f, 2)]
        public void 가중치가_비면_균등하게_고른다(float roll, int expected)
        {
            Assert.That(ShopDraw.PickWeightedIndex(null, roll, 3), Is.EqualTo(expected));
        }

        /// <summary>
        /// 가중치 [60,30,10] 이면 0~59 가 0번, 60~89 가 1번, 90~99 가 2번이다.
        ///
        /// <b>경계값(0.60f, 0.90f)은 일부러 안 넣는다.</b> <c>0.90f</c> 는 실제로
        /// 0.89999997 이라 1번 칸이 맞는데, 그것을 2번으로 기대하면 <b>코드가 아니라 부동소수를
        /// 테스트하게</b> 된다(AGENTS.md 부동소수 절). 칸 안쪽만 보고, 비율 자체는
        /// 아래 분포 테스트가 잠근다.
        /// </summary>
        [TestCase(0.00f, 0)]
        [TestCase(0.30f, 0)]
        [TestCase(0.58f, 0)]
        [TestCase(0.62f, 1)]
        [TestCase(0.88f, 1)]
        [TestCase(0.92f, 2)]
        [TestCase(0.999f, 2)]
        public void 가중치대로_고른다(float roll, int expected)
        {
            var weights = new System.Collections.Generic.List<int> { 60, 30, 10 };
            Assert.That(ShopDraw.PickWeightedIndex(weights, roll, 3), Is.EqualTo(expected));
        }

        /// <summary>
        /// 비율이 실제로 가중치를 따른다. <b>이것이 이 함수의 본체다</b> —
        /// 개별 경계는 부동소수를 타지만 1000 칸을 고르게 훑은 분포는 안 흔들린다.
        /// </summary>
        [Test]
        public void 뽑은_분포가_가중치를_따른다()
        {
            var weights = new System.Collections.Generic.List<int> { 60, 30, 10 };
            var hits = new int[3];

            for (int i = 0; i < 1000; i++)
                hits[ShopDraw.PickWeightedIndex(weights, i / 1000f, 3)]++;

            // 1000 칸을 60:30:10 으로 자르면 정확히 600:300:100 이다.
            // 부동소수 때문에 경계에서 한두 칸 밀릴 수 있어 여유를 둔다.
            Assert.That(hits[0], Is.EqualTo(600).Within(2));
            Assert.That(hits[1], Is.EqualTo(300).Within(2));
            Assert.That(hits[2], Is.EqualTo(100).Within(2));
        }

        /// <summary>
        /// roll 이 1.0 이어도 범위를 넘지 않는다. <c>Random.value</c> 는 1.0 을 포함하므로
        /// 여기서 안 막으면 <b>아주 가끔</b> 인덱스가 튄다 — 재현이 거의 안 되는 종류다.
        /// </summary>
        [Test]
        public void roll_이_1이어도_범위를_안_넘는다()
        {
            var weights = new System.Collections.Generic.List<int> { 60, 30, 10 };

            Assert.That(ShopDraw.PickWeightedIndex(weights, 1f, 3), Is.EqualTo(2));
            Assert.That(ShopDraw.PickWeightedIndex(null, 1f, 3), Is.EqualTo(2));
            Assert.That(ShopDraw.PickWeightedIndex(null, -0.5f, 3), Is.EqualTo(0));
        }

        /// <summary>가중치가 0인 등급은 절대 안 나온다. 0 을 적는 것이 "빼기" 여야 한다.</summary>
        [Test]
        public void 가중치_0_인_등급은_안_나온다()
        {
            var weights = new System.Collections.Generic.List<int> { 50, 0, 50 };

            for (int i = 0; i <= 100; i++)
                Assert.That(ShopDraw.PickWeightedIndex(weights, i / 100f, 3), Is.Not.EqualTo(1));
        }

        [TestCase(Rarity.Common, Rarity.Rare, 3)]
        [TestCase(Rarity.Normal, Rarity.Epic, 3)]
        [TestCase(Rarity.Rare, Rarity.Rare, 1)]
        public void 등급_구간_칸수(Rarity min, Rarity max, int expected)
        {
            Assert.That(ShopDraw.RarityStepCount(min, max), Is.EqualTo(expected));
        }

        /// <summary>구간이 뒤집혀 들어와도 1 이상이다. 0 이면 뽑기가 아무것도 안 준다.</summary>
        [Test]
        public void 등급_구간이_뒤집혀도_최소_1칸이다()
        {
            Assert.That(ShopDraw.RarityStepCount(Rarity.Epic, Rarity.Common), Is.EqualTo(1));
        }

        [TestCase(6, "6시간")]
        [TestCase(24, "1일")]
        [TestCase(48, "2일")]
        [TestCase(52, "2일 4시간")]
        public void 남은_기간_표기(int hours, string expected)
        {
            Assert.That(ShopText.FormatDuration(hours), Is.EqualTo(expected));
        }
    }
}
