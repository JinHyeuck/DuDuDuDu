using System;
using System.Collections.Generic;
using NUnit.Framework;
using OJ;
using OJ.Pinball;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 핀볼의 판정 규칙을 잠근다.
    ///
    /// <b>왜 여기서만 테스트가 되는가.</b> 헤드리스 러너 안에서는
    /// <c>ScriptableObject.CreateInstance</c> 도 <c>MonoSingleton.Instance</c> 도 돌지 않는다
    /// (<see cref="AssemblyPilotTests"/> 주석). 그래서 규칙을 <c>PinballRules</c> 라는
    /// 상태 없는 <c>static</c> 과 <c>PinballRewardDatabase.Validate</c> 의 목록 오버로드로
    /// 빼 두었고, 이 파일은 그것만 두드린다.
    ///
    /// <b>확률이 왜 중요한가.</b> 이 판은 물리로 칸이 정해지지 않는다 — 여기서 뽑은 칸으로
    /// 가는 궤적을 골라 재생할 뿐이다(<c>Assets/Pinball/README.md</c>). 즉
    /// <see cref="PinballRules.DrawSlot"/> 이 <b>지급 확률 그 자체</b>다.
    /// </summary>
    public sealed class PinballRulesTests
    {
        /// <summary>현재 판(<c>PinballBoard.asset</c>)의 실제 확률표.</summary>
        private static readonly float[] Declared = { 0.20f, 0.22f, 0.16f, 0.22f, 0.20f };

        // ── 칸 추첨 ─────────────────────────────────────────────────────

        /// <summary>
        /// 누적 구간의 경계가 정확히 어느 칸에 속하는지 못 박는다.
        /// 경계가 한 칸씩 밀리면 표기 확률과 실제 지급이 어긋나는데,
        /// <b>그 차이는 수만 판을 돌려야 눈에 보인다.</b>
        /// </summary>
        [TestCase(0f, 0)]
        [TestCase(0.1999f, 0)]
        [TestCase(0.2f, 1)]       // 0.20 경계는 다음 칸의 시작이다
        [TestCase(0.41f, 1)]
        [TestCase(0.42f, 2)]      // 0.20 + 0.22
        [TestCase(0.57f, 2)]
        [TestCase(0.58f, 3)]      // + 0.16
        [TestCase(0.79f, 3)]
        [TestCase(0.8f, 4)]       // + 0.22
        [TestCase(0.9999f, 4)]
        public void 칸_추첨이_누적구간_경계를_지킨다(float roll, int expected)
        {
            Assert.That(PinballRules.DrawSlot(Declared, roll), Is.EqualTo(expected));
        }

        /// <summary>
        /// <c>UnityEngine.Random.value</c> 는 1.0 을 포함한다. 가두지 않으면
        /// 누적 합과 같아져 어느 칸에도 안 걸리고, 그 판은 티켓만 쓰고 끝난다.
        /// </summary>
        [Test]
        public void roll_이_1이어도_마지막_칸이_나온다()
        {
            Assert.That(PinballRules.DrawSlot(Declared, 1f), Is.EqualTo(4));
        }

        [Test]
        public void roll_이_범위를_벗어나도_칸_안에_떨어진다()
        {
            Assert.That(PinballRules.DrawSlot(Declared, -5f), Is.EqualTo(0));
            Assert.That(PinballRules.DrawSlot(Declared, 12f), Is.EqualTo(4));
        }

        /// <summary>
        /// 합이 1이 아니어도 비율은 지켜진다. 합이 어긋난 것 자체는 CI 의
        /// <c>DeclaredProbability_SumsToOne</c> 이 잡으므로, 런타임은 지급을 멈추는 대신
        /// 비율을 지키는 쪽을 택했다.
        /// </summary>
        [Test]
        public void 합이_1이_아니어도_비율로_정규화된다()
        {
            float[] weights = { 1f, 1f, 2f };   // 25 / 25 / 50

            Assert.That(PinballRules.DrawSlot(weights, 0f), Is.EqualTo(0));
            Assert.That(PinballRules.DrawSlot(weights, 0.24f), Is.EqualTo(0));
            Assert.That(PinballRules.DrawSlot(weights, 0.25f), Is.EqualTo(1));
            Assert.That(PinballRules.DrawSlot(weights, 0.49f), Is.EqualTo(1));
            Assert.That(PinballRules.DrawSlot(weights, 0.5f), Is.EqualTo(2));
            Assert.That(PinballRules.DrawSlot(weights, 0.99f), Is.EqualTo(2));
        }

        /// <summary>가중치가 0 인 칸은 절대 나오지 않는다. 기획이 칸을 잠그는 방법이다.</summary>
        [Test]
        public void 가중치가_0인_칸은_나오지_않는다()
        {
            float[] weights = { 0f, 1f, 0f };

            for (int i = 0; i <= 100; i++)
                Assert.That(PinballRules.DrawSlot(weights, i / 100f), Is.EqualTo(1));
        }

        [Test]
        public void 뽑을_수_없는_표는_음수를_돌려준다()
        {
            Assert.That(PinballRules.DrawSlot(null, 0.5f), Is.EqualTo(-1));
            Assert.That(PinballRules.DrawSlot(new float[0], 0.5f), Is.EqualTo(-1));
            Assert.That(PinballRules.DrawSlot(new[] { 0f, 0f }, 0.5f), Is.EqualTo(-1));
            Assert.That(PinballRules.DrawSlot(new[] { -1f, -2f }, 0.5f), Is.EqualTo(-1));
        }

        /// <summary>
        /// 표기 확률이 실제 분포로 나오는지 본다. 고정 시드를 써서 결정론적이다 —
        /// 랜덤 시드로 두면 <b>이 테스트가 가끔 빨개지고, 그러면 아무도 안 믿게 된다.</b>
        /// </summary>
        [Test]
        public void 많이_돌리면_표기_확률에_수렴한다()
        {
            const int Trials = 200000;
            const float Tolerance = 0.005f;

            var random = new Random(12345);
            var counts = new int[Declared.Length];

            for (int i = 0; i < Trials; i++)
            {
                int slot = PinballRules.DrawSlot(Declared, (float)random.NextDouble());
                Assert.That(slot, Is.InRange(0, Declared.Length - 1));
                counts[slot]++;
            }

            for (int i = 0; i < Declared.Length; i++)
            {
                float actual = counts[i] / (float)Trials;
                Assert.That(actual, Is.EqualTo(Declared[i]).Within(Tolerance),
                    i + "번 칸의 실제 비율이 표기와 다르다");
            }
        }

        // ── 특수 핀 게이지 ──────────────────────────────────────────────

        [Test]
        public void 게이지는_임계치_직전까지_쌓인다()
        {
            int gauge = 0;
            for (int i = 1; i < 5; i++)
            {
                gauge = PinballRules.AdvanceGauge(gauge, 5, out bool granted);
                Assert.That(granted, Is.False, i + "번째에서 일찍 지급됐다");
                Assert.That(gauge, Is.EqualTo(i));
            }
        }

        [Test]
        public void 임계치에_닿으면_지급하고_0으로_돌아간다()
        {
            int gauge = PinballRules.AdvanceGauge(4, 5, out bool granted);

            Assert.That(granted, Is.True);
            Assert.That(gauge, Is.EqualTo(0));
        }

        /// <summary>
        /// 임계치가 0 이면 "매번 지급"이 되어 보상 설정을 빠뜨린 태그가 무한 지급으로 샌다.
        /// 설정 실수를 사고로 키우지 않도록 게이지도 올리지 않는다.
        /// </summary>
        [TestCase(0)]
        [TestCase(-3)]
        public void 임계치가_없으면_지급도_적립도_없다(int requiredHits)
        {
            int gauge = PinballRules.AdvanceGauge(7, requiredHits, out bool granted);

            Assert.That(granted, Is.False);
            Assert.That(gauge, Is.EqualTo(7));
        }

        /// <summary>손상된 세이브가 음수를 들고 와도 0부터 다시 센다.</summary>
        [Test]
        public void 음수_게이지는_0으로_보정된다()
        {
            int gauge = PinballRules.AdvanceGauge(-5, 3, out bool granted);

            Assert.That(granted, Is.False);
            Assert.That(gauge, Is.EqualTo(1));
        }

        // ── 경품표 검사 ─────────────────────────────────────────────────

        private static List<PinballSlotReward> Slots(int count)
        {
            var list = new List<PinballSlotReward>();
            for (int i = 0; i < count; i++)
            {
                var slot = new PinballSlotReward();
                slot.rewards.Add(new PinballReward(PointType.Gold, 100));
                list.Add(slot);
            }

            return list;
        }

        private static PinballSpecialReward Special(int tag, int requiredHits)
        {
            var rule = new PinballSpecialReward { tag = tag, requiredHits = requiredHits };
            rule.rewards.Add(new PinballReward(PointType.FreeGem, 5));
            return rule;
        }

        [Test]
        public void 판과_짝이_맞는_경품표는_통과한다()
        {
            bool ok = PinballRewardDatabase.Validate(
                Slots(5), new List<PinballSpecialReward> { Special(1, 10), Special(2, 20) },
                5, out string error);

            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Empty);
        }

        /// <summary>
        /// 칸 수가 어긋나면 그 칸에 떨어진 공이 빈손으로 끝난다.
        /// 판을 고치고 경품표를 안 고쳤을 때 여기가 잡는다.
        /// </summary>
        [Test]
        public void 칸_수가_판과_다르면_잡힌다()
        {
            Assert.That(PinballRewardDatabase.Validate(Slots(4), null, 5, out _), Is.False);
            Assert.That(PinballRewardDatabase.Validate(Slots(6), null, 5, out _), Is.False);
            Assert.That(PinballRewardDatabase.Validate(null, null, 5, out _), Is.False);
        }

        [Test]
        public void 경품이_빈_칸은_잡힌다()
        {
            List<PinballSlotReward> slots = Slots(5);
            slots[2].rewards.Clear();

            Assert.That(PinballRewardDatabase.Validate(slots, null, 5, out string error), Is.False);
            Assert.That(error, Does.Contain("2번 칸"));
        }

        /// <summary>
        /// 태그가 중복이면 <c>GetSpecial</c> 이 앞엣것만 돌려주고 뒤엣것은 조용히 무시된다.
        /// 기획은 보상을 넣었다고 생각하는데 안 나오는 상태다.
        /// </summary>
        [Test]
        public void 특수핀_태그_중복이_잡힌다()
        {
            var specials = new List<PinballSpecialReward> { Special(1, 10), Special(1, 20) };

            Assert.That(PinballRewardDatabase.Validate(Slots(5), specials, 5, out string error), Is.False);
            Assert.That(error, Does.Contain("중복"));
        }

        [Test]
        public void 필요_적중수가_0이면_잡힌다()
        {
            var specials = new List<PinballSpecialReward> { Special(1, 0) };

            Assert.That(PinballRewardDatabase.Validate(Slots(5), specials, 5, out string error), Is.False);
            Assert.That(error, Does.Contain("0 이하"));
        }
    }
}
