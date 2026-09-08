using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using OJ.Dice;
using OJ.Point;
using OJ.UI;

namespace OJ.Hunting
{
    public class UIRewardResultDialog : DialogBase
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
            Open(rewards, null, message, onClose);
        }

        /// <summary>
        /// 재화와 함께 <b>다이스</b>도 보여 준다. (다이스 언락)
        ///
        /// <b>새 칸을 만들지 않았다.</b> <c>UIRewardElement</c> 는 아이콘 + 숫자 칸이라
        /// 재화든 다이스든 같은 모양으로 들어간다 — 그쪽에 <c>BindDice</c> 를 더해
        /// 네 화면이 한 번에 따라오게 했다.
        ///
        /// <b>환급은 재화 칸으로 또 그리지 않는다.</b> 이미 가진 다이스가 재화로 돌아온
        /// 경우 그 사실을 말하는 것은 <b>딤 처리된 다이스 칸</b>이다. 같은 재화를 옆에
        /// 한 번 더 세우면 두 번 받은 것처럼 보인다.
        /// </summary>
        public void Open(
            IReadOnlyList<PointRewardEntry> rewards,
            IReadOnlyList<DiceRewardView> diceRewards,
            string message,
            Action onClose = null)
        {
            if (!_isLoaded)
                Load();

            closeAction = onClose;

            if (messageText != null)
                messageText.SetText(string.IsNullOrEmpty(message) ? "보상을 획득했습니다." : message);

            BindRewards(rewards, diceRewards);
            Enter();
        }

        private void BindRewards(IReadOnlyList<PointRewardEntry> rewards, IReadOnlyList<DiceRewardView> diceRewards)
        {
            List<PointRewardEntry> mergedRewards = PointRewardUtility.MergeRewards(rewards);
            int diceCount = diceRewards != null ? diceRewards.Count : 0;

            // 다이스 칸이 말해 줄 환급은 재화 목록에서 뺀다. 지급은 이미 끝났고
            // 여기서 하는 것은 <b>보여 주는 일</b>뿐이라, 빼도 받은 것은 그대로다.
            for (int i = 0; i < diceCount; i++)
            {
                DiceRewardView view = diceRewards[i];
                if (view.WasNew || view.RefundAmount <= 0)
                    continue;

                for (int j = 0; j < mergedRewards.Count; j++)
                {
                    if (mergedRewards[j].PointType != view.RefundCurrency)
                        continue;

                    int left = mergedRewards[j].Amount - view.RefundAmount;
                    if (left > 0)
                        mergedRewards[j] = new PointRewardEntry(view.RefundCurrency, left);
                    else
                        mergedRewards.RemoveAt(j);

                    break;
                }
            }

            int total = mergedRewards.Count + diceCount;
            EnsureRewardElements(total);

            for (int i = 0; i < rewardElements.Count; i++)
            {
                UIRewardElement rewardElement = rewardElements[i];
                if (rewardElement == null)
                    continue;

                bool shouldShow = i < total;
                rewardElement.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                if (i < mergedRewards.Count)
                {
                    PointRewardEntry reward = mergedRewards[i];
                    rewardElement.Bind(PointRewardUtility.GetPointIcon(reward.PointType), reward.Amount, "x{0:#,##0}");
                    continue;
                }

                // 다이스는 재화 뒤에 이어 붙인다.
                DiceRewardView view = diceRewards[i - mergedRewards.Count];
                Sprite diceSprite = DiceMetaDataProvider.GetIcon(view.DiceType);

                if (view.WasNew)
                    rewardElement.BindDice(diceSprite);
                else
                    rewardElement.BindDice(diceSprite,
                        PointRewardUtility.GetPointIcon(view.RefundCurrency), view.RefundAmount);
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
