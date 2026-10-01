using System.Collections.Generic;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 미션·업적의 순수 규칙을 잠근다.
    ///
    /// <b>여기서 잠그는 핵심은 "업적 한 계열에서 무엇이 보이는가" 하나다.</b>
    /// 기획의 규칙이 "10회를 못 깼으면 10회를, 깼으면 100회를 보여 주되 카운트는
    /// 이어서 센다" 인데, 넘어가는 기준을 <b>달성</b>으로 잡으면 10회 보상을 받을 자리가
    /// 화면에서 사라진다 — 그 실수가 코드에서 자연스러워 보이기 때문에 테스트로 못박는다.
    /// </summary>
    public sealed class MissionRulesTests
    {
        // ── 카운터 키 ──────────────────────────────────────────────────

        [Test]
        public void CounterKeyUsesEnumName()
        {
            Assert.AreEqual("GemMerge", MissionRules.CounterKey(MissionAction.GemMerge));
        }

        [Test]
        public void CounterKeyWithSubKeyJoinsWithSeparator()
        {
            Assert.AreEqual("GemMerge:Rare",
                MissionRules.CounterKey(MissionAction.GemMerge, "Rare"));
        }

        /// <summary>
        /// 하위 키가 비면 상위 키와 같아야 한다. 일일 미션이 "보석 합성 전체" 를 세고
        /// 업적이 등급별로 세는 구조가 이것에 기대고 있다.
        /// </summary>
        [Test]
        public void EmptySubKeyFallsBackToPlainKey()
        {
            Assert.AreEqual(MissionRules.CounterKey(MissionAction.GemMerge),
                MissionRules.CounterKey(MissionAction.GemMerge, string.Empty));

            Assert.AreEqual(MissionRules.CounterKey(MissionAction.GemMerge),
                MissionRules.CounterKey(MissionAction.GemMerge, null));
        }

        /// <summary>캐시 표가 열거형 전체를 덮는지. 빠진 항목은 null 을 돌려줘 조용히 깨진다.</summary>
        [Test]
        public void EveryActionHasACachedKey()
        {
            foreach (MissionAction action in System.Enum.GetValues(typeof(MissionAction)))
            {
                string key = MissionRules.CounterKey(action);
                Assert.IsNotNull(key, action + " 의 키가 null 이다.");
                Assert.AreEqual(action.ToString(), key);
            }
        }

        /// <summary>
        /// 같은 행동에 대해 호출을 반복해도 <b>같은 문자열 인스턴스</b>를 돌려줘야 한다.
        /// 적 처치는 한 웨이브에 수백 번 들어오는 경로라 여기서 새로 찍으면 안 된다.
        /// </summary>
        [Test]
        public void CounterKeyDoesNotAllocatePerCall()
        {
            string first = MissionRules.CounterKey(MissionAction.MonsterKill);
            string second = MissionRules.CounterKey(MissionAction.MonsterKill);

            Assert.AreSame(first, second);
        }

        [Test]
        public void SubKeyWithSeparatorIsRejected()
        {
            Assert.IsFalse(MissionRules.IsValidSubKey("Ra:re"));
            Assert.IsTrue(MissionRules.IsValidSubKey("Rare"));
            Assert.IsTrue(MissionRules.IsValidSubKey(string.Empty));
            Assert.IsTrue(MissionRules.IsValidSubKey(null));
        }

        // ── 달성·진행 ──────────────────────────────────────────────────

        [Test]
        public void IsClearedNeedsPositiveRequirement()
        {
            Assert.IsTrue(MissionRules.IsCleared(2, 2));
            Assert.IsTrue(MissionRules.IsCleared(5, 2));
            Assert.IsFalse(MissionRules.IsCleared(1, 2));

            // 요구치가 0 이면 데이터 사고다. 달성으로 쳐 주면 보상이 공짜로 나간다.
            Assert.IsFalse(MissionRules.IsCleared(0, 0));
            Assert.IsFalse(MissionRules.IsCleared(100, 0));
        }

        [Test]
        public void ProgressClampsToUnitRange()
        {
            Assert.AreEqual(0f, MissionRules.Progress(0, 10), 0.0001f);
            Assert.AreEqual(0.5f, MissionRules.Progress(5, 10), 0.0001f);
            Assert.AreEqual(1f, MissionRules.Progress(10, 10), 0.0001f);
            Assert.AreEqual(1f, MissionRules.Progress(99, 10), 0.0001f);

            // 요구치가 0 이면 꽉 찬 바가 아니라 빈 바여야 한다 — 빠진 데이터가 보여야 한다.
            Assert.AreEqual(0f, MissionRules.Progress(5, 0), 0.0001f);
        }

        // ── 업적 계열 ──────────────────────────────────────────────────

        [Test]
        public void VisibleTierIsFirstUnclaimed()
        {
            var claimed = new List<bool> { true, false, false };
            Assert.AreEqual(1, MissionRules.VisibleTierIndex(3, claimed));
        }

        [Test]
        public void VisibleTierIsFirstWhenNothingClaimed()
        {
            var claimed = new List<bool> { false, false, false };
            Assert.AreEqual(0, MissionRules.VisibleTierIndex(3, claimed));
        }

        /// <summary>
        /// <b>달성했어도 안 받았으면 그 단계가 그대로 보여야 한다.</b>
        /// 이 테스트가 떨어지면 받을 수 없는 보상이 생긴 것이다.
        /// </summary>
        [Test]
        public void ClearedButUnclaimedTierStaysVisible()
        {
            var claimed = new List<bool> { false, false };

            // 10회를 이미 넘겼다(누적 250). 그래도 아직 안 받았으므로 0번이 보인다.
            Assert.AreEqual(0, MissionRules.VisibleTierIndex(2, claimed));
            Assert.IsTrue(MissionRules.IsCleared(250, 10));
        }

        [Test]
        public void AllClaimedShowsLastTierAsComplete()
        {
            var claimed = new List<bool> { true, true, true };

            Assert.AreEqual(2, MissionRules.VisibleTierIndex(3, claimed));
            Assert.IsTrue(MissionRules.IsSeriesComplete(3, claimed));
        }

        [Test]
        public void SeriesIsNotCompleteWhileAnyTierUnclaimed()
        {
            Assert.IsFalse(MissionRules.IsSeriesComplete(3, new List<bool> { true, true, false }));
            Assert.IsFalse(MissionRules.IsSeriesComplete(3, new List<bool> { false, true, true }));
        }

        /// <summary>기록이 단계 수보다 짧으면 그 뒤는 안 받은 것이다(단계를 새로 추가한 경우).</summary>
        [Test]
        public void ShorterClaimedListMeansRemainingTiersUnclaimed()
        {
            var claimed = new List<bool> { true, true };

            Assert.AreEqual(2, MissionRules.VisibleTierIndex(4, claimed));
            Assert.IsFalse(MissionRules.IsSeriesComplete(4, claimed));
        }

        [Test]
        public void EmptySeriesHasNoVisibleTier()
        {
            Assert.AreEqual(-1, MissionRules.VisibleTierIndex(0, new List<bool>()));
            Assert.IsFalse(MissionRules.IsSeriesComplete(0, new List<bool>()));
        }

        [Test]
        public void NullClaimedListShowsFirstTier()
        {
            Assert.AreEqual(0, MissionRules.VisibleTierIndex(3, null));
            Assert.IsFalse(MissionRules.IsSeriesComplete(3, null));
        }

        // ── 일일 추가 보상 ─────────────────────────────────────────────

        private static List<int> Thresholds() => new List<int> { 3, 5, 7, 10 };

        [Test]
        public void UnlockedTierCountCountsReachedThresholds()
        {
            List<int> thresholds = Thresholds();

            Assert.AreEqual(0, MissionRules.UnlockedTierCount(0, thresholds));
            Assert.AreEqual(0, MissionRules.UnlockedTierCount(2, thresholds));
            Assert.AreEqual(1, MissionRules.UnlockedTierCount(3, thresholds));
            Assert.AreEqual(1, MissionRules.UnlockedTierCount(4, thresholds));
            Assert.AreEqual(2, MissionRules.UnlockedTierCount(5, thresholds));
            Assert.AreEqual(3, MissionRules.UnlockedTierCount(8, thresholds));
            Assert.AreEqual(4, MissionRules.UnlockedTierCount(10, thresholds));
            Assert.AreEqual(4, MissionRules.UnlockedTierCount(11, thresholds));
        }

        /// <summary>
        /// 게이지는 <b>문턱 번째</b>로 나아간다. 요구 횟수로 비례시키면 3·5·7·10 처럼
        /// 간격이 고르지 않은 표에서 선물 상자(등간격)와 게이지가 어긋난다.
        /// </summary>
        [Test]
        public void TierGaugeAdvancesByThresholdIndexNotByCount()
        {
            List<int> thresholds = Thresholds();

            // 3개를 깨면 첫 칸에 정확히 닿는다 = 4칸 중 1칸.
            Assert.AreEqual(0.25f, MissionRules.TierGaugeProgress(3, thresholds), 0.0001f);

            // 5개면 두 칸째. 10개(마지막 문턱)면 가득 찬다.
            Assert.AreEqual(0.5f, MissionRules.TierGaugeProgress(5, thresholds), 0.0001f);
            Assert.AreEqual(1f, MissionRules.TierGaugeProgress(10, thresholds), 0.0001f);
            Assert.AreEqual(1f, MissionRules.TierGaugeProgress(99, thresholds), 0.0001f);
        }

        [Test]
        public void TierGaugeInterpolatesInsideSegment()
        {
            List<int> thresholds = Thresholds();

            // 0 → 3 구간의 중간(1.5 는 정수가 아니므로 2 로 본다): 2/3 만큼 첫 칸을 채운다.
            float at2 = MissionRules.TierGaugeProgress(2, thresholds);
            Assert.Greater(at2, 0f);
            Assert.Less(at2, 0.25f);

            // 3 → 5 구간의 중간(4): 첫 칸을 채우고 둘째 칸의 절반.
            Assert.AreEqual((1f + 0.5f) / 4f, MissionRules.TierGaugeProgress(4, thresholds), 0.0001f);
        }

        [Test]
        public void EmptyThresholdsAreHarmless()
        {
            Assert.AreEqual(0, MissionRules.UnlockedTierCount(5, null));
            Assert.AreEqual(0f, MissionRules.TierGaugeProgress(5, null), 0.0001f);
            Assert.AreEqual(0f, MissionRules.TierGaugeProgress(5, new List<int>()), 0.0001f);
        }
    }
}
