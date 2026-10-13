using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Object = UnityEngine.Object;
using OJ.Point;
using OJ.UI;

namespace OJ.Hunting
{
    public class UIStageResultDialog : DialogBase
    {
        [Header("State")]
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private List<TMP_Text> stageLabelText;
        [SerializeField] private TMP_Text mainValueText;
        [SerializeField] private TMP_Text bestStageLabelText;
        [SerializeField] private TMP_Text bestStageValueText;
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIRewardElement rewardElementTemplate;

        private Action closeAction;
        [SerializeField] private Transform winSprite;
        [SerializeField] private Transform failSprite;
        private readonly List<UIRewardElement> rewardElements = new List<UIRewardElement>();

        /// <summary>지금 보이는 보상 칸 수. <see cref="AppendItemReward"/> 가 그 다음 칸에 붙인다.</summary>
        private int shownRewardCount;

        protected override void OnLoad()
        {
            if (rewardRoot == null && rewardElementTemplate != null)
                rewardRoot = rewardElementTemplate.transform.parent as RectTransform;

            bool hasSceneTemplate = rewardElementTemplate != null && rewardElementTemplate.gameObject.scene.IsValid();

            if (hasSceneTemplate)
                rewardElementTemplate.gameObject.SetActive(false);

            if (rewardRoot != null)
            {
                for (int i = 0; i < rewardRoot.childCount; i++)
                {
                    Transform child = rewardRoot.GetChild(i);
                    if (hasSceneTemplate && child == rewardElementTemplate.transform)
                        continue;

                    child.gameObject.SetActive(false);
                }
            }
        }

        protected override void OnExit()
        {
            Action callback = closeAction;
            closeAction = null;
            callback?.Invoke();
        }

        public void Open(bool isWin, int stageIndex, int reachedWaveCount, int bestStageIndex, IReadOnlyList<PointRewardEntry> rewards, Action onClose)
        {
            closeAction = onClose;

            if (winSprite != null)
                winSprite.gameObject.SetActive(isWin);

            if (failSprite != null)
                failSprite.gameObject.SetActive(!isWin);

            if (stageLabelText != null)
            {
                foreach (var text in stageLabelText)
                {
                    text.SetText("Stage {0}", Mathf.Max(1, stageIndex));
                }
            }

            if (mainValueText != null)
                mainValueText.SetText("{0}", Mathf.Max(0, reachedWaveCount));

            if (bestStageLabelText != null)
                bestStageLabelText.SetText("Best Stage");

            if (bestStageValueText != null)
                bestStageValueText.SetText("{0}", Mathf.Max(1, bestStageIndex));

            BindRewards(rewards);
            Enter();
        }


        private void BindRewards(IReadOnlyList<PointRewardEntry> rewards)
        {
            List<PointRewardEntry> mergedRewards = PointRewardUtility.MergeRewards(rewards);
            shownRewardCount = mergedRewards.Count;

            Debug.Log(BuildRewardLog(mergedRewards));

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
                rewardElement.Bind(PointRewardUtility.GetPointIcon(reward.PointType), reward.Amount);
            }
        }

        /// <summary>
        /// 재화가 아닌 보상(다이스 등)을 한 칸 더 그린다. <see cref="Open"/> 뒤에 부른다.
        ///
        /// <b>왜 따로인가.</b> 재화 보상은 <c>PointRewardEntry</c> 로 오고 아이콘도 재화 표에서 찾는다.
        /// 다이스는 그 표에 없어서 같은 목록에 넣을 수 없다. 칸 모양은 같은 것을 쓴다.
        /// </summary>
        public void AppendItemReward(Sprite icon, int amount)
        {
            EnsureRewardElements(shownRewardCount + 1);
            if (shownRewardCount >= rewardElements.Count)
                return;

            UIRewardElement element = rewardElements[shownRewardCount];
            if (element == null)
                return;

            element.gameObject.SetActive(true);
            element.Bind(icon, amount);
            shownRewardCount++;
        }

        private string BuildRewardLog(IReadOnlyList<PointRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return "[UIStageResultDialog] Result rewards: none";

            var parts = new List<string>(rewards.Count);
            for (int i = 0; i < rewards.Count; i++)
            {
                PointRewardEntry reward = rewards[i];
                Sprite icon = PointRewardUtility.GetPointIcon(reward.PointType);
                string iconName = icon != null ? icon.name : "null";
                parts.Add($"{reward.PointType}:{reward.Amount} (icon:{iconName})");
            }

            return $"[UIStageResultDialog] Result rewards: {string.Join(", ", parts)}";
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
