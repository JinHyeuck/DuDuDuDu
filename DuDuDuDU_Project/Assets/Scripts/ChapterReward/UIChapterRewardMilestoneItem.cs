using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIChapterRewardMilestoneItem : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text chapterText;
        [SerializeField] private TMP_Text requirementText;
        [SerializeField] private TMP_Text lockedText;
        [SerializeField] private TMP_Text claimedText;

        [Header("State")]
        [SerializeField] private GameObject selectedRoot;
        [SerializeField] private GameObject claimableRoot;
        [SerializeField] private GameObject lockedRoot;
        [SerializeField] private GameObject claimedRoot;
        [SerializeField] private GameObject redDot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float unselectedScale = 0.82f;
        [SerializeField] private float lockedAlpha = 0.58f;

        [Header("Rewards")]
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIRewardElement rewardElementTemplate;

        [Header("Input")]
        [SerializeField] private Button selectButton;

        private readonly List<UIRewardElement> rewardElements = new List<UIRewardElement>();
        private Action<UIChapterRewardMilestoneItem> onSelected;
        private ChapterRewardMilestone milestone;

        public ChapterRewardMilestone Milestone => milestone;

        private void Awake()
        {
            if (selectButton != null)
                selectButton.onClick.AddListener(HandleSelectClicked);

            if (rewardRoot == null && rewardElementTemplate != null)
                rewardRoot = rewardElementTemplate.transform.parent as RectTransform;

            if (rewardElementTemplate != null)
                rewardElementTemplate.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (selectButton != null)
                selectButton.onClick.RemoveListener(HandleSelectClicked);
        }

        public void Bind(
            ChapterRewardMilestone nextMilestone,
            ChapterRewardState state,
            bool isSelected,
            Action<UIChapterRewardMilestoneItem> selectedCallback)
        {
            milestone = nextMilestone;
            onSelected = selectedCallback;

            bool hasMilestone = milestone != null;
            gameObject.SetActive(hasMilestone);
            if (!hasMilestone)
                return;

            if (chapterText != null)
                chapterText.SetText("{0}", Mathf.Max(1, milestone.chapterIndex));

            if (requirementText != null)
                requirementText.SetText(milestone.ShortRequirementText);

            if (lockedText != null)
                lockedText.SetText(milestone.RequirementText);

            if (claimedText != null)
                claimedText.SetText("수령 완료");

            if (selectedRoot != null)
                selectedRoot.SetActive(isSelected);
            if (claimableRoot != null)
                claimableRoot.SetActive(state == ChapterRewardState.Claimable);
            if (lockedRoot != null)
                lockedRoot.SetActive(state == ChapterRewardState.Locked);
            if (claimedRoot != null)
                claimedRoot.SetActive(state == ChapterRewardState.Claimed);
            if (redDot != null)
                redDot.SetActive(state == ChapterRewardState.Claimable);

            transform.localScale = Vector3.one * (isSelected ? 1f : Mathf.Max(0.1f, unselectedScale));

            if (canvasGroup != null)
            {
                canvasGroup.alpha = state == ChapterRewardState.Locked ? lockedAlpha : 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            BindRewards(milestone.rewards);
        }

        private void BindRewards(IReadOnlyList<ChapterRewardEntry> rewards)
        {
            int count = rewards != null ? rewards.Count : 0;
            EnsureRewardElements(count);

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

        private void EnsureRewardElements(int count)
        {
            if (rewardElementTemplate == null || rewardRoot == null)
                return;

            while (rewardElements.Count < count)
            {
                UIRewardElement rewardElement = Object.Instantiate(rewardElementTemplate, rewardRoot);
                rewardElement.gameObject.SetActive(true);
                rewardElements.Add(rewardElement);
            }
        }

        private void HandleSelectClicked()
        {
            onSelected?.Invoke(this);
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
