using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using OJ.DI;
using OJ.Dice;
using OJ.Hunting;
using OJ.Point;
using OJ.UI;

namespace OJ.StageReward
{
    public class UIStageRewardDialog : DialogBase
    {
        [Header("Milestones")]
        [SerializeField] private RectTransform[] milestoneSlots;
        [SerializeField] private UIStageRewardMilestoneItem milestoneTemplate;

        [Header("Reward Detail")]
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIRewardElement rewardElementTemplate;
        [SerializeField] private TMP_Text stateText;

        [Header("Buttons")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button claimButton;

        private readonly List<UIStageRewardMilestoneItem> milestoneItems = new List<UIStageRewardMilestoneItem>();
        private readonly List<UIRewardElement> rewardElements = new List<UIRewardElement>();

        private int selectedIndex = -1;

        protected override void OnLoad()
        {
            base.OnLoad();

            if (rewardRoot == null && rewardElementTemplate != null)
                rewardRoot = rewardElementTemplate.transform.parent as RectTransform;

            if (milestoneTemplate != null)
                milestoneTemplate.gameObject.SetActive(false);
            if (rewardElementTemplate != null)
                rewardElementTemplate.gameObject.SetActive(false);

            if (previousButton != null) previousButton.onClick.AddListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.AddListener(SelectNext);
            if (claimButton != null) claimButton.onClick.AddListener(ClaimSelected);
        }

        protected override void OnDestroy()
        {
            if (previousButton != null) previousButton.onClick.RemoveListener(SelectPrevious);
            if (nextButton != null) nextButton.onClick.RemoveListener(SelectNext);
            if (claimButton != null) claimButton.onClick.RemoveListener(ClaimSelected);

            if (StageRewardManager.Instance != null)
                StageRewardManager.Instance.OnChanged -= Refresh;

            base.OnDestroy();
        }

        protected override void OnEnter()
        {
            base.OnEnter();

            if (StageRewardManager.Instance != null)
                StageRewardManager.Instance.OnChanged += Refresh;

            selectedIndex = StageRewardManager.Instance != null ? StageRewardManager.Instance.GetFocusIndex() : -1;
            Refresh();
        }

        protected override void OnExit()
        {
            if (StageRewardManager.Instance != null)
                StageRewardManager.Instance.OnChanged -= Refresh;

            base.OnExit();
        }

        public void Open()
        {
            Enter();
        }

        private void SelectPrevious()
        {
            SelectIndex(selectedIndex - 1);
        }

        private void SelectNext()
        {
            SelectIndex(selectedIndex + 1);
        }

        private void SelectMilestone(UIStageRewardMilestoneItem item)
        {
            if (item == null || item.Milestone == null)
                return;

            int index = StageRewardDatabaseProvider.GetDatabase().IndexOf(item.Milestone.StableId);
            SelectIndex(index);
        }

        private void SelectIndex(int nextIndex)
        {
            IReadOnlyList<StageRewardMilestone> milestones = StageRewardDatabaseProvider.GetDatabase().Milestones;
            if (milestones == null || milestones.Count == 0)
            {
                selectedIndex = -1;
                Refresh();
                return;
            }

            selectedIndex = Mathf.Clamp(nextIndex, 0, milestones.Count - 1);
            Refresh();
        }

        private void Refresh()
        {
            StageRewardManager manager = StageRewardManager.Instance;
            IReadOnlyList<StageRewardMilestone> milestones = StageRewardDatabaseProvider.GetDatabase().Milestones;
            int totalCount = milestones != null ? milestones.Count : 0;

            if (totalCount <= 0)
            {
                selectedIndex = -1;
                SetEmptyState();
                return;
            }

            if (selectedIndex < 0)
                selectedIndex = manager != null ? manager.GetFocusIndex() : 0;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, totalCount - 1);

            EnsureMilestoneItems();
            RefreshMilestoneSlots(manager, milestones, totalCount);

            StageRewardMilestone selectedMilestone = milestones[selectedIndex];
            StageRewardState selectedState = manager != null ? manager.GetState(selectedMilestone) : StageRewardState.Locked;
            RefreshSelectedDetail(selectedMilestone, selectedState, totalCount);
        }

