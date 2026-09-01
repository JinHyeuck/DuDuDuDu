using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 저장 파이프라인 잠금. (MIGRATION_BASELINE 7단계 게이트)
    ///
    /// 게이트가 "저장·로드 왕복 후 모든 값이 동일"이라 <b>왕복이 여기서 실제로 돌아야 한다.</b>
    /// <c>JsonUtility</c> 를 쓰지 않고 Newtonsoft 로 간 이유가 이것이다 — 엔진 API 였다면
    /// 이 파일 전체가 에디터 없이는 돌지 못한다.
    ///
    /// 파일 테스트는 진짜 디스크를 쓴다. 임시 폴더에 쓰고 매번 지운다.
    /// <c>File.Replace</c> 의 원자성과 <c>.bak</c> 생성은 실제 파일 시스템에서만 확인된다.
    /// </summary>
    [TestFixture]
    public sealed class SaveStateTests
    {
        private string directory;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "oj-save-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            savePath = Path.Combine(directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch (IOException)
            {
                // 임시 폴더 정리 실패로 테스트를 떨어뜨리지 않는다.
            }
        }

        /// <summary>모든 필드에 서로 다른 값이 들어간 상태. 기본값과 구별되어야 의미가 있다.</summary>
        private static SaveState MakePopulated()
        {
            var state = new SaveState();

            state.Points["Gold"] = 12345;
            state.Points["Meat"] = 30;
            state.Points["FireScroll"] = 7;

            state.DiceLevels["Fire"] = 12;
            state.DiceLevels["Water"] = 1;
            state.DiceLevels["Thunder"] = 45;

            state.Relics.SummonCount = 987;
            state.Relics.Levels["QuickHands"] = 3;
            state.Relics.Levels["LastWall"] = 1;

            state.Equipment.Levels["Weapon"] = 20;
            state.Equipment.Levels["Helmet"] = 5;
            state.Equipment.GemSlots["Weapon"] = new List<string> { "gem_atk_1", "", "gem_crit_2" };
            state.Equipment.GemSlots["Helmet"] = new List<string> { "", "" };
            state.Equipment.GemInventory["gem_atk_1"] = 4;
            state.Equipment.GemInventory["gem_crit_2"] = 1;

            state.Stage.SelectedIndex = 6;
            state.Stage.HighestUnlockedIndex = 9;
            state.Stage.Records["1"] = new StageRecordSave
            {
                ClaimedRewardFlags = 7,
                BestClearGrade = 3,
                BestClearedWave = 30,
            };
            state.Stage.Records["12"] = new StageRecordSave
            {
                ClaimedRewardFlags = 1,
                BestClearGrade = 1,
                BestClearedWave = 4,
            };
            state.Stage.ClaimedRewardIds.Add("stage_1_gold");
            state.Stage.ClaimedRewardIds.Add("stage_2_meat");
            state.Stage.ClaimedStarRewardIndices.Add(0);
            state.Stage.ClaimedStarRewardIndices.Add(3);

            state.Idle.AutoBattleStartUtcTicks = 638_600_000_000_000_000L;
            state.Idle.MeatFestivalStartUtcTicks = 638_600_000_000_000_001L;

            return state;
        }

        private static void AssertSame(SaveState expected, SaveState actual)
        {
            Assert.That(actual.Version, Is.EqualTo(expected.Version), "version");
            CollectionAssert.AreEqual(expected.Points, actual.Points, "points");
            CollectionAssert.AreEqual(expected.DiceLevels, actual.DiceLevels, "diceLevels");

            Assert.That(actual.Relics.SummonCount, Is.EqualTo(expected.Relics.SummonCount), "relics.summonCount");
            CollectionAssert.AreEqual(expected.Relics.Levels, actual.Relics.Levels, "relics.levels");

            CollectionAssert.AreEqual(expected.Equipment.Levels, actual.Equipment.Levels, "equipment.levels");
            CollectionAssert.AreEqual(expected.Equipment.GemInventory, actual.Equipment.GemInventory, "equipment.gemInventory");
            CollectionAssert.AreEqual(
                new List<string>(expected.Equipment.GemSlots.Keys),
                new List<string>(actual.Equipment.GemSlots.Keys),
                "equipment.gemSlots keys");
            foreach (KeyValuePair<string, List<string>> pair in expected.Equipment.GemSlots)
                CollectionAssert.AreEqual(pair.Value, actual.Equipment.GemSlots[pair.Key], "equipment.gemSlots[" + pair.Key + "]");

            Assert.That(actual.Stage.SelectedIndex, Is.EqualTo(expected.Stage.SelectedIndex), "stage.selectedIndex");
            Assert.That(actual.Stage.HighestUnlockedIndex, Is.EqualTo(expected.Stage.HighestUnlockedIndex), "stage.highestUnlockedIndex");
            CollectionAssert.AreEqual(expected.Stage.ClaimedRewardIds, actual.Stage.ClaimedRewardIds, "stage.claimedRewardIds");
            CollectionAssert.AreEqual(expected.Stage.ClaimedStarRewardIndices, actual.Stage.ClaimedStarRewardIndices, "stage.claimedStarRewardIndices");
            CollectionAssert.AreEqual(
                new List<string>(expected.Stage.Records.Keys),
                new List<string>(actual.Stage.Records.Keys),
                "stage.records keys");
            foreach (KeyValuePair<string, StageRecordSave> pair in expected.Stage.Records)
            {
                StageRecordSave a = actual.Stage.Records[pair.Key];
                Assert.That(a.ClaimedRewardFlags, Is.EqualTo(pair.Value.ClaimedRewardFlags), "records[" + pair.Key + "].claimedRewardFlags");
                Assert.That(a.BestClearGrade, Is.EqualTo(pair.Value.BestClearGrade), "records[" + pair.Key + "].bestClearGrade");
                Assert.That(a.BestClearedWave, Is.EqualTo(pair.Value.BestClearedWave), "records[" + pair.Key + "].bestClearedWave");
            }

            Assert.That(actual.Idle.AutoBattleStartUtcTicks, Is.EqualTo(expected.Idle.AutoBattleStartUtcTicks), "idle.autoBattleStartUtcTicks");
            Assert.That(actual.Idle.MeatFestivalStartUtcTicks, Is.EqualTo(expected.Idle.MeatFestivalStartUtcTicks), "idle.meatFestivalStartUtcTicks");
        }

        // --- 직렬화 왕복 ------------------------------------------------------------------

        [Test]
        public void 채워진_상태가_왕복해도_모든_값이_같다()
        {
            SaveState original = MakePopulated();

            SaveState restored = SaveSerializer.Deserialize(SaveSerializer.Serialize(original));

            AssertSame(original, restored);
        }

        [Test]
        public void 빈_상태도_왕복한다()
        {
            SaveState original = new SaveState();

            SaveState restored = SaveSerializer.Deserialize(SaveSerializer.Serialize(original));

            AssertSame(original, restored);
        }

        /// <summary>
        /// 저장→로드→저장이 바이트까지 같아야 한다. 다르면 왕복 어딘가에서 값이 바뀌었거나
        /// 순서가 흔들린다는 뜻이고, 세이브 파일을 diff 로 볼 수 없게 된다.
        /// </summary>
        [Test]
        public void 두_번_직렬화해도_바이트가_같다()
        {
            string once = SaveSerializer.Serialize(MakePopulated());

            string twice = SaveSerializer.Serialize(SaveSerializer.Deserialize(once));

            Assert.That(twice, Is.EqualTo(once));
        }

        /// <summary>
        /// 넣은 순서와 무관하게 출력이 같아야 한다. <see cref="SortedDictionary{TKey,TValue}"/>
        /// 를 고른 이유가 이것이다.
        /// </summary>
        [Test]
        public void 넣은_순서가_달라도_출력이_같다()
        {
            var a = new SaveState();
            a.Points["Gold"] = 1;
            a.Points["Meat"] = 2;
            a.Points["Zinc"] = 3;

            var b = new SaveState();
            b.Points["Zinc"] = 3;
            b.Points["Gold"] = 1;
            b.Points["Meat"] = 2;

            Assert.That(SaveSerializer.Serialize(b), Is.EqualTo(SaveSerializer.Serialize(a)));
        }

        /// <summary>
        /// 키 비교가 Ordinal 이어야 한다. 대소문자를 무시하면 enum 이름 두 개가 한 칸으로
        /// 합쳐져 값 하나가 조용히 사라진다.
        /// </summary>
        [Test]
        public void 대소문자가_다른_키는_다른_키다()
        {
            var state = new SaveState();
            state.Points["gold"] = 1;
            state.Points["Gold"] = 2;

            SaveState restored = SaveSerializer.Deserialize(SaveSerializer.Serialize(state));

            Assert.That(restored.Points.Count, Is.EqualTo(2));
            Assert.That(restored.Points["gold"], Is.EqualTo(1));
            Assert.That(restored.Points["Gold"], Is.EqualTo(2));
        }

        /// <summary>
        /// 컬렉션은 get-only 라 역직렬화 뒤에도 null 이 될 수 없다. 기존 매니저들이 손으로
        /// 넣던 null 방어를 대신하는 성질이라 잠가 둔다.
        /// </summary>
        [Test]
        public void 빈_JSON_을_읽어도_컬렉션이_null_이_아니다()
        {
            SaveState state = SaveSerializer.Deserialize("{}");

            Assert.That(state.Points, Is.Not.Null);
            Assert.That(state.DiceLevels, Is.Not.Null);
            Assert.That(state.Relics, Is.Not.Null);
            Assert.That(state.Relics.Levels, Is.Not.Null);
            Assert.That(state.Equipment, Is.Not.Null);
            Assert.That(state.Equipment.Levels, Is.Not.Null);
            Assert.That(state.Equipment.GemSlots, Is.Not.Null);
            Assert.That(state.Equipment.GemInventory, Is.Not.Null);
            Assert.That(state.Stage, Is.Not.Null);
            Assert.That(state.Stage.Records, Is.Not.Null);
            Assert.That(state.Stage.ClaimedRewardIds, Is.Not.Null);
            Assert.That(state.Stage.ClaimedStarRewardIndices, Is.Not.Null);
            Assert.That(state.Idle, Is.Not.Null);
        }

        /// <summary>
        /// 빈 슬롯은 위치를 지켜야 한다. 하나 빠지면 뒤가 당겨져 다른 슬롯에 낀 보석이 된다.
        /// </summary>
        [Test]
        public void 보석_빈_슬롯이_자리를_지킨다()
        {
            var state = new SaveState();
            state.Equipment.GemSlots["Weapon"] = new List<string> { "", "gem_b", "" };

            SaveState restored = SaveSerializer.Deserialize(SaveSerializer.Serialize(state));

            CollectionAssert.AreEqual(
                new List<string> { "", "gem_b", "" },
                restored.Equipment.GemSlots["Weapon"]);
        }

        /// <summary>
        /// Newtonsoft 기본값은 날짜처럼 생긴 문자열을 DateTime 으로 바꾼다. 보상 id 가
        /// 그렇게 생겼으면 왕복이 깨진다. DateParseHandling.None 을 잠근다.
        /// </summary>
        [Test]
        public void 날짜처럼_생긴_id_도_문자열_그대로_왕복한다()
        {
            var state = new SaveState();
            state.Stage.ClaimedRewardIds.Add("2024-01-15T00:00:00Z");
            state.Equipment.GemInventory["2024-01-15"] = 2;

            SaveState restored = SaveSerializer.Deserialize(SaveSerializer.Serialize(state));

            Assert.That(restored.Stage.ClaimedRewardIds[0], Is.EqualTo("2024-01-15T00:00:00Z"));
            Assert.That(restored.Equipment.GemInventory["2024-01-15"], Is.EqualTo(2));
        }

        /// <summary>tick 은 int 범위를 넘는다. long 으로 살아남아야 한다.</summary>
        [Test]
        public void 큰_tick_값이_잘리지_않는다()
        {
            var state = new SaveState();
            state.Idle.AutoBattleStartUtcTicks = long.MaxValue;
            state.Idle.MeatFestivalStartUtcTicks = DateTime.MaxValue.Ticks;

            SaveState restored = SaveSerializer.Deserialize(SaveSerializer.Serialize(state));

            Assert.That(restored.Idle.AutoBattleStartUtcTicks, Is.EqualTo(long.MaxValue));
            Assert.That(restored.Idle.MeatFestivalStartUtcTicks, Is.EqualTo(DateTime.MaxValue.Ticks));
        }

        /// <summary>새 빌드에서 지운 필드가 옛 세이브에 남아 있어도 읽혀야 한다.</summary>
        [Test]
        public void 모르는_키는_무시한다()
        {
            SaveState state = SaveSerializer.Deserialize("{\"version\":1,\"이건없는필드\":123}");

            Assert.That(state.Version, Is.EqualTo(1));
        }

        [Test]
        public void 버전이_JSON_에_나간다()
        {
            string json = SaveSerializer.Serialize(new SaveState());

            Assert.That(json, Does.Contain("\"version\""));
        }

        /// <summary>
        /// 파일 형식 자체를 못 박는다.
        ///
        /// 위의 왕복 테스트들은 "쓴 것을 내가 다시 읽을 수 있다"만 본다. 그래서 직렬화 설정을
        /// 통째로 바꿔도 (예: 이름 규칙, 들여쓰기, 키 순서) 전부 통과한다 — 그런데 그건
        /// <b>이미 배포된 세이브를 못 읽게 만드는 변경</b>이다. 실제 바이트를 여기 적어 둔다.
        /// 이 테스트가 깨지면 형식이 바뀐 것이고, 그때는 버전을 올려야 하는지 판단해야 한다.
        /// </summary>
        [Test]
        public void 파일_형식이_고정돼_있다()
        {
            var state = new SaveState();
            state.Points["Gold"] = 10;
            state.Relics.Levels["QuickHands"] = 2;
            state.Equipment.GemSlots["Weapon"] = new List<string> { "gem_a", "" };
            state.Stage.Records["3"] = new StageRecordSave { BestClearGrade = 1 };
            state.Stage.ClaimedStarRewardIndices.Add(4);
            state.Idle.AutoBattleStartUtcTicks = 123L;

            string json = SaveSerializer.Serialize(state).Replace("\r\n", "\n");

            Assert.That(json, Is.EqualTo(string.Join("\n", new[]
            {
                "{",
                "  \"version\": 1,",
                "  \"points\": {",
                "    \"Gold\": 10",
                "  },",
                "  \"diceLevels\": {},",
                "  \"relics\": {",
                "    \"summonCount\": 0,",
                "    \"levels\": {",
                "      \"QuickHands\": 2",
                "    }",
                "  },",
                "  \"equipment\": {",
                "    \"levels\": {},",
                "    \"gemSlots\": {",
                "      \"Weapon\": [",
                "        \"gem_a\",",
                "        \"\"",
                "      ]",
                "    },",
                "    \"gemInventory\": {}",
                "  },",
                "  \"stage\": {",
                "    \"selectedIndex\": 1,",
                "    \"highestUnlockedIndex\": 1,",
                "    \"records\": {",
                "      \"3\": {",
                "        \"claimedRewardFlags\": 0,",
                "        \"bestClearGrade\": 1,",
                "        \"bestClearedWave\": 0",
                "      }",
                "    },",
                "    \"claimedRewardIds\": [],",
                "    \"claimedStarRewardIndices\": [",
                "      4",
                "    ]",
                "  },",
                "  \"idle\": {",
                "    \"autoBattleStartUtcTicks\": 123,",
                "    \"meatFestivalStartUtcTicks\": 0",
                "  }",
                "}",
            })));
        }

        // --- 못 읽는 입력은 예외로 드러난다 ---------------------------------------------

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("null")]
        [TestCase("{ 잘린")]
        [TestCase("[1,2,3]")]
        [TestCase("\"문자열\"")]
        public void 못_읽는_입력은_예외를_던진다(string json)
        {
            Assert.Throws<SaveFormatException>(() => SaveSerializer.Deserialize(json));
        }

        /// <summary>
        /// 타입이 안 맞는 값은 조용히 기본값이 되면 안 된다. 그게 진행도가 사라지는 경로다.
        /// </summary>
        [Test]
        public void 타입이_틀리면_예외를_던진다()
        {
            Assert.Throws<SaveFormatException>(
                () => SaveSerializer.Deserialize("{\"version\":\"하나\"}"));
        }

        // --- 버전 정책 --------------------------------------------------------------------

        [Test]
        public void 현재_버전은_통과한다()
        {
            var state = new SaveState { Version = SaveState.CurrentVersion };

            string error;
            Assert.That(SaveStateMigration.TryUpgrade(state, out error), Is.True, error);
            Assert.That(state.Version, Is.EqualTo(SaveState.CurrentVersion));
        }

        /// <summary>
        /// 앱을 롤백한 유저의 세이브. 읽어서 그대로 저장하면 새 빌드가 쓴 필드가 전부 사라진다.
        /// </summary>
        [Test]
        public void 미래_버전은_거부한다()
        {
            var state = new SaveState { Version = SaveState.CurrentVersion + 1 };

            string error;
            Assert.That(SaveStateMigration.TryUpgrade(state, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void 버전이_1_미만이면_거부한다(int version)
        {
            var state = new SaveState { Version = version };

            string error;
            Assert.That(SaveStateMigration.TryUpgrade(state, out error), Is.False);
        }

        // --- 파일 왕복 --------------------------------------------------------------------

        [Test]
        public void 파일에_저장하고_읽으면_값이_같다()
        {
            SaveState original = MakePopulated();

            SaveFile.Save(savePath, original);
            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.Primary));
            Assert.That(result.IsUsable, Is.True);
            AssertSame(original, result.State);
        }

        [Test]
        public void 세이브가_없으면_None_이다()
        {
            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.None));
            Assert.That(result.State, Is.Null);
            Assert.That(result.IsUsable, Is.False);
        }

        [Test]
        public void 없는_폴더에도_저장된다()
        {
            string nested = Path.Combine(directory, "a", "b", "save.json");

            SaveFile.Save(nested, MakePopulated());

            Assert.That(File.Exists(nested), Is.True);
        }

        /// <summary>쓰기가 끝나면 <c>.writing</c> 이 남아 있으면 안 된다.</summary>
        [Test]
        public void 저장_뒤_writing_찌꺼기가_없다()
        {
            SaveFile.Save(savePath, MakePopulated());
            SaveFile.Save(savePath, MakePopulated());

            Assert.That(File.Exists(SaveFile.WritingPathOf(savePath)), Is.False);
        }

        /// <summary>중단된 쓰기가 남긴 <c>.writing</c> 이 다음 저장을 막으면 안 된다.</summary>
        [Test]
        public void 중단된_writing_찌꺼기가_있어도_저장된다()
        {
            File.WriteAllText(SaveFile.WritingPathOf(savePath), "반쯤 쓰이다 만 내용");

            SaveState original = MakePopulated();
            SaveFile.Save(savePath, original);

            Assert.That(File.Exists(SaveFile.WritingPathOf(savePath)), Is.False);
            AssertSame(original, SaveFile.Load(savePath).State);
        }

        [Test]
        public void 첫_저장은_백업을_만들지_않는다()
        {
            SaveFile.Save(savePath, MakePopulated());

            Assert.That(File.Exists(SaveFile.BackupPathOf(savePath)), Is.False);
        }

        /// <summary>두 번째 저장부터 직전 내용이 <c>.bak</c> 에 남는다.</summary>
        [Test]
        public void 두_번째_저장은_직전_내용을_백업한다()
        {
            var first = new SaveState();
            first.Points["Gold"] = 111;
            SaveFile.Save(savePath, first);

            var second = new SaveState();
            second.Points["Gold"] = 222;
            SaveFile.Save(savePath, second);

            SaveState backup = SaveSerializer.Deserialize(File.ReadAllText(SaveFile.BackupPathOf(savePath)));
            Assert.That(backup.Points["Gold"], Is.EqualTo(111));
            Assert.That(SaveFile.Load(savePath).State.Points["Gold"], Is.EqualTo(222));
        }

        // --- 깨진 파일 ----------------------------------------------------------------------

        /// <summary>본 파일이 잘렸을 때 백업으로 살아난다. 이 클래스가 존재하는 이유다.</summary>
        [Test]
        public void 본_파일이_깨지면_백업에서_읽는다()
        {
            var first = new SaveState();
            first.Points["Gold"] = 111;
            SaveFile.Save(savePath, first);

            var second = new SaveState();
            second.Points["Gold"] = 222;
            SaveFile.Save(savePath, second);

            // 전원이 끊겨 파일이 잘린 상황.
            File.WriteAllText(savePath, "{\"version\":1,\"points\":{\"Gol");

            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.Backup));
            Assert.That(result.State.Points["Gold"], Is.EqualTo(111));
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void 본_파일이_0바이트여도_백업에서_읽는다()
        {
            SaveFile.Save(savePath, new SaveState());
            SaveFile.Save(savePath, MakePopulated());
            File.WriteAllText(savePath, string.Empty);

            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.Backup));
        }

        /// <summary>
        /// 둘 다 못 읽으면 <see cref="SaveSource.Unreadable"/> 이고 State 는 null 이다.
        /// 호출부가 "새 게임"과 구별해서 <b>덮어쓰지 않도록</b> 하기 위한 구분이다.
        /// </summary>
        [Test]
        public void 본_파일과_백업이_둘_다_깨지면_Unreadable_이다()
        {
            SaveFile.Save(savePath, new SaveState());
            SaveFile.Save(savePath, MakePopulated());

            File.WriteAllText(savePath, "{ 깨짐");
            File.WriteAllText(SaveFile.BackupPathOf(savePath), "{ 이것도 깨짐");

            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.Unreadable));
            Assert.That(result.State, Is.Null);
            Assert.That(result.IsUsable, Is.False);
        }

        [Test]
        public void 백업_없이_본_파일만_깨지면_Unreadable_이다()
        {
            File.WriteAllText(savePath, "{ 깨짐");

            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.Unreadable));
            Assert.That(result.State, Is.Null);
        }

        /// <summary>본 파일이 없고 백업만 있으면 백업으로 읽는다.</summary>
        [Test]
        public void 본_파일이_없으면_백업으로_읽는다()
        {
            SaveState original = MakePopulated();
            SaveFile.WriteText(SaveFile.BackupPathOf(savePath), SaveSerializer.Serialize(original));

            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.Backup));
            AssertSame(original, result.State);
        }

        /// <summary>미래 버전 파일은 백업이 없으면 Unreadable 이다. 덮어쓰면 안 되는 쪽이다.</summary>
        [Test]
        public void 미래_버전_파일은_읽지_않는다()
        {
            SaveFile.WriteText(savePath, "{\"version\":" + (SaveState.CurrentVersion + 1) + "}");

            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.Unreadable));
            Assert.That(result.State, Is.Null);
        }

        // --- 초기화 (7.6) -------------------------------------------------------------------

        [Test]
        public void Delete_는_본_파일과_백업과_찌꺼기를_모두_지운다()
        {
            SaveFile.Save(savePath, new SaveState());
            SaveFile.Save(savePath, MakePopulated());
            File.WriteAllText(SaveFile.WritingPathOf(savePath), "찌꺼기");

            SaveFile.Delete(savePath);

            Assert.That(File.Exists(savePath), Is.False);
            Assert.That(File.Exists(SaveFile.BackupPathOf(savePath)), Is.False);
            Assert.That(File.Exists(SaveFile.WritingPathOf(savePath)), Is.False);
            Assert.That(SaveFile.Load(savePath).Source, Is.EqualTo(SaveSource.None));
        }

        [Test]
        public void Delete_는_없는_파일에도_던지지_않는다()
        {
            Assert.DoesNotThrow(() => SaveFile.Delete(savePath));
        }

        // --- File.Replace 를 못 쓰는 플랫폼용 대체 경로 -------------------------------------
        //
        // 안드로이드·iOS 실기에서만 탈 수 있는 경로라 Windows 에디터 실행으로는 절대 안 걸린다.
        // 확인할 수 없는 코드를 남겨 두지 않으려고 internal 로 열어 직접 부른다.

        [Test]
        public void 대체_경로도_같은_결과를_만든다()
        {
            var first = new SaveState();
            first.Points["Gold"] = 111;
            SaveFile.Save(savePath, first);

            var second = new SaveState();
            second.Points["Gold"] = 222;
            SaveFile.WriteText(SaveFile.WritingPathOf(savePath), SaveSerializer.Serialize(second));

            SaveFile.ReplaceByMove(savePath);

            Assert.That(SaveFile.Load(savePath).State.Points["Gold"], Is.EqualTo(222));
            SaveState backup = SaveSerializer.Deserialize(File.ReadAllText(SaveFile.BackupPathOf(savePath)));
            Assert.That(backup.Points["Gold"], Is.EqualTo(111));
            Assert.That(File.Exists(SaveFile.WritingPathOf(savePath)), Is.False);
        }

        /// <summary>
        /// 대체 경로가 원자적이지 않다는 것을 인정하고, <b>그 중간 상태가 안전한지</b>를 본다.
        /// 1단계(본 파일 -> 백업)까지만 되고 죽으면 본 파일이 없고 백업만 남는데,
        /// 그건 <c>Load</c> 가 이미 다루는 상태여야 한다. 가장 나쁜 결과가 "직전 저장분으로
        /// 되돌아감"이지 소실이 아니라는 것이 이 설계의 근거다.
        /// </summary>
        [Test]
        public void 대체_경로가_중간에_죽어도_직전_저장분이_남는다()
        {
            var first = new SaveState();
            first.Points["Gold"] = 111;
            SaveFile.Save(savePath, first);

            // 1단계만 수행한 상태를 손으로 만든다.
            File.Move(savePath, SaveFile.BackupPathOf(savePath));

            SaveLoadResult result = SaveFile.Load(savePath);

            Assert.That(result.Source, Is.EqualTo(SaveSource.Backup));
            Assert.That(result.State.Points["Gold"], Is.EqualTo(111));
        }
    }
}
