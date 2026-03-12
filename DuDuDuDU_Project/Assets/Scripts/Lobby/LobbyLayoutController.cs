using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace OJ
{
    public enum LobbyTab
    {
        Home = 0,
        Shop,
        Equipment,
        Bullet,
        Helper
    }

    public class LobbyLayoutController : MonoBehaviour
    {
        [Header("Top / Middle")]
        [SerializeField] private Button enterStageButton;
        [SerializeField] private Button previousStageButton;
        [SerializeField] private Button nextStageButton;
        [SerializeField] private TMP_Text selectedStageText;
        [SerializeField] private TMP_Text stageSummaryText;

        [Header("Bottom Buttons")]
        [SerializeField] private List<LobbyBottomBtn> bottomButtons;

        [Header("Tab Panels")]
        [SerializeField] private IDialog shopPanel;
        [SerializeField] private IDialog equipmentPanel;
        [SerializeField] private IDialog bulletPanel;
        [SerializeField] private IDialog helperPanel;

        [SerializeField] private LobbyTab defaultTab = LobbyTab.Home;

        private int selectedStageIndex = 1;

        private void Awake()
        {
            if (enterStageButton != null) enterStageButton.onClick.AddListener(OnClickEnterStage);
            if (previousStageButton != null) previousStageButton.onClick.AddListener(OnClickPreviousStage);
            if (nextStageButton != null) nextStageButton.onClick.AddListener(OnClickNextStage);
            if (bottomButtons != null)
            {
                for (int i = 0; i < bottomButtons.Count; i++)
                {
                    if (bottomButtons[i] == null) continue;
                    bottomButtons[i].Init(ShowTab);
                }
            }
        }

        private void OnEnable()
        {
            selectedStageIndex = StageProgressManager.Instance != null ? StageProgressManager.Instance.GetSelectedStageIndex() : 1;
            ShowTab(defaultTab);
            RefreshStageUI();
        }

        private void OnDestroy()
        {
            if (enterStageButton != null) enterStageButton.onClick.RemoveListener(OnClickEnterStage);
            if (previousStageButton != null) previousStageButton.onClick.RemoveListener(OnClickPreviousStage);
            if (nextStageButton != null) nextStageButton.onClick.RemoveListener(OnClickNextStage);
        }

        public void OnClickEnterStage()
        {
            if (StageProgressManager.Instance != null)
            {
                if (!StageProgressManager.Instance.IsStageUnlocked(selectedStageIndex))
                    return;

                StageProgressManager.Instance.SelectStage(selectedStageIndex);
            }

            SceneFlowManager.LoadBattle();
        }

        public void OnClickPreviousStage()
        {
            SelectStage(selectedStageIndex - 1);
        }

        public void OnClickNextStage()
        {
            SelectStage(selectedStageIndex + 1);
        }

        public void SelectStage(int stageIndex)
        {
            int maxStage = Mathf.Max(1, StageDatabaseProvider.GetDatabase().StageCount);
            selectedStageIndex = Mathf.Clamp(stageIndex, 1, maxStage);
            if (StageProgressManager.Instance != null)
                StageProgressManager.Instance.SelectStage(selectedStageIndex);

            RefreshStageUI();
        }

        public void ShowTab(LobbyTab tab)
        {
            if (shopPanel != null) shopPanel.SetActive(tab == LobbyTab.Shop);
            if (equipmentPanel != null) equipmentPanel.SetActive(tab == LobbyTab.Equipment);
            if (bulletPanel != null) bulletPanel.SetActive(tab == LobbyTab.Bullet);
            if (helperPanel != null) helperPanel.SetActive(tab == LobbyTab.Helper);

            if (bottomButtons != null)
            {
                for (int i = 0; i < bottomButtons.Count; i++)
                {
                    LobbyBottomBtn button = bottomButtons[i];
                    if (button == null) continue;
                    button.SetState(button._tab == tab);
                }
            }
        }

        private void RefreshStageUI()
        {
            StageData stageData = StageDatabaseProvider.GetStage(selectedStageIndex);
            bool isUnlocked = StageProgressManager.Instance == null || StageProgressManager.Instance.IsStageUnlocked(selectedStageIndex);
            int highestUnlockedStage = StageProgressManager.Instance != null ? StageProgressManager.Instance.GetHighestUnlockedStageIndex() : 1;
            StageClearGrade bestGrade = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetBestClearGrade(selectedStageIndex)
                : StageClearGrade.None;

            if (selectedStageText != null)
            {
                selectedStageText.SetText(
                    isUnlocked
                        ? $"Stage {selectedStageIndex}"
                        : $"Stage {selectedStageIndex} Locked");
            }

            if (stageSummaryText != null)
            {
                if (stageData == null)
                {
                    stageSummaryText.SetText("No Stage Data");
                }
                else
                {
                    stageSummaryText.SetText(
                        $"Wave {stageData.totalWaves} / Start SP {stageData.initialSP} / Wave SP {stageData.waveClearSP} / Best {bestGrade}");
                }
            }

            if (enterStageButton != null)
                enterStageButton.interactable = isUnlocked;

            if (previousStageButton != null)
                previousStageButton.interactable = selectedStageIndex > 1;

            if (nextStageButton != null)
            {
                int maxStage = Mathf.Max(highestUnlockedStage, selectedStageIndex);
                nextStageButton.interactable = selectedStageIndex < Mathf.Min(StageDatabaseProvider.GetDatabase().StageCount, maxStage + 1);
            }
        }
    }
}
