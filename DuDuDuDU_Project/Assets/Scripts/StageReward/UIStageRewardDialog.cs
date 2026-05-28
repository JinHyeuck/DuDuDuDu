using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIStageRewardDialog : IDialog
    {
        [Header("Milestones")]
        [SerializeField] private RectTransform[] milestoneSlots;
        [SerializeField] private UIStageRewardMilestoneItem milestoneTemplate;

        [Header("Reward Detail")]
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIRewardElement rewardElementTemplate;
        [SerializeField] private TMP_Text stateText;

        [Header("Buttons")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button claimButton;

        [Header("Reward Result")]
        [SerializeField] private UIRewardResultDialog rewardResultDialog;

        private readonly List<UIStageRewardMilestoneItem> milestoneItems = new List<UIStageRewardMilestoneItem>();
        private readonly List<UIRewardElement> rewardElements = new List<UIRewardElement>();

        private int selectedIndex = -1;

        protected override void OnLoad()
        {
            base.OnLoad();

            if (rewardRoot == null && rewardElementTemplate != null)
                rewardRoot = rewardElementTemplate.transform.parent as RectTransform;

            if (milestoneTemplate != null)
                milestoneTemplate.gameObject.SetActive(false);
            if (rewardElementTemplate != null)
                rewardElementTemplate.gameObject.SetActive(false);

            if (previousButton != null) previousButton.onClick.AddListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(SelectNext);
            if (claimButton != null) claimButton.onClick.AddListener(ClaimSelected);
        }

        protected override void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(SelectNext);
            if (claimButton != null) claimButton.onClick.RemoveListener(ClaimSelected);

            if (StageRewardManager.Instance != null)
                StageRewardManager.Instance.OnChanged -= Refresh;

            base.OnDestroy();
        }

        protected override void OnEnter()
        {
            base.OnEnter();

            if (StageRewardManager.Instance != null)
                StageRewardManager.Instance.OnChanged += Refresh;

            selectedIndex = StageRewardManager.Instance != null ? StageRewardManager.Instance.GetFocusIndex() : -1;
            Refresh();
        }

        protected override void OnExit()
        {
            if (StageRewardManager.Instance != null)
                StageRewardManager.Instance.OnChanged -= Refresh;

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

        private void SelectMilestone(UIStageRewardMilestoneItem item)
        {
            if (item == null || item.Milestone == null)
                return;

            int index = StageRewardDatabaseProvider.GetDatabase().IndexOf(item.Milestone.StableId);
            SelectIndex(index);
        }

        private void SelectIndex(int nextIndex)
        {
            IReadOnlyList<StageRewardMilestone> milestones = StageRewardDatabaseProvider.GetDatabase().Milestones;
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
            StageRewardManager manager = StageRewardManager.Instance;
            IReadOnlyList<StageRewardMilestone> milestones = StageRewardDatabaseProvider.GetDatabase().Milestones;
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

            EnsureMilestoneItems();
            RefreshMilestoneSlots(manager, milestones, totalCount);

            StageRewardMilestone selectedMilestone = milestones[selectedIndex];
            StageRewardState selectedState = manager != null ? manager.GetState(selectedMilestone) : StageRewardState.Locked;
            RefreshSelectedDetail(selectedMilestone, selectedState, totalCount);
        }

        private void RefreshMilestoneSlots(
            StageRewardManager manager,
            IReadOnlyList<StageRewardMilestone> milestones,
            int totalCount)
        {
            int slotCount = GetMilestoneSlotCount();
            int centerSlotIndex = slotCount / 2;

            for (int i = 0; i < milestoneItems.Count; i++)
            {
                UIStageRewardMilestoneItem item = milestoneItems[i];
                if (item == null)
                    continue;

                item.gameObject.SetActive(true);

                int milestoneIndex = selectedIndex + i - centerSlotIndex;
                bool hasMilestone = milestoneIndex >= 0 && milestoneIndex < totalCount;
                if (!hasMilestone)
                {
                    item.BindEmpty();
                    continue;
                }

                StageRewardMilestone milestone = milestones[milestoneIndex];
                StageRewardState state = manager != null ? manager.GetState(milestone) : StageRewardState.Locked;
                item.Bind(milestone, state, milestoneIndex == selectedIndex, SelectMilestone);
            }
        }

        private void SetEmptyState()
        {
            if (stateText != null)
                stateText.SetText(string.Empty);

            SetClaimButtonVisible(false);

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

        private void RefreshSelectedDetail(StageRewardMilestone milestone, StageRewardState state, int totalCount)
        {
            if (stateText != null)
            {
                switch (state)
                {
                    case StageRewardState.Claimable:
                        stateText.SetText("수령 가능");
                        break;
                    case StageRewardState.Claimed:
                        stateText.SetText("수령 완료");
                        break;
                    default:
                        stateText.SetText(milestone != null ? milestone.RequirementText : string.Empty);
                        break;
                }
            }

            SetClaimButtonVisible(state == StageRewardState.Claimable);

            if (previousButton != null)
                previousButton.interactable = selectedIndex > 0;
            if (nextButton != null)
                nextButton.interactable = selectedIndex >= 0 && selectedIndex < totalCount - 1;

            BindRewards(milestone != null ? milestone.rewards : null);
        }

        private void SetClaimButtonVisible(bool visible)
        {
            if (claimButton == null)
                return;

            claimButton.gameObject.SetActive(visible);
            claimButton.interactable = visible;
        }

        private void BindRewards(IReadOnlyList<StageRewardEntry> rewards)
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

                StageRewardEntry reward = rewards[i];
                rewardElement.Bind(PointRewardUtility.GetPointIcon(reward.pointType), reward.amount, "x{0:#,##0}");
            }
        }

        private void ClaimSelected()
        {
            StageRewardManager manager = StageRewardManager.Instance;
            IReadOnlyList<StageRewardMilestone> milestones = StageRewardDatabaseProvider.GetDatabase().Milestones;
            if (manager == null || milestones == null || selectedIndex < 0 || selectedIndex >= milestones.Count)
                return;

            if (!manager.TryClaim(milestones[selectedIndex], out List<PointRewardEntry> rewards))
                return;

            Refresh();

            if (rewardResultDialog != null)
            {
                rewardResultDialog.Open(rewards, "보상을 획득했습니다.", HandleClaimResultClosed);
                return;
            }

            HandleClaimResultClosed();
        }

        private void HandleClaimResultClosed()
        {
            if (StageRewardManager.Instance != null)
                selectedIndex = StageRewardManager.Instance.GetFocusIndex();

            Refresh();
        }

        private int GetMilestoneSlotCount()
        {
            return milestoneSlots != null ? milestoneSlots.Length : 0;
        }

        private void EnsureMilestoneItems()
        {
            int slotCount = GetMilestoneSlotCount();
            if (slotCount <= 0)
                return;

            while (milestoneItems.Count < slotCount)
                milestoneItems.Add(null);

            for (int i = 0; i < slotCount; i++)
            {
                RectTransform slot = milestoneSlots[i];
                if (slot == null)
                    continue;

                slot.gameObject.SetActive(true);

                if (milestoneItems[i] != null)
                    continue;

                UIStageRewardMilestoneItem item = slot.GetComponentInChildren<UIStageRewardMilestoneItem>(true);
                if (item == null || item == milestoneTemplate)
                {
                    if (milestoneTemplate == null)
                        continue;

                    item = Object.Instantiate(milestoneTemplate, slot);
                }

                RectTransform itemRect = item.transform as RectTransform;
                if (itemRect != null)
                    StretchToSlot(itemRect);

                item.gameObject.SetActive(false);
                milestoneItems[i] = item;
            }
        }

        private static void StretchToSlot(RectTransform rectTransform)
        {
            rectTransform.SetParent(rectTransform.parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
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
    }
}
