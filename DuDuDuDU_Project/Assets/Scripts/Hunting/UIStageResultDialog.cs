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
            // 큰 숫자 칸은 원래 한두 자리 숫자 크기다. "15/20" 처럼 길어지면 줄이 깨지므로
            // 칸을 넘을 때만 글자를 줄인다. 상한은 프리팹에 정해 둔 크기 그대로다.
            if (mainValueText != null)
            {
                mainValueText.textWrappingMode = TextWrappingModes.NoWrap;
                mainValueText.fontSizeMax = mainValueText.fontSize;
                mainValueText.fontSizeMin = mainValueText.fontSize * 0.4f;
                mainValueText.enableAutoSizing = true;
            }

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

        /// <summary>
        /// 결과를 채우고 연다.
        ///
        /// <b>아래 칸은 "그 스테이지의 최고 웨이브" 다.</b> 예전에는 "Best Stage" 로 지금까지 열린
        /// 가장 높은 스테이지를 보였는데, 방금 한 판과 상관없는 숫자라 무엇을 뜻하는지 애매했다.
        /// 이번 판의 웨이브(큰 숫자)와 나란히 놓여야 "이번에 얼마나 갔고, 이 스테이지 최고는 얼마인가" 로 읽힌다.
        /// </summary>
        /// <param name="reachedWaveCount">이번 판에 깬 웨이브 수.</param>
        /// <param name="totalWaves">이 스테이지의 전체 웨이브 수. 큰 숫자 칸에 "깬 수/전체" 로 나온다.</param>
        /// <param name="bestWave">이 스테이지에서 가장 멀리 간 웨이브. 이번 판을 포함한다.</param>
        public void Open(bool isWin, int stageIndex, int reachedWaveCount, int totalWaves, int bestWave, IReadOnlyList<PointRewardEntry> rewards, Action onClose)
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
                // 이번 판 웨이브가 주인공이다. "/전체" 는 작게 붙여 기준만 알린다.
                mainValueText.SetText("{0}<size=55%>/{1}</size>", Mathf.Max(0, reachedWaveCount), Mathf.Max(1, totalWaves));

            if (bestStageLabelText != null)
                bestStageLabelText.SetText("최고 기록");

            if (bestStageValueText != null)
                bestStageValueText.SetText("Wave {0}", Mathf.Max(Mathf.Max(0, reachedWaveCount), bestWave));

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
        /// 스테이지 이름 칸("Stage N")을 다른 글자로 바꾼다. <see cref="Open"/> 뒤에 부른다.
        /// 스테이지가 아닌 판(튜토리얼)이 같은 결과창을 쓸 때 쓴다.
        /// </summary>
        public void SetStageLabel(string label)
        {
            if (stageLabelText == null)
                return;

            foreach (var text in stageLabelText)
            {
                if (text != null)
                    text.SetText(label);
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
