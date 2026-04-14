using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIStageResultDialog : IDialog
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

        public void Open(bool isWin, int stageIndex, int reachedWaveCount, int bestStageIndex, IReadOnlyList<StageRewardEntry> rewards, Action onClose)
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


        private void BindRewards(IReadOnlyList<StageRewardEntry> rewards)
        {
            List<StageRewardEntry> mergedRewards = MergeRewards(rewards);

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

                StageRewardEntry reward = mergedRewards[i];
                rewardElement.Bind(GetPointIcon(reward.PointType), reward.Amount);
            }
        }

        private string BuildRewardLog(IReadOnlyList<StageRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return "[UIStageResultDialog] Result rewards: none";

            var parts = new List<string>(rewards.Count);
            for (int i = 0; i < rewards.Count; i++)
            {
                StageRewardEntry reward = rewards[i];
                Sprite icon = GetPointIcon(reward.PointType);
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

        private static List<StageRewardEntry> MergeRewards(IReadOnlyList<StageRewardEntry> rewards)
        {
            var merged = new Dictionary<PointType, int>();
            if (rewards != null)
            {
                for (int i = 0; i < rewards.Count; i++)
                {
                    StageRewardEntry reward = rewards[i];
                    if (reward.Amount <= 0)
                        continue;

                    if (!merged.ContainsKey(reward.PointType))
                        merged[reward.PointType] = 0;

                    merged[reward.PointType] += reward.Amount;
                }
            }

            var list = new List<StageRewardEntry>();
            foreach (KeyValuePair<PointType, int> pair in merged)
                list.Add(new StageRewardEntry(pair.Key, pair.Value));

            list.Sort((left, right) =>
            {
                if (left.PointType == PointType.Gold)
                    return -1;
                if (right.PointType == PointType.Gold)
                    return 1;
                return left.PointType.CompareTo(right.PointType);
            });

            return list;
        }

        private Sprite GetPointIcon(PointType pointType)
        {
            if (StaticResource.Instance == null || StaticResource.Instance.PointMetadataDatabase == null)
                return null;

            PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(pointType);
            return metadata != null ? metadata.icon : null;
        }
    }
}
