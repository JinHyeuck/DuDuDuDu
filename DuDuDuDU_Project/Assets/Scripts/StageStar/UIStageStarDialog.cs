using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIStageStarDialog : IDialog
    {
        [Header("Stage List")]
        [SerializeField] private RectTransform stageRoot;
        [SerializeField] private UIStageStarStageItem stageItemTemplate;

        [Header("Summary")]
        [SerializeField] private TMP_Text totalStarText;
        [SerializeField] private string totalStarFormat = "{0}";

        [Header("Buttons")]
        [SerializeField] private Button rewardButton;
        [SerializeField] private GameObject rewardRedDot;
        [SerializeField] private Button closeButton;

        [Header("Reward Popup")]
        [SerializeField] private UIStageStarRewardDialog rewardDialog;

        private readonly List<UIStageStarStageItem> stageItems = new List<UIStageStarStageItem>();

        protected override void OnLoad()
        {
            base.OnLoad();

            if (stageRoot == null && stageItemTemplate != null)
                stageRoot = stageItemTemplate.transform.parent as RectTransform;

            if (stageItemTemplate != null)
                stageItemTemplate.gameObject.SetActive(false);

            if (rewardButton != null)
                rewardButton.onClick.AddListener(OpenRewardDialog);
            if (closeButton != null)
                closeButton.onClick.AddListener(Exit);
        }

        protected override void OnDestroy()
        {
            if (rewardButton != null)
                rewardButton.onClick.RemoveListener(OpenRewardDialog);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Exit);

            if (StageProgressManager.Instance != null)
                StageProgressManager.Instance.OnProgressChanged -= Refresh;
            if (StageStarManager.Instance != null)
                StageStarManager.Instance.OnChanged -= Refresh;

            base.OnDestroy();
        }

        protected override void OnEnter()
        {
            base.OnEnter();

            if (StageProgressManager.Instance != null)
                StageProgressManager.Instance.OnProgressChanged += Refresh;
            if (StageStarManager.Instance != null)
                StageStarManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        protected override void OnExit()
        {
            if (StageProgressManager.Instance != null)
                StageProgressManager.Instance.OnProgressChanged -= Refresh;
            if (StageStarManager.Instance != null)
                StageStarManager.Instance.OnChanged -= Refresh;

            base.OnExit();
        }

        public void Open()
        {
            Enter();
        }

        private void Refresh()
        {
            StageDatabase stageDatabase = StageDatabaseProvider.GetDatabase();
            int stageCount = stageDatabase != null ? Mathf.Max(1, stageDatabase.StageCount) : 1;

            EnsureStageItems(stageCount);

            for (int i = 0; i < stageItems.Count; i++)
            {
                UIStageStarStageItem stageItem = stageItems[i];
                if (stageItem == null)
                    continue;

                bool shouldShow = i < stageCount;
                stageItem.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                int stageIndex = i + 1;
                StageData stageData = stageDatabase.GetStage(stageIndex);
                bool isUnlocked = StageProgressManager.Instance == null ||
                    StageProgressManager.Instance.IsStageUnlocked(stageIndex);
                StageClearGrade bestGrade = StageProgressManager.Instance != null
                    ? StageProgressManager.Instance.GetBestClearGrade(stageIndex)
                    : StageClearGrade.None;

                stageItem.Bind(stageData, bestGrade, isUnlocked, StartStage);
            }

            RefreshSummary();
        }

        private void RefreshSummary()
        {
            StageStarManager manager = StageStarManager.Instance;
            int totalStars = manager != null ? manager.GetTotalStarCount() : 0;

            if (totalStarText != null)
            {
                if (string.IsNullOrEmpty(totalStarFormat))
                    totalStarText.SetText("{0}", totalStars);
                else
                    totalStarText.SetText(totalStarFormat, totalStars);
            }

            if (rewardRedDot != null)
                rewardRedDot.SetActive(manager != null && manager.HasClaimableReward());
        }

        private void StartStage(int stageIndex)
        {
            if (StageProgressManager.Instance != null)
            {
                if (!StageProgressManager.Instance.IsStageUnlocked(stageIndex))
                    return;

                StageProgressManager.Instance.SelectStage(stageIndex);
            }

            SceneFlowManager.LoadBattle();
        }

        private void OpenRewardDialog()
        {
            if (rewardDialog != null)
                rewardDialog.Open();
        }

        private void EnsureStageItems(int count)
        {
            if (stageItemTemplate == null || stageRoot == null)
                return;

            while (stageItems.Count < count)
            {
                UIStageStarStageItem stageItem = Object.Instantiate(stageItemTemplate, stageRoot);
                stageItem.gameObject.SetActive(true);
                stageItems.Add(stageItem);
            }
        }
    }
}
