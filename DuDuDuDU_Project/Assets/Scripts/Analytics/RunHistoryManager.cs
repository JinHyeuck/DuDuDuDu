using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
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

    public class RunHistoryManager : MonoBehaviour
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

        public static RunHistoryManager Instance { get; private set; }

        private const string SaveKey = "OJ.RunHistory";
        private const int MaxStoredRuns = 30;
        private const int MaxEventsPerRun = 400;

        private readonly List<RunRecord> runs = new List<RunRecord>();
        private RunRecord currentRun;
        private float runStartRealtime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var go = new GameObject(nameof(RunHistoryManager));
            go.AddComponent<RunHistoryManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                Save();
        }

        private void OnApplicationQuit()
        {
            if (currentRun != null && string.IsNullOrEmpty(currentRun.endedAtUtc))
                EndRun(RunResultType.Abandoned, GameManager.Instance != null ? GameManager.Instance.CurrentWaveIndex : currentRun.finalWaveIndex, GameManager.Instance != null && GameManager.Instance.wall != null ? GameManager.Instance.wall.CurrentHp : currentRun.wallHpEnd);

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
            if (GameManager.Instance != null && GameManager.Instance.wall != null)
                return GameManager.Instance.wall.CurrentHp;

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
