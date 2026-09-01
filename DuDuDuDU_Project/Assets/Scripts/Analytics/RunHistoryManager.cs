using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.Hunting;
using OJ.Stage;
using OJ.Utils;

namespace OJ.Analytics
{
    public enum RunResultType
    {
        Unknown = 0,
        Clear = 1,
        Fail = 2,
        Abandoned = 3,
    }

    public enum RunEventType
    {
        RunStart = 0,
        WaveStart = 1,
        WaveComplete = 2,
        Summon = 3,
        Merge = 4,
        Craft = 5,
        RunEnd = 6,
    }

    /// <summary>
    /// 판 기록(진단용 로그). (MIGRATION_BASELINE 8.3b)
    ///
    /// <b>세이브 파일(7단계)에는 들어가지 않는다.</b> 런 30개 x 이벤트 400개라 크기도
    /// 수명도 다르고, 로그가 깨졌다고 진행도까지 잃을 이유가 없다. 계속 자기 PlayerPrefs
    /// 키를 쓴다.
    ///
    /// <b>루트에 사는 영구 서비스다.</b> 전투 밖(로비·타이틀)에서도 살아 있으므로
    /// <see cref="IBattleRefs.Game"/> 이 null 인 상태가 <b>정상</b>이다. 그래서 여기 있는
    /// null 검사는 조용한 폴백이 아니라 <b>기록해 둔 마지막 값으로 되돌아가는 정상 경로</b>다 —
    /// 지우면 로비에서 터진다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class RunHistoryManager : ISaveOnApplicationLifecycle
    {
        [Serializable]
        private class RunEventRecord
        {
            public string eventType;
            public int waveIndex;
            public float elapsedSeconds;
            public string primaryDiceType;
            public int primaryStar;
            public string secondaryDiceType;
            public int secondaryStar;
            public string resultDiceType;
            public int resultStar;
            public int wallHp;
            public int summonCost;
            public int currentSp;
            public int monsterHp;
            public int monsterDefense;
            public string note;
        }

        [Serializable]
        private class RunRecord
        {
            public string runId;
            public string startedAtUtc;
            public string endedAtUtc;
            public int stageIndex;
            public int totalWaves;
            public int finalWaveIndex;
            public int wallHpStart;
            public int wallHpEnd;
            public string result;
            public float durationSeconds;
            public int totalSummons;
            public int totalMerges;
            public int totalCrafts;
            public List<string> craftedKings = new List<string>();
            public List<RunEventRecord> events = new List<RunEventRecord>();
        }

        [Serializable]
        private class RunHistorySaveData
        {
            public List<RunRecord> runs = new List<RunRecord>();
        }

        /// <summary>과도기 다리. 대입은 <see cref="GameContainer"/> 에서만 한다.</summary>
        public static RunHistoryManager Instance { get; internal set; }

        private const string SaveKey = "OJ.RunHistory";
        private const int MaxStoredRuns = 30;
        private const int MaxEventsPerRun = 400;

        private readonly List<RunRecord> runs = new List<RunRecord>();
        private RunRecord currentRun;
        private float runStartRealtime;

        // 8.3b: 전투 씬 매니저로 가는 창구. 루트에 등록돼 있어 생성자로 받는다.
        // 전투 밖에서는 안이 비어 있는 것이 정상이므로, 쓰는 쪽에서 반드시 null 을 봐야 한다.
        private readonly IBattleRefs battle;

        public RunHistoryManager(IBattleRefs battle)
        {
            this.battle = battle;
            Load();
        }

        /// <summary>
        /// 앱이 멈추거나 끝날 때 <see cref="SaveOnApplicationLifecycle"/> 이 부른다.
        ///
        /// 예전에는 OnApplicationPause 가 저장만 하고 OnApplicationQuit 만 미완 런을
        /// 닫았다. 이제 한 곳이라 <b>둘 다 닫는다</b> — 모바일에서는 Quit 이 아예 안
        /// 불리는 경우가 많아서, 나가는 판이 영영 '진행 중'으로 남던 쪽이 문제였다.
        /// </summary>
        public void SaveAll()
        {
            if (currentRun != null && string.IsNullOrEmpty(currentRun.endedAtUtc))
            {
                // null 검사를 남긴다. 여기는 앱이 멈추거나 끝날 때 불리는데, 그 시점에
                // 전투 씬이 이미 내려갔거나 애초에 로비였을 수 있다. 그때는 기록해 둔
                // 마지막 웨이브·벽 HP 로 판을 닫는 것이 맞다.
                EndRun(
                    RunResultType.Abandoned,
                    battle.Game != null ? battle.Game.CurrentWaveIndex : currentRun.finalWaveIndex,
                    battle.Game != null && battle.Game.wall != null
                        ? battle.Game.wall.CurrentHp
                        : currentRun.wallHpEnd);
            }

            Save();
        }

        public void StartRun(StageData stageData, int wallHp)
        {
            if (stageData == null)
                return;

            if (currentRun != null && string.IsNullOrEmpty(currentRun.endedAtUtc))
                EndRun(RunResultType.Abandoned, currentRun.finalWaveIndex, currentRun.wallHpEnd);

            currentRun = new RunRecord
            {
                runId = Guid.NewGuid().ToString("N"),
                startedAtUtc = DateTime.UtcNow.ToString("o"),
                stageIndex = stageData.stageIndex,
                totalWaves = stageData.totalWaves,
                finalWaveIndex = 0,
                wallHpStart = wallHp,
                wallHpEnd = wallHp,
                result = RunResultType.Unknown.ToString(),
                durationSeconds = 0f,
            };

            runStartRealtime = Time.realtimeSinceStartup;
            AppendEvent(RunEventType.RunStart, 0, wallHp, note: $"Stage {stageData.stageIndex} start");
            Save();
        }

        public void RecordWaveStart(int waveIndex, int wallHp, int monsterHp, int monsterDefense)
        {
            if (currentRun == null)
                return;

            currentRun.finalWaveIndex = Mathf.Max(currentRun.finalWaveIndex, waveIndex);
            currentRun.wallHpEnd = wallHp;
            AppendEvent(RunEventType.WaveStart, waveIndex, wallHp, monsterHp: monsterHp, monsterDefense: monsterDefense);
        }

        public void RecordWaveComplete(int waveIndex, int wallHp, int currentSp)
        {
            if (currentRun == null)
                return;

            currentRun.finalWaveIndex = Mathf.Max(currentRun.finalWaveIndex, waveIndex);
            currentRun.wallHpEnd = wallHp;
            AppendEvent(RunEventType.WaveComplete, waveIndex, wallHp, currentSp: currentSp);
        }

        public void RecordSummon(DiceType diceType, int star, int waveIndex, int summonCost, int currentSp)
        {
            if (currentRun == null)
                return;

            currentRun.totalSummons++;
            AppendEvent(
                RunEventType.Summon,
                waveIndex,
                GetCurrentWallHp(),
                primaryDiceType: diceType,
                primaryStar: star,
                summonCost: summonCost,
                currentSp: currentSp);
        }

        public void RecordMerge(DiceType fromType, int fromStar, DiceType toType, int toStar, DiceType resultType, int resultStar, int waveIndex)
        {
            if (currentRun == null)
                return;

            currentRun.totalMerges++;
            AppendEvent(
                RunEventType.Merge,
                waveIndex,
                GetCurrentWallHp(),
                primaryDiceType: fromType,
                primaryStar: fromStar,
                secondaryDiceType: toType,
                secondaryStar: toStar,
                resultDiceType: resultType,
                resultStar: resultStar);
        }

        public void RecordCraft(DiceType craftedType, int waveIndex)
        {
            if (currentRun == null)
                return;

            currentRun.totalCrafts++;
            string craftedName = craftedType.ToString();
            if (!currentRun.craftedKings.Contains(craftedName))
                currentRun.craftedKings.Add(craftedName);

            AppendEvent(
                RunEventType.Craft,
                waveIndex,
                GetCurrentWallHp(),
                resultDiceType: craftedType,
                resultStar: 1,
                note: "Crafted mythic dice");
        }

        public void EndRun(RunResultType resultType, int finalWaveIndex, int wallHp)
        {
            if (currentRun == null)
                return;

            currentRun.finalWaveIndex = Mathf.Max(currentRun.finalWaveIndex, finalWaveIndex);
            currentRun.wallHpEnd = wallHp;
            currentRun.durationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - runStartRealtime);
            currentRun.endedAtUtc = DateTime.UtcNow.ToString("o");
            currentRun.result = resultType.ToString();

            AppendEvent(RunEventType.RunEnd, currentRun.finalWaveIndex, wallHp, note: resultType.ToString());

            runs.Add(currentRun);
            TrimRuns();
            currentRun = null;
            Save();
        }

