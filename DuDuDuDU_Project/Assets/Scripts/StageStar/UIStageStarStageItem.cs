using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIStageStarStageItem : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text stageTitleText;
        [SerializeField] private TMP_Text stageSummaryText;

        [Header("Visuals")]
        [SerializeField] private Image bannerImage;
        [SerializeField] private Sprite[] bannerSprites;
        [SerializeField] private GameObject lockedRoot;
        [SerializeField] private GameObject perfectRoot;

        [Header("Conditions")]
        [SerializeField] private UIStageStarConditionRow minimumRow;
        [SerializeField] private UIStageStarConditionRow halfRow;
        [SerializeField] private UIStageStarConditionRow perfectRow;
        [SerializeField] private string minimumConditionText = "\uD074\uB9AC\uC5B4";
        [SerializeField] private string halfConditionText = "HP 50% \uC774\uC0C1 \uC0C1\uD0DC\uB85C \uD074\uB9AC\uC5B4";
        [SerializeField] private string perfectConditionText = "HP 100% \uC0C1\uD0DC\uB85C \uD074\uB9AC\uC5B4";

        [Header("Input")]
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startButtonText;
        [SerializeField] private bool hideStartButtonWhenPerfect = true;

        private int stageIndex;
        private Action<int> onStartStage;

        private void Awake()
        {
            if (startButton != null)
                startButton.onClick.AddListener(HandleStartClicked);
        }

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(HandleStartClicked);
        }

        public void Bind(StageData stageData, StageClearGrade bestGrade, bool isUnlocked, Action<int> startCallback)
        {
            stageIndex = stageData != null ? Mathf.Max(1, stageData.stageIndex) : 1;
            onStartStage = startCallback;

            if (stageTitleText != null)
                stageTitleText.SetText("{0}. {1}", stageIndex, GetStageDisplayName(stageIndex));

            if (stageSummaryText != null)
                stageSummaryText.SetText("{0}/3", StageStarUtility.GetStarCount(bestGrade));

            ApplyBanner(stageIndex);

            if (minimumRow != null)
                minimumRow.Bind(StageClearGrade.Minimum, bestGrade, minimumConditionText);
            if (halfRow != null)
                halfRow.Bind(StageClearGrade.Half, bestGrade, halfConditionText);
            if (perfectRow != null)
                perfectRow.Bind(StageClearGrade.Perfect, bestGrade, perfectConditionText);

            bool isPerfect = bestGrade >= StageClearGrade.Perfect;
            if (lockedRoot != null)
                lockedRoot.SetActive(!isUnlocked);
            if (perfectRoot != null)
                perfectRoot.SetActive(isPerfect);

            if (startButton != null)
            {
                bool showStartButton = isUnlocked && (!isPerfect || !hideStartButtonWhenPerfect);
                startButton.gameObject.SetActive(showStartButton);
                startButton.interactable = showStartButton;
            }

            if (startButtonText != null)
                startButtonText.SetText(isUnlocked ? "\uAC8C\uC784 \uC2DC\uC791" : "\uC7A0\uAE40");
        }

        private void ApplyBanner(int nextStageIndex)
        {
            if (bannerImage == null || bannerSprites == null || bannerSprites.Length == 0)
                return;

            int spriteIndex = (Mathf.Max(1, nextStageIndex) - 1) % bannerSprites.Length;
            Sprite sprite = bannerSprites[spriteIndex];
            if (sprite != null)
                bannerImage.sprite = sprite;
        }

        private static string GetStageDisplayName(int nextStageIndex)
        {
            switch (nextStageIndex)
            {
                case 1:
                    return "\uC5B4\uB460\uC758 \uC232\uC18D";
                case 2:
                    return "\uACA8\uC6B8 \uC232\uC18D";
                case 3:
                    return "\uC0AC\uB9C9 \uB3C4\uC2DC";
                default:
                    return string.Format("Stage {0}", nextStageIndex);
            }
        }

        private void HandleStartClicked()
        {
            onStartStage?.Invoke(stageIndex);
        }
    }
}
