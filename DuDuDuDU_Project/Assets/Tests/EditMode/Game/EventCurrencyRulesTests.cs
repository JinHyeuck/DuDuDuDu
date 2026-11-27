using System;
using System.Linq;
using NUnit.Framework;
using OJ.Pinball;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>이벤트 재화 배선: 9일 주기(3종 × 3일)와 핀볼 보상표의 이벤트 몫.</summary>
    public sealed class EventCurrencyRulesTests
    {
        [Test]
        public void 열린_이벤트는_3일마다_바뀌고_9일에_한_바퀴다()
        {
            PointType[] expected =
            {
                PointType.EventCurrency1, PointType.EventCurrency1, PointType.EventCurrency1,
                PointType.EventCurrency2, PointType.EventCurrency2, PointType.EventCurrency2,
                PointType.EventCurrency3, PointType.EventCurrency3, PointType.EventCurrency3,
            };

            for (int day = 0; day < 27; day++)
                Assert.AreEqual(expected[day % 9], EventCurrencyRules.Resolve(day), "day " + day);
        }

        [Test]
        public void 음수_날짜도_같은_주기로_접힌다()
        {
            Assert.AreEqual(EventCurrencyRules.Resolve(8), EventCurrencyRules.Resolve(-1));
            Assert.AreEqual(EventCurrencyRules.Resolve(0), EventCurrencyRules.Resolve(-9));
        }

        [Test]
        public void 표지만_풀리고_나머지는_그대로다()
        {
            DateTime now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

            Assert.IsTrue(EventCurrencyRules.IsEventCurrency(EventCurrencyRules.ResolveReward(PointType.OpenEventCurrency, now)));
            Assert.AreEqual(PointType.Gold, EventCurrencyRules.ResolveReward(PointType.Gold, now));
            Assert.AreEqual(PointType.EventCurrency2, EventCurrencyRules.ResolveReward(PointType.EventCurrency2, now));
        }

        [Test]
        public void 날짜_번호는_UTC_자정에_바뀐다()
        {
            var a = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);
            var b = new DateTime(2026, 11, 18, 23, 59, 59, DateTimeKind.Utc);
            var c = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(EventCurrencyRules.DayIndex(a), EventCurrencyRules.DayIndex(b));
            Assert.AreEqual(EventCurrencyRules.DayIndex(a) + 1, EventCurrencyRules.DayIndex(c));
        }

        /// <summary>PointType 의 숫자는 세이브에 박힌다 — 새 값이 기존 값과 겹치면 안 된다.</summary>
        [Test]
        public void PointType_숫자가_겹치지_않는다()
        {
            var values = Enum.GetValues(typeof(PointType)).Cast<PointType>().Select(v => (int)v).ToList();

            Assert.AreEqual(values.Count, values.Distinct().Count());
            Assert.AreEqual(134, (int)PointType.OpenEventCurrency);
            Assert.AreEqual(140, (int)PointType.EventCurrency1);
        }

        /// <summary>핀볼 아래 칸의 이벤트 재화: 발당 0.09 — 5,000발에 450개(장부). 확률 지급이라 양 × 확률이다.</summary>
        [Test]
        public void 아래칸_이벤트_재화는_5천발에_450개_근처다()
        {
            double[] prob = { 0.2, 0.2, 0.2, 0.2, 0.2 };
            double perBall = 0;
            for (int i = 0; i < prob.Length; i++)
                perBall += prob[i] * PinballRewardDatabase.EventCurrencyPerSlot[i];
            perBall *= PinballRewardDatabase.EventCurrencyChancePercent / 100.0;

            Assert.That(perBall * 5000, Is.EqualTo(450).Within(10));
        }

        /// <summary>하루 2,000발 + Bonus 24번 추첨 → 이벤트 재화 하루 약 200개(장부).</summary>
        [Test]
        public void 하루_이벤트_재화는_200개_근처다()
        {
            double[] prob = { 0.2, 0.2, 0.2, 0.2, 0.2 };
            double baseDaily = 0;
            for (int i = 0; i < prob.Length; i++)
                baseDaily += prob[i] * PinballRewardDatabase.EventCurrencyPerSlot[i] * 2000;
            baseDaily *= PinballRewardDatabase.EventCurrencyChancePercent / 100.0;
            double bonusDaily = BonusDiceDatabase.BuildDefaultPool()
                .Where(d => d.rewards.Any(r => r.pointType == PointType.OpenEventCurrency))
                .Sum(d => 24.0 * d.weight / 1000.0 * d.rewards.First(r => r.pointType == PointType.OpenEventCurrency).amount);

            Assert.That(baseDaily + bonusDaily, Is.EqualTo(200).Within(10));
        }
    }
}
