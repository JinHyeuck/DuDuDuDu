using System;
using NUnit.Framework;
using OJ.Pinball;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 보상 라운드(주사위 족보 → 공 개수)의 판정을 잠근다.
    ///
    /// <b>이 컨텐츠의 목적이 곧 테스트의 기준이다.</b> 보상 라운드는 핀볼에서 센터핀을
    /// 많이 맞힌 것에 대한 <i>칭찬</i>이고, "조금만 참여해도 좋은 걸 거의 다 가져간다"가
    /// 설계 목표다. 그래서 여기서 제일 중요한 것은 확률이 아니라
    /// <b>어떤 손패로도 공이 한 발은 나간다</b>는 불변식이다 — 꽝이 생기는 순간 이 컨텐츠는
    /// 존재 이유를 잃는다.
    ///
    /// 특수 핀 카운트 누적은 <see cref="PinballRules.AdvanceGauge"/> 를 그대로 쓰므로
    /// 여기서 다시 테스트하지 않는다. <c>PinballRulesTests</c> 가 경계까지 덮고 있고,
    /// 같은 규칙을 두 군데서 잠그면 둘 중 하나만 고쳐지는 날이 온다.
    /// </summary>
    public sealed class BonusDiceRulesTests
    {
        // ── 족보 판정 ───────────────────────────────────────────────────

        [TestCase(1, 1, 1, 1, 1, DiceHand.FiveOfAKind)]
        [TestCase(6, 6, 6, 6, 6, DiceHand.FiveOfAKind)]
        [TestCase(3, 3, 3, 3, 5, DiceHand.FourOfAKind)]
        [TestCase(5, 3, 3, 3, 3, DiceHand.FourOfAKind)]
        [TestCase(1, 2, 3, 4, 5, DiceHand.LargeStraight)]
        [TestCase(2, 3, 4, 5, 6, DiceHand.LargeStraight)]
        [TestCase(6, 4, 2, 5, 3, DiceHand.LargeStraight)]
        [TestCase(2, 2, 2, 5, 5, DiceHand.FullHouse)]
        [TestCase(5, 2, 5, 2, 2, DiceHand.FullHouse)]
        [TestCase(2, 2, 2, 4, 5, DiceHand.Triple)]
        [TestCase(1, 1, 4, 4, 6, DiceHand.TwoPair)]
        [TestCase(1, 1, 2, 2, 4, DiceHand.TwoPair)]
        [TestCase(1, 1, 3, 5, 6, DiceHand.OnePair)]
        [TestCase(1, 2, 3, 5, 6, DiceHand.None)]
        [TestCase(1, 3, 5, 2, 6, DiceHand.None)]
        public void EvaluateHand_ReadsTheHand(int a, int b, int c, int d, int e, DiceHand expected)
        {
            Assert.AreEqual(expected, BonusDiceRules.EvaluateHand(new[] { a, b, c, d, e }));
        }

        /// <summary>
        /// 한 손패가 여러 족보를 동시에 만족할 때 <b>항상 큰 쪽</b>이 잡혀야 한다.
        ///
        /// 이게 뒤집히면 유저가 손해를 보는데, 그 손해는 화면에 "원페어"라고 정직하게
        /// 적히기 때문에 <b>버그로 안 보이고 그냥 운이 나쁜 것으로 보인다.</b>
        /// </summary>
        [TestCase(1, 1, 2, 3, 4, DiceHand.SmallStraight, TestName = "4연속 + 원페어 → 4연속")]
        [TestCase(1, 2, 3, 4, 6, DiceHand.SmallStraight, TestName = "4연속 + 노페어 → 4연속")]
        [TestCase(3, 4, 5, 6, 1, DiceHand.SmallStraight, TestName = "5연속 아님 → 4연속")]
        [TestCase(2, 2, 3, 4, 5, DiceHand.SmallStraight, TestName = "4연속 + 원페어(중간) → 4연속")]
        public void EvaluateHand_PrefersTheBetterHand(
            int a, int b, int c, int d, int e, DiceHand expected)
        {
            Assert.AreEqual(expected, BonusDiceRules.EvaluateHand(new[] { a, b, c, d, e }));
        }

        /// <summary>
        /// 눈이 1~6 밖이면 없는 것으로 친다. 굴림이 깨졌을 때 엉뚱하게 큰 족보가
        /// 나오는 것보다 최하위로 떨어지는 편이 안전하다 — 최하위에도 보상이 있으므로
        /// 이 선택은 유저를 빈손으로 만들지 않는다.
        /// </summary>
        [Test]
        public void EvaluateHand_IgnoresPipsOutOfRange()
        {
            Assert.AreEqual(DiceHand.None, BonusDiceRules.EvaluateHand(new[] { 0, 7, -1, 99, 8 }));

            // 유효한 눈만으로 판정한다. 1 이 셋이고 나머지는 버려지므로 트리플.
            Assert.AreEqual(DiceHand.Triple, BonusDiceRules.EvaluateHand(new[] { 1, 1, 1, 0, 9 }));
        }

        [Test]
        public void EvaluateHand_EmptyOrNullIsLowest()
        {
            Assert.AreEqual(DiceHand.None, BonusDiceRules.EvaluateHand(null));
            Assert.AreEqual(DiceHand.None, BonusDiceRules.EvaluateHand(Array.Empty<int>()));
        }

        // ── 공 개수 ─────────────────────────────────────────────────────

        /// <summary>
        /// <b>이 컨텐츠의 핵심 불변식.</b> 어떤 족보든, 표가 어떻게 망가져 있든
        /// 공은 최소 한 발 나간다. 꽝이 생기면 "칭찬"이 아니게 된다.
        /// </summary>
        [Test]
        public void BallCount_IsNeverZero_ForAnyHandOrTable()
        {
            foreach (DiceHand hand in Enum.GetValues(typeof(DiceHand)))
            {
                Assert.Greater(BonusDiceRules.BallCountFor(hand, null), 0,
                    hand + ": 표가 없을 때 공이 안 나간다");

                Assert.Greater(BonusDiceRules.BallCountFor(hand, new[] { 0, 0, 0 }), 0,
                    hand + ": 표가 0 으로 채워졌을 때 공이 안 나간다");

                Assert.Greater(BonusDiceRules.BallCountFor(hand, Array.Empty<int>()), 0,
                    hand + ": 표가 비었을 때 공이 안 나간다");
            }
        }

        /// <summary>
        /// 족보가 좋아질수록 공이 <b>반드시</b> 늘어야 한다. enum 값의 크기가 곧 순위라는
        /// 규약(<see cref="DiceHand"/> 주석)을 표가 실제로 지키는지 보는 것이다 —
        /// 중간에 족보를 하나 끼워 넣으면 여기가 먼저 터진다.
        /// </summary>
        [Test]
        public void DefaultBallTable_RisesWithHandRank()
        {
            int[] table = BonusDiceRules.DefaultBallTable;

            Assert.AreEqual(Enum.GetValues(typeof(DiceHand)).Length, table.Length,
                "족보 수와 표의 길이가 다르다");

            for (int i = 1; i < table.Length; i++)
            {
                Assert.Greater(table[i], table[i - 1],
                    (DiceHand)i + " 가 " + (DiceHand)(i - 1) + " 보다 공이 적거나 같다");
            }
        }

        [Test]
        public void BallCount_UsesTableWhenPresent()
        {
            var table = new[] { 7, 9, 11, 13, 15, 17, 19, 21, 23 };

            Assert.AreEqual(7, BonusDiceRules.BallCountFor(DiceHand.None, table));
            Assert.AreEqual(9, BonusDiceRules.BallCountFor(DiceHand.OnePair, table));
            Assert.AreEqual(23, BonusDiceRules.BallCountFor(DiceHand.FiveOfAKind, table));
        }

        /// <summary>표가 짧거나 그 자리가 0 이면 기본표로 내려간다.</summary>
        [Test]
        public void BallCount_FallsBackPerEntry()
        {
            var shortTable = new[] { 7, 9 };

            Assert.AreEqual(9, BonusDiceRules.BallCountFor(DiceHand.OnePair, shortTable));
            Assert.AreEqual(BonusDiceRules.DefaultBallTable[(int)DiceHand.FiveOfAKind],
                BonusDiceRules.BallCountFor(DiceHand.FiveOfAKind, shortTable));

            var holeTable = new[] { 7, 0, 11, 13, 15, 17, 19, 21, 23 };
            Assert.AreEqual(BonusDiceRules.DefaultBallTable[(int)DiceHand.OnePair],
                BonusDiceRules.BallCountFor(DiceHand.OnePair, holeTable));
        }

        // ── 특수 핀 카운트 ─────────────────────────────────────

        /// <summary>
        /// 카운트가 임계치까지 오르는 동안에는 보상이 안 나가고,
        /// 닿는 호출에서 딱 한 번 나간다.
        /// </summary>
        [Test]
        public void AdvanceCount_GrantsOnceOnReachingThreshold()
        {
            bool done;

            Assert.AreEqual(1, BonusDiceRules.AdvanceCount(0, 3, 1, out done));
            Assert.IsFalse(done);

            Assert.AreEqual(2, BonusDiceRules.AdvanceCount(1, 3, 1, out done));
            Assert.IsFalse(done);

            Assert.AreEqual(3, BonusDiceRules.AdvanceCount(2, 3, 1, out done));
            Assert.IsTrue(done, "임계치에 닿았는데 보상이 안 나간다");
        }

        /// <summary>
        /// <b>한 핀은 한 라운드에 한 번만 준다.</b> 이게 뚫리면 공이 계속 맞는 동안
        /// 같은 핀이 보상을 무한히 뱉는다 — 이 컨텐츠는 공이 한 번에 수십 개 나가므로
        /// 그 사고가 한 판 안에서 바로 터진다.
        /// </summary>
        [Test]
        public void AdvanceCount_NeverGrantsTwice()
        {
            bool done;

            int count = BonusDiceRules.AdvanceCount(2, 3, 1, out done);
            Assert.IsTrue(done);
            Assert.AreEqual(3, count);

            for (int i = 0; i < 10; i++)
            {
                count = BonusDiceRules.AdvanceCount(count, 3, 5, out done);
                Assert.IsFalse(done, "이미 채운 핀이 다시 보상을 내봐준다");
                Assert.AreEqual(3, count, "카운트가 임계치를 넘어 자란다");
            }
        }

        /// <summary>공이 한꺼번에 많이 나가 임계치를 훌씩 넘겨도 보상은 한 번이다.</summary>
        [Test]
        public void AdvanceCount_OvershootStillGrantsOnce()
        {
            bool done;
            Assert.AreEqual(5, BonusDiceRules.AdvanceCount(0, 5, 60, out done));
            Assert.IsTrue(done);

            Assert.AreEqual(5, BonusDiceRules.AdvanceCount(0, 5, int.MaxValue, out done));
            Assert.IsTrue(done);
        }

        /// <summary>
        /// 임계치가 0 이하면 보상이 안 걸린 태그다. 올리지도, 주지도 않는다 —
        /// 0 을 "매번 지급"으로 읽으면 설정을 빠뜨린 핀이 무한 지급으로 새는다.
        /// </summary>
        [Test]
        public void AdvanceCount_NoRuleMeansNoProgress()
        {
            bool done;

            Assert.AreEqual(4, BonusDiceRules.AdvanceCount(4, 0, 10, out done));
            Assert.IsFalse(done);

            Assert.AreEqual(4, BonusDiceRules.AdvanceCount(4, -1, 10, out done));
            Assert.IsFalse(done);

            Assert.AreEqual(4, BonusDiceRules.AdvanceCount(4, 5, 0, out done));
            Assert.IsFalse(done);

            Assert.AreEqual(0, BonusDiceRules.AdvanceCount(-3, 5, 0, out done));
            Assert.IsFalse(done);
        }

        [Test]
        public void IsComplete_NeedsARule()
        {
            Assert.IsTrue(BonusDiceRules.IsComplete(3, 3));
            Assert.IsTrue(BonusDiceRules.IsComplete(9, 3));
            Assert.IsFalse(BonusDiceRules.IsComplete(2, 3));

            // 보상이 안 걸린 태그는 "채울 것이 없다" — 광고 버튼 조건에서도 빠진다.
            Assert.IsFalse(BonusDiceRules.IsComplete(0, 0));
            Assert.IsFalse(BonusDiceRules.IsComplete(99, 0));
        }

        // ── 락 ──────────────────────────────────────────────────────────

        [Test]
        public void UnlockedCount_CountsWhatWillBeRerolled()
        {
            Assert.AreEqual(5, BonusDiceRules.UnlockedCount(5, null));
            Assert.AreEqual(5, BonusDiceRules.UnlockedCount(5, new[] { false, false, false, false, false }));
            Assert.AreEqual(0, BonusDiceRules.UnlockedCount(5, new[] { true, true, true, true, true }));
            Assert.AreEqual(3, BonusDiceRules.UnlockedCount(5, new[] { true, false, true, false, false }));
        }

        /// <summary>락 배열이 주사위보다 짧으면 남는 자리는 <b>안 잠긴 것</b>으로 본다.</summary>
        [Test]
        public void UnlockedCount_TreatsMissingLocksAsUnlocked()
        {
            Assert.AreEqual(3, BonusDiceRules.UnlockedCount(5, new[] { true, true }));
            Assert.AreEqual(0, BonusDiceRules.UnlockedCount(0, new[] { true, true }));
        }
    }
}
