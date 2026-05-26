using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIRewardResultDialog : IDialog
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIRewardElement rewardElementTemplate;

        private readonly List<UIRewardElement> rewardElements = new List<UIRewardElement>();
        private Action closeAction;

        protected override void OnLoad()
        {
            if (rewardRoot == null && rewardElementTemplate != null)
                rewardRoot = rewardElementTemplate.transform.parent as RectTransform;

            if (rewardElementTemplate != null)
                rewardElementTemplate.gameObject.SetActive(false);

        }

        protected override void OnExit()
        {
            Action callback = closeAction;
            closeAction = null;
            callback?.Invoke();
        }

        public void Open(IReadOnlyList<PointRewardEntry> rewards, string message, Action onClose = null)
        {
            if (!_isLoaded)
                Load();

            closeAction = onClose;

            if (messageText != null)
                messageText.SetText(string.IsNullOrEmpty(message) ? "보상을 획득했습니다." : message);

            BindRewards(rewards);
            Enter();
        }

        private void BindRewards(IReadOnlyList<PointRewardEntry> rewards)
        {
            List<PointRewardEntry> mergedRewards = PointRewardUtility.MergeRewards(rewards);
            EnsureRewardElements(mergedRewards.Count);

            for (int i = 0; i < rewardElements.Count; i++)
            {
                UIRewardElement rewardElement = rewardElements[i];
                if (rewardElement == null)
                    continue;

                bool shouldShow = i < mergedRewards.Count;
                rewardElement.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                PointRewardEntry reward = mergedRewards[i];
                rewardElement.Bind(PointRewardUtility.GetPointIcon(reward.PointType), reward.Amount, "x{0:#,##0}");
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
    }
}