        private void RefreshMilestoneSlots(
            StageRewardManager manager,
            IReadOnlyList<StageRewardMilestone> milestones,
            int totalCount)
        {
            int slotCount = GetMilestoneSlotCount();
            int centerSlotIndex = slotCount / 2;

            for (int i = 0; i < milestoneItems.Count; i++)
            {
                UIStageRewardMilestoneItem item = milestoneItems[i];
                if (item == null)
                    continue;

                item.gameObject.SetActive(true);

                int milestoneIndex = selectedIndex + i - centerSlotIndex;
                bool hasMilestone = milestoneIndex >= 0 && milestoneIndex < totalCount;
                if (!hasMilestone)
                {
                    item.BindEmpty();
                    continue;
                }

                StageRewardMilestone milestone = milestones[milestoneIndex];
                StageRewardState state = manager != null ? manager.GetState(milestone) : StageRewardState.Locked;
                item.Bind(milestone, state, milestoneIndex == selectedIndex, SelectMilestone);
            }
        }

        private void SetEmptyState()
        {
            if (stateText != null)
                stateText.SetText(string.Empty);

            SetClaimButtonVisible(false);

            if (previousButton != null)
                previousButton.interactable = false;
            if (nextButton != null)
                nextButton.interactable = false;

            for (int i = 0; i < milestoneItems.Count; i++)
            {
                if (milestoneItems[i] != null)
                    milestoneItems[i].gameObject.SetActive(false);
            }

            BindRewards(null);
        }

        private void RefreshSelectedDetail(StageRewardMilestone milestone, StageRewardState state, int totalCount)
        {
            if (stateText != null)
            {
                switch (state)
                {
                    case StageRewardState.Claimable:
                        stateText.SetText("수령 가능");
                        break;
                    case StageRewardState.Claimed:
                        stateText.SetText("수령 완료");
                        break;
                    default:
                        stateText.SetText(milestone != null ? milestone.RequirementText : string.Empty);
                        break;
                }
            }

            SetClaimButtonVisible(state == StageRewardState.Claimable);

            if (previousButton != null)
                previousButton.interactable = selectedIndex > 0;
            if (nextButton != null)
                nextButton.interactable = selectedIndex >= 0 && selectedIndex < totalCount - 1;

            BindRewards(milestone);
        }

        private void SetClaimButtonVisible(bool visible)
        {
            if (claimButton == null)
                return;

            claimButton.gameObject.SetActive(visible);
            claimButton.interactable = visible;
        }

        /// <summary>
        /// 고른 마일스톤의 보상을 그린다.
        ///
        /// <b>다이스는 <c>milestone.rewards</c> 에 없다.</b> 그 목록은 <c>PointType</c> 전용이고
        /// 다이스 언락의 정본은 <c>DiceUnlockDatabase</c> 다. 그래서 재화를 다 그린 뒤
        /// 다이스 칸을 <b>한 칸 이어 붙인다</b> — 이걸 빠뜨리면 "스테이지 8 클리어로 얻는다"
        /// 고 안내해 놓고 정작 그 스테이지의 보상 목록에는 안 보이는 상태가 된다.
        /// </summary>
        private void BindRewards(StageRewardMilestone milestone)
        {
            IReadOnlyList<StageRewardEntry> rewards = milestone != null ? milestone.rewards : null;
            int count = rewards != null ? rewards.Count : 0;

            DiceType dice = ResolvePreviewDice(milestone);
            int total = count + (dice != DiceType.Max ? 1 : 0);

            EnsureRewardElements(rewardElements, rewardElementTemplate, rewardRoot, total);

            for (int i = 0; i < rewardElements.Count; i++)
            {
                UIRewardElement rewardElement = rewardElements[i];
                if (rewardElement == null)
                    continue;

                bool shouldShow = i < total;
                rewardElement.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                if (i < count)
                {
                    StageRewardEntry reward = rewards[i];
                    rewardElement.Bind(PointRewardUtility.GetPointIcon(reward.pointType), reward.amount, "x{0:#,##0}");
                    continue;
                }

                BindDicePreview(rewardElement, dice);
            }
        }

        /// <summary>
        /// 이 마일스톤이 열어 주는 다이스. 없으면 <c>DiceType.Max</c>.
        ///
        /// <b>지급과 같은 판정을 쓴다</b>(마지막 칸인가 + 스테이지 번호로 조회) —
        /// 미리보기가 다른 식으로 판정하면 보여 준 것과 준 것이 갈라진다.
        /// </summary>
        private static DiceType ResolvePreviewDice(StageRewardMilestone milestone)
        {
            if (milestone == null)
                return DiceType.Max;

            if (!StageRewardDatabaseProvider.GetDatabase().IsFinalMilestoneOf(milestone))
                return DiceType.Max;

            return DiceUnlockDatabaseProvider.Database.GetStageUnlock(milestone.requiredStageIndex);
        }

