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
    /// 보물찾기 화면. 레퍼런스처럼 <b>사막 배경 위에 판이 한가운데</b> 있고, 위에 남은 보물 실루엣, 아래 독에 곡괭이 정보와 보상 버튼이 있다.
    /// <b>남은 보물의 실루엣만 보여 주고 확률은 보여 주지 않는다</b>(설계 합의). 보상은 스테이지 클리어당이라 "보상" 판에
    /// 스테이지별 보상을 나열한다.
    /// </summary>
    public sealed class UITreasureHuntPage : UIEventMinigameDialog
    {
        private const int MaxTiles = 64;
        private const int SilhouetteSlots = 7;
        private const int CellsPerSilhouette = 5;

        /// <summary>보상 판에 늘어놓는 스테이지 수(상자 3번째까지).</summary>
        private const int SheetStages = 36;

        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private TMP_Text dockText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private Button[] tiles;
        [SerializeField] private Image[] tileImages;
        [SerializeField] private TMP_Text[] tileLabels;
        [SerializeField] private GameObject[] silhouetteSlots;
        [SerializeField] private Image[] silhouetteCells;

        /// <summary>칸 그림: 아직 안 판 칸(회색 슬롯) / 보물 조각(주황 슬롯) / 다 찾은 보물(초록 슬롯).</summary>
        [SerializeField] private Sprite tileHiddenSprite;
        [SerializeField] private Sprite tileTreasureSprite;
        [SerializeField] private Sprite tileFoundSprite;

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

        protected override void RefreshPlay()
        {
            if (Manager == null)
                return;

            Manager.GetTreasureBoard(UtcNow, out board, out revealed);

            int stage = Manager.TreasureStage;
            int cost = TreasureHuntRules.DigCost(stage);
            stageText.text = "스테이지 " + stage;
            costText.text = "칸당 곡괭이 " + cost + "개";
            dockText.text = "곡괭이\n칸당 " + cost + "개";

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

            for (int i = 0; i < tiles.Length; i++)
            {
                bool active = i < cells;
                tiles[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                int owner = board.Owner[i];
                bool shown = revealed[i];

                Color color;
                Sprite sprite;
                string label = string.Empty;
                if (!shown)
                {
                    sprite = tileHiddenSprite;
                    color = UIEventMinigameFactory.TileHidden;
                }
                else if (owner < 0)
                {
                    // 판 빈칸은 같은 회색 슬롯을 어둡게 눌러 "파 보았다" 를 말한다.
                    sprite = tileHiddenSprite;
                    color = UIEventMinigameFactory.TileEmpty;
                }
                else
                {
                    bool found = TreasureHuntRules.IsTreasureFound(board, revealed, owner);
                    sprite = found ? tileFoundSprite : tileTreasureSprite;
                    color = found ? UIEventMinigameFactory.TileFound : UIEventMinigameFactory.TileTreasure;
                    label = ((char)('A' + owner)).ToString();
                }

                tileImages[i].sprite = sprite;
                tileImages[i].color = color;
                tileLabels[i].text = label;
                tiles[i].interactable = open && !shown;
            }
        }

        private void RefreshSilhouettes()
        {
            List<int> remaining = TreasureHuntRules.RemainingShapes(board, revealed);

            for (int slot = 0; slot < silhouetteSlots.Length; slot++)
            {
                bool active = slot < remaining.Count;
                silhouetteSlots[slot].SetActive(active);
                if (!active)
                    continue;

                // 남은 개수만큼 가운데로 모아 놓는다.
                var slotRect = (RectTransform)silhouetteSlots[slot].transform;
                slotRect.anchoredPosition = new Vector2((slot - (remaining.Count - 1) / 2f) * 116f, 590f);

                int[] cells = TreasureHuntRules.ShapeSilhouette(remaining[slot]);
                int count = cells.Length / 2;

                int maxX = 0;
                int maxY = 0;
                for (int c = 0; c < count; c++)
                {
                    if (cells[c * 2] > maxX) maxX = cells[c * 2];
                    if (cells[c * 2 + 1] > maxY) maxY = cells[c * 2 + 1];
                }

                const float step = 24f;
                for (int c = 0; c < CellsPerSilhouette; c++)
                {
                    Image cell = silhouetteCells[slot * CellsPerSilhouette + c];
                    cell.gameObject.SetActive(c < count);
                    if (c >= count)
                        continue;

                    // 칸 좌표(x 오른쪽, y 아래)를 슬롯 가운데로 모아 놓는다.
                    float px = (cells[c * 2] - maxX / 2f) * step;
                    float py = -(cells[c * 2 + 1] - maxY / 2f) * step;
                    cell.rectTransform.anchoredPosition = new Vector2(px, py);
                }
            }
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        public static UITreasureHuntPage Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIMissionUIFactory.CreateRect("UITreasureHuntPage", parent);
            UIMissionUIFactory.Stretch(root.GetComponent<RectTransform>());

            var page = root.AddComponent<UITreasureHuntPage>();
            page.BakeOpenStyle(DialogOpenStyle.Popup);

            Transform p = UIEventMinigameFactory.BakeScreen(page, root.transform, font, "보물찾기", "StageBg/Map_4");

            page.stageText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "StageInfo", 0, 34f, Color.white, font);
            page.costText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "CostInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);
            page.keyText = UIEventMinigameFactory.BadgeLine(p, "InfoRight", "KeyInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);

            UIMissionUIFactory.SetRect(page.StatusTextInternal.rectTransform, new Vector2(860f, 150f), new Vector2(0f, -330f));

            // 남은 보물 실루엣: 슬롯 7개 × 칸 5개.
            page.silhouetteSlots = new GameObject[SilhouetteSlots];
            page.silhouetteCells = new Image[SilhouetteSlots * CellsPerSilhouette];
            for (int slot = 0; slot < SilhouetteSlots; slot++)
            {
                GameObject slotRoot = UIMissionUIFactory.CreateRect("Silhouette" + slot, p);
                UIMissionUIFactory.SetRect(slotRoot.GetComponent<RectTransform>(), new Vector2(110f, 110f), new Vector2((slot - 3) * 116f, 590f));
                page.silhouetteSlots[slot] = slotRoot;

                for (int c = 0; c < CellsPerSilhouette; c++)
                {
                    Image cell = UIMissionUIFactory.Sliced(slotRoot.transform, "Cell" + c, UIEventMinigameFactory.SmallBox, 0.5f,
                        new Vector2(28f, 28f), Vector2.zero, UIEventMinigameFactory.GoldText);
                    cell.raycastTarget = false;
                    page.silhouetteCells[slot * CellsPerSilhouette + c] = cell;
                }
            }

            // 판: 최대 8×8. 가로 칸 수는 판마다 GridLayoutGroup 의 열 수로 바꾼다.
            GameObject gridRoot = UIMissionUIFactory.CreateRect("Grid", p);
            UIMissionUIFactory.SetRect(gridRoot.GetComponent<RectTransform>(), new Vector2(740f, 740f), new Vector2(0f, 120f));
            page.grid = gridRoot.AddComponent<GridLayoutGroup>();
            page.grid.cellSize = new Vector2(88f, 88f);
            page.grid.spacing = new Vector2(4f, 4f);
            page.grid.childAlignment = TextAnchor.MiddleCenter;
            page.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            page.grid.constraintCount = 8;

            page.tileHiddenSprite = UIMissionUIFactory.LoadSprite(UIEventMinigameFactory.SlotGrey);
            page.tileTreasureSprite = UIMissionUIFactory.LoadSprite(UIEventMinigameFactory.SlotGold);
            page.tileFoundSprite = UIMissionUIFactory.LoadSprite(UIEventMinigameFactory.SlotGreen);

            page.tiles = new Button[MaxTiles];
            page.tileImages = new Image[MaxTiles];
            page.tileLabels = new TMP_Text[MaxTiles];
            for (int i = 0; i < MaxTiles; i++)
            {
                Image image = UIMissionUIFactory.CreateImage("Tile" + i, gridRoot.transform, UIEventMinigameFactory.TileHidden);
                image.sprite = page.tileHiddenSprite;

                // 칸 그림(64²)은 보이는 틀이 45px 라 같은 칸 크기에서 작아 보인다 — 키워서 칸을 채운다.
                image.rectTransform.localScale = new Vector3(1.38f, 1.38f, 1f);

                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;

                TMP_Text label = UIMissionUIFactory.CreateText(
                    "Label", image.transform, string.Empty, 44f, TextAlignmentOptions.Center, UIEventMinigameFactory.DarkText, font);
                UIMissionUIFactory.Stretch(label.rectTransform);

                page.tiles[i] = button;
                page.tileImages[i] = image;
                page.tileLabels[i] = label;
            }

            // 이번 스테이지 보상과 다음 상자(레퍼런스의 상단 "Rewards" 줄).
            Image rewardChip = UIEventMinigameFactory.Chip(p, "RewardChip", new Vector2(950f, 150f), new Vector2(0f, -480f));
            page.rewardText = UIMissionUIFactory.CreateText(
                "RewardInfo", rewardChip.transform, string.Empty, 26f, TextAlignmentOptions.Center, Color.white, font);
            page.rewardText.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(page.rewardText.rectTransform, new Vector2(900f, 130f), Vector2.zero);

            // 독 가운데: 곡괭이 안내(누르는 버튼이 아니다).
            Image dockChip = UIMissionUIFactory.Sliced(p, "DockInfo", UIEventMinigameFactory.PanelLight, 3f,
                new Vector2(340f, 190f), new Vector2(0f, -765f), Color.white);
            dockChip.raycastTarget = false;
            page.dockText = UIMissionUIFactory.CreateText(
                "DockText", dockChip.transform, string.Empty, 38f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.dockText.rectTransform, new Vector2(320f, 160f), Vector2.zero);

            UIEventMinigameFactory.BakeRewardSheet(page, p, font);

            return page;
        }
    }
}
