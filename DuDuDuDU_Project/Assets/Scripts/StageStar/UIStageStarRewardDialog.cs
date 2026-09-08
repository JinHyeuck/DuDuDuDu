using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using OJ.DI;
using OJ.Hunting;
using OJ.Dice;
using OJ.Point;
using OJ.UI;

namespace OJ.StageStar
{
    public class UIStageStarRewardDialog : DialogBase
    {
        [SerializeField] private TMP_Text totalStarText;
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIStageStarRewardElement rewardElementTemplate;

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
                // 이 단계에 걸린 다이스. 보유 여부에 따라 <b>받게 될 것</b>이 달라지므로
                // 미리보기도 그 답을 그대로 쓴다 — 지급 때와 다른 그림을 보여 주면
                // "분명 다이스였는데 재화가 왔다" 가 된다.
                DiceType dice = DiceUnlockDatabaseProvider.Database.GetStarUnlock(requiredStars);
                DiceRewardView diceView = default;

                if (dice != DiceType.Max)
                {
                    DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
                    diceView = ownership != null && ownership.IsOwned(dice)
                        ? DiceRewardView.Refunded(dice, DiceOwnershipManager.GetPriceCurrency(dice),
                            ownership.GetPrice(dice))
                        : DiceRewardView.NewlyOwned(dice);
                }

                rewardElement.Bind(i, requiredStars, totalStars, isClaimed, isClaimable, ClaimReward,
                    dice, diceView);
            }
        }

        private void ClaimReward(int rewardIndex)
        {
            StageStarManager manager = StageStarManager.Instance;
            if (manager == null)
                return;

            if (!manager.TryClaimReward(rewardIndex, out List<PointRewardEntry> rewards,
                    out DiceRewardView diceView, out bool hasDice))
                return;

            Refresh();

            // 카탈로그에서 꺼내 띄운다. (10.4)
            //
            // 예전에는 결과창을 [SerializeField] 로 직접 가리켰다. 그 참조가 None 이면
            // 보상은 이미 지급돼 되돌릴 수 없는데 결과창만 아무 로그 없이 안 떴다 —
            // 유저에게는 버튼이 먹통인 것으로 보이고 단서는 하나도 안 남는다.
            //
            // Open 이 값을 채운 뒤 스스로 Enter 까지 부르므로 Show 가 아니라 Get 이다.
            // 다이스가 걸린 보상이면 처음 얻었든 환급이든 <b>둘 다</b> 결과창에 세운다.
            // 환급도 "그 다이스 때문에 받은 재화" 라는 것이 보여야 하고, 그 표현은
            // 딤 처리된 다이스 칸이 맡는다.
            List<DiceRewardView> diceRewards = hasDice
                ? new List<DiceRewardView> { diceView }
                : null;

            GameContainer.UI?.Get<UIRewardResultDialog>()
                ?.Open(rewards, diceRewards,
                    hasDice && diceView.WasNew ? "새 다이스를 얻었습니다!" : "보상을 획득했습니다.",
                    Refresh);
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