        /// <summary>
        /// 이미 보유한 다이스면 <b>받게 될 것은 환급 재화</b>다. 그것을 미리 보여 주지 않으면
        /// 수령하고 나서 "다이스인 줄 알았는데" 가 된다.
        /// </summary>
        private static void BindDicePreview(UIRewardElement element, DiceType dice)
        {
            Sprite diceSprite = DiceMetaDataProvider.GetIcon(dice);
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;

            if (ownership == null || !ownership.IsOwned(dice))
            {
                element.BindDice(diceSprite);
                return;
            }

            element.BindDice(diceSprite,
                PointRewardUtility.GetPointIcon(DiceOwnershipManager.GetPriceCurrency(dice)),
                ownership.GetPrice(dice));
        }

        private void ClaimSelected()
        {
            StageRewardManager manager = StageRewardManager.Instance;
            IReadOnlyList<StageRewardMilestone> milestones = StageRewardDatabaseProvider.GetDatabase().Milestones;
            if (manager == null || milestones == null || selectedIndex < 0 || selectedIndex >= milestones.Count)
                return;

            if (!manager.TryClaim(milestones[selectedIndex], out List<PointRewardEntry> rewards,
                    out DiceRewardView diceView, out bool hasDice))
                return;

            Refresh();

            // 카탈로그에서 꺼내 띄운다. (10.4)
            //
            // 예전에는 결과창을 [SerializeField] 로 직접 가리켰고, 그 참조가 None 이면
            // 아무 로그 없이 창만 안 떴다. 보상은 이미 지급된 뒤라 유저 눈에는
            // 그냥 눌러도 아무 일이 없는 버튼이 된다. UIService 는 못 열면 사유를 남긴다.
            //
            // Show 가 아니라 Get 인 이유: Open 이 보상 목록을 채운 뒤 스스로 Enter 를 부른다.
            UIRewardResultDialog resultDialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (resultDialog != null)
            {
                // 처음 얻었든 환급이든 다이스 칸을 세운다 — 딤 처리 여부로 둘을 구별한다.
                List<DiceRewardView> diceRewards = hasDice
                    ? new List<DiceRewardView> { diceView }
                    : null;

                resultDialog.Open(rewards, diceRewards,
                    hasDice && diceView.WasNew ? "새 다이스를 얻었습니다!" : "보상을 획득했습니다.",
                    HandleClaimResultClosed);
                return;
            }

            HandleClaimResultClosed();
        }

        private void HandleClaimResultClosed()
        {
            if (StageRewardManager.Instance != null)
                selectedIndex = StageRewardManager.Instance.GetFocusIndex();

            Refresh();
        }

        private int GetMilestoneSlotCount()
        {
            return milestoneSlots != null ? milestoneSlots.Length : 0;
        }

        private void EnsureMilestoneItems()
        {
            int slotCount = GetMilestoneSlotCount();
            if (slotCount <= 0)
                return;

            while (milestoneItems.Count < slotCount)
                milestoneItems.Add(null);

            for (int i = 0; i < slotCount; i++)
            {
                RectTransform slot = milestoneSlots[i];
                if (slot == null)
                    continue;

                slot.gameObject.SetActive(true);

                if (milestoneItems[i] != null)
                    continue;

                UIStageRewardMilestoneItem item = slot.GetComponentInChildren<UIStageRewardMilestoneItem>(true);
                if (item == null || item == milestoneTemplate)
                {
                    if (milestoneTemplate == null)
                        continue;

                    item = Object.Instantiate(milestoneTemplate, slot);
                }

                RectTransform itemRect = item.transform as RectTransform;
                if (itemRect != null)
                    StretchToSlot(itemRect);

                item.gameObject.SetActive(false);
                milestoneItems[i] = item;
            }
        }

        private static void StretchToSlot(RectTransform rectTransform)
        {
            rectTransform.SetParent(rectTransform.parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }

        private static void EnsureRewardElements(
            List<UIRewardElement> elements,
            UIRewardElement template,
            RectTransform root,
            int count)
        {
            if (elements == null || template == null || root == null)
                return;

            while (elements.Count < count)
            {
                UIRewardElement rewardElement = Object.Instantiate(template, root);
                rewardElement.gameObject.SetActive(true);
                elements.Add(rewardElement);
            }
        }
    }
}