        public string ExportRecentRunsJson(int count = 5)
        {
            RunHistorySaveData saveData = new RunHistorySaveData();
            int startIndex = Mathf.Max(0, runs.Count - Mathf.Max(1, count));
            for (int i = startIndex; i < runs.Count; i++)
                saveData.runs.Add(runs[i]);

            return JsonUtility.ToJson(saveData, true);
        }

        private void AppendEvent(
            RunEventType eventType,
            int waveIndex,
            int wallHp,
            DiceType primaryDiceType = DiceType.Max,
            int primaryStar = 0,
            DiceType secondaryDiceType = DiceType.Max,
            int secondaryStar = 0,
            DiceType resultDiceType = DiceType.Max,
            int resultStar = 0,
            int summonCost = 0,
            int currentSp = 0,
            int monsterHp = 0,
            int monsterDefense = 0,
            string note = null)
        {
            if (currentRun == null)
                return;

            if (currentRun.events == null)
                currentRun.events = new List<RunEventRecord>();

            if (currentRun.events.Count >= MaxEventsPerRun)
                return;

            currentRun.events.Add(new RunEventRecord
            {
                eventType = eventType.ToString(),
                waveIndex = waveIndex,
                elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - runStartRealtime),
                primaryDiceType = primaryDiceType == DiceType.Max ? string.Empty : primaryDiceType.ToString(),
                primaryStar = primaryStar,
                secondaryDiceType = secondaryDiceType == DiceType.Max ? string.Empty : secondaryDiceType.ToString(),
                secondaryStar = secondaryStar,
                resultDiceType = resultDiceType == DiceType.Max ? string.Empty : resultDiceType.ToString(),
                resultStar = resultStar,
                wallHp = wallHp,
                summonCost = summonCost,
                currentSp = currentSp,
                monsterHp = monsterHp,
                monsterDefense = monsterDefense,
                note = note ?? string.Empty,
            });
        }

        private void TrimRuns()
        {
            while (runs.Count > MaxStoredRuns)
                runs.RemoveAt(0);
        }

        private int GetCurrentWallHp()
        {
            // 전투 중이면 지금 벽 HP 를 읽고, 아니면(로비·타이틀·씬 전환 중) 마지막으로
            // 기록해 둔 값을 쓴다. 진단 로그라 값이 없다고 멈출 이유가 없다.
            if (battle.Game != null && battle.Game.wall != null)
                return battle.Game.wall.CurrentHp;

            return currentRun != null ? currentRun.wallHpEnd : 0;
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            RunHistorySaveData saveData = string.IsNullOrEmpty(json)
                ? new RunHistorySaveData()
                : JsonUtility.FromJson<RunHistorySaveData>(json) ?? new RunHistorySaveData();

            runs.Clear();
            if (saveData.runs != null)
                runs.AddRange(saveData.runs);

            TrimRuns();
        }

        private void Save()
        {
            RunHistorySaveData saveData = new RunHistorySaveData
            {
                runs = new List<RunRecord>(runs)
            };

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }
    }
}
