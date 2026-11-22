using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Mission;
using OJ.Point;
using OJ.UI;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 래프팅 화면. 보트로 룰렛을 돌려 거리를 쌓는다. 룰렛은 결과가 이미 정해진 뒤 칸을 훑는 연출이고,
    /// 무료 칸이 나오면 같은 배율로 이어 도는 모습을 그대로 보여 준다.
    /// 큰 마일스톤은 3택1 상자라 목록 줄의 "선택" 버튼으로 연다(회차가 끝나면 첫 묶음이 자동 지급된다).
    /// </summary>
    public sealed class UIRaftingPage : UIEventMinigameDialog
    {
        private const float ScanStep = 0.06f;
        private const float ScanSeconds = 0.8f;
        private const float HoldSeconds = 0.35f;

        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private Image[] cellImages;
        [SerializeField] private TMP_Text[] cellLabels;
        [SerializeField] private Button[] multiplierButtons;
        [SerializeField] private Image[] multiplierImages;
        [SerializeField] private Button spinButton;
        [SerializeField] private TMP_Text spinLabel;
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private TMP_Text choiceTitle;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceTexts;
        [SerializeField] private Button choiceCancel;

        private int multiplierIndex;
        private int choiceRow = -1;

        // 룰렛 연출
        private bool animating;
        private RaftingSpinResult animResult;
        private int animStep;
        private float animTime;
        private float scanTimer;
        private int highlight = -1;

        protected override EventKind Kind
        {
            get { return EventKind.Rafting; }
        }

        protected override void OnLoadPlay()
        {
            for (int i = 0; i < multiplierButtons.Length; i++)
            {
                int index = i;
                multiplierButtons[i].onClick.AddListener(() => SelectMultiplier(index));
            }

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int option = i;
                choiceButtons[i].onClick.AddListener(() => OnChoose(option));
            }

            spinButton.onClick.AddListener(OnSpin);
            choiceCancel.onClick.AddListener(CloseChoice);
        }

        protected override void OnOpened()
        {
            animating = false;
            highlight = -1;
            choicePanel.SetActive(false);
            SetStatus(string.Empty);
            RefreshPlay();

            if (Manager != null)
                ScrollListTo(Manager.RaftingClaimedCount);
        }

        private void SelectMultiplier(int index)
        {
            multiplierIndex = index;
            RefreshPlay();
        }

        private void OnSpin()
        {
            if (animating || !IsOpenNow || Manager == null)
                return;

            RaftingSpinResult result = Manager.Spin(RaftingRules.Multipliers[multiplierIndex], UtcNow);
            if (!result.Valid)
            {
                SetStatus("보트가 모자라요");
                return;
            }

            animating = true;
            animResult = result;
            animStep = 0;
            animTime = 0f;
            scanTimer = 0f;
            SetStatus(string.Empty);
            RefreshPlay();
        }

        protected override void OnFrame()
        {
            if (!animating || animResult == null)
                return;

            float dt = Time.unscaledDeltaTime;
            animTime += dt;
            scanTimer += dt;

            int target = animResult.Cells[animStep];
            if (animTime < ScanSeconds)
            {
                if (scanTimer >= ScanStep)
                {
                    scanTimer = 0f;
                    highlight = (highlight + 1 + RaftingRules.CellCount) % RaftingRules.CellCount;
                    PaintCells();
                }

                return;
            }

            // 목표 칸에 선다.
            if (highlight != target)
            {
                highlight = target;
                PaintCells();
            }

            if (animTime < ScanSeconds + HoldSeconds)
                return;

            animStep++;
            animTime = 0f;
            scanTimer = 0f;
            if (animStep < animResult.Cells.Count)
                return;

            FinishAnimation();
        }

        private void FinishAnimation()
        {
            RaftingSpinResult result = animResult;
            animating = false;
            animResult = null;

            var text = new StringBuilder();
            text.Append('+').Append(EventMinigameText.Number(result.Distance)).Append('m');
            if (result.Multiplier > 1)
                text.Append("  (x").Append(result.Multiplier).Append(')');
            if (result.Spins > 1)
                text.Append("\n무료 칸으로 ").Append(result.Spins - 1).Append("번 더 돌았어요!");
            if (result.NewPendingChoices.Count > 0)
                text.Append("\n상자를 골라 주세요");

            SetStatus(text.ToString());
            RefreshAll();
            ShowRewards(result.Rewards, "래프팅 보상");

            if (result.NewPendingChoices.Count > 0 && result.Rewards.Count == 0)
                OpenChoice(result.NewPendingChoices[0]);
        }

        protected override void RefreshPlay()
        {
            if (Manager == null)
                return;

            long distance = Manager.RaftingDistance;
            EventMilestone[] table = EventRewardTables.Rafting;
            int claimed = Manager.RaftingClaimedCount;

            var text = new StringBuilder("누적 거리 ").Append(EventMinigameText.Number(distance)).Append('m');
            if (claimed < table.Length)
                text.Append("   (다음 ").Append(EventMinigameText.Number(table[claimed].Threshold)).Append("m)");
            distanceText.text = text.ToString();

            bool open = IsOpenNow && !animating;
            int held = HeldCurrency;

            while (multiplierIndex > 0 && held < RaftingRules.Multipliers[multiplierIndex])
                multiplierIndex--;

            for (int i = 0; i < multiplierButtons.Length; i++)
            {
                multiplierButtons[i].interactable = open && held >= RaftingRules.Multipliers[i];
                multiplierImages[i].color = i == multiplierIndex ? UIEventMinigameFactory.ButtonSelected : UIEventMinigameFactory.ButtonOff;
            }

            int cost = RaftingRules.Multipliers[multiplierIndex];
            spinButton.interactable = open && held >= cost;
            spinLabel.text = animating ? "돌리는 중..." : "돌리기  (보트 " + cost + "개)";

            PaintCells();
        }

        private void PaintCells()
        {
            for (int i = 0; i < cellImages.Length; i++)
            {
                Color color = UIEventMinigameFactory.CellIdle;
                if (RaftingRules.IsFreeCell(i))
                    color = UIEventMinigameFactory.CellFree;
                else if (RaftingRules.CellDistance[i] >= 20)
                    color = UIEventMinigameFactory.CellJackpot;

                if (i == highlight)
                    color = UIEventMinigameFactory.CellActive;

                cellImages[i].color = color;
            }
        }

        protected override void RefreshList()
        {
            if (Manager == null || animating)
                return;

            EventMilestone[] table = EventRewardTables.Rafting;
            var list = EnsureRows(table.Length);
            int claimed = Manager.RaftingClaimedCount;
            List<int> pending = Manager.GetRaftingPending();

            for (int i = 0; i < table.Length; i++)
            {
                int row = i;
                string label = EventMinigameText.Number(table[i].Threshold) + "m";

                UIEventMilestoneRow.RowState state = i < claimed
                    ? UIEventMilestoneRow.RowState.Reached
                    : (i == claimed ? UIEventMilestoneRow.RowState.Next : UIEventMilestoneRow.RowState.Locked);

                string reward = table[i].IsChoice
                    ? "상자 3택1  " + EventMinigameText.Describe(table[i].Options[0]).Split('·')[0].Trim() + " 외"
                    : EventMinigameText.Describe(table[i].Default);

                if (pending.Contains(i))
                {
                    state = UIEventMilestoneRow.RowState.Pending;
                    list[i].Bind(label, reward, state, () => OpenChoice(row));
                }
                else
                {
                    list[i].Bind(label, reward, state);
                }
            }
        }

        // ── 3택1 ───────────────────────────────────────────────────────

        private void OpenChoice(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= EventRewardTables.Rafting.Length)
                return;

            EventMilestone milestone = EventRewardTables.Rafting[rowIndex];
            choiceRow = rowIndex;
            choiceTitle.text = EventMinigameText.Number(milestone.Threshold) + "m 상자 — 하나를 고르세요";

            string[] names = { "A", "B", "C" };
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool has = i < milestone.Options.Length;
                choiceButtons[i].gameObject.SetActive(has);
                if (has)
                    choiceTexts[i].text = names[i] + "   " + EventMinigameText.Describe(milestone.Options[i]);
            }

            choicePanel.SetActive(true);
        }

        private void CloseChoice()
        {
            choicePanel.SetActive(false);
            choiceRow = -1;
        }

        private void OnChoose(int option)
        {
            if (choiceRow < 0 || Manager == null)
                return;

            List<PointRewardEntry> rewards = Manager.TryClaimChoice(choiceRow, option, UtcNow);
            CloseChoice();

            if (rewards == null)
            {
                SetStatus("이미 받은 상자예요");
                return;
            }

            ShowRewards(rewards, "상자 보상");
            RefreshAll();
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        public static UIRaftingPage Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIMissionUIFactory.CreateRect("UIRaftingPage", parent);
            UIMissionUIFactory.Stretch(root.GetComponent<RectTransform>());

            var page = root.AddComponent<UIRaftingPage>();
            page.BakeOpenStyle(DialogOpenStyle.Popup);

            Transform p = UIEventMinigameFactory.BakeFrame(page, root.transform, font, "래프팅", true);

            page.distanceText = UIMissionUIFactory.CreateText("Distance", p, string.Empty, 32f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.distanceText.rectTransform, new Vector2(840f, 44f), new Vector2(0f, 540f));

            // 룰렛 칸 8개 (4 × 2).
            int cells = RaftingRules.CellCount;
            page.cellImages = new Image[cells];
            page.cellLabels = new TMP_Text[cells];
            for (int i = 0; i < cells; i++)
            {
                int col = i % 4;
                int row = i / 4;
                Image cell = UIMissionUIFactory.CreateImage("Cell" + i, p, UIEventMinigameFactory.CellIdle);
                UIMissionUIFactory.SetRect(cell.rectTransform, new Vector2(190f, 110f),
                    new Vector2((col - 1.5f) * 198f, 420f - row * 118f));
                cell.raycastTarget = false;

                TMP_Text label = UIMissionUIFactory.CreateText(
                    "Label", cell.transform, EventMinigameText.RaftingCell(i), 36f,
                    TextAlignmentOptions.Center, Color.white, font);
                UIMissionUIFactory.Stretch(label.rectTransform);

                page.cellImages[i] = cell;
                page.cellLabels[i] = label;
            }

            int count = RaftingRules.Multipliers.Length;
            page.multiplierButtons = new Button[count];
            page.multiplierImages = new Image[count];
            for (int i = 0; i < count; i++)
            {
                Button button = UIEventMinigameFactory.TextButton(
                    p, "Multiplier" + RaftingRules.Multipliers[i], "x" + RaftingRules.Multipliers[i],
                    new Vector2(220f, 84f), new Vector2((i - 1) * 250f, 172f),
                    UIEventMinigameFactory.ButtonOff, 38f, font);
                page.multiplierButtons[i] = button;
                page.multiplierImages[i] = button.GetComponent<Image>();
            }

            page.spinButton = UIEventMinigameFactory.TextButton(
                p, "SpinButton", "돌리기", new Vector2(520f, 130f), new Vector2(0f, 56f),
                UIEventMinigameFactory.ButtonOn, 46f, font);
            page.spinLabel = page.spinButton.GetComponentInChildren<TMP_Text>();

            // 3택1 덮개: 맨 마지막 자식이라 위에 그려진다.
            Image panel = UIMissionUIFactory.CreateImage("ChoicePanel", p, UIMissionUIFactory.Hex(0x1f2030));
            UIMissionUIFactory.SetRect(panel.rectTransform, new Vector2(820f, 700f), new Vector2(0f, 180f));
            page.choicePanel = panel.gameObject;

            page.choiceTitle = UIMissionUIFactory.CreateText(
                "ChoiceTitle", panel.transform, string.Empty, 34f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.choiceTitle.rectTransform, new Vector2(760f, 60f), new Vector2(0f, 290f));

            page.choiceButtons = new Button[3];
            page.choiceTexts = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                Button button = UIEventMinigameFactory.TextButton(
                    panel.transform, "Choice" + i, string.Empty, new Vector2(760f, 140f),
                    new Vector2(0f, 150f - i * 160f), UIEventMinigameFactory.ButtonOn, 28f, font);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                label.textWrappingMode = TextWrappingModes.Normal;
                label.alignment = TextAlignmentOptions.Center;
                page.choiceButtons[i] = button;
                page.choiceTexts[i] = label;
            }

            page.choiceCancel = UIEventMinigameFactory.TextButton(
                panel.transform, "ChoiceCancel", "나중에", new Vector2(240f, 70f), new Vector2(0f, -310f),
                UIEventMinigameFactory.ButtonOff, 30f, font);

            return page;
        }
    }
}
