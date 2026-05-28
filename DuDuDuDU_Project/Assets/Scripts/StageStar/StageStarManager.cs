using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class StageStarManager : MonoBehaviour
    {
        [Serializable]
        private class StageStarSaveData
        {
            public List<int> claimedRewardIndices = new List<int>();
        }

        public static StageStarManager Instance { get; private set; }

        private const string SaveKey = "OJ.StageStar.Progress";

        public event Action OnChanged;

        private readonly HashSet<int> claimedRewardIndices = new HashSet<int>();
        private StageStarSaveData saveData = new StageStarSaveData();
        private bool isStageProgressSubscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var go = new GameObject(nameof(StageStarManager));
            go.AddComponent<StageStarManager>();
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
            SubscribeStageProgress();
        }

        private void Start()
        {
            SubscribeStageProgress();
        }

        private void OnDestroy()
        {
            if (isStageProgressSubscribed && StageProgressManager.Instance != null)
                StageProgressManager.Instance.OnProgressChanged -= HandleStageProgressChanged;

            if (Instance == this)
                Instance = null;
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

        public int GetStageStarCount(int stageIndex)
        {
            if (StageProgressManager.Instance == null)
                return 0;

            return StageStarUtility.GetStarCount(StageProgressManager.Instance.GetBestClearGrade(stageIndex));
        }

        public int GetTotalStarCount()
        {
            int total = 0;
            int stageCount = GetStageCount();
            for (int i = 1; i <= stageCount; i++)
                total += GetStageStarCount(i);

            return total;
        }

        public int GetMaxStarCount()
        {
            return GetStageCount() * StageStarUtility.MaxStarsPerStage;
        }

        public int GetRewardCount()
        {
            return GetMaxStarCount() / StageStarUtility.StarsPerReward;
        }

        public int GetRequiredStars(int rewardIndex)
        {
            return (Mathf.Max(0, rewardIndex) + 1) * StageStarUtility.StarsPerReward;
        }

        public bool IsRewardClaimed(int rewardIndex)
        {
            return claimedRewardIndices.Contains(rewardIndex);
        }

        public bool IsRewardClaimable(int rewardIndex)
        {
            return !IsRewardClaimed(rewardIndex) && GetTotalStarCount() >= GetRequiredStars(rewardIndex);
        }

        public bool HasClaimableReward()
        {
            int rewardCount = GetRewardCount();
            for (int i = 0; i < rewardCount; i++)
            {
                if (IsRewardClaimable(i))
                    return true;
            }

            return false;
        }

        public bool TryClaimReward(int rewardIndex, out List<PointRewardEntry> grantedRewards)
        {
            grantedRewards = new List<PointRewardEntry>();

            if (rewardIndex < 0 || rewardIndex >= GetRewardCount())
                return false;

            if (!IsRewardClaimable(rewardIndex))
                return false;

            grantedRewards.Add(new PointRewardEntry(PointType.Dia, StageStarUtility.DiaRewardAmount));
            claimedRewardIndices.Add(rewardIndex);
            PointRewardUtility.GrantRewards(grantedRewards);
            SyncSaveData();
            Save();
            OnChanged?.Invoke();
            return true;
        }

        private void HandleStageProgressChanged()
        {
            OnChanged?.Invoke();
        }

        private void SubscribeStageProgress()
        {
            if (isStageProgressSubscribed || StageProgressManager.Instance == null)
                return;

            StageProgressManager.Instance.OnProgressChanged += HandleStageProgressChanged;
            isStageProgressSubscribed = true;
        }

        private void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            saveData = string.IsNullOrEmpty(json)
                ? new StageStarSaveData()
                : JsonUtility.FromJson<StageStarSaveData>(json) ?? new StageStarSaveData();

            if (saveData.claimedRewardIndices == null)
                saveData.claimedRewardIndices = new List<int>();

            claimedRewardIndices.Clear();
            for (int i = 0; i < saveData.claimedRewardIndices.Count; i++)
            {
                int rewardIndex = saveData.claimedRewardIndices[i];
                if (rewardIndex >= 0)
                    claimedRewardIndices.Add(rewardIndex);
            }

            SyncSaveData();
        }

        private void Save()
        {
            SyncSaveData();
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }

        private void SyncSaveData()
        {
            if (saveData.claimedRewardIndices == null)
                saveData.claimedRewardIndices = new List<int>();

            saveData.claimedRewardIndices.Clear();
            foreach (int rewardIndex in claimedRewardIndices)
                saveData.claimedRewardIndices.Add(rewardIndex);
        }

        private static int GetStageCount()
        {
            return Mathf.Max(1, StageDatabaseProvider.GetDatabase().StageCount);
        }
    }
}
