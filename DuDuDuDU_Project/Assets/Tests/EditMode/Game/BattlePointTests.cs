using System;
using NUnit.Framework;
using OJ;
using OJ.Battle;
using OJ.Core;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 전투 재화의 <b>순수 규칙</b>만 잠근다. 창구와 이름 판정 둘 다 싱글톤 없이 돈다 —
    /// 에디터 밖 러너에서는 <c>StaticResource</c> 도 <c>GameContainer</c> 도 없기 때문에,
    /// 검증돼야 하는 판단은 처음부터 그 바깥에 두었다.
    ///
    /// <b>여기서 안 하는 것.</b> 되돌리기·판 리셋에서 값이 살아남는지는
    /// <c>RunStateSnapshotTests</c> 가 리플렉션으로 전 필드를 훑어 이미 덮는다.
    /// 같은 것을 두 군데서 잠그면 둘 중 하나만 고쳐지는 날이 온다.
    /// </summary>
    public sealed class BattlePointTests
    {
        private static BattlePointManager NewBank(out RunState run)
        {
            run = new RunState();
            return new BattlePointManager(run);
        }

        // ── 창구가 RunState 를 정본으로 쓰는가 ──────────────────────────

        [Test]
        public void 창구는_자기_값을_들지_않고_RunState_를_읽는다()
        {
            BattlePointManager bank = NewBank(out RunState run);

            // 창구를 거치지 않고 RunState 를 직접 고친다. 되돌리기가 하는 일과 같다.
            run.EnhanceStone = 7;
            run.SummonPoint = 30;

            Assert.AreEqual(7, bank.Get(BattlePointType.EnhanceStone));
            Assert.AreEqual(30, bank.Get(BattlePointType.SummonPoint));
        }

        [Test]
        public void 창구로_쓴_값이_RunState_에_남는다()
        {
            BattlePointManager bank = NewBank(out RunState run);

            bank.Add(BattlePointType.EnhanceStone, 5);

            // 창구가 값을 따로 들고 있으면 여기가 0 으로 남는다. 그러면 되돌리기와
            // 판 리셋이 RunState 만 보므로 재화가 조용히 살아남거나 사라진다.
            Assert.AreEqual(5, run.EnhanceStone);
        }

        // ── 보유량 규칙 ────────────────────────────────────────────────

        [Test]
        public void 모자라면_쓰지_못하고_한_개도_깎이지_않는다()
        {
            BattlePointManager bank = NewBank(out RunState run);
            bank.Add(BattlePointType.EnhanceStone, 3);

            Assert.IsFalse(bank.TrySpend(BattlePointType.EnhanceStone, 4));

            // 실패했는데 일부가 깎이면 "못 샀는데 돈이 줄었다"가 된다.
            Assert.AreEqual(3, bank.Get(BattlePointType.EnhanceStone));
        }

        [Test]
        public void 정확히_가진_만큼은_쓸_수_있다()
        {
            BattlePointManager bank = NewBank(out RunState run);
            bank.Add(BattlePointType.EnhanceStone, 3);

            Assert.IsTrue(bank.TrySpend(BattlePointType.EnhanceStone, 3));
            Assert.AreEqual(0, bank.Get(BattlePointType.EnhanceStone));
        }

        [Test]
        public void 음수_비용은_거절한다()
        {
            BattlePointManager bank = NewBank(out RunState run);
            bank.Add(BattlePointType.EnhanceStone, 3);

            // 통과시키면 "보유량 < 음수" 가 항상 거짓이라 검사를 뚫고,
            // Get - (음수) = 증가가 되어 <b>재화가 발급된다.</b>
            // 데이터 테이블에 음수 비용이 하나 들어가면 그대로 열린다.
            Assert.IsFalse(bank.TrySpend(BattlePointType.EnhanceStone, -5));
            Assert.AreEqual(3, bank.Get(BattlePointType.EnhanceStone));
        }

        [Test]
        public void 보유량은_음수가_되지_않는다()
        {
            BattlePointManager bank = NewBank(out RunState run);

            bank.Set(BattlePointType.EnhanceStone, -10);

            // "보유량은 음수가 아니다"가 나머지 코드의 전제다. 깨지면 이후에 얻은 재화가
            // 빚을 메우는 데 먼저 쓰이고 조용히 사라진다.
            Assert.AreEqual(0, bank.Get(BattlePointType.EnhanceStone));
        }

        [Test]
        public void Max_에는_쓰지도_읽히지도_않는다()
        {
            BattlePointManager bank = NewBank(out RunState run);

            bank.Set(BattlePointType.Max, 99);

            Assert.AreEqual(0, bank.Get(BattlePointType.Max));
            Assert.AreEqual(0, run.EnhanceStone);
            Assert.AreEqual(0, run.SummonPoint);
        }

        // ── 변경 통보 ──────────────────────────────────────────────────

        [Test]
        public void 값이_바뀌면_종류와_바뀐_뒤의_보유량을_알린다()
        {
            BattlePointManager bank = NewBank(out RunState run);

            BattlePointType got = BattlePointType.Max;
            int amount = -1;
            bank.OnBattlePointChanged += (type, value) => { got = type; amount = value; };

            bank.Add(BattlePointType.SummonPoint, 12);

            Assert.AreEqual(BattlePointType.SummonPoint, got);
            Assert.AreEqual(12, amount);
        }

        [Test]
        public void NotifyAllChanged_는_Max_를_빼고_전부_알린다()
        {
            BattlePointManager bank = NewBank(out RunState run);
            run.SummonPoint = 4;
            run.EnhanceStone = 9;

            var seen = new System.Collections.Generic.Dictionary<BattlePointType, int>();
            bank.OnBattlePointChanged += (type, value) => seen[type] = value;

            // 되돌리기가 RunState 를 직접 고친 뒤 부르는 경로다. 여기가 빠지면
            // 열려 있던 UI 가 옛 숫자를 든 채로 남는다.
            bank.NotifyAllChanged();

            Assert.AreEqual(4, seen[BattlePointType.SummonPoint]);
            Assert.AreEqual(9, seen[BattlePointType.EnhanceStone]);
            Assert.IsFalse(seen.ContainsKey(BattlePointType.Max));
        }

        // ── 표시명 ─────────────────────────────────────────────────────

        [Test]
        public void 등록된_이름이_폴백을_이긴다()
        {
            Assert.AreEqual("정예 강화석",
                BattlePointUtility.ResolveName("정예 강화석", BattlePointType.EnhanceStone));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void 등록이_비면_폴백으로_내려간다(string registered)
        {
            Assert.AreEqual("강화석",
                BattlePointUtility.ResolveName(registered, BattlePointType.EnhanceStone));
        }

        [Test]
        public void enum_명_그대로인_등록은_미등록으로_본다()
        {
            // 그러지 않으면 등록되지 않은 재화가 화면에 "EnhanceStone" 으로 뜨는데,
            // 그건 값이 있는 것처럼 보여서 빠뜨렸다는 사실이 드러나지 않는다.
            Assert.AreEqual("강화석",
                BattlePointUtility.ResolveName("EnhanceStone", BattlePointType.EnhanceStone));
        }

        [Test]
        public void 모든_재화에_폴백_이름이_있다()
        {
            foreach (BattlePointType type in Enum.GetValues(typeof(BattlePointType)))
            {
                if (type == BattlePointType.Max)
                    continue;

                // enum 명이 그대로 나오면 그 재화는 표에서 빠진 것이다.
                Assert.AreNotEqual(
                    type.ToString(), BattlePointUtility.FallbackName(type),
                    type + " 의 한글 이름이 BattlePointUtility.FallbackName 에 없다.");
            }
        }
    }
}
