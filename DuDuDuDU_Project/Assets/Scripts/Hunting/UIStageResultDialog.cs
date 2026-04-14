using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIStageResultDialog : IDialog
    {
        private const string WinSpritePath = "Art/Layout/Reward_Win";
        private const string FailSpritePath = "Art/Layout/Reward_Failed";

        [Serializable]
        private sealed class RewardSlot
        {
            public RectTransform Root;
            public Image Icon;
            public TMP_Text AmountText;
        }

        [Header("State")]
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Image dimmedBackgroundImage;
        [SerializeField] private List<TMP_Text> stageLabelText;
        [SerializeField] private TMP_Text mainValueText;
        [SerializeField] private TMP_Text bestStageLabelText;
        [SerializeField] private TMP_Text bestStageValueText;
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private List<RewardSlot> rewardSlots = new List<RewardSlot>();

        private Action closeAction;
        [SerializeField] private Transform winSprite;
        [SerializeField] private Transform failSprite;

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

            for (int i = 0; i < rewardSlots.Count; i++)
            {
                RewardSlot slot = rewardSlots[i];
                if (slot == null || slot.Root == null)
                    continue;

                bool shouldShow = i < mergedRewards.Count;
                slot.Root.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                StageRewardEntry reward = mergedRewards[i];
                if (slot.AmountText != null)
                    slot.AmountText.SetText("{0:N0}", reward.Amount);

                if (slot.Icon != null)
                    slot.Icon.sprite = GetPointIcon(reward.PointType);
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
