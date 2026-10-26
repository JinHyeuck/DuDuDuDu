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

        /// <summary>합의한 순서표(2026-10-27 개정 — 스테이지 보상 → 장비 → 다이스). 숫자를 바꾸려면 이 테스트와 Docs 를 같이 고친다.</summary>
        [TestCase(ContentId.Dice, 1, UnlockPresentation.Guide)]
        [TestCase(ContentId.StageReward, 1, UnlockPresentation.Guide)]
        [TestCase(ContentId.StageStar, 0, UnlockPresentation.Quiet)]
        [TestCase(ContentId.MeatFestival, 0, UnlockPresentation.Guide)]
        [TestCase(ContentId.Equipment, 1, UnlockPresentation.Guide)]
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

        /// <summary>1스테이지의 세 가이드가 열리는 조건. 스테이지 보상은 첫 판 종료, 장비는 5웨이브, 다이스는 10웨이브.</summary>
        [Test]
        public void StageOneGuidesOpenAtRunEndThenWave5ThenWave10()
        {
            Assert.IsTrue(Get(ContentId.StageReward).requiresRunEnded);
            Assert.AreEqual(0, Get(ContentId.StageReward).requiredWave);
            Assert.AreEqual(5, Get(ContentId.Equipment).requiredWave);
            Assert.AreEqual(10, Get(ContentId.Dice).requiredWave);

            // 표 순서가 곧 나오는 순서다 — 스테이지 보상, 장비, 다이스.
            int reward = Defaults.FindIndex(d => d.contentId == ContentId.StageReward);
            int equipment = Defaults.FindIndex(d => d.contentId == ContentId.Equipment);
            int dice = Defaults.FindIndex(d => d.contentId == ContentId.Dice);
            Assert.Less(reward, equipment);
            Assert.Less(equipment, dice);
        }

        /// <summary>튜토리얼만 마친 신규 유저: 고기 축제 가이드 하나뿐이다. 판을 마치기 전에는 아무것도 더 안 열린다.</summary>
        [Test]
        public void FreshPlayerGetsOnlyMeatFestival()
        {
            var seen = new HashSet<ContentId>();
            var first = ContentUnlockRules.VisitQueue(Defaults, 0, seen);
            CollectionAssert.AreEqual(new[] { ContentId.MeatFestival }, Ids(first));

            seen.Add(ContentId.MeatFestival);
            CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen));
        }

        /// <summary>첫 판이 끝나면 몇 웨이브에서 죽었든(0 이든) 스테이지 보상 가이드가 나온다.</summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4)]
        public void AnyDeathShowsStageRewardGuideFirst(int bestWave)
        {
            var seen = new HashSet<ContentId> { ContentId.MeatFestival };

            // 판을 마치기 전에는 웨이브 기록이 없으면 나오지 않는다.
            if (bestWave == 0)
                CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen, 0, runEnded: false));

            CollectionAssert.AreEqual(new[] { ContentId.StageReward }, Ids(ContentUnlockRules.VisitQueue(Defaults, 0, seen, bestWave, runEnded: true)));

            seen.Add(ContentId.StageReward);
            CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen, bestWave, runEnded: true));
        }

        /// <summary>판 종료 기록이 없는 옛 세이브라도 웨이브를 깬 기록이 있으면 판을 마친 것으로 본다.</summary>
        [Test]
        public void WaveRecordCountsAsFinishedRun()
        {
            var seen = new HashSet<ContentId> { ContentId.MeatFestival };
            CollectionAssert.AreEqual(new[] { ContentId.StageReward }, Ids(ContentUnlockRules.VisitQueue(Defaults, 0, seen, 2, runEnded: false)));
        }

        /// <summary>5웨이브를 넘으면 장비, 10웨이브를 넘으면 다이스 가이드(스테이지를 못 깼어도).</summary>
        [Test]
        public void StageOneWavesOpenEquipmentThenDice()
        {
            var seen = new HashSet<ContentId> { ContentId.MeatFestival, ContentId.StageReward };
            CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen, 4, runEnded: true));
            CollectionAssert.AreEqual(new[] { ContentId.Equipment }, Ids(ContentUnlockRules.VisitQueue(Defaults, 0, seen, 5, runEnded: true)));

            seen.Add(ContentId.Equipment);
            CollectionAssert.IsEmpty(ContentUnlockRules.VisitQueue(Defaults, 0, seen, 9, runEnded: true));
            CollectionAssert.AreEqual(new[] { ContentId.Dice }, Ids(ContentUnlockRules.VisitQueue(Defaults, 0, seen, 10, runEnded: true)));
        }

        /// <summary>
        /// <b>첫 판에 12웨이브에서 죽었으면 스테이지 보상 → 장비 → 다이스.</b> 다이스부터 나오면 안 된다(사용자 지시, 2026-10-27).
        /// 가이드는 하나씩 끊겨 나오고 디렉터가 이어서 건다.
        /// </summary>
        [Test]
        public void DeathAtWave12ShowsRewardThenEquipmentThenDice()
        {
            var seen = new HashSet<ContentId> { ContentId.MeatFestival };
            var order = new List<ContentId>();
            for (int guard = 0; guard < 10; guard++)
            {
                List<ContentUnlockDefinition> queue = ContentUnlockRules.VisitQueue(Defaults, 0, seen, 12, runEnded: true);
                if (queue.Count == 0)
                    break;

                foreach (ContentUnlockDefinition d in queue)
                {
                    order.Add(d.contentId);
                    seen.Add(d.contentId);
                }
            }

            CollectionAssert.AreEqual(new[] { ContentId.StageReward, ContentId.Equipment, ContentId.Dice }, order);
        }

        /// <summary>
        /// 어떤 웨이브에서 죽었든(0~30) 가이드 순서가 뒤집히지 않는다 — 나온 것 중 스테이지 보상 &lt; 장비 &lt; 다이스.
        /// "종종 순서가 바뀐다" 를 표와 규칙에서 못박는다.
        /// </summary>
        [Test]
        public void GuideOrderNeverInvertsForAnyBestWave()
        {
            ContentId[] chain = { ContentId.StageReward, ContentId.Equipment, ContentId.Dice };
            for (int wave = 0; wave <= 30; wave++)
            {
                foreach (bool runEnded in new[] { false, true })
                {
                    var seen = new HashSet<ContentId> { ContentId.MeatFestival };
                    var order = new List<ContentId>();
                    for (int guard = 0; guard < 20; guard++)
                    {
                        List<ContentUnlockDefinition> queue = ContentUnlockRules.VisitQueue(Defaults, 0, seen, wave, runEnded);
                        if (queue.Count == 0)
                            break;

                        foreach (ContentUnlockDefinition d in queue)
                        {
                            order.Add(d.contentId);
                            seen.Add(d.contentId);
                        }
                    }

                    string at = "웨이브 " + wave + " 판 종료 " + runEnded;
                    int lastPosition = -1;
                    foreach (ContentId id in chain)
                    {
                        int position = order.IndexOf(id);
                        if (position < 0)
                            continue;

                        Assert.Greater(position, lastPosition, at + ": " + id + " 가 앞 가이드보다 먼저 나왔다 (" + string.Join(",", order) + ")");
                        lastPosition = position;
                    }

                    // 뒤의 것이 나왔다면 앞의 것도 나왔어야 한다(앞이 안 열렸는데 뒤만 열리는 일이 없다).
                    if (order.Contains(ContentId.Dice))
                        Assert.IsTrue(order.Contains(ContentId.Equipment) && order.Contains(ContentId.StageReward), at + ": 다이스만 나왔다");
                    if (order.Contains(ContentId.Equipment))
                        Assert.IsTrue(order.Contains(ContentId.StageReward), at + ": 장비만 나왔다");
                }
            }
        }

        /// <summary>스테이지를 통째로 깨면 웨이브 기록이 없어도 열린다. 방치 보상 연출은 마지막에 나온다.</summary>
        [Test]
        public void FirstClearStillOpensEverythingAndShowsIdle()
        {
            var seen = new HashSet<ContentId> { ContentId.MeatFestival, ContentId.StageReward, ContentId.Dice, ContentId.Equipment };
            CollectionAssert.AreEqual(new[] { ContentId.IdleReward }, Ids(ContentUnlockRules.VisitQueue(Defaults, 1, seen)));

            CollectionAssert.AreEqual(new[] { ContentId.StageReward },
                Ids(ContentUnlockRules.VisitQueue(Defaults, 1, new HashSet<ContentId> { ContentId.MeatFestival })));
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
            var queue = ContentUnlockRules.VisitQueue(Defaults, 1, seen);
            CollectionAssert.AreEqual(new[] { ContentId.Equipment }, Ids(queue));
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
            table.Find(d => d.contentId == ContentId.Dice).requiredClear = 0;
            StringAssert.Contains("요구 웨이브가 있는데", string.Join("\n", ContentUnlockRules.Validate(table, 30)));
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
