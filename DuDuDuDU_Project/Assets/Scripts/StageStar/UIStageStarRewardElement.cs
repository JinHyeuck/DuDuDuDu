using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Dice;
using OJ.Hunting;
using OJ.Point;

namespace OJ.StageStar
{
    public class UIStageStarRewardElement : MonoBehaviour
    {
        [SerializeField] private TMP_Text requirementText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private UIRewardElement rewardElement;

        /// <summary>
        /// 다이스가 걸린 단계에만 켜지는 두 번째 칸.
        ///
        /// <b>수령하기 <i>전에</i> 보여야 뜻이 있다.</b> 이 화면의 목적은 별을 모으게 만드는
        /// 것인데, 다이스가 걸려 있다는 사실이 수령 버튼을 누른 뒤에야 드러나면
        /// 그 목표가 유도를 못 한다.
        /// </summary>
        [SerializeField] private UIRewardElement diceRewardElement;
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
            Bind(nextRewardIndex, requiredStars, totalStars, isClaimed, isClaimable, claimCallback,
                DiceType.Max, default);
        }

        /// <summary>
        /// 다이스 미리보기까지 함께 그린다.
        ///
        /// <paramref name="diceType"/> 가 <c>DiceType.Max</c> 면 그 단계에 다이스가 없다는 뜻이라
        /// 칸을 감춘다. 이미 보유한 다이스면 <paramref name="diceView"/> 가 환급을 담고 있어
        /// 칸이 딤 처리 + 재화 아이콘으로 그려진다 — 지급 화면과 <b>같은 그림</b>이라
        /// 유저가 받기 전과 받은 뒤를 같은 것으로 읽는다.
        /// </summary>
        public void Bind(
            int nextRewardIndex,
            int requiredStars,
            int totalStars,
            bool isClaimed,
            bool isClaimable,
            Action<int> claimCallback,
            DiceType diceType,
            DiceRewardView diceView)
        {
            RefreshDiceSlot(diceType, diceView);
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
                    PointRewardUtility.GetPointIcon(PointType.FreeGem),
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

        /// <summary>
        /// 다이스 칸. 없으면 감추고, 있으면 처음 얻을 것인지 환급될 것인지에 따라 다르게 그린다.
        /// </summary>
        private void RefreshDiceSlot(DiceType diceType, DiceRewardView diceView)
        {
            if (diceRewardElement == null)
                return;

            if (diceType == DiceType.Max)
            {
                diceRewardElement.gameObject.SetActive(false);
                return;
            }

            diceRewardElement.gameObject.SetActive(true);

            Sprite diceSprite = DiceMetaDataProvider.GetIcon(diceType);
            if (diceView.WasNew)
                diceRewardElement.BindDice(diceSprite);
            else
                diceRewardElement.BindDice(diceSprite,
                    PointRewardUtility.GetPointIcon(diceView.RefundCurrency), diceView.RefundAmount);
        }

        private void HandleClaimClicked()
        {
            onClaim?.Invoke(rewardIndex);
        }
    }
}
