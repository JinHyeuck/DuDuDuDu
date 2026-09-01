using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using OJ.DI;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.UI;

namespace OJ.StageStar
{
    public class UIStageStarDialog : DialogBase
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

        private readonly List<UIStageStarStageItem> stageItems = new List<UIStageStarStageItem>();

        protected override void OnLoad()
        {
            base.OnLoad();

            if (stageRoot == null && stageItemTemplate != null)
                stageRoot = stageItemTemplate.transform.parent as RectTransform;

            // 템플릿이 <b>씬 안의 오브젝트일 때만</b> 끈다.
            //
            // 지금 이 필드에는 프로젝트의 프리팹 에셋이 꽂혀 있다(씬 자식이 아니다).
            // 에셋에 대고 SetActive(false) 를 부르면 에디터에서 <b>에셋 자체가 수정된다</b> —
            // 실제로 UIStageStarStageItem.prefab 이 m_IsActive: 0 인 채로 커밋돼 있는데
            // 그게 이 줄이 남긴 자국이다. 플레이할 때마다 프로젝트가 더러워지고,
            // 어느 날 그 변경이 의도치 않게 커밋된다.
            //
            // 클론은 EnsureStageItems 가 만든 직후 SetActive(true) 로 켜므로 템플릿이
            // 켜져 있든 꺼져 있든 결과는 같다. 씬에 놓인 템플릿을 화면에서 감추는 것이
            // 이 줄의 원래 목적이었고, 그 경우에만 필요하다.
            if (stageItemTemplate != null && stageItemTemplate.gameObject.scene.IsValid())
                stageItemTemplate.gameObject.SetActive(false);

            if (rewardButton != null)
                rewardButton.onClick.AddListener(OpenRewardDialog);
        }

        protected override void OnDestroy()
        {
            if (rewardButton != null)
                rewardButton.onClick.RemoveListener(OpenRewardDialog);

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
            // 예전에는 씬 인스턴스를 [SerializeField] 로 직접 가리켰다. 그 참조가 None 이면
            // null 검사에 조용히 걸러져, 배선이 끊긴 줄도 모른 채 버튼만 먹통이 됐다.
            GameContainer.UI?.Show<UIStageStarRewardDialog>();
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
