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
    /// 래프팅 화면. 레퍼런스(가이드 PDF 의 실제 플레이 프레임) 구성을 따른다:
    /// 위 = 누적 거리·다음 목표 진행 띠, 가운데 = <b>강 위의 보트</b>(오른쪽 끝에 다음 상자·다음 보상 거리 표식),
    /// 아래 = <b>반원 룰렛</b>(가운데 둥근 돌리기 버튼, 위 꼭짓점의 바늘, 오른쪽 배율·왼쪽 보상 버튼).
    /// 룰렛은 결과가 이미 정해진 뒤 바퀴가 돌아 그 칸에서 멈추는 연출이고, 무료 칸이 나오면 같은 배율로 한 번 더 돈다.
    /// 큰 마일스톤은 3택1 상자라 선택 창으로 연다(회차가 끝나면 첫 묶음이 자동 지급된다).
    /// 그림이 없어 원·둥근 상자를 색으로 칠한 임시 모습이다.
    /// </summary>
    public sealed class UIRaftingPage : UIEventMinigameDialog
    {
        private const float SpinSeconds = 1.4f;
        private const float HoldSeconds = 0.5f;
        private const float GainSeconds = 1.1f;
        private const int SpinTurns = 2;
        private const float TrackWidth = 880f;
        private const float SegmentAngle = 360f / RaftingRules.CellCount;

        private const float RiverY = 300f;
        private const float RiverHeight = 480f;
        private const float WheelY = -760f;
        private const float WheelRadius = 400f;
        private const float WaveSpan = 1240f;
        private const float BoatX = -90f;
        private const float BoatY = 250f;

        private static readonly Color SegmentA = UIMissionUIFactory.Hex(0x7b4fd6);
        private static readonly Color SegmentB = UIMissionUIFactory.Hex(0x3f8fe0);
        private static readonly Color SegmentFree = UIMissionUIFactory.Hex(0xf2b821);
        private static readonly Color SegmentBig = UIMissionUIFactory.Hex(0xe0485a);

        [SerializeField] private TMP_Text distanceText;
        [SerializeField] private TMP_Text nextText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform trackBoat;
        [SerializeField] private RectTransform boat;
        [SerializeField] private RectTransform[] waves;
        [SerializeField] private GameObject chestMarker;
        [SerializeField] private TMP_Text chestMarkerText;
        [SerializeField] private GameObject rewardMarker;
        [SerializeField] private TMP_Text rewardMarkerText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text gainText;
        [SerializeField] private RectTransform wheel;
        [SerializeField] private Image[] segmentImages;
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
        private bool holding;
        private RaftingSpinResult animResult;
        private int animStep;
        private float animTime;
        private float startAngle;
        private float targetAngle;
        private float wheelAngle;
        private int highlight = -1;

        // 돌리는 동안 아직 화면에 안 올린 거리. 매니저는 돌리는 순간 거리를 다 더하지만, 화면은 칸에 멈출 때마다 올린다.
        private long unshownMeters;

        // 강물·보트
        private float waveSpeed;
        private float bobTime;
        private float gainTimer;

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

            // 처음엔 칸 경계가 아니라 첫 칸 가운데가 바늘 아래에 오게 둔다.
            SetWheelAngle(SegmentAngle * 0.5f);
        }

        protected override void OnOpened()
        {
            animating = false;
            holding = false;
            highlight = -1;
            unshownMeters = 0;
            gainTimer = 0f;
            gainText.gameObject.SetActive(false);
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
            unshownMeters = result.Distance;
            animStep = 0;
            highlight = -1;
            SetStatus(string.Empty);
            BeginStep();
            RefreshPlay();
        }

        /// <summary>바퀴가 지금 각도에서 <see cref="SpinTurns"/> 바퀴 넘게 돌아 이번 칸의 가운데가 바늘(위) 아래에 오게 한다.</summary>
        private void BeginStep()
        {
            holding = false;
            animTime = 0f;
            highlight = -1;
            PaintSegments();

            float cellCenter = (animResult.Cells[animStep] + 0.5f) * SegmentAngle;
            startAngle = wheelAngle;
            float current = Mathf.Repeat(wheelAngle, 360f);
            float delta = Mathf.Repeat(cellCenter - current, 360f);
            targetAngle = wheelAngle + delta + 360f * SpinTurns;
        }

        protected override void OnFrame()
        {
            float dt = Time.unscaledDeltaTime;
            AnimateRiver(dt);

            if (!animating || animResult == null)
                return;

            animTime += dt;

            if (!holding)
            {
                // 감속하며 멈춘다(3차 이즈아웃).
                float t = Mathf.Clamp01(animTime / SpinSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                SetWheelAngle(Mathf.Lerp(startAngle, targetAngle, eased));
                waveSpeed = Mathf.Lerp(900f, 60f, eased);

                if (t < 1f)
                    return;

                holding = true;
                animTime = 0f;
                Land(animResult.Cells[animStep]);
                return;
            }

            if (animTime < HoldSeconds)
                return;

            animStep++;
            if (animStep < animResult.Cells.Count)
            {
                BeginStep();
                return;
            }

            FinishAnimation();
        }

        /// <summary>칸에 멈춘 순간: 칸을 밝히고 보트 위에 이번에 간 거리를 띄운다.</summary>
        private void Land(int cell)
        {
            highlight = cell;
            PaintSegments();

            long meters = (long)RaftingRules.CellDistance[cell] * animResult.Multiplier;
            string gain = "+" + EventMinigameText.Number(meters) + "m";
            gainText.text = RaftingRules.IsFreeCell(cell) ? "무료! " + gain : gain;
            gainText.color = RaftingRules.IsFreeCell(cell) ? SegmentFree : Color.white;
            gainText.gameObject.SetActive(true);
            gainTimer = GainSeconds;

            unshownMeters = System.Math.Max(0L, unshownMeters - meters);
            RefreshPlay();
        }

        private void AnimateRiver(float dt)
        {
            if (!animating)
                waveSpeed = Mathf.MoveTowards(waveSpeed, 60f, 900f * dt);

            // 물결이 왼쪽으로 흘러 보트가 앞으로 나아가 보인다. 화면 밖으로 나가면 오른쪽 끝으로 돌린다.
            for (int i = 0; i < waves.Length; i++)
            {
                Vector2 position = waves[i].anchoredPosition;
                position.x -= waveSpeed * dt;
                if (position.x < -WaveSpan / 2f)
                    position.x += WaveSpan;
                waves[i].anchoredPosition = position;
            }

            bobTime += dt;
            boat.anchoredPosition = new Vector2(BoatX, BoatY + Mathf.Sin(bobTime * 2.4f) * 6f);
            boat.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(bobTime * 1.7f) * 2f);

            if (gainTimer > 0f)
            {
                gainTimer -= dt;
                float k = 1f - Mathf.Clamp01(gainTimer / GainSeconds);
                gainText.rectTransform.anchoredPosition = new Vector2(BoatX, BoatY + 150f + k * 60f);
                Color c = gainText.color;
                c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                gainText.color = c;
                if (gainTimer <= 0f)
                    gainText.gameObject.SetActive(false);
            }
        }

        private void SetWheelAngle(float angle)
        {
            wheelAngle = angle;
            wheel.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void FinishAnimation()
        {
            RaftingSpinResult result = animResult;
            animating = false;
            holding = false;
            animResult = null;
            unshownMeters = 0;

            var text = new StringBuilder();
            text.Append("총 +").Append(EventMinigameText.Number(result.Distance)).Append('m');
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

            long distance = Manager.RaftingDistance - unshownMeters;
            EventMilestone[] table = EventRewardTables.Rafting;
            int claimed = Manager.RaftingClaimedCount;

            // 돌리는 중에는 화면에 올린 거리까지만 목표를 넘긴 것으로 본다(지급은 끝난 뒤 팝업으로 보여 준다).
            if (unshownMeters > 0)
            {
                claimed = 0;
                while (claimed < table.Length && table[claimed].Threshold <= distance)
                    claimed++;
            }

            distanceText.text = "누적 거리 " + EventMinigameText.Number(distance) + "m";

            // 진행 띠: 지난 목표에서 다음 목표까지. 작은 보트가 그 위를 달린다.
            float ratio = 1f;
            if (claimed < table.Length)
            {
                int previous = claimed > 0 ? table[claimed - 1].Threshold : 0;
                int next = table[claimed].Threshold;
                ratio = Mathf.Clamp01((float)(distance - previous) / Mathf.Max(1, next - previous));
                nextText.text = "다음 목표까지 " + EventMinigameText.Number(System.Math.Max(0L, next - distance)) + "m";
                progressText.text = EventMinigameText.Number(previous) + "m ~ " + EventMinigameText.Number(next) + "m";
            }
            else
            {
                nextText.text = "모든 보상을 받았어요";
                progressText.text = "넘친 거리는 핀볼로 바뀌어요";
            }

            UIMissionUIFactory.SetGauge(progressFill, TrackWidth, ratio);
            trackBoat.anchoredPosition = new Vector2(-TrackWidth / 2f + TrackWidth * ratio, trackBoat.anchoredPosition.y);

            RefreshMarkers(table, claimed);

            bool open = IsOpenNow && !animating;
            int held = HeldCurrency;

            while (multiplierIndex > 0 && held < RaftingRules.Multipliers[multiplierIndex])
                multiplierIndex--;

            int multiplier = RaftingRules.Multipliers[multiplierIndex];
            multiplierLabel.text = "x" + multiplier;
            multiplierButton.interactable = open;
            spinButton.interactable = open && held >= multiplier;
            spinLabel.text = animating ? "..." : "돌리기";

            // 칸 글자는 배율을 곱한 거리다(레퍼런스도 배율을 올리면 칸 숫자가 커진다).
            for (int i = 0; i < cellLabels.Length; i++)
            {
                string meters = EventMinigameText.Number((long)RaftingRules.CellDistance[i] * multiplier) + "m";
                cellLabels[i].text = RaftingRules.IsFreeCell(i) ? "무료\n" + meters : meters;
            }

            PaintSegments();
        }

        /// <summary>강 오른쪽 끝 표식(다음 3택1 상자 · 다음 일반 보상)과 진행 띠 아래 다음 보상 글자.</summary>
        private void RefreshMarkers(EventMilestone[] table, int claimed)
        {
            int nextChest = -1;
            int nextReward = -1;
            for (int i = claimed; i < table.Length; i++)
            {
                if (table[i].IsChoice)
                {
                    if (nextChest < 0)
                        nextChest = i;
                }
                else if (nextReward < 0)
                {
                    nextReward = i;
                }

                if (nextChest >= 0 && nextReward >= 0)
                    break;
            }

            chestMarker.SetActive(nextChest >= 0);
            if (nextChest >= 0)
                chestMarkerText.text = EventMinigameText.Number(table[nextChest].Threshold) + "m";

            rewardMarker.SetActive(nextReward >= 0);
            if (nextReward >= 0)
                rewardMarkerText.text = EventMinigameText.Number(table[nextReward].Threshold) + "m";

            List<int> pending = Manager.GetRaftingPending();
            if (pending.Count > 0 && unshownMeters == 0)
            {
                rewardText.text = "고르지 않은 상자가 있어요 — 보상에서 골라 주세요";
            }
            else if (claimed < table.Length)
            {
                EventMilestone next = table[claimed];
                string reward = next.IsChoice ? "상자 3택1" : EventMinigameText.Describe(next.Default);
                rewardText.text = "다음 보상 " + EventMinigameText.Number(next.Threshold) + "m   " + reward;
            }
            else
            {
                rewardText.text = "모든 보상을 받았어요";
            }
        }

        /// <summary>칸 색: 보라·파랑 번갈아, 무료 칸 노랑, 20m 빨강. 멈춘 칸은 밝게 빛난다.</summary>
        private void PaintSegments()
        {
            for (int i = 0; i < segmentImages.Length; i++)
            {
                Color color = i % 2 == 0 ? SegmentA : SegmentB;
                if (RaftingRules.IsFreeCell(i))
                    color = SegmentFree;
                else if (RaftingRules.CellDistance[i] >= 20)
                    color = SegmentBig;

                if (i == highlight)
                    color = Color.Lerp(color, Color.white, 0.55f);

                segmentImages[i].color = color;
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

        private const string CirclePath = "EventMinigame/Event_Circle";

        private static Image Circle(Transform parent, string name, float diameter, Vector2 position, Color color)
        {
            Image image = UIMissionUIFactory.Picture(parent, name, CirclePath, new Vector2(diameter, diameter), position);
            image.color = color;
            return image;
        }

        /// <summary>강 오른쪽 끝 표식 하나: 둥근 그림 + 아래 거리 글자.</summary>
        private static GameObject Marker(Transform parent, string name, string caption, Color color, float y,
            TMP_FontAsset font, out TMP_Text distance)
        {
            GameObject root = UIMissionUIFactory.CreateRect(name, parent);
            UIMissionUIFactory.SetRect(root.GetComponent<RectTransform>(), new Vector2(170f, 190f), new Vector2(440f, y));

            Circle(root.transform, "Ring", 132f, new Vector2(0f, 30f), UIMissionUIFactory.Hex(0x2a2a40));
            Circle(root.transform, "Icon", 116f, new Vector2(0f, 30f), color);

            TMP_Text label = UIMissionUIFactory.CreateText(
                "Caption", root.transform, caption, 30f, TextAlignmentOptions.Center, UIEventMinigameFactory.DarkText, font);
            UIMissionUIFactory.SetRect(label.rectTransform, new Vector2(120f, 50f), new Vector2(0f, 30f));

            Image chip = UIEventMinigameFactory.Chip(root.transform, "DistanceChip", new Vector2(170f, 48f), new Vector2(0f, -58f));
            distance = UIMissionUIFactory.CreateText(
                "Distance", chip.transform, string.Empty, 28f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.Stretch(distance.rectTransform);
            return root;
        }

        public static UIRaftingPage Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIMissionUIFactory.CreateRect("UIRaftingPage", parent);
            UIMissionUIFactory.Stretch(root.GetComponent<RectTransform>());

            var page = root.AddComponent<UIRaftingPage>();
            page.BakeOpenStyle(DialogOpenStyle.Popup);

            Transform p = UIEventMinigameFactory.BakeScreen(page, root.transform, font, "래프팅 이벤트", "StageBg/Map_2", false);

            page.distanceText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "DistanceInfo", 0, 34f, Color.white, font);
            page.nextText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "NextInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);

            TMP_Text hint = UIEventMinigameFactory.BadgeLine(p, "InfoRight", "HintInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);
            hint.text = "보트 1개당 평균 약 6.5m";

            // 진행 띠: 검은 둥근 틀 위에 초록 채움, 작은 보트 표식이 채움 끝에서 달린다. 구간 글자는 띠 안에.
            Image trackFrame = UIMissionUIFactory.Sliced(p, "ProgressFrame", UIEventMinigameFactory.GaugeBack, 3f,
                new Vector2(TrackWidth + 16f, 52f), new Vector2(0f, 612f), Color.white);
            trackFrame.raycastTarget = false;

            page.progressFill = UIMissionUIFactory.CreateGauge(
                "Progress", p, new Vector2(TrackWidth, 34f), new Vector2(0f, 612f),
                UIMissionUIFactory.LimeColor, UIMissionUIFactory.DarkBoxColor, 0f);

            page.progressText = UIMissionUIFactory.CreateText(
                "ProgressText", p, string.Empty, 24f, TextAlignmentOptions.Center, Color.white, font);
            page.progressText.raycastTarget = false;
            UIMissionUIFactory.SetRect(page.progressText.rectTransform, new Vector2(700f, 34f), new Vector2(0f, 612f));

            Image trackBoat = UIMissionUIFactory.Sliced(p, "TrackBoat", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(52f, 34f), new Vector2(-TrackWidth / 2f, 648f), UIMissionUIFactory.Hex(0xf5843d));
            trackBoat.raycastTarget = false;
            page.trackBoat = trackBoat.rectTransform;

            // 강: 위아래 풀숲 둑 + 물. 물결 줄이 왼쪽으로 흘러 보트가 나아가 보인다.
            Image bankTop = UIMissionUIFactory.CreateImage("BankTop", p, UIMissionUIFactory.Hex(0x4f9a3c));
            bankTop.raycastTarget = false;
            UIMissionUIFactory.SetRect(bankTop.rectTransform, new Vector2(1080f, 34f), new Vector2(0f, RiverY + RiverHeight / 2f + 10f));

            Image river = UIMissionUIFactory.CreateImage("River", p, UIMissionUIFactory.Hex(0x35b9d6));
            river.raycastTarget = false;
            UIMissionUIFactory.SetRect(river.rectTransform, new Vector2(1080f, RiverHeight), new Vector2(0f, RiverY));

            Image bankBottom = UIMissionUIFactory.CreateImage("BankBottom", p, UIMissionUIFactory.Hex(0x4f9a3c));
            bankBottom.raycastTarget = false;
            UIMissionUIFactory.SetRect(bankBottom.rectTransform, new Vector2(1080f, 34f), new Vector2(0f, RiverY - RiverHeight / 2f - 10f));

            GameObject waveRoot = UIMissionUIFactory.CreateRect("Waves", p);
            UIMissionUIFactory.SetRect(waveRoot.GetComponent<RectTransform>(), new Vector2(1080f, RiverHeight), new Vector2(0f, RiverY));
            waveRoot.AddComponent<RectMask2D>();

            float[] waveRows = { 180f, 120f, 40f, -30f, -110f, -175f, 150f, -60f, 80f, -150f };
            page.waves = new RectTransform[waveRows.Length];
            for (int i = 0; i < waveRows.Length; i++)
            {
                Image wave = UIMissionUIFactory.Sliced(waveRoot.transform, "Wave" + i, UIEventMinigameFactory.SmallBox, 0.6f,
                    new Vector2(90f + (i % 3) * 40f, 10f), new Vector2(-560f + i * 127f, waveRows[i]), new Color(1f, 1f, 1f, 0.38f));
                wave.raycastTarget = false;
                page.waves[i] = wave.rectTransform;
            }

            // 보트: 주황 선체 + 위에 탄 동료(임시 상자) + 깃발. 위아래로 흔들린다.
            GameObject boatRoot = UIMissionUIFactory.CreateRect("Boat", p);
            UIMissionUIFactory.SetRect(boatRoot.GetComponent<RectTransform>(), new Vector2(340f, 220f), new Vector2(BoatX, BoatY));
            page.boat = boatRoot.GetComponent<RectTransform>();

            Image rider = UIMissionUIFactory.Sliced(boatRoot.transform, "Rider", UIEventMinigameFactory.SmallBox, 2f,
                new Vector2(104f, 104f), new Vector2(-40f, 50f), UIMissionUIFactory.Hex(0xf07fb0));
            rider.raycastTarget = false;
            Image rider2 = UIMissionUIFactory.Sliced(boatRoot.transform, "Rider2", UIEventMinigameFactory.SmallBox, 2f,
                new Vector2(90f, 90f), new Vector2(70f, 42f), UIMissionUIFactory.Hex(0x9fd8ff));
            rider2.raycastTarget = false;
            Image hull = UIMissionUIFactory.Sliced(boatRoot.transform, "Hull", UIEventMinigameFactory.SmallBox, 3f,
                new Vector2(330f, 100f), new Vector2(0f, -30f), UIMissionUIFactory.Hex(0xf5843d));
            hull.raycastTarget = false;
            Image stripe = UIMissionUIFactory.Sliced(boatRoot.transform, "Stripe", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(290f, 22f), new Vector2(0f, -22f), UIMissionUIFactory.Hex(0xffd36b));
            stripe.raycastTarget = false;

            page.gainText = UIMissionUIFactory.CreateText(
                "GainText", p, string.Empty, 64f, TextAlignmentOptions.Center, Color.white, font);
            page.gainText.raycastTarget = false;
            UIMissionUIFactory.SetRect(page.gainText.rectTransform, new Vector2(600f, 90f), new Vector2(BoatX, BoatY + 150f));
            page.gainText.gameObject.SetActive(false);

            // 강 오른쪽 끝 표식(레퍼런스의 상자 6816m · 보상 816m 자리).
            page.chestMarker = Marker(p, "ChestMarker", "상자", UIMissionUIFactory.Hex(0xf2b821), RiverY + 120f, font, out page.chestMarkerText);
            page.rewardMarker = Marker(p, "RewardMarker", "보상", UIMissionUIFactory.Hex(0x9fe6ff), RiverY - 110f, font, out page.rewardMarkerText);

            // 강 아래: 결과 글자 · 다음 보상 칩.
            UIMissionUIFactory.SetRect(page.StatusTextInternal.rectTransform, new Vector2(860f, 120f), new Vector2(0f, -30f));

            Image rewardChip = UIEventMinigameFactory.Chip(p, "RewardChip", new Vector2(950f, 84f), new Vector2(0f, -165f));
            page.rewardText = UIMissionUIFactory.CreateText(
                "RewardInfo", rewardChip.transform, string.Empty, 28f, TextAlignmentOptions.Center, Color.white, font);
            page.rewardText.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(page.rewardText.rectTransform, new Vector2(910f, 76f), Vector2.zero);

            // 반원 룰렛: 테두리 원 위에 8칸(원을 45° 부채꼴로 채움), 바퀴째 돈다. 아래 절반은 바닥 띠가 가린다.
            Circle(p, "WheelRim", WheelRadius * 2f + 36f, new Vector2(0f, WheelY), UIMissionUIFactory.Hex(0x2a2140));

            GameObject wheelRoot = UIMissionUIFactory.CreateRect("Wheel", p);
            UIMissionUIFactory.SetRect(wheelRoot.GetComponent<RectTransform>(), new Vector2(WheelRadius * 2f, WheelRadius * 2f), new Vector2(0f, WheelY));
            page.wheel = wheelRoot.GetComponent<RectTransform>();

            int cells = RaftingRules.CellCount;
            page.segmentImages = new Image[cells];
            page.cellLabels = new TMP_Text[cells];
            for (int i = 0; i < cells; i++)
            {
                // 위에서 시계 방향으로 칸 i 가 [i·45°, (i+1)·45°] 를 차지한다.
                Image segment = Circle(wheelRoot.transform, "Segment" + i, WheelRadius * 2f, Vector2.zero, SegmentA);
                segment.type = Image.Type.Filled;
                segment.fillMethod = Image.FillMethod.Radial360;
                segment.fillOrigin = (int)Image.Origin360.Top;
                segment.fillClockwise = true;
                segment.fillAmount = 1f / cells - 0.006f;
                segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -i * SegmentAngle);
                page.segmentImages[i] = segment;
            }

            for (int i = 0; i < cells; i++)
            {
                float angle = (i + 0.5f) * SegmentAngle;
                float radians = angle * Mathf.Deg2Rad;
                float r = WheelRadius * 0.7f;

                TMP_Text label = UIMissionUIFactory.CreateText(
                    "Label" + i, wheelRoot.transform, EventMinigameText.RaftingCell(i), 36f,
                    TextAlignmentOptions.Center, Color.white, font);
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.lineSpacing = -20f;
                UIMissionUIFactory.SetRect(label.rectTransform, new Vector2(200f, 100f),
                    new Vector2(Mathf.Sin(radians) * r, Mathf.Cos(radians) * r));
                label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
                page.cellLabels[i] = label;
            }

            Circle(p, "Hub", 280f, new Vector2(0f, WheelY), UIMissionUIFactory.Hex(0x2a2140));

            // 바늘: 바퀴 꼭대기에서 아래를 가리키는 노란 마름모.
            Image needle = UIMissionUIFactory.Sliced(p, "Needle", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(56f, 56f), new Vector2(0f, WheelY + WheelRadius + 18f), UIMissionUIFactory.Hex(0xffd84a));
            needle.raycastTarget = false;
            needle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // 바닥 띠(바퀴 아래 절반을 가리고 보유 보트 수를 올린다).
            Image floor = UIMissionUIFactory.Sliced(p, "Floor", UIEventMinigameFactory.PanelDark, 3f,
                new Vector2(1120f, 170f), new Vector2(0f, -885f), Color.white);
            floor.raycastTarget = true;
            page.CurrencyText.transform.SetAsLastSibling();
            UIMissionUIFactory.SetRect(page.CurrencyText.rectTransform, new Vector2(620f, 52f), new Vector2(0f, -920f));

            // 가운데 둥근 돌리기 버튼.
            Image spinImage = Circle(p, "SpinButton", 230f, new Vector2(0f, WheelY), UIMissionUIFactory.Hex(0x3fae5a));
            spinImage.raycastTarget = true;
            var spin = spinImage.gameObject.AddComponent<Button>();
            spin.targetGraphic = spinImage;
            ColorBlock colors = spin.colors;
            colors.disabledColor = UIEventMinigameFactory.DisabledTint;
            spin.colors = colors;
            page.spinButton = spin;

            page.spinLabel = UIMissionUIFactory.CreateText(
                "Label", spinImage.transform, "돌리기", 50f, TextAlignmentOptions.Center, Color.white, font);
            page.spinLabel.raycastTarget = false;
            UIMissionUIFactory.Stretch(page.spinLabel.rectTransform);

            // 바퀴 양옆: 배율(오른쪽, 레퍼런스의 x50 자리) · 보상(왼쪽).
            page.multiplierButton = UIEventMinigameFactory.KitButton(p, "MultiplierButton", "x1", EventButtonKind.Primary,
                new Vector2(160f, 96f), new Vector2(410f, -440f), 40f, font);
            page.multiplierLabel = page.multiplierButton.GetComponentInChildren<TMP_Text>();

            page.SetRewardButton(UIEventMinigameFactory.KitButton(p, "RewardButton", "보상", EventButtonKind.Secondary,
                new Vector2(200f, 100f), new Vector2(-400f, -440f), 38f, font));

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
