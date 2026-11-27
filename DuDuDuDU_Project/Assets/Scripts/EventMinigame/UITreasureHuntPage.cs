using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Mission;
using OJ.UI;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 보물찾기 화면(레퍼런스 영상 구성). 위쪽 상단바(스테이지 + <b>보물 진행 게이지</b> / 열쇠), 그 아래 <b>빨간 스캐너 판</b>에 이번 스테이지
    /// 보물들의 실루엣(찾으면 색이 켜진다), 가운데 <b>파이프 틀 안의 파란 칸 판</b>, 아래에 곡괭이 수와 보상 버튼. 독 패널은 없다.
    /// 칸을 누르면 곡괭이가 내리찍고 먼지가 터지며, 판 칸은 갈색 바닥이 되고 보물 조각은 초록으로 드러난다.
    /// <b>남은 보물의 실루엣만 보여 주고 확률은 보여 주지 않는다</b>(설계 합의). 보상은 스테이지 클리어당이라 "보상" 판에
    /// 스테이지별 보상을 나열한다.
    /// </summary>
    public sealed class UITreasureHuntPage : UIEventMinigameDialog
    {
        private const int MaxTiles = 64;

        /// <summary>한 판의 보물은 최대 7개(8단계). 실루엣 자리와 보물 그림 자리를 같은 수로 둔다.</summary>
        private const int SilhouetteSlots = 7;

        // 칸 한 줄 간격(칸 88 + 틈 4)과 칸 판 가운데 높이. 보물 그림을 칸 위에 정확히 깔 때 쓴다.
        private const float CellStep = 92f;
        private const float GridCenterY = -110f;

        // 자리 표시 보물 그림 색(모양 번호 순: 1×1 / 2×1 / 2×2 / 3×1 / 3×2 / 3×3). 아트가 오면 그림으로 바뀐다.
        private static readonly Color[] TreasureColors =
        {
            new Color(1f, 0.84f, 0.29f, 1f),
            new Color(1f, 0.48f, 0.72f, 1f),
            new Color(0.43f, 0.88f, 0.48f, 1f),
            new Color(1f, 0.6f, 0.24f, 1f),
            new Color(0.69f, 0.49f, 1f, 1f),
            new Color(0.3f, 0.84f, 1f, 1f),
        };

        /// <summary>보상 판에 늘어놓는 스테이지 수(상자 3번째까지).</summary>
        private const int SheetStages = 36;

        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private TMP_Text dockText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private GridLayoutGroup coverGrid;
        [SerializeField] private Image[] covers;
        [SerializeField] private Button[] tiles;
        [SerializeField] private Image[] tileImages;
        [SerializeField] private Image[] treasureImages;
        [SerializeField] private TMP_Text[] treasureLabels;
        [SerializeField] private Image[] silhouetteImages;
        [SerializeField] private Image stageFill;
        [SerializeField] private RectTransform gridFrameRect;
        [SerializeField] private RectTransform pickaxe;
        [SerializeField] private RectTransform dust;
        [SerializeField] private Image dustImage;

        private const float StageBarWidth = 394f;
        private const float DigSeconds = 0.55f;

        // 스캐너 판 위의 실루엣 자리.
        private const float ScannerY = 430f;
        private const float ScannerInnerWidth = 640f;

        private static readonly Color CoverColor = new Color(0.165f, 0.196f, 0.439f, 1f);
        private static readonly Color SilhouetteDark =new Color(0.42f, 0.32f, 0.32f, 1f);
        private static readonly Color SilhouetteFound = new Color(0.45f, 1f, 0.58f, 1f);

        private float digTime = 99f;
        private Vector2 digPosition;

        /// <summary>칸 그림(회색 슬롯). 안 판 칸은 파랗게, 판 빈칸은 어둡게 칠해 쓴다.</summary>
        [SerializeField] private Sprite tileHiddenSprite;

        private TreasureBoard board;
        private bool[] revealed;

        protected override EventKind Kind
        {
            get { return EventKind.TreasureHunt; }
        }

        protected override void OnLoadPlay()
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                int index = i;
                tiles[i].onClick.AddListener(() => OnTile(index));
            }
        }

        protected override void OnOpened()
        {
            SetStatus(string.Empty);
        }

        protected override int SheetFocusRow()
        {
            return Manager != null ? Manager.TreasureStage - 1 : 0;
        }

        private void OnTile(int index)
        {
            if (!IsOpenNow || Manager == null || board == null)
                return;

            int x = index % board.Width;
            int y = index / board.Width;
            int cost = TreasureHuntRules.DigCost(Manager.TreasureStage);

            if (HeldCurrency < cost)
            {
                SetStatus("곡괭이가 모자라요");
                return;
            }

            TreasureDigOutcome outcome = Manager.Dig(x, y, UtcNow);
            if (!outcome.Valid)
                return;

            StartDigAnimation(index);

            if (outcome.StageCleared)
            {
                var text = new StringBuilder("스테이지 ").Append(outcome.Stage).Append(" 클리어!");
                if (outcome.KeyGranted)
                    text.Append("\n열쇠를 얻었어요");
                if (outcome.ChestOpened > 0)
                    text.Append("\n상자 ").Append(outcome.ChestOpened).Append("번이 열렸어요");

                SetStatus(text.ToString());
                ShowRewards(outcome.Rewards, "스테이지 " + outcome.Stage + " 클리어");
            }
            else if (outcome.Dig.TreasureCompleted)
            {
                SetStatus("보물을 하나 다 찾았어요!");
            }
            else if (outcome.Dig.IsHit)
            {
                SetStatus("보물 조각을 찾았어요");
            }
            else
            {
                SetStatus("빈 칸이에요");
            }
        }

        private void StartDigAnimation(int tileIndex)
        {
            var screen = (RectTransform)pickaxe.parent;
            Vector3 local = screen.InverseTransformPoint(tiles[tileIndex].transform.position);
            digPosition = new Vector2(local.x, local.y);
            digTime = 0f;
        }

        /// <summary>곡괭이가 칸 위에서 내리찍히고(0~0.2초) 먼지가 퍼지며 사라진다.</summary>
        protected override void OnFrame()
        {
            digTime += Time.unscaledDeltaTime;
            bool swing = digTime < DigSeconds;

            pickaxe.gameObject.SetActive(swing && digTime < 0.3f);
            dust.gameObject.SetActive(swing && digTime >= 0.16f);
            if (!swing)
                return;

            float hit = Mathf.Clamp01(digTime / 0.16f);
            float angle = Mathf.Lerp(55f, -25f, hit * hit);
            pickaxe.anchoredPosition = digPosition + new Vector2(14f, -6f);
            pickaxe.localEulerAngles = new Vector3(0f, 0f, angle);

            float t = Mathf.Clamp01((digTime - 0.16f) / (DigSeconds - 0.16f));
            dust.anchoredPosition = digPosition;
            dust.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.7f, t);
            Color c = dustImage.color;
            c.a = 0.85f * (1f - t);
            dustImage.color = c;
        }

        protected override void RefreshPlay()
        {
            if (Manager == null)
                return;

            Manager.GetTreasureBoard(UtcNow, out board, out revealed);

            int stage = Manager.TreasureStage;
            int cost = TreasureHuntRules.DigCost(stage);
            stageText.text = "스테이지 " + stage;
            dockText.text = "칸당 곡괭이 " + cost + "개";

            int foundCount = 0;
            for (int t = 0; t < board.TreasureCount; t++)
            {
                if (TreasureHuntRules.IsTreasureFound(board, revealed, t))
                    foundCount++;
            }

            costText.text = "보물 " + foundCount + " / " + board.TreasureCount;
            UIMissionUIFactory.SetGauge(stageFill, StageBarWidth, board.TreasureCount > 0 ? (float)foundCount / board.TreasureCount : 0f);

            int keys = Manager.TreasureKeys;
            int toChest = TreasureHuntRules.KeysPerChest - keys % TreasureHuntRules.KeysPerChest;
            keyText.text = "열쇠 " + keys + "개 (상자까지 " + toChest + "개)";

            int chest = (stage - 1) / TreasureHuntRules.StagesPerChest + 1;
            rewardText.text = "클리어 보상  " + EventMinigameText.Describe(TreasureHuntRules.StageReward(stage))
                              + "\n상자 " + chest + "  " + EventMinigameText.Describe(TreasureHuntRules.ChestReward(chest));

            RefreshGrid();
            RefreshSilhouettes();
        }

        protected override void RefreshList()
        {
            if (Manager == null)
                return;

            var list = EnsureRows(SheetStages);
            int current = Manager.TreasureStage;

            for (int i = 0; i < SheetStages; i++)
            {
                int stage = i + 1;
                string label = "스테이지 " + stage;
                string reward = EventMinigameText.Describe(TreasureHuntRules.StageReward(stage));

                if (TreasureHuntRules.GrantsKey(stage))
                    label += "\n열쇠 +1";

                int chest = TreasureHuntRules.ChestIndexAt(stage);
                if (chest > 0)
                    reward += "\n상자 " + chest + ": " + EventMinigameText.Describe(TreasureHuntRules.ChestReward(chest));

                UIEventMilestoneRow.RowState state = stage < current
                    ? UIEventMilestoneRow.RowState.Reached
                    : (stage == current ? UIEventMilestoneRow.RowState.Next : UIEventMilestoneRow.RowState.Locked);

                list[i].Bind(label, reward, state);
            }
        }

        private void RefreshGrid()
        {
            bool open = IsOpenNow;
            int cells = board.Width * board.Height;
            grid.constraintCount = board.Width;
            coverGrid.constraintCount = board.Width;

            // 파이프 틀은 판 크기에 맞춘다(칸 92px 간격 + 가장자리).
            gridFrameRect.sizeDelta = new Vector2(board.Width * CellStep + 56f, board.Height * CellStep + 56f);

            for (int i = 0; i < tiles.Length; i++)
            {
                bool active = i < cells;
                tiles[i].gameObject.SetActive(active);
                covers[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                bool shown = revealed[i];

                // 덮개는 끄지 않고 투명하게 둔다 — GridLayoutGroup 은 꺼진 자식을 건너뛰어 나머지 덮개가 밀린다.
                covers[i].color = shown ? Color.clear : CoverColor;

                // 안 판 칸은 파란 칸. 판 칸 중 빈칸은 어두운 바닥, 보물 칸은 칸을 투명하게 해서 아래의 보물 그림이 비친다.
                tileImages[i].sprite = tileHiddenSprite;
                if (!shown)
                    tileImages[i].color = UIEventMinigameFactory.TileHidden;
                else if (board.Owner[i] < 0)
                    tileImages[i].color = UIEventMinigameFactory.TileEmpty;
                else
                    tileImages[i].color = Color.clear;

                tiles[i].interactable = open && !shown;
            }

            RefreshTreasures();
        }

        /// <summary>
        /// 보물은 칸마다 쪼개지 않고 <b>한 장의 그림</b>(1×1~3×3)으로, 차지한 칸 전체 위에 깐다. 위의 파란 칸이 걷혀야 보인다.
        /// 아직 자리 표시(색 블록 + 크기 글자)다.
        /// </summary>
        private void RefreshTreasures()
        {
            for (int t = 0; t < treasureImages.Length; t++)
            {
                bool active = t < board.TreasureCount;
                treasureImages[t].gameObject.SetActive(active);
                if (!active)
                    continue;

                TreasureHuntRules.TreasureRect(board, t, out int x, out int y, out int w, out int h);

                RectTransform rect = treasureImages[t].rectTransform;
                rect.anchoredPosition = new Vector2(
                    (x + w / 2f - board.Width / 2f) * CellStep,
                    GridCenterY - (y + h / 2f - board.Height / 2f) * CellStep);
                rect.sizeDelta = new Vector2(w * CellStep - 4f, h * CellStep - 4f);

                bool found = TreasureHuntRules.IsTreasureFound(board, revealed, t);
                Color color = TreasureColors[Mathf.Clamp(board.TreasureShapes[t], 0, TreasureColors.Length - 1)];
                treasureImages[t].color = found ? color : new Color(color.r * 0.82f, color.g * 0.82f, color.b * 0.82f, 1f);

                treasureLabels[t].text = Mathf.Max(w, h) + "x" + Mathf.Min(w, h);
                treasureLabels[t].fontSize = Mathf.Clamp(Mathf.Min(w, h) * 36f, 34f, 84f);
            }
        }

        private void RefreshSilhouettes()
        {
            int count = board.TreasureCount;

            // 보물이 적을수록 실루엣을 크게(최대 52px 칸), 많으면 작게 — 스캐너 안쪽 폭에 다 들어가게.
            float slotStep = Mathf.Min(170f, ScannerInnerWidth / Mathf.Max(1, count));
            float cellStep = Mathf.Clamp(slotStep / 3.6f, 18f, 52f);

            for (int slot = 0; slot < silhouetteImages.Length; slot++)
            {
                bool active = slot < count;
                silhouetteImages[slot].gameObject.SetActive(active);
                if (!active)
                    continue;

                // 보물은 한 장짜리 직사각형이라 실루엣도 칸 수만큼 큰 블록 하나다(가로가 긴 쪽으로 눕힌다).
                int[] cells = TreasureHuntRules.ShapeSilhouette(board.TreasureShapes[slot]);
                int maxX = 0;
                int maxY = 0;
                for (int c = 0; c < cells.Length; c += 2)
                {
                    if (cells[c] > maxX) maxX = cells[c];
                    if (cells[c + 1] > maxY) maxY = cells[c + 1];
                }

                int w = Mathf.Max(maxX, maxY) + 1;
                int h = Mathf.Min(maxX, maxY) + 1;

                // 보물 수만큼 스캐너 가운데로 모아 놓는다. 찾은 보물은 색이 켜진다.
                RectTransform rect = silhouetteImages[slot].rectTransform;
                rect.anchoredPosition = new Vector2((slot - (count - 1) / 2f) * slotStep, ScannerY);
                rect.sizeDelta = new Vector2(w * cellStep - 3f, h * cellStep - 3f);
                silhouetteImages[slot].color = TreasureHuntRules.IsTreasureFound(board, revealed, slot) ? SilhouetteFound : SilhouetteDark;
            }
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        public static UITreasureHuntPage Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIMissionUIFactory.CreateRect("UITreasureHuntPage", parent);
            UIMissionUIFactory.Stretch(root.GetComponent<RectTransform>());

            var page = root.AddComponent<UITreasureHuntPage>();
            page.BakeOpenStyle(DialogOpenStyle.Popup);

            Transform p = UIEventMinigameFactory.BakeScreen(page, root.transform, font, "보물찾기", "StageBg/Map_4", false);

            // 상단바: 왼쪽 = 스테이지 + 보물 진행 게이지, 오른쪽 = 남은 시간 + 열쇠.
            page.stageText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "StageInfo", 0, 34f, Color.white, font);
            page.keyText = UIEventMinigameFactory.BadgeLine(p, "InfoRight", "KeyInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);

            Transform leftBadge = p.Find("InfoLeft");
            page.stageFill = UIMissionUIFactory.CreateGauge("StageBar", leftBadge,
                new Vector2(StageBarWidth + 6f, 30f), new Vector2(0f, -30f), UIMissionUIFactory.Hex(0x3ee0f0), UIMissionUIFactory.TrackColor, 3f);
            page.costText = UIMissionUIFactory.CreateText(
                "StageBarText", leftBadge, string.Empty, 22f, TextAlignmentOptions.Center, Color.white, font);
            page.costText.raycastTarget = false;
            UIMissionUIFactory.SetRect(page.costText.rectTransform, new Vector2(StageBarWidth, 30f), new Vector2(0f, -30f));

            UIMissionUIFactory.SetRect(page.StatusTextInternal.rectTransform, new Vector2(860f, 100f), new Vector2(0f, -560f));
            UIMissionUIFactory.SetRect(page.CurrencyText.rectTransform, new Vector2(620f, 56f), new Vector2(0f, -800f));

            // 스캐너 판: 빨간 틀 + 어두운 안쪽. 이번 스테이지 보물의 실루엣이 켜지고 꺼진다.
            Image scanner = UIMissionUIFactory.Sliced(p, "Scanner", UIEventMinigameFactory.PanelLight, 4f,
                new Vector2(720f, 340f), new Vector2(0f, ScannerY), UIMissionUIFactory.Hex(0xc2452b));
            scanner.raycastTarget = false;
            Image scannerInner = UIMissionUIFactory.Sliced(p, "ScannerInner", UIEventMinigameFactory.SmallBox, 1.5f,
                new Vector2(660f, 250f), new Vector2(0f, ScannerY), UIMissionUIFactory.Hex(0x3a2a2a));
            scannerInner.raycastTarget = false;

            // 남은 보물 실루엣: 보물 하나당 직사각형 블록 하나(최대 7개).
            page.silhouetteImages = new Image[SilhouetteSlots];
            for (int slot = 0; slot < SilhouetteSlots; slot++)
            {
                Image block = UIMissionUIFactory.Sliced(p, "Silhouette" + slot, UIEventMinigameFactory.SmallBox, 0.5f,
                    new Vector2(60f, 60f), new Vector2((slot - 3) * 96f, ScannerY), SilhouetteDark);
                block.raycastTarget = false;
                page.silhouetteImages[slot] = block;
            }

            // 파이프 틀(어두운 청보라) 안에 판. 최대 8×8, 가로 칸 수는 판마다 GridLayoutGroup 의 열 수로 바꾼다.
            Image gridFrame = UIMissionUIFactory.Sliced(p, "GridFrame", UIEventMinigameFactory.PanelDark, 3f,
                new Vector2(810f, 810f), new Vector2(0f, -110f), UIMissionUIFactory.Hex(0x4a52a8));
            gridFrame.raycastTarget = false;
            page.gridFrameRect = gridFrame.rectTransform;

            // 보물 그림(칸 아래 층): 보물 하나가 차지한 칸 전체를 덮는 한 장. 위의 덮개·칸이 걷히면 비친다.
            page.treasureImages = new Image[SilhouetteSlots];
            page.treasureLabels = new TMP_Text[SilhouetteSlots];
            for (int t = 0; t < SilhouetteSlots; t++)
            {
                Image art = UIMissionUIFactory.Sliced(p, "Treasure" + t, UIEventMinigameFactory.SmallBox, 3f,
                    new Vector2(88f, 88f), new Vector2(0f, -110f), TreasureColors[0]);
                art.raycastTarget = false;

                TMP_Text label = UIMissionUIFactory.CreateText(
                    "Label", art.transform, string.Empty, 60f, TextAlignmentOptions.Center, UIEventMinigameFactory.DarkText, font);
                label.raycastTarget = false;
                UIMissionUIFactory.Stretch(label.rectTransform);

                page.treasureImages[t] = art;
                page.treasureLabels[t] = label;
            }

            // 덮개 층: 안 판 칸 밑에 깔리는 불투명 바닥(칸 사이 틈으로 보물 색이 새어 나오지 않게 한다).
            GameObject coverRoot = UIMissionUIFactory.CreateRect("Covers", p);
            UIMissionUIFactory.SetRect(coverRoot.GetComponent<RectTransform>(), new Vector2(740f, 740f), new Vector2(0f, GridCenterY));
            page.coverGrid = coverRoot.AddComponent<GridLayoutGroup>();
            page.coverGrid.cellSize = new Vector2(CellStep, CellStep);
            page.coverGrid.spacing = Vector2.zero;
            page.coverGrid.childAlignment = TextAnchor.MiddleCenter;
            page.coverGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            page.coverGrid.constraintCount = 8;

            page.covers = new Image[MaxTiles];
            for (int i = 0; i < MaxTiles; i++)
            {
                Image cover = UIMissionUIFactory.CreateImage("Cover" + i, coverRoot.transform, CoverColor);
                cover.raycastTarget = false;
                page.covers[i] = cover;
            }

            GameObject gridRoot = UIMissionUIFactory.CreateRect("Grid", p);
            UIMissionUIFactory.SetRect(gridRoot.GetComponent<RectTransform>(), new Vector2(740f, 740f), new Vector2(0f, GridCenterY));
            page.grid = gridRoot.AddComponent<GridLayoutGroup>();
            page.grid.cellSize = new Vector2(88f, 88f);
            page.grid.spacing = new Vector2(4f, 4f);
            page.grid.childAlignment = TextAnchor.MiddleCenter;
            page.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            page.grid.constraintCount = 8;

            // 칸 그림: 회색 슬롯을 색으로 칠해 쓴다(안 판 칸 = 파랑, 판 빈칸 = 어두운 갈색, 보물 칸 = 투명).
            page.tileHiddenSprite = UIMissionUIFactory.LoadSprite(UIEventMinigameFactory.SlotGrey);

            page.tiles = new Button[MaxTiles];
            page.tileImages = new Image[MaxTiles];
            for (int i = 0; i < MaxTiles; i++)
            {
                Image image = UIMissionUIFactory.CreateImage("Tile" + i, gridRoot.transform, UIEventMinigameFactory.TileHidden);
                image.sprite = page.tileHiddenSprite;

                // 칸 그림(64²)은 보이는 틀이 45px 라 같은 칸 크기에서 작아 보인다 — 키워서 칸을 채운다.
                image.rectTransform.localScale = new Vector3(1.38f, 1.38f, 1f);

                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;

                page.tiles[i] = button;
                page.tileImages[i] = image;
            }

            // 곡괭이 연출: 내리찍는 곡괭이(자루 + 머리) + 먼지. 칸 위에서만 잠깐 보인다.
            GameObject pickaxeRoot = UIMissionUIFactory.CreateRect("Pickaxe", p);
            RectTransform pickaxeRect = pickaxeRoot.GetComponent<RectTransform>();
            pickaxeRect.pivot = new Vector2(0.5f, 0f);
            pickaxeRect.sizeDelta = new Vector2(24f, 130f);
            page.pickaxe = pickaxeRect;

            Image handle = UIMissionUIFactory.CreateImage("Handle", pickaxeRoot.transform, UIMissionUIFactory.Hex(0x8a5a2c));
            handle.raycastTarget = false;
            UIMissionUIFactory.Stretch(handle.rectTransform);
            Image head = UIMissionUIFactory.Sliced(pickaxeRoot.transform, "Head", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(96f, 34f), new Vector2(0f, 118f), UIMissionUIFactory.Hex(0xb9c3d6));
            head.raycastTarget = false;
            pickaxeRoot.SetActive(false);

            Image dustImage = UIMissionUIFactory.Sliced(p, "Dust", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(110f, 110f), Vector2.zero, new Color(1f, 0.96f, 0.85f, 0.85f));
            dustImage.raycastTarget = false;
            page.dust = dustImage.rectTransform;
            page.dustImage = dustImage;
            dustImage.gameObject.SetActive(false);

            // 이번 스테이지 보상과 다음 상자(레퍼런스의 상단 "Rewards" 줄).
            Image rewardChip = UIEventMinigameFactory.Chip(p, "RewardChip", new Vector2(950f, 120f), new Vector2(0f, -690f));
            page.rewardText = UIMissionUIFactory.CreateText(
                "RewardInfo", rewardChip.transform, string.Empty, 26f, TextAlignmentOptions.Center, Color.white, font);
            page.rewardText.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(page.rewardText.rectTransform, new Vector2(900f, 100f), Vector2.zero);

            // 아래: 칸당 곡괭이 안내(가운데) · 보상 버튼(오른쪽). 보유 수는 공통 재화 글자가 보여 준다.
            page.dockText = UIMissionUIFactory.CreateText(
                "DockText", p, string.Empty, 30f, TextAlignmentOptions.Center, UIEventMinigameFactory.MutedText, font);
            page.dockText.raycastTarget = false;
            UIMissionUIFactory.SetRect(page.dockText.rectTransform, new Vector2(620f, 44f), new Vector2(0f, -855f));

            page.SetRewardButton(UIEventMinigameFactory.KitButton(p, "RewardButton", "보상", EventButtonKind.Secondary,
                new Vector2(220f, 116f), new Vector2(400f, -820f), 38f, font));

            UIEventMinigameFactory.BakeRewardSheet(page, p, font);

            return page;
        }
    }
}
