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

        /// <summary>스테이지별 최고 웨이브 조회. <c>W(5)</c> 는 1스테이지 5웨이브, <c>W(0, 10)</c> 은 2스테이지 10웨이브.</summary>
        private static System.Func<int, int> W(int stage1, int stage2 = 0)
        {
            return stage => stage == 1 ? stage1 : stage == 2 ? stage2 : 0;
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

        /// <summary>합의한 순서표(2026-10-29 개정 — 다이스는 튜토리얼 직후, 스테이지 보상은 첫 판 종료, 고기 축제는 1스테이지 클리어, 장비는 2-10). 숫자를 바꾸려면 이 테스트와 Docs 를 같이 고친다.</summary>
        [TestCase(ContentId.Dice, 0, UnlockPresentation.Guide)]
        [TestCase(ContentId.StageReward, 1, UnlockPresentation.Guide)]
        [TestCase(ContentId.StageStar, 0, UnlockPresentation.Quiet)]
        [TestCase(ContentId.MeatFestival, 1, UnlockPresentation.Guide)]
        [TestCase(ContentId.Equipment, 2, UnlockPresentation.Guide)]
        [TestCase(ContentId.IdleReward, 1, UnlockPresentation.Announce)]
        [TestCase(ContentId.Pinball, 2, UnlockPresentation.Guide)]
        [TestCase(ContentId.Tower, 3, UnlockPresentation.Guide)]
        [TestCase(ContentId.Quest, 3, UnlockPresentation.Announce)]
        [TestCase(ContentId.Sweep, 4, UnlockPresentation.Guide)]
        [TestCase(ContentId.Membership, 4, UnlockPresentation.Quiet)]
        [TestCase(ContentId.SpecialSevenDays, 4, UnlockPresentation.Quiet)]
        [TestCase(ContentId.Shop, 5, UnlockPresentation.Guide)]
        [TestCase(ContentId.SeasonPass, 5, UnlockPresentation.Quiet)]
        [TestCase(ContentId.Relic, 6, UnlockPresentation.Guide)]
        public void DefaultsMatchAgreedOrder(ContentId id, int requiredClear, UnlockPresentation presentation)
        {
            ContentUnlockDefinition d = Get(id);
            Assert.IsNotNull(d, id + " 가 기본표에 없다.");
            Assert.AreEqual(requiredClear, d.requiredClear, id + " 요구 클리어");
            Assert.AreEqual(presentation, d.presentation, id + " 알리는 방법");
        }

        /// <summary>가이드가 열리는 조건. 다이스는 처음부터, 스테이지 보상은 첫 판 종료, 고기 축제는 1스테이지 클리어, 장비는 2-10.</summary>
        [Test]
        public void GuideOpeningConditions()
        {
            Assert.AreEqual(0, Get(ContentId.Dice).requiredWave);
            Assert.IsTrue(Get(ContentId.StageReward).requiresRunEnded);
            Assert.AreEqual(0, Get(ContentId.StageReward).requiredWave);
            Assert.AreEqual(0, Get(ContentId.MeatFestival).requiredWave);
            Assert.IsFalse(Get(ContentId.MeatFestival).requiresRunEnded);
            Assert.AreEqual(10, Get(ContentId.Equipment).requiredWave);
            Assert.AreEqual(2, Get(ContentId.Equipment).requiredWaveStage);

            // 표 순서가 곧 나오는 순서다 — 다이스, 스테이지 보상, 방치 보상(연출), 고기 축제, 장비.
            int dice = Defaults.FindIndex(d => d.contentId == ContentId.Dice);
            int reward = Defaults.FindIndex(d => d.contentId == ContentId.StageReward);
            int idle = Defaults.FindIndex(d => d.contentId == ContentId.IdleReward);
            int meat = Defaults.FindIndex(d => d.contentId == ContentId.MeatFestival);
            int equipment = Defaults.FindIndex(d => d.contentId == ContentId.Equipment);
            Assert.Less(dice, reward);
            Assert.Less(reward, idle);
            Assert.Less(idle, meat);
            Assert.Less(meat, equipment);
        }

        /// <summary>튜토리얼만 마친 신규 유저: 다이스 가이드 하나뿐이다. 판을 마치기 전에는 아무것도 더 안 열린다.</summary>
        [Test]
        public void FreshPlayerGetsOnlyDice()
        {
            var seen = new HashSet<ContentId>();
            var first = ContentUnlockRules.VisitQueue(Defaults, 0, seen);
            CollectionAssert.AreEqual(new[] { ContentId.Dice }, Ids(first));

            seen.Add(ContentId.Dice);
            CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen));
        }

        /// <summary>첫 판이 끝나면 몇 웨이브에서 죽었든(0 이든) 스테이지 보상 가이드가 나온다.</summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4)]
        public void AnyDeathShowsStageRewardGuide(int bestWave)
        {
            var seen = new HashSet<ContentId> { ContentId.Dice };

            // 판을 마치기 전에는 웨이브 기록이 없으면 나오지 않는다.
            if (bestWave == 0)
                CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen, W(0), runEnded: false));

            CollectionAssert.AreEqual(new[] { ContentId.StageReward }, Ids(ContentUnlockRules.VisitQueue(Defaults, 0, seen, W(bestWave), runEnded: true)));

            seen.Add(ContentId.StageReward);
            CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen, W(bestWave), runEnded: true));
        }

        /// <summary>판 종료 기록이 없는 옛 세이브라도 웨이브를 깬 기록이 있으면 판을 마친 것으로 본다.</summary>
        [Test]
        public void WaveRecordCountsAsFinishedRun()
        {
            var seen = new HashSet<ContentId> { ContentId.Dice };
            CollectionAssert.AreEqual(new[] { ContentId.StageReward }, Ids(ContentUnlockRules.VisitQueue(Defaults, 0, seen, W(2), runEnded: false)));
        }

        /// <summary>
        /// <b>1스테이지를 클리어하기 전에는 몇 웨이브를 깼든 스테이지 보상 말고는 아무것도 안 열린다</b> —
        /// 고기 축제도, 장비도(2-10). 웨이브로 열리는 것은 2-10 장비 하나뿐이다.
        /// </summary>
        [Test]
        public void Stage1WavesOpenNothingBeyondStageReward()
        {
            for (int wave = 0; wave <= 30; wave++)
            {
                var seen = new HashSet<ContentId> { ContentId.Dice, ContentId.StageReward };
                CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen, W(wave), runEnded: true), "웨이브 " + wave);
            }
        }

        /// <summary>장비는 2스테이지 10웨이브(2-10)에서 열린다. 1스테이지를 깼어도 2-9 까지는 닫혀 있다.</summary>
        [Test]
        public void EquipmentOpensAtStage2Wave10()
        {
            var seen = new HashSet<ContentId>
            {
                ContentId.Dice, ContentId.StageReward, ContentId.IdleReward, ContentId.MeatFestival,
            };

            CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 1, seen, W(30, 9), runEnded: true));
            CollectionAssert.AreEqual(new[] { ContentId.Equipment },
                Ids(ContentUnlockRules.VisitQueue(Defaults, 1, seen, W(30, 10), runEnded: true)));

            // 2스테이지를 통째로 깨도 열린다(웨이브 기록이 없어도).
            Assert.IsTrue(ContentUnlockRules.IsOpen(Get(ContentId.Equipment), 2, W(0, 0), true));
            Assert.IsFalse(ContentUnlockRules.IsOpen(Get(ContentId.Equipment), 1, W(0, 0), true));
        }

        /// <summary>1스테이지를 깨면 방치 보상 연출 → 고기 축제 가이드 순서로 나온다(연출이 가이드 앞).</summary>
        [Test]
        public void FirstClearShowsIdleThenMeatFestival()
        {
            var seen = new HashSet<ContentId> { ContentId.Dice, ContentId.StageReward };
            CollectionAssert.AreEqual(new[] { ContentId.IdleReward, ContentId.MeatFestival },
                Ids(ContentUnlockRules.VisitQueue(Defaults, 1, seen)));

            CollectionAssert.AreEqual(new[] { ContentId.StageReward },
                Ids(ContentUnlockRules.VisitQueue(Defaults, 1, new HashSet<ContentId> { ContentId.Dice })));
        }

        /// <summary>3클리어: 퀘스트 연출 → 탑 가이드. 탑 가이드가 봉인 연출로 이어지는 이야기다.</summary>
        [Test]
        public void ThirdClearShowsQuestThenTowerGuide()
        {
            var seen = new HashSet<ContentId>
            {
                ContentId.Dice, ContentId.MeatFestival, ContentId.IdleReward, ContentId.Equipment, ContentId.Pinball, ContentId.StageReward,
            };

            var queue = ContentUnlockRules.VisitQueue(Defaults, 3, seen);
            CollectionAssert.AreEqual(new[] { ContentId.Quest, ContentId.Tower }, Ids(queue));
        }

        /// <summary>4클리어: 소탕 가이드 하나(멤버십·특별한 7일은 조용히 나타난다).</summary>
        [Test]
        public void FourthClearShowsSweepOnly()
        {
            var seen = new HashSet<ContentId>
            {
                ContentId.Dice, ContentId.MeatFestival, ContentId.IdleReward, ContentId.Equipment, ContentId.StageReward,
                ContentId.Pinball, ContentId.Quest, ContentId.Tower,
            };

            var queue = ContentUnlockRules.VisitQueue(Defaults, 4, seen);
            CollectionAssert.AreEqual(new[] { ContentId.Sweep }, Ids(queue));
        }

        /// <summary>가이드를 끝까지 안 보고 나가면 다음 진입에 그대로 다시 나온다.</summary>
        [Test]
        public void UnfinishedGuideRepeats()
        {
            var seen = new HashSet<ContentId> { ContentId.Dice, ContentId.MeatFestival, ContentId.IdleReward, ContentId.StageReward };
            var queue = ContentUnlockRules.VisitQueue(Defaults, 2, seen);
            CollectionAssert.AreEqual(new[] { ContentId.Equipment }, Ids(queue));
        }

        /// <summary>첫 판을 지고 나온 뒤 시너지를 한 번도 안 굴렸을 때만 입장 버튼을 가린다.</summary>
        [TestCase(false, true, 0, false)]
        [TestCase(true, true, 0, true)]
        [TestCase(true, true, 1, false)]
        [TestCase(true, false, 0, false)]
        public void StageEntryHeldOnlyUntilFirstSynergyReroll(bool runEnded, bool open, int rerolls, bool expected)
        {
            Assert.AreEqual(expected, ContentUnlockRules.IsStageEntryHeldForSynergyGuide(runEnded, open, rerolls));
        }

        /// <summary>시작 고기는 입장료로 여러 판을 할 만큼이다.</summary>
        [Test]
        public void StarterMeatCoversSeveralEntries()
        {
            Assert.AreEqual(30, ContentUnlockRules.StarterMeat);
        }

        [Test]
        public void QuietIsVisibleAsSoonAsOpen()
        {
            var none = new HashSet<ContentId>();
            Assert.IsTrue(ContentUnlockRules.IsVisible(Get(ContentId.StageStar), 0, none));
            Assert.IsFalse(ContentUnlockRules.IsVisible(Get(ContentId.Membership), 3, none));
            Assert.IsTrue(ContentUnlockRules.IsVisible(Get(ContentId.Membership), 4, none));
        }

        /// <summary>열렸어도 연출 전에는 숨는다 — 연출이 입구를 튀어나오게 한다.</summary>
        [Test]
        public void AnnouncedContentHidesUntilShown()
        {
            ContentUnlockDefinition quest = Get(ContentId.Quest);
            Assert.IsFalse(ContentUnlockRules.IsVisible(quest, 3, new HashSet<ContentId>()));
            Assert.IsTrue(ContentUnlockRules.IsVisible(quest, 3, new HashSet<ContentId> { ContentId.Quest }));
            Assert.IsFalse(ContentUnlockRules.IsVisible(quest, 2, new HashSet<ContentId> { ContentId.Quest }));
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
        public void WaveConditionNeedsAStageFallback()
        {
            var table = ContentUnlockRules.BuildDefaults();
            table.Find(d => d.contentId == ContentId.Equipment).requiredClear = 0;
            StringAssert.Contains("요구 웨이브가 있는데", string.Join("\n", ContentUnlockRules.Validate(table, 30)));
        }

        /// <summary>웨이브를 세는 스테이지가 요구 클리어보다 크면, 그 스테이지를 깨기 전에 열려 웨이브 조건이 뜻이 없다.</summary>
        [Test]
        public void WaveStageCannotExceedRequiredClear()
        {
            var table = ContentUnlockRules.BuildDefaults();
            table.Find(d => d.contentId == ContentId.Equipment).requiredWaveStage = 3;
            StringAssert.Contains("요구 웨이브의 스테이지", string.Join("\n", ContentUnlockRules.Validate(table, 30)));
        }

        [Test]
        public void RunEndedConditionNeedsAStageFallback()
        {
            var table = ContentUnlockRules.BuildDefaults();
            table.Find(d => d.contentId == ContentId.StageReward).requiredClear = 0;
            StringAssert.Contains("판 종료 조건이", string.Join("\n", ContentUnlockRules.Validate(table, 30)));
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
