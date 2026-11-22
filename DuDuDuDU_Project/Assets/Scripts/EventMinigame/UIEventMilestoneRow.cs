using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Mission;

namespace OJ.EventMinigame
{
    /// <summary>마일스톤 줄 하나(낚시 점수 / 래프팅 거리 / 보물찾기 스테이지). 목록 템플릿을 복제해 쓴다.</summary>
    public sealed class UIEventMilestoneRow : MonoBehaviour
    {
        /// <summary>줄의 상태. 색이 이것으로 갈린다.</summary>
        public enum RowState
        {
            /// <summary>아직 못 넘었다.</summary>
            Locked = 0,

            /// <summary>다음에 열리는 줄.</summary>
            Next,

            /// <summary>이미 받았다.</summary>
            Reached,

            /// <summary>넘었지만 3택1 을 아직 고르지 않았다.</summary>
            Pending,
        }

        public static readonly Vector2 RowSize = new Vector2(860f, 104f);

        [SerializeField] private Image background;
        [SerializeField] private TMP_Text thresholdText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionText;

        private Action onAction;

        private void Awake()
        {
            if (actionButton != null)
                actionButton.onClick.AddListener(OnActionClicked);
        }

        private void OnDestroy()
        {
            if (actionButton != null)
                actionButton.onClick.RemoveListener(OnActionClicked);
        }

        private void OnActionClicked()
        {
            if (onAction != null)
                onAction();
        }

        public void Bind(string threshold, string reward, RowState state, Action onActionClick = null)
        {
            thresholdText.text = threshold;
            rewardText.text = reward;
            onAction = onActionClick;

            Color color;
            string stateLabel;
            switch (state)
            {
                case RowState.Reached:
                    color = UIEventMinigameFactory.RowReached;
                    stateLabel = "받음";
                    break;
                case RowState.Next:
                    color = UIEventMinigameFactory.RowNext;
                    stateLabel = "다음";
                    break;
                case RowState.Pending:
                    color = UIEventMinigameFactory.RowPending;
                    stateLabel = string.Empty;
                    break;
                default:
                    color = UIEventMinigameFactory.RowIdle;
                    stateLabel = string.Empty;
                    break;
            }

            background.color = color;
            stateText.text = stateLabel;
            stateText.gameObject.SetActive(state != RowState.Pending);
            actionButton.gameObject.SetActive(state == RowState.Pending);
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        public static UIEventMilestoneRow Create(Transform parent, TMP_FontAsset font)
        {
            Image root = UIMissionUIFactory.CreateImage("EventMilestoneRow", parent, UIEventMinigameFactory.RowIdle);
            UIMissionUIFactory.SetRect(root.rectTransform, RowSize, Vector2.zero);
            root.raycastTarget = false;

            var row = root.gameObject.AddComponent<UIEventMilestoneRow>();
            row.background = root;

            row.thresholdText = UIMissionUIFactory.CreateFittedText(
                "Threshold", root.transform, string.Empty, 32f, 20f,
                TextAlignmentOptions.MidlineLeft, Color.white, font);
            UIMissionUIFactory.SetRect(row.thresholdText.rectTransform, new Vector2(220f, 90f), new Vector2(-310f, 0f));

            row.rewardText = UIMissionUIFactory.CreateText(
                "Reward", root.transform, string.Empty, 25f,
                TextAlignmentOptions.MidlineLeft, UIEventMinigameFactory.MutedText, font);
            row.rewardText.textWrappingMode = TextWrappingModes.Normal;
            row.rewardText.enableAutoSizing = true;
            row.rewardText.fontSizeMax = 25f;
            row.rewardText.fontSizeMin = 16f;
            UIMissionUIFactory.SetRect(row.rewardText.rectTransform, new Vector2(430f, 92f), new Vector2(10f, 0f));

            row.stateText = UIMissionUIFactory.CreateText(
                "State", root.transform, string.Empty, 28f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(row.stateText.rectTransform, new Vector2(140f, 60f), new Vector2(355f, 0f));

            Button button = UIMissionUIFactory.CreateButton(
                "ActionButton", root.transform, "선택", new Vector2(150f, 70f), new Vector2(355f, 0f),
                UIEventMinigameFactory.ButtonOn, UIEventMinigameFactory.DarkText, 30f, font);
            row.actionButton = button;
            row.actionText = button.GetComponentInChildren<TMP_Text>();
            button.gameObject.SetActive(false);

            return row;
        }
    }
}
