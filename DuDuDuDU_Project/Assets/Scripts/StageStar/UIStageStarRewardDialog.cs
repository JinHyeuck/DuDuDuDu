using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIStageStarRewardDialog : IDialog
    {
        [SerializeField] private TMP_Text totalStarText;
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIStageStarRewardElement rewardElementTemplate;
        [SerializeField] private UIRewardResultDialog rewardResultDialog;

        private readonly List<UIStageStarRewardElement> rewardElements = new List<UIStageStarRewardElement>();

        protected override void OnLoad()
        {
            base.OnLoad();

            if (rewardRoot == null && rewardElementTemplate != null)
                rewardRoot = rewardElementTemplate.transform.parent as RectTransform;

            if (rewardElementTemplate != null)
                rewardElementTemplate.gameObject.SetActive(false);
        }

        protected override void OnDestroy()
        {
            if (StageStarManager.Instance != null)
                StageStarManager.Instance.OnChanged -= Refresh;

            base.OnDestroy();
        }

        protected override void OnEnter()
        {
            base.OnEnter();

            if (StageStarManager.Instance != null)
                StageStarManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        protected override void OnExit()
        {
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
            StageStarManager manager = StageStarManager.Instance;
            int totalStars = manager != null ? manager.GetTotalStarCount() : 0;
            int maxStars = manager != null ? manager.GetMaxStarCount() : 0;
            int rewardCount = manager != null ? manager.GetRewardCount() : 0;

            if (totalStarText != null)
                totalStarText.SetText("{0}/{1}", totalStars, maxStars);

            EnsureRewardElements(rewardCount);

            for (int i = 0; i < rewardElements.Count; i++)
            {
                UIStageStarRewardElement rewardElement = rewardElements[i];
                if (rewardElement == null)
                    continue;

                bool shouldShow = i < rewardCount;
                rewardElement.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                int requiredStars = manager.GetRequiredStars(i);
                bool isClaimed = manager.IsRewardClaimed(i);
                bool isClaimable = manager.IsRewardClaimable(i);
                rewardElement.Bind(i, requiredStars, totalStars, isClaimed, isClaimable, ClaimReward);
            }
        }

        private void ClaimReward(int rewardIndex)
        {
            StageStarManager manager = StageStarManager.Instance;
            if (manager == null)
                return;

            if (!manager.TryClaimReward(rewardIndex, out List<PointRewardEntry> rewards))
                return;

            Refresh();

            if (rewardResultDialog != null)
                rewardResultDialog.Open(rewards, "\uBCF4\uC0C1\uC744 \uD68D\uB4DD\uD588\uC2B5\uB2C8\uB2E4.", Refresh);
        }

        private void EnsureRewardElements(int count)
        {
            if (rewardElementTemplate == null || rewardRoot == null)
                return;

            while (rewardElements.Count < count)
            {
                UIStageStarRewardElement rewardElement = Object.Instantiate(rewardElementTemplate, rewardRoot);
                rewardElement.gameObject.SetActive(true);
                rewardElements.Add(rewardElement);
            }
        }
    }
}
