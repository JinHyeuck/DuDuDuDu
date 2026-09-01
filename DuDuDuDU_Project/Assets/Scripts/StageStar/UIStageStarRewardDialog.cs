using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using OJ.DI;
using OJ.Hunting;
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

            // 카탈로그에서 꺼내 띄운다. (10.4)
            //
            // 예전에는 결과창을 [SerializeField] 로 직접 가리켰다. 그 참조가 None 이면
            // 보상은 이미 지급돼 되돌릴 수 없는데 결과창만 아무 로그 없이 안 떴다 —
            // 유저에게는 버튼이 먹통인 것으로 보이고 단서는 하나도 안 남는다.
            //
            // Open 이 값을 채운 뒤 스스로 Enter 까지 부르므로 Show 가 아니라 Get 이다.
            GameContainer.UI?.Get<UIRewardResultDialog>()
                ?.Open(rewards, "보상을 획득했습니다.", Refresh);
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
