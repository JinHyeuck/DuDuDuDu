using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class ChapterRewardManager : MonoBehaviour
    {
        [Serializable]
        private class ChapterRewardSaveData
        {
            public List<string> claimedRewardIds = new List<string>();
        }

        public static ChapterRewardManager Instance { get; private set; }

        private const string SaveKey = "OJ.ChapterReward.Progress";

        public event Action OnChanged;

        private readonly HashSet<string> claimedRewardIds = new HashSet<string>();
        private ChapterRewardSaveData saveData = new ChapterRewardSaveData();
        private bool isStageProgressSubscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var go = new GameObject(nameof(ChapterRewardManager));
            go.AddComponent<ChapterRewardManager>();
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

        public IReadOnlyList<ChapterRewardMilestone> GetMilestones()
        {
            return ChapterRewardDatabaseProvider.GetDatabase().Milestones;
        }

        public bool HasClaimableReward()
        {
            IReadOnlyList<ChapterRewardMilestone> milestones = GetMilestones();
            for (int i = 0; i < milestones.Count; i++)
            {
                if (GetState(milestones[i]) == ChapterRewardState.Claimable)
                    return true;
            }

            return false;
        }

        public ChapterRewardState GetState(ChapterRewardMilestone milestone)
        {
            if (milestone == null)
                return ChapterRewardState.Locked;

            if (IsClaimed(milestone))
                return ChapterRewardState.Claimed;

            return IsUnlocked(milestone) ? ChapterRewardState.Claimable : ChapterRewardState.Locked;
        }

        public bool IsClaimed(ChapterRewardMilestone milestone)
        {
            return milestone != null && claimedRewardIds.Contains(milestone.StableId);
        }

        public bool IsUnlocked(ChapterRewardMilestone milestone)
        {
            if (milestone == null)
                return false;

            if (StageProgressManager.Instance == null)
                return false;

            return StageProgressManager.Instance.HasClearedWave(
                milestone.requiredStageIndex,
                milestone.requiredWaveIndex);
        }

        public int GetFocusIndex()
        {
            IReadOnlyList<ChapterRewardMilestone> milestones = GetMilestones();
            if (milestones == null || milestones.Count == 0)
                return -1;

            for (int i = 0; i < milestones.Count; i++)
            {
                if (GetState(milestones[i]) == ChapterRewardState.Claimable)
                    return i;
            }

            for (int i = 0; i < milestones.Count; i++)
            {
                if (GetState(milestones[i]) != ChapterRewardState.Claimed)
                    return i;
            }

            return milestones.Count - 1;
        }

        public int GetProgressIndex()
        {
            IReadOnlyList<ChapterRewardMilestone> milestones = GetMilestones();
            if (milestones == null || milestones.Count == 0)
                return 0;

            int focusIndex = GetFocusIndex();
            if (focusIndex < 0)
                return 0;

            return Mathf.Clamp(focusIndex + 1, 1, milestones.Count);
        }

        public int GetTotalCount()
        {
            IReadOnlyList<ChapterRewardMilestone> milestones = GetMilestones();
            return milestones != null ? milestones.Count : 0;
        }

        public bool TryClaim(ChapterRewardMilestone milestone, out List<PointRewardEntry> grantedRewards)
        {
            grantedRewards = new List<PointRewardEntry>();

            if (GetState(milestone) != ChapterRewardState.Claimable)
                return false;

            if (milestone.rewards != null)
            {
                for (int i = 0; i < milestone.rewards.Count; i++)
                {
                    ChapterRewardEntry reward = milestone.rewards[i];
                    if (reward.amount <= 0)
                        continue;

                    grantedRewards.Add(reward.ToPointRewardEntry());
                }
            }

            claimedRewardIds.Add(milestone.StableId);
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
                ? new ChapterRewardSaveData()
                : JsonUtility.FromJson<ChapterRewardSaveData>(json) ?? new ChapterRewardSaveData();

            if (saveData.claimedRewardIds == null)
                saveData.claimedRewardIds = new List<string>();

            claimedRewardIds.Clear();
            for (int i = 0; i < saveData.claimedRewardIds.Count; i++)
            {
                string rewardId = saveData.claimedRewardIds[i];
                if (!string.IsNullOrWhiteSpace(rewardId))
                    claimedRewardIds.Add(rewardId);
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
            if (saveData.claimedRewardIds == null)
                saveData.claimedRewardIds = new List<string>();

            saveData.claimedRewardIds.Clear();
            foreach (string rewardId in claimedRewardIds)
                saveData.claimedRewardIds.Add(rewardId);
        }
    }
}
