using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.IdleReward;
using OJ.Save;
using OJ.StageReward;
using OJ.StageStar;

namespace OJ.Stage
{
    /// <summary>
    /// 스테이지 선택·해금·기록. (MIGRATION_BASELINE 8.3a)
    ///
    /// <b>이 넷의 뿌리다.</b> <c>StageRewardManager</c> · <c>StageStarManager</c> ·
    /// <c>IdleRewardManager</c> 가 전부 이것을 본다. 그래서 컨테이너에 가장 먼저 등록된다 —
    /// 예전에는 <c>Awake</c> 순서가 정해져 있지 않아 구독이 <b>조용히 no-op</b> 이 되곤 했고,
    /// 그래서 저쪽 둘이 <c>Awake</c> 와 <c>Start</c> 에서 구독을 두 번 시도하고 있었다.
    ///
    /// 데이터베이스는 아직 <c>StageDatabaseProvider</c>(정적)를 그대로 부른다. 인터페이스로
    /// 감싸면 에디터 없이 테스트할 수 있게 되지만, 그건 이 단계의 목적이 아니다 —
    /// 지금은 <b>수명과 소유권</b>만 옮긴다. 한 번에 둘 다 바꾸면 무엇이 깨졌는지 못 가린다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class StageProgressManager : ISaveStateOwner
    {
        /// <summary>
        /// 선택·해금 번호를 담는 그릇. <b>더 이상 직렬화 대상이 아니다.</b>
        ///
        /// 7.5 전에는 이것이 JsonUtility 로 PlayerPrefs 에 통째로 찍히는 DTO 였고, 그래서
        /// JsonUtility 가 딕셔너리를 못 쓰는 탓에 기록을 <c>List&lt;StageRecord&gt;</c> 로
        /// 한 벌 더 들고 다녔다. 파일 쪽 표현이 <see cref="OJ.Core.SaveState"/> 로 넘어간
        /// 지금 그 리스트를 읽는 곳은 없다 — 남겨 두면 <b>아무도 안 보는 사본이 늘어나기만
        /// 하고</b>, 딕셔너리와 어긋나도 아무 증상이 없어 나중에 그쪽을 정본으로 착각하기 좋다.
        /// </summary>
        private class StageProgressSaveData
        {
            public int selectedStageIndex = 1;
            public int highestUnlockedStageIndex = 1;
        }

        /// <summary>
        /// 스테이지 하나의 기록. 스테이지 번호는 <see cref="stageRecords"/> 의 키가 들고 있다 —
        /// 예전에 이 안에도 번호를 두었던 것은 직렬화된 리스트 항목이 자기 번호를 알아야
        /// 했기 때문이고, 리스트가 사라진 지금 같은 값을 두 곳에 두면 어긋날 자리만 생긴다.
        /// </summary>
        private class StageRecord
        {
            public int claimedRewardFlags;
            public int bestClearGrade;
            public int bestClearedWave;
        }

        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// 호출부를 전부 주입으로 옮기면 사라진다.
        /// </summary>
        public static StageProgressManager Instance { get; internal set; }

        public event Action OnProgressChanged;

        /// <summary>
        /// 스테이지별 기록의 <b>정본</b>. 필드 초기화라 생성자와 함께 무조건 만들어진다 —
        /// 세이브 파일이 없는 첫 실행에서는 <see cref="ReadFrom"/> 이 <b>아예 호출되지 않으므로</b>
        /// (<c>SaveService.TryLoadAll</c> 이 파일이 없으면 owners 루프 전에 돌아간다) 여기서
        /// 만들어 두지 않으면 신규 설치가 첫 조회에서 NRE 로 죽는다.
        /// </summary>
        private readonly Dictionary<int, StageRecord> stageRecords = new Dictionary<int, StageRecord>();

        /// <summary>
        /// 선택·해금 번호. 필드 초기화가 곧 <b>신규 설치의 정답</b>이다(1스테이지 선택,
        /// 1스테이지까지 해금). 7.5 전에는 PlayerPrefs 가 비었을 때 옛 <c>Load()</c> 가 같은
        /// 값을 세워 주고 있었으므로, 그 경로만 지우고 여기를 0 으로 두면 신규 설치가
        /// 아무 스테이지도 들어가지 못하는 상태로 시작한다.
        /// </summary>
        private StageProgressSaveData saveData = new StageProgressSaveData();

        public StageProgressManager()
        {
            // 7.5 이전에는 여기서 Load() 가 PlayerPrefs 를 읽었다. 이제 로드는 SaveService 가
            // ReadFrom 으로 밀어 넣으므로 생성자는 <b>초기 상태를 세우는 일만</b> 한다.
            // 이 매니저의 초기 상태는 "1스테이지 선택 / 빈 기록"이고 그건 위 필드 초기화가
            // 이미 끝내 준다 — 그래서 여기로 옮겨 올 초기화가 없다.
            //
            // <b>ClampStageIndices() 는 일부러 부르지 않는다.</b> 두 가지 이유다.
            // 하나, 초기값 (1, 1) 은 최대 스테이지 수가 몇이든 잘리지 않아 아무 일도 안 한다.
            // 둘, 그 안의 StageDatabaseProvider 가 StaticResource 를 깨운다 — 컨테이너가
            // 매니저를 만드는 도중에 Resources 프리팹을 인스턴스화하게 되고, 에디트 모드나
            // 아직 준비 안 된 시점이면 폴백 30스테이지로 내려가면서 "StageDatabase 를 찾지
            // 못했다" 는 <b>거짓 에러</b>를 한 번 찍는다. 그 로그는 한 번만 나오게 잠기므로
            // 나중에 일어나는 진짜 사고를 대신 삼켜 버린다.
            // 세이브가 있을 때의 클램프는 ReadFrom 끝에서 한다.
        }

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        public void WriteTo(OJ.Core.SaveState state)
        {
            // 파일에 나가기 직전에 클램프를 태운다. <b>이제 여기가 유일한 클램프 지점이다</b> —
            // 7.5 에서 <see cref="Save"/> 가 통합 저장 호출 한 줄이 되면서 그쪽 클램프가 사라졌고,
            // 모든 저장은 결국 이 메서드를 지난다. 자를지 말지는 <see cref="ClampStageIndices"/> 가
            // 판단하므로(데이터베이스가 폴백이면 자르지 않는다) 여기서 조건을 다시 쓰면 나중에
            // 한쪽만 고쳐져 진행도가 30으로 잘리는 그 사고가 되살아난다.
            ClampStageIndices();

            state.Stage.SelectedIndex = saveData.selectedStageIndex;
            state.Stage.HighestUnlockedIndex = saveData.highestUnlockedStageIndex;

            state.Stage.Records.Clear();

            // 딕셔너리가 정본이다. 예전에는 같은 기록을 리스트로도 들고 있었고 그쪽에는
            // <c>Load()</c> 가 걸러 낸 항목(번호가 1 미만이거나 같은 번호가 둘)이 그대로
            // 남아 있었다 — 두 컨테이너 중 어느 쪽을 내보내느냐가 결과를 바꿨다는 뜻이다.
            // 7.5 에서 리스트를 지워 그 선택지 자체를 없앴다.
            foreach (KeyValuePair<int, StageRecord> pair in stageRecords)
            {
                StageRecord record = pair.Value;
                if (record == null)
                    continue;

                // 키는 반드시 불변 문화권으로 만든다. 비교자가 Ordinal 이라 아랍-인도 숫자 같은
                // 다른 자형으로 찍히면 같은 스테이지가 다른 칸에 들어간다.
                state.Stage.Records[pair.Key.ToString(CultureInfo.InvariantCulture)] = new OJ.Core.StageRecordSave
                {
                    ClaimedRewardFlags = record.claimedRewardFlags,
                    BestClearGrade = record.bestClearGrade,
                    BestClearedWave = record.bestClearedWave,
                };
            }
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            // 컨테이너를 통째로 갈아 끼운다. 생성자가 세워 둔 초기 상태(1스테이지)든 이전
            // 진행도든, 파일에서 읽은 것과 섞이면 어느 쪽이 진짜인지 구분할 방법이 없다.
            saveData = new StageProgressSaveData
            {
                selectedStageIndex = state.Stage.SelectedIndex,
                highestUnlockedStageIndex = state.Stage.HighestUnlockedIndex,
            };

            stageRecords.Clear();
            foreach (KeyValuePair<string, OJ.Core.StageRecordSave> pair in state.Stage.Records)
            {
                OJ.Core.StageRecordSave saved = pair.Value;
                if (saved == null)
                    continue;

                // 키가 숫자로 안 읽히거나 번호가 1 미만이면 조용히 버린다. 손상된 기록 하나
                // 때문에 나머지 진행도까지 잃으면 안 된다.
                if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int stageIndex) || stageIndex < 1)
                    continue;

                // "12" 와 "012" 처럼 같은 번호로 읽히는 키가 둘일 수 있다. 그대로 두면 아래
                // <c>Add</c> 가 예외를 던지고, SaveService 는 ReadFrom 예외를 매니저 단위로
                // 삼키므로 <b>그 뒤 기록이 통째로 안 실린 채</b> 게임이 시작된다.
                if (stageRecords.ContainsKey(stageIndex))
                    continue;

                stageRecords.Add(stageIndex, new StageRecord
                {
                    claimedRewardFlags = saved.ClaimedRewardFlags,
                    bestClearGrade = saved.BestClearGrade,
                    bestClearedWave = saved.BestClearedWave,
                });
            }

