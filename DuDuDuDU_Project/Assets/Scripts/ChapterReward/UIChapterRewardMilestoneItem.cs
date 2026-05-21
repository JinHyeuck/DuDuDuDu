using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIChapterRewardMilestoneItem : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text chapterText;
        [SerializeField] private TMP_Text requirementText;

        [Header("State")]
        [SerializeField] private GameObject selectedRoot;
        [SerializeField] private GameObject redDot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float unselectedScale = 0.82f;
        [SerializeField, Range(0f, 1f)] private float selectedAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float unselectedAlpha = 0.65f;
        [SerializeField] private float lockedAlpha = 0.58f;

        [Header("Input")]
        [SerializeField] private Button selectButton;

        private Action<UIChapterRewardMilestoneItem> onSelected;
        private ChapterRewardMilestone milestone;

        public ChapterRewardMilestone Milestone => milestone;

        private void Awake()
        {
            ResolveReferences();

            if (selectButton != null)
                selectButton.onClick.AddListener(HandleSelectClicked);
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
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

            if (milestone == null)
                return;

            if (chapterText != null)
                chapterText.SetText("Chapter {0}", Mathf.Max(1, milestone.chapterIndex));

            if (requirementText != null)
                requirementText.SetText("{0}", Mathf.Max(1, milestone.requiredWaveIndex));

            if (selectedRoot != null && selectedRoot != gameObject)
                selectedRoot.SetActive(isSelected);
            if (redDot != null)
                redDot.SetActive(state == ChapterRewardState.Claimable);

            transform.localScale = Vector3.one * (isSelected ? 1f : Mathf.Max(0.1f, unselectedScale));

            if (canvasGroup != null)
            {
                float alpha = selectedAlpha;
                if (!isSelected)
                {
                    alpha = unselectedAlpha;
                }

                if (!isSelected && state == ChapterRewardState.Locked)
                    alpha = Mathf.Min(alpha, lockedAlpha);

                canvasGroup.alpha = alpha;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public void BindEmpty()
        {
            milestone = null;
            onSelected = null;

            gameObject.SetActive(true);

            if (chapterText != null)
                chapterText.SetText(string.Empty);

            if (requirementText != null)
                requirementText.SetText(string.Empty);

            if (selectedRoot != null && selectedRoot != gameObject)
                selectedRoot.SetActive(false);

            if (redDot != null)
                redDot.SetActive(false);

            transform.localScale = Vector3.one * Mathf.Max(0.1f, unselectedScale);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = unselectedAlpha;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void HandleSelectClicked()
        {
            onSelected?.Invoke(this);
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
