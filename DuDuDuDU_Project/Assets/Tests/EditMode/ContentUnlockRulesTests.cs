using System.Collections.Generic;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 컨텐츠 해금 판정과 기본표를 잠근다. (Docs/ContentUnlock.md)
    ///
    /// <b>대기열이 가장 중요하다.</b> 한 로비 진입에 가이드가 둘 나오거나, 같은 클리어의 연출이
    /// 가이드 뒤로 밀려 엉뚱한 진입에 나오면 — 둘 다 눌러 보기 전에는 아무도 모른다.
    /// </summary>
    public sealed class ContentUnlockRulesTests
    {
        private static readonly List<ContentUnlockDefinition> Defaults = ContentUnlockRules.BuildDefaults();

        private static List<ContentId> Ids(List<ContentUnlockDefinition> queue)
        {
            var ids = new List<ContentId>();
            foreach (ContentUnlockDefinition d in queue)
                ids.Add(d.contentId);
            return ids;
        }

        private static ContentUnlockDefinition Get(ContentId id)
        {
            return Defaults.Find(d => d.contentId == id);
        }

        [Test]
        public void DefaultsAreValid()
        {
            CollectionAssert.IsEmpty(ContentUnlockRules.Validate(Defaults, 30));
        }

        /// <summary>합의한 순서표(2026-10-13). 숫자를 바꾸려면 이 테스트와 Docs 를 같이 고친다.</summary>
        [TestCase(ContentId.Dice, 0, UnlockPresentation.Guide)]
        [TestCase(ContentId.StageReward, 1, UnlockPresentation.Quiet)]
        [TestCase(ContentId.Equipment, 1, UnlockPresentation.Guide)]
        [TestCase(ContentId.StageStar, 2, UnlockPresentation.Quiet)]
        [TestCase(ContentId.IdleReward, 2, UnlockPresentation.Announce)]
        [TestCase(ContentId.MeatFestival, 3, UnlockPresentation.Announce)]
        [TestCase(ContentId.Sweep, 3, UnlockPresentation.Guide)]
        [TestCase(ContentId.SpecialSevenDays, 3, UnlockPresentation.Announce)]
        [TestCase(ContentId.Quest, 4, UnlockPresentation.Announce)]
        [TestCase(ContentId.Pinball, 4, UnlockPresentation.Guide)]
        [TestCase(ContentId.Membership, 5, UnlockPresentation.Quiet)]
        [TestCase(ContentId.Tower, 5, UnlockPresentation.Guide)]
        [TestCase(ContentId.Shop, 5, UnlockPresentation.Guide)]
        [TestCase(ContentId.SeasonPass, 5, UnlockPresentation.Announce)]
        [TestCase(ContentId.Relic, 7, UnlockPresentation.Guide)]
        public void DefaultsMatchAgreedOrder(ContentId id, int requiredClear, UnlockPresentation presentation)
        {
            ContentUnlockDefinition d = Get(id);
            Assert.IsNotNull(d, id + " 가 기본표에 없다.");
            Assert.AreEqual(requiredClear, d.requiredClear, id + " 요구 클리어");
            Assert.AreEqual(presentation, d.presentation, id + " 알리는 방법");
        }

        /// <summary>튜토리얼만 마친 신규 유저: 다이스 가이드 하나.</summary>
        [Test]
        public void FreshPlayerGetsDiceGuide()
        {
            var queue = ContentUnlockRules.VisitQueue(Defaults, 0, new HashSet<ContentId>());
            CollectionAssert.AreEqual(new[] { ContentId.Dice }, Ids(queue));
        }

        /// <summary>3클리어: 고기 연출 → 소탕 가이드에서 끊고, 7일은 다음 진입으로.</summary>
        [Test]
        public void ThirdClearDefersSevenDaysPastSweepGuide()
        {
            var seen = new HashSet<ContentId> { ContentId.Dice, ContentId.Equipment, ContentId.IdleReward };

            var first = ContentUnlockRules.VisitQueue(Defaults, 3, seen);
            CollectionAssert.AreEqual(new[] { ContentId.MeatFestival, ContentId.Sweep }, Ids(first));

            seen.Add(ContentId.MeatFestival);
            seen.Add(ContentId.Sweep);
            var second = ContentUnlockRules.VisitQueue(Defaults, 3, seen);
            CollectionAssert.AreEqual(new[] { ContentId.SpecialSevenDays }, Ids(second));
        }

        /// <summary>4클리어: 퀘스트 연출과 핀볼 가이드가 한 진입에 나온다.</summary>
        [Test]
        public void FourthClearShowsQuestThenPinball()
        {
            var seen = new HashSet<ContentId>
            {
                ContentId.Dice, ContentId.Equipment, ContentId.IdleReward,
                ContentId.MeatFestival, ContentId.Sweep, ContentId.SpecialSevenDays,
            };

            var queue = ContentUnlockRules.VisitQueue(Defaults, 4, seen);
            CollectionAssert.AreEqual(new[] { ContentId.Quest, ContentId.Pinball }, Ids(queue));
        }

        /// <summary>가이드를 끝까지 안 보고 나가면 다음 진입에 그대로 다시 나온다.</summary>
        [Test]
        public void UnfinishedGuideRepeats()
        {
            var seen = new HashSet<ContentId> { ContentId.Dice };
            var queue = ContentUnlockRules.VisitQueue(Defaults, 1, seen);
            CollectionAssert.AreEqual(new[] { ContentId.Equipment }, Ids(queue));
        }

        [Test]
        public void QuietIsVisibleAsSoonAsOpen()
        {
            var none = new HashSet<ContentId>();
            Assert.IsFalse(ContentUnlockRules.IsVisible(Get(ContentId.StageReward), 0, none));
            Assert.IsTrue(ContentUnlockRules.IsVisible(Get(ContentId.StageReward), 1, none));
        }

        /// <summary>열렸어도 연출 전에는 숨는다 — 연출이 입구를 튀어나오게 한다.</summary>
        [Test]
        public void AnnouncedContentHidesUntilShown()
        {
            ContentUnlockDefinition quest = Get(ContentId.Quest);
            Assert.IsFalse(ContentUnlockRules.IsVisible(quest, 4, new HashSet<ContentId>()));
            Assert.IsTrue(ContentUnlockRules.IsVisible(quest, 4, new HashSet<ContentId> { ContentId.Quest }));
            Assert.IsFalse(ContentUnlockRules.IsVisible(quest, 3, new HashSet<ContentId> { ContentId.Quest }));
        }

        [TestCase(false, 0, false)]
        [TestCase(false, 1, true)]
        [TestCase(true, 5, false)]
        public void BackfillOnlyForOldSavesWithProgress(bool initialized, int lastCleared, bool expected)
        {
            Assert.AreEqual(expected, ContentUnlockRules.ShouldBackfill(initialized, lastCleared));
        }

        [Test]
        public void LastClearedFromSaveIgnoresUnclearedRecords()
        {
            var state = new SaveState();
            state.Stage.Records["3"] = new StageRecordSave { BestClearGrade = 1 };
            state.Stage.Records["4"] = new StageRecordSave { BestClearGrade = 0, BestClearedWave = 5 };
            state.Stage.Records["x"] = new StageRecordSave { BestClearGrade = 2 };

            Assert.AreEqual(3, ContentUnlockRules.LastClearedFromSave(state));
        }

        [Test]
        public void ValidateCatchesMissingDuplicateAndOrder()
        {
            var table = ContentUnlockRules.BuildDefaults();
            table.RemoveAll(d => d.contentId == ContentId.Tower);
            table.Add(new ContentUnlockDefinition(ContentId.Dice, 0, UnlockPresentation.Quiet, "다이스", ""));
            table.Add(new ContentUnlockDefinition(ContentId.Relic, 40, UnlockPresentation.Guide, "", ""));

            string all = string.Join("\n", ContentUnlockRules.Validate(table, 30));
            StringAssert.Contains("Tower: 표에 없다", all);
            StringAssert.Contains("중복", all);
            StringAssert.Contains("앞 줄", all);
            StringAssert.Contains("스테이지 수 30", all);
            StringAssert.Contains("이름이 비었다", all);
            StringAssert.Contains("guideTapText", all);
        }
    }
}
