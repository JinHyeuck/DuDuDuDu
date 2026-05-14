using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIChapterRewardDialog : IDialog
    {
        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text requirementText;

        [Header("Milestones")]
        [SerializeField] private RectTransform milestoneRoot;
        [SerializeField] private UIChapterRewardMilestoneItem milestoneTemplate;
        [SerializeField] private int visibleMilestoneCount = 3;

        [Header("Reward Detail")]
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIRewardElement rewardElementTemplate;
        [SerializeField] private TMP_Text stateText;

        [Header("Buttons")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button claimButton;

        [Header("Claim Result")]
        [SerializeField] private GameObject claimResultOverlay;
        [SerializeField] private TMP_Text claimResultText;
        [SerializeField] private RectTransform claimResultRewardRoot;
        [SerializeField] private UIRewardElement claimResultRewardTemplate;
        [SerializeField] private Button claimResultCloseButton;

        private readonly List<UIChapterRewardMilestoneItem> milestoneItems = new List<UIChapterRewardMilestoneItem>();
        private readonly List<UIRewardElement> rewardElements = new List<UIRewardElement>();
        private readonly List<UIRewardElement> claimResultRewardElements = new List<UIRewardElement>();

        private int selectedIndex = -1;
        private bool closeResultShouldAdvance;

        protected override void OnLoad()
        {
            base.OnLoad();

            if (titleText != null)
                titleText.SetText("챕터 보상");

            if (milestoneRoot == null && milestoneTemplate != null)
                milestoneRoot = milestoneTemplate.transform.parent as RectTransform;
            if (rewardRoot == null && rewardElementTemplate != null)
                rewardRoot = rewardElementTemplate.transform.parent as RectTransform;
            if (claimResultRewardRoot == null && claimResultRewardTemplate != null)
                claimResultRewardRoot = claimResultRewardTemplate.transform.parent as RectTransform;

            if (milestoneTemplate != null)
                milestoneTemplate.gameObject.SetActive(false);
            if (rewardElementTemplate != null)
                rewardElementTemplate.gameObject.SetActive(false);
            if (claimResultRewardTemplate != null)
                claimResultRewardTemplate.gameObject.SetActive(false);

            if (previousButton != null) previousButton.onClick.AddListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(SelectNext);
            if (claimButton != null) claimButton.onClick.AddListener(ClaimSelected);
            if (claimResultCloseButton != null) claimResultCloseButton.onClick.AddListener(CloseClaimResult);

            if (claimResultOverlay != null)
                claimResultOverlay.SetActive(false);
        }

        protected override void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(SelectNext);
            if (claimButton != null) claimButton.onClick.RemoveListener(ClaimSelected);
            if (claimResultCloseButton != null) claimResultCloseButton.onClick.RemoveListener(CloseClaimResult);

            if (ChapterRewardManager.Instance != null)
                ChapterRewardManager.Instance.OnChanged -= Refresh;

            base.OnDestroy();
        }

        protected override void OnEnter()
        {
            base.OnEnter();

            if (ChapterRewardManager.Instance != null)
                ChapterRewardManager.Instance.OnChanged += Refresh;

            selectedIndex = ChapterRewardManager.Instance != null ? ChapterRewardManager.Instance.GetFocusIndex() : -1;
            if (claimResultOverlay != null)
                claimResultOverlay.SetActive(false);

            Refresh();
        }

        protected override void OnExit()
        {
            if (ChapterRewardManager.Instance != null)
                ChapterRewardManager.Instance.OnChanged -= Refresh;

            base.OnExit();
        }

        public void Open()
        {
            Enter();
        }

        private void SelectPrevious()
        {
            SelectIndex(selectedIndex - 1);
        }

        private void SelectNext()
        {
            SelectIndex(selectedIndex + 1);
        }

        private void SelectMilestone(UIChapterRewardMilestoneItem item)
        {
            if (item == null || item.Milestone == null)
                return;

            int index = ChapterRewardDatabaseProvider.GetDatabase().IndexOf(item.Milestone.StableId);
            SelectIndex(index);
        }

        private void SelectIndex(int nextIndex)
        {
            IReadOnlyList<ChapterRewardMilestone> milestones = ChapterRewardDatabaseProvider.GetDatabase().Milestones;
            if (milestones == null || milestones.Count == 0)
            {
                selectedIndex = -1;
                Refresh();
                return;
            }

            selectedIndex = Mathf.Clamp(nextIndex, 0, milestones.Count - 1);
            Refresh();
        }

        private void Refresh()
        {
            ChapterRewardManager manager = ChapterRewardManager.Instance;
            IReadOnlyList<ChapterRewardMilestone> milestones = ChapterRewardDatabaseProvider.GetDatabase().Milestones;
            int totalCount = milestones != null ? milestones.Count : 0;

            if (totalCount <= 0)
            {
                selectedIndex = -1;
                SetEmptyState();
                return;
            }

            if (selectedIndex < 0)
                selectedIndex = manager != null ? manager.GetFocusIndex() : 0;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, totalCount - 1);

            EnsureMilestoneItems(Mathf.Max(1, visibleMilestoneCount));

            int visibleCount = Mathf.Min(Mathf.Max(1, visibleMilestoneCount), totalCount);
            int firstVisibleIndex = Mathf.Clamp(selectedIndex - (visibleCount / 2), 0, Mathf.Max(0, totalCount - visibleCount));

            for (int i = 0; i < milestoneItems.Count; i++)
            {
                UIChapterRewardMilestoneItem item = milestoneItems[i];
                if (item == null)
                    continue;

                bool shouldShow = i < visibleCount;
                item.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                int milestoneIndex = firstVisibleIndex + i;
                ChapterRewardMilestone milestone = milestones[milestoneIndex];
                ChapterRewardState state = manager != null ? manager.GetState(milestone) : ChapterRewardState.Locked;
                item.Bind(milestone, state, milestoneIndex == selectedIndex, SelectMilestone);
            }

            ChapterRewardMilestone selectedMilestone = milestones[selectedIndex];
            ChapterRewardState selectedState = manager != null ? manager.GetState(selectedMilestone) : ChapterRewardState.Locked;
            RefreshSelectedDetail(selectedMilestone, selectedState, totalCount);
        }

        private void SetEmptyState()
        {
            if (requirementText != null)
                requirementText.SetText("등록된 챕터 보상이 없습니다.");
            if (stateText != null)
                stateText.SetText(string.Empty);
            if (claimButton != null)
                claimButton.interactable = false;
            if (previousButton != null)
                previousButton.interactable = false;
            if (nextButton != null)
                nextButton.interactable = false;

            for (int i = 0; i < milestoneItems.Count; i++)
            {
                if (milestoneItems[i] != null)
                    milestoneItems[i].gameObject.SetActive(false);
            }

            BindRewards(null);
        }

        private void RefreshSelectedDetail(ChapterRewardMilestone milestone, ChapterRewardState state, int totalCount)
        {
            if (requirementText != null)
                requirementText.SetText(milestone != null ? milestone.RequirementText : string.Empty);

            if (stateText != null)
            {
                switch (state)
                {
                    case ChapterRewardState.Claimable:
                        stateText.SetText("수령 가능");
                        break;
                    case ChapterRewardState.Claimed:
                        stateText.SetText("수령 완료");
                        break;
                    default:
                        stateText.SetText(milestone != null ? milestone.RequirementText : string.Empty);
                        break;
                }
            }

            if (claimButton != null)
                claimButton.interactable = state == ChapterRewardState.Claimable;
            if (previousButton != null)
                previousButton.interactable = selectedIndex > 0;
            if (nextButton != null)
                nextButton.interactable = selectedIndex >= 0 && selectedIndex < totalCount - 1;

            BindRewards(milestone != null ? milestone.rewards : null);
        }

        private void BindRewards(IReadOnlyList<ChapterRewardEntry> rewards)
        {
            int count = rewards != null ? rewards.Count : 0;
            EnsureRewardElements(rewardElements, rewardElementTemplate, rewardRoot, count);

            for (int i = 0; i < rewardElements.Count; i++)
            {
                UIRewardElement rewardElement = rewardElements[i];
                if (rewardElement == null)
                    continue;

                bool shouldShow = i < count;
                rewardElement.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                ChapterRewardEntry reward = rewards[i];
                rewardElement.Bind(GetPointIcon(reward.pointType), reward.amount, "x{0:#,##0}");
            }
        }

        private void ClaimSelected()
        {
            ChapterRewardManager manager = ChapterRewardManager.Instance;
            IReadOnlyList<ChapterRewardMilestone> milestones = ChapterRewardDatabaseProvider.GetDatabase().Milestones;
            if (manager == null || milestones == null || selectedIndex < 0 || selectedIndex >= milestones.Count)
                return;

            if (!manager.TryClaim(milestones[selectedIndex], out List<StageRewardEntry> rewards))
                return;

            ShowClaimResult(rewards);
            Refresh();
        }

        private void ShowClaimResult(IReadOnlyList<StageRewardEntry> rewards)
        {
            if (claimResultOverlay == null)
                return;

            claimResultOverlay.SetActive(true);
            closeResultShouldAdvance = true;

            if (claimResultText != null)
                claimResultText.SetText("보상을 획득하였습니다.");

            int count = rewards != null ? rewards.Count : 0;
            EnsureRewardElements(claimResultRewardElements, claimResultRewardTemplate, claimResultRewardRoot, count);

            for (int i = 0; i < claimResultRewardElements.Count; i++)
            {
                UIRewardElement rewardElement = claimResultRewardElements[i];
                if (rewardElement == null)
                    continue;

                bool shouldShow = i < count;
                rewardElement.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                StageRewardEntry reward = rewards[i];
                rewardElement.Bind(GetPointIcon(reward.PointType), reward.Amount, "x{0:#,##0}");
            }
        }

        private void CloseClaimResult()
        {
            if (claimResultOverlay != null)
                claimResultOverlay.SetActive(false);

            if (closeResultShouldAdvance && ChapterRewardManager.Instance != null)
                selectedIndex = ChapterRewardManager.Instance.GetFocusIndex();

            closeResultShouldAdvance = false;
            Refresh();
        }

        private void EnsureMilestoneItems(int count)
        {
            if (milestoneTemplate == null || milestoneRoot == null)
                return;

            while (milestoneItems.Count < count)
            {
                UIChapterRewardMilestoneItem item = Object.Instantiate(milestoneTemplate, milestoneRoot);
                item.gameObject.SetActive(true);
                milestoneItems.Add(item);
            }
        }

        private static void EnsureRewardElements(
            List<UIRewardElement> elements,
            UIRewardElement template,
            RectTransform root,
            int count)
        {
            if (elements == null || template == null || root == null)
                return;

            while (elements.Count < count)
            {
                UIRewardElement rewardElement = Object.Instantiate(template, root);
                rewardElement.gameObject.SetActive(true);
                elements.Add(rewardElement);
            }
        }

        private static Sprite GetPointIcon(PointType pointType)
        {
            if (!StaticResource.isAlive || StaticResource.Instance == null || StaticResource.Instance.PointMetadataDatabase == null)
                return null;

            PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(pointType);
            return metadata != null ? metadata.icon : null;
        }
    }
}
