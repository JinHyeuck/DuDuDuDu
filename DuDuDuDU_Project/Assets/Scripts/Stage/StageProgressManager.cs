using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class StageProgressManager : MonoBehaviour
    {
        [Serializable]
        private class StageProgressSaveData
        {
            public int selectedStageIndex = 1;
            public int highestUnlockedStageIndex = 1;
            public List<StageRecord> stageRecords = new List<StageRecord>();
        }

        [Serializable]
        private class StageRecord
        {
            public int stageIndex;
            public int claimedRewardFlags;
            public int bestClearGrade;
        }

        public static StageProgressManager Instance { get; private set; }

        private const string SaveKey = "OJ.Stage.Progress";

        private readonly Dictionary<int, StageRecord> stageRecords = new Dictionary<int, StageRecord>();

        private StageProgressSaveData saveData = new StageProgressSaveData();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var go = new GameObject(nameof(StageProgressManager));
            go.AddComponent<StageProgressManager>();
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
            Save();
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

            if (clearGrade != StageClearGrade.None)
                saveData.highestUnlockedStageIndex = Mathf.Max(saveData.highestUnlockedStageIndex, Mathf.Min(GetMaxStageIndex(), stageIndex + 1));

            Save();
            return newlyClaimedFlags;
        }

        private StageRecord GetOrCreateRecord(int stageIndex)
        {
            if (stageRecords.TryGetValue(stageIndex, out StageRecord record))
                return record;

            record = new StageRecord
            {
                stageIndex = stageIndex,
                claimedRewardFlags = 0,
                bestClearGrade = 0,
            };

            stageRecords.Add(stageIndex, record);
            saveData.stageRecords.Add(record);
            return record;
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            saveData = string.IsNullOrEmpty(json)
                ? new StageProgressSaveData()
                : JsonUtility.FromJson<StageProgressSaveData>(json) ?? new StageProgressSaveData();

            if (saveData.stageRecords == null)
                saveData.stageRecords = new List<StageRecord>();

            stageRecords.Clear();
            for (int i = 0; i < saveData.stageRecords.Count; i++)
            {
                StageRecord record = saveData.stageRecords[i];
                if (record == null || record.stageIndex < 1)
                    continue;

                stageRecords[record.stageIndex] = record;
            }

            saveData.selectedStageIndex = Mathf.Clamp(saveData.selectedStageIndex, 1, GetMaxStageIndex());
            saveData.highestUnlockedStageIndex = Mathf.Clamp(saveData.highestUnlockedStageIndex, 1, GetMaxStageIndex());
        }

        private void Save()
        {
            saveData.selectedStageIndex = Mathf.Clamp(saveData.selectedStageIndex, 1, GetMaxStageIndex());
            saveData.highestUnlockedStageIndex = Mathf.Clamp(saveData.highestUnlockedStageIndex, 1, GetMaxStageIndex());
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }

        private static int GetMaxStageIndex()
        {
            return Mathf.Max(1, StageDatabaseProvider.GetDatabase().StageCount);
        }
    }
}