            ClampStageIndices();
        }

        public int GetSelectedStageIndex()
        {
            return Mathf.Clamp(saveData.selectedStageIndex, 1, GetMaxStageIndex());
        }

        public StageData GetSelectedStage()
        {
            return StageDatabaseProvider.GetStage(GetSelectedStageIndex()) ?? StageDatabaseProvider.GetStage(1);
        }

        public int GetHighestUnlockedStageIndex()
        {
            return Mathf.Clamp(saveData.highestUnlockedStageIndex, 1, GetMaxStageIndex());
        }

        public int GetLastClearedStageIndex()
        {
            int lastClearedStageIndex = 0;
            foreach (KeyValuePair<int, StageRecord> pair in stageRecords)
            {
                StageRecord record = pair.Value;
                if (record == null || record.bestClearGrade <= (int)StageClearGrade.None)
                    continue;

                lastClearedStageIndex = Mathf.Max(lastClearedStageIndex, pair.Key);
            }

            return Mathf.Clamp(lastClearedStageIndex, 0, GetMaxStageIndex());
        }

        public bool IsStageUnlocked(int stageIndex)
        {
            return stageIndex >= 1 && stageIndex <= GetHighestUnlockedStageIndex();
        }

        public void SelectStage(int stageIndex)
        {
            int clamped = Mathf.Clamp(stageIndex, 1, GetMaxStageIndex());
            saveData.selectedStageIndex = clamped;
            Save();
        }

        public StageClearGrade GetBestClearGrade(int stageIndex)
        {
            if (!stageRecords.TryGetValue(stageIndex, out StageRecord record))
                return StageClearGrade.None;

            return (StageClearGrade)Mathf.Clamp(record.bestClearGrade, 0, (int)StageClearGrade.Perfect);
        }

        public StageRewardTierFlags GetClaimedRewardFlags(int stageIndex)
        {
            if (!stageRecords.TryGetValue(stageIndex, out StageRecord record))
                return StageRewardTierFlags.None;

            return (StageRewardTierFlags)record.claimedRewardFlags;
        }

        public int GetBestClearedWave(int stageIndex)
        {
            if (!stageRecords.TryGetValue(stageIndex, out StageRecord record))
                return GetHighestUnlockedStageIndex() > stageIndex ? GetTotalWaves(stageIndex) : 0;

            if (record.bestClearGrade > (int)StageClearGrade.None)
                return Mathf.Max(record.bestClearedWave, GetTotalWaves(stageIndex));

            return Mathf.Max(0, record.bestClearedWave);
        }

        public bool HasClearedWave(int stageIndex, int waveIndex)
        {
            if (stageIndex < 1)
                return false;

            int requiredWave = ClampWaveIndex(stageIndex, waveIndex);
            if (GetHighestUnlockedStageIndex() > stageIndex)
                return true;

            return GetBestClearedWave(stageIndex) >= requiredWave;
        }

        public bool RecordClearedWave(int stageIndex, int clearedWaveIndex)
        {
            if (stageIndex < 1 || clearedWaveIndex < 1)
                return false;

            StageRecord record = GetOrCreateRecord(stageIndex);
            int clampedWave = ClampWaveIndex(stageIndex, clearedWaveIndex);
            if (record.bestClearedWave >= clampedWave)
                return false;

            record.bestClearedWave = clampedWave;
            Save();
            OnProgressChanged?.Invoke();
            return true;
        }

        public StageRewardTierFlags RecordStageClear(int stageIndex, StageClearGrade clearGrade)
        {
            if (stageIndex < 1)
                return StageRewardTierFlags.None;

            StageRecord record = GetOrCreateRecord(stageIndex);
            StageRewardTierFlags previousFlags = (StageRewardTierFlags)record.claimedRewardFlags;
            StageRewardTierFlags achievedFlags = StageRewardCalculator.GetRewardFlagsForGrade(clearGrade);
            StageRewardTierFlags newlyClaimedFlags = achievedFlags & ~previousFlags;

            record.claimedRewardFlags = (int)(previousFlags | achievedFlags);
            record.bestClearGrade = Mathf.Max(record.bestClearGrade, (int)clearGrade);
            record.bestClearedWave = Mathf.Max(record.bestClearedWave, GetTotalWaves(stageIndex));

            if (clearGrade != StageClearGrade.None)
                saveData.highestUnlockedStageIndex = Mathf.Max(saveData.highestUnlockedStageIndex, Mathf.Min(GetMaxStageIndex(), stageIndex + 1));

            Save();
            OnProgressChanged?.Invoke();
            return newlyClaimedFlags;
        }

        private StageRecord GetOrCreateRecord(int stageIndex)
        {
            if (stageRecords.TryGetValue(stageIndex, out StageRecord record))
                return record;

            record = new StageRecord
            {
                claimedRewardFlags = 0,
                bestClearGrade = 0,
                bestClearedWave = 0,
            };

            stageRecords.Add(stageIndex, record);
            return record;
        }

        /// <summary>
        /// 지금 상태를 통합 세이브 파일에 즉시 쓴다.
        ///
        /// 7.5: PlayerPrefs 대신 통합 세이브를 쓴다. <b>호출 지점은 그대로 두는 것이 중요하다</b> —
        /// 스테이지 선택·웨이브 돌파·클리어는 되돌릴 수 없는 진행이고, 여기서 즉시 저장하지
        /// 않으면 앱이 백그라운드로 갈 때까지 그 진행이 메모리에만 남는다. 모바일에서 OS 가
        /// 프로세스를 죽이는 것은 일상이다.
        ///
        /// 클램프는 여기서 하지 않는다. <see cref="WriteTo"/> 가 첫 줄에서 하고, 모든 저장이
        /// 그곳을 지난다 — 같은 판정을 두 곳에 두면 나중에 한쪽만 고쳐져 갈라진다.
        ///
        /// <c>?.</c> 가 필요하다. 매니저 <b>생성자가 도는 시점에는 SaveService 가 아직 없다</b> —
        /// 컨테이너가 매니저를 만든 뒤에 SaveService 를 해석하기 때문이다. 그 시점에 저장이
        /// 간접적으로 불려 오면 조용히 건너뛰는 것이 맞다(아직 쓸 것도 없다).
        /// </summary>
        private void Save() => OJ.DI.GameContainer.SaveService?.SaveAll();

        /// <summary>
        /// 스테이지 번호를 유효 범위로 자른다.
        ///
        /// <b>데이터베이스가 폴백이면 자르지 않는다.</b> 이게 이 메서드의 존재 이유다.
        ///
        /// 예전에는 로드 경로와 저장 경로가 각자 같은 두 줄을 들고 있었고 둘 다
        /// <see cref="GetMaxStageIndex"/> 를 무조건 믿었다. 그런데 그 값은
        /// <c>StageDatabaseProvider</c> 가 에셋을 못 찾으면 <b>코드 기본값 30</b>으로 내려간다.
        /// 그러면 45스테이지까지 깬 유저의 기록이 30으로 잘리고, 다음 저장이 그 잘린 값을
        /// 그대로 써 버린다. <b>한 번 일어나면 되돌릴 수 없다.</b>
        ///
        /// 저장 쪽 클램프만 빼는 것으로는 부족했다 — 로드가 이미 메모리 값을 잘라 놓으면
        /// 이후의 정상적인 저장이 그 값을 쓰기 때문이다. 그래서 자르는 행위 자체를
        /// "진짜 에셋이 있을 때"로 묶는다.
        ///
        /// 7.5 로 진입점이 <see cref="ReadFrom"/> 과 <see cref="WriteTo"/> 둘로 줄었지만
        /// 이 가드는 그대로 필요하다. 오히려 더 필요하다 — 이제 세이브 파일이 유일한
        /// 진행도라, 여기서 한 번 잘리면 되돌릴 PlayerPrefs 사본이 없다.
        ///
        /// 정상 경로에서는 아무것도 바뀌지 않는다. 해금 번호가 실제 스테이지 수를 넘는 일이
        /// 없으므로 클램프는 원래 아무 일도 하지 않는다.
        /// </summary>
        private void ClampStageIndices()
        {
            if (!StageDatabaseProvider.HasRealDatabase)
                return;

            int max = GetMaxStageIndex();
            saveData.selectedStageIndex = Mathf.Clamp(saveData.selectedStageIndex, 1, max);
            saveData.highestUnlockedStageIndex = Mathf.Clamp(saveData.highestUnlockedStageIndex, 1, max);
        }

        private static int GetMaxStageIndex()
        {
            return Mathf.Max(1, StageDatabaseProvider.GetDatabase().StageCount);
        }

        private static int GetTotalWaves(int stageIndex)
        {
            StageData stageData = StageDatabaseProvider.GetStage(stageIndex);
            return stageData != null ? Mathf.Max(1, stageData.totalWaves) : 1;
        }

        private static int ClampWaveIndex(int stageIndex, int waveIndex)
        {
            return Mathf.Clamp(Mathf.Max(1, waveIndex), 1, GetTotalWaves(stageIndex));
        }
    }
}
