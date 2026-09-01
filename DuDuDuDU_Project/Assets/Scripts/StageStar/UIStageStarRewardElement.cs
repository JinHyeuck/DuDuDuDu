using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Hunting;
using OJ.Point;

namespace OJ.StageStar
{
    public class UIStageStarRewardElement : MonoBehaviour
    {
        [SerializeField] private TMP_Text requirementText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private UIRewardElement rewardElement;
        [SerializeField] private Button claimButton;
        [SerializeField] private TMP_Text claimButtonText;
        [SerializeField] private GameObject claimedRoot;
        [SerializeField] private GameObject redDot;

        private int rewardIndex;
        private Action<int> onClaim;

        private void Awake()
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(HandleClaimClicked);
        }

        private void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(HandleClaimClicked);
        }

        public void Bind(
            int nextRewardIndex,
            int requiredStars,
            int totalStars,
            bool isClaimed,
            bool isClaimable,
            Action<int> claimCallback)
        {
            rewardIndex = nextRewardIndex;
            onClaim = claimCallback;

            if (requirementText != null)
                requirementText.SetText("\uB204\uC801 \uBCC4 {0}\uAC1C \uD68D\uB4DD", requiredStars);

            if (progressText != null)
                progressText.SetText("{0}/{1}", Mathf.Min(totalStars, requiredStars), requiredStars);

            if (rewardElement != null)
            {
                rewardElement.gameObject.SetActive(true);
                rewardElement.Bind(
                    PointRewardUtility.GetPointIcon(PointType.Dia),
                    StageStarUtility.DiaRewardAmount,
                    "x{0:#,##0}");
            }

            if (claimButton != null)
            {
                claimButton.gameObject.SetActive(!isClaimed);
                claimButton.interactable = isClaimable;
            }

            if (claimButtonText != null)
                claimButtonText.SetText(isClaimable ? "\uD68D\uB4DD" : "\uBBF8\uB2EC\uC131");

            if (claimedRoot != null)
                claimedRoot.SetActive(isClaimed);

            if (redDot != null)
                redDot.SetActive(isClaimable);
        }

        private void HandleClaimClicked()
        {
            onClaim?.Invoke(rewardIndex);
        }
    }
}
