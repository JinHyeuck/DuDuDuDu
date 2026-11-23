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
    /// 래프팅 화면. 레퍼런스처럼 위에 <b>다음 목표까지의 진행 띠(보트가 달린다)</b>, 가운데에 <b>룰렛</b>, 아래 독에서 돌리고 배율을 바꾼다.
    /// 룰렛은 결과가 이미 정해진 뒤 칸을 훑는 연출이고, 무료 칸이 나오면 같은 배율로 이어 도는 모습을 그대로 보여 준다.
    /// 큰 마일스톤은 3택1 상자라 보상 판의 "선택" 버튼으로 연다(회차가 끝나면 첫 묶음이 자동 지급된다).
    /// </summary>
    public sealed class UIRaftingPage : UIEventMinigameDialog
    {
        private const float ScanStep = 0.06f;
        private const float ScanSeconds = 0.8f;
        private const float HoldSeconds = 0.35f;
        private const float TrackWidth = 880f;

        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text nextText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform boat;
        [SerializeField] private Image[] cellImages;
        [SerializeField] private TMP_Text[] cellLabels;
        [SerializeField] private Button multiplierButton;
        [SerializeField] private TMP_Text multiplierLabel;
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
            multiplierButton.onClick.AddListener(CycleMultiplier);

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
        }

        protected override int SheetFocusRow()
        {
            if (Manager == null)
                return 0;

            // 고르지 않은 상자가 있으면 그 줄부터 보여 준다.
            List<int> pending = Manager.GetRaftingPending();
            return pending.Count > 0 ? pending[0] : Manager.RaftingClaimedCount;
        }

        /// <summary>x1 → x10 → x100 → x1. 보트가 모자란 배율은 건너뛰고 x1 로 돌아간다.</summary>
        private void CycleMultiplier()
        {
            int next = (multiplierIndex + 1) % RaftingRules.Multipliers.Length;
            if (HeldCurrency < RaftingRules.Multipliers[next])
                next = 0;

            multiplierIndex = next;
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

            // 3택1 상자가 열렸으면 보상 판이 그 줄부터 보이게 하고, 보상 팝업이 있으면 닫힌 뒤에 선택 창을 연다.
            // 안 그러면 선택 줄이 묻혀, 못 고른 채 회차가 끝나 첫 묶음이 자동 지급된다.
            if (result.NewPendingChoices.Count > 0)
            {
                int row = result.NewPendingChoices[0];
                FocusSheetOn(row);
                ShowRewards(result.Rewards, "래프팅 보상", () => OpenChoice(row));
                return;
            }

            ShowRewards(result.Rewards, "래프팅 보상");
        }

        protected override void RefreshPlay()
        {
            if (Manager == null)
                return;

            long distance = Manager.RaftingDistance;
            EventMilestone[] table = EventRewardTables.Rafting;
            int claimed = Manager.RaftingClaimedCount;

            distanceText.text = "누적 거리 " + EventMinigameText.Number(distance) + "m";

            // 진행 띠: 지난 목표에서 다음 목표까지. 보트가 그 위를 달린다.
            float ratio = 1f;
            if (claimed < table.Length)
            {
                int previous = claimed > 0 ? table[claimed - 1].Threshold : 0;
                int next = table[claimed].Threshold;
                ratio = Mathf.Clamp01((float)(distance - previous) / Mathf.Max(1, next - previous));
                nextText.text = "다음 목표 " + EventMinigameText.Number(next) + "m";
                progressText.text = EventMinigameText.Number(previous) + "m ~ " + EventMinigameText.Number(next) + "m";
            }
            else
            {
                nextText.text = "모든 보상을 받았어요";
                progressText.text = "넘친 거리는 핀볼로 바뀌어요";
            }

            UIMissionUIFactory.SetGauge(progressFill, TrackWidth, ratio);
            boat.anchoredPosition = new Vector2(-TrackWidth / 2f + TrackWidth * ratio, boat.anchoredPosition.y);

            bool open = IsOpenNow && !animating;
            int held = HeldCurrency;

            while (multiplierIndex > 0 && held < RaftingRules.Multipliers[multiplierIndex])
                multiplierIndex--;

            int cost = RaftingRules.Multipliers[multiplierIndex];
            multiplierLabel.text = "x" + cost;
            multiplierButton.interactable = open;
            spinButton.interactable = open && held >= cost;
            spinLabel.text = animating ? "돌리는 중" : "돌리기";

            PaintCells();
        }

        private void PaintCells()
        {
            for (int i = 0; i < cellImages.Length; i++)
            {
                // 칸 그림은 킷 버튼이다: 평범한 칸 회색, 무료 칸 초록, 20m 빨강, 멈춘 칸(훑는 칸) 노랑.
                EventButtonKind kind = EventButtonKind.Secondary;
                if (RaftingRules.IsFreeCell(i))
                    kind = EventButtonKind.Positive;
                else if (RaftingRules.CellDistance[i] >= 20)
                    kind = EventButtonKind.Danger;

                if (i == highlight)
                    kind = EventButtonKind.Primary;

                cellImages[i].sprite = ButtonSprite(kind);
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

            Transform p = UIEventMinigameFactory.BakeScreen(page, root.transform, font, "래프팅 이벤트", "StageBg/Map_1");

            page.distanceText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "DistanceInfo", 0, 32f, Color.white, font);
            page.nextText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "NextInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);

            TMP_Text hint = UIEventMinigameFactory.BadgeLine(p, "InfoRight", "HintInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);
            hint.text = "보트 1개당 평균 약 6.5m";

            // 진행 띠: 검은 둥근 틀 위에 초록 채움, 보트 표식이 채움 끝에서 달린다.
            Image trackFrame = UIMissionUIFactory.Sliced(p, "ProgressFrame", UIEventMinigameFactory.GaugeBack, 3f,
                new Vector2(TrackWidth + 16f, 56f), new Vector2(0f, 520f), Color.white);
            trackFrame.raycastTarget = false;

            page.progressFill = UIMissionUIFactory.CreateGauge(
                "Progress", p, new Vector2(TrackWidth, 36f), new Vector2(0f, 520f),
                UIMissionUIFactory.LimeColor, UIMissionUIFactory.DarkBoxColor, 0f);

            Image boatImage = UIMissionUIFactory.Sliced(p, "Boat", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(56f, 56f), new Vector2(-TrackWidth / 2f, 547f), UIMissionUIFactory.Hex(0xf5b83d));
            boatImage.raycastTarget = false;
            page.boat = boatImage.rectTransform;

            page.progressText = UIMissionUIFactory.CreateText(
                "ProgressText", p, string.Empty, 26f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.progressText.rectTransform, new Vector2(700f, 36f), new Vector2(0f, 468f));

            // 룰렛 칸 8개 (4 × 2).
            int cells = RaftingRules.CellCount;
            page.cellImages = new Image[cells];
            page.cellLabels = new TMP_Text[cells];
            for (int i = 0; i < cells; i++)
            {
                int col = i % 4;
                int row = i / 4;
                Image cell = UIMissionUIFactory.Sliced(p, "Cell" + i, UIEventMinigameFactory.ButtonSecondary, 3f,
                    new Vector2(204f, 128f), new Vector2((col - 1.5f) * 216f, 290f - row * 150f), Color.white);
                cell.raycastTarget = false;

                TMP_Text label = UIMissionUIFactory.CreateText(
                    "Label", cell.transform, EventMinigameText.RaftingCell(i), 38f,
                    TextAlignmentOptions.Center, Color.white, font);
                UIMissionUIFactory.Stretch(label.rectTransform);

                page.cellImages[i] = cell;
                page.cellLabels[i] = label;
            }

            UIMissionUIFactory.SetRect(page.StatusTextInternal.rectTransform, new Vector2(860f, 220f), new Vector2(0f, -110f));

            // 아래 독: 돌리기(가운데) · x배율 · 보상(오른쪽, 공통).
            page.spinButton = UIEventMinigameFactory.DockMainButton(p, "SpinButton", "돌리기", font);
            page.spinLabel = page.spinButton.GetComponentInChildren<TMP_Text>();
            page.multiplierButton = UIEventMinigameFactory.DockMultiplierButton(p, font);
            page.multiplierLabel = page.multiplierButton.GetComponentInChildren<TMP_Text>();

            // 보상 판 위에 3택1 덮개를 얹는다.
            UIEventMinigameFactory.BakeRewardSheet(page, p, font);

            Image veil = UIMissionUIFactory.CreateImage("ChoicePanel", p, new Color(0f, 0f, 0f, 0.62f));
            UIMissionUIFactory.Stretch(veil.rectTransform);
            page.choicePanel = veil.gameObject;

            Image panel = UIMissionUIFactory.Sliced(veil.transform, "Panel", "Upgrade/Ui_Popup_Bg", 4f,
                new Vector2(900f, 880f), new Vector2(0f, 60f), Color.white);
            panel.raycastTarget = true;

            page.choiceTitle = UIMissionUIFactory.CreateText(
                "ChoiceTitle", veil.transform, string.Empty, 36f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.choiceTitle.rectTransform, new Vector2(800f, 60f), new Vector2(0f, 420f));

            page.choiceButtons = new Button[3];
            page.choiceTexts = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                Button button = UIEventMinigameFactory.KitButton(
                    veil.transform, "Choice" + i, string.Empty, EventButtonKind.Primary,
                    new Vector2(800f, 150f), new Vector2(0f, 270f - i * 180f), 28f, font);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                label.textWrappingMode = TextWrappingModes.Normal;
                label.alignment = TextAlignmentOptions.Center;
                page.choiceButtons[i] = button;
                page.choiceTexts[i] = label;
            }

            page.choiceCancel = UIEventMinigameFactory.KitButton(
                veil.transform, "ChoiceCancel", "나중에", EventButtonKind.Secondary,
                new Vector2(260f, 88f), new Vector2(0f, -340f), 32f, font);

            return page;
        }
    }
}
