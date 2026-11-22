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
    /// 보물찾기 화면. 칸을 파서 숨은 보물을 찾는다. <b>남은 보물의 실루엣만 보여 주고 확률은 보여 주지 않는다</b>(설계 합의).
    /// 보상은 스테이지 클리어당이라 마일스톤 목록 대신 이번 스테이지와 다음 상자의 보상을 적는다.
    /// </summary>
    public sealed class UITreasureHuntPage : UIEventMinigameDialog
    {
        private const int MaxTiles = 64;
        private const int SilhouetteSlots = 7;
        private const int CellsPerSilhouette = 5;

        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text keyText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private Button[] tiles;
        [SerializeField] private Image[] tileImages;
        [SerializeField] private TMP_Text[] tileLabels;
        [SerializeField] private GameObject[] silhouetteSlots;
        [SerializeField] private Image[] silhouetteCells;

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
            stageText.text = "스테이지 " + stage + "   칸당 곡괭이 " + TreasureHuntRules.DigCost(stage) + "개";

            int keys = Manager.TreasureKeys;
            int toChest = TreasureHuntRules.KeysPerChest - keys % TreasureHuntRules.KeysPerChest;
            keyText.text = "열쇠 " + keys + "개   (상자까지 " + toChest + "개)";

            int chest = (stage - 1) / TreasureHuntRules.StagesPerChest + 1;
            rewardText.text = "클리어 보상: " + EventMinigameText.Describe(TreasureHuntRules.StageReward(stage))
                              + "\n상자 " + chest + ": " + EventMinigameText.Describe(TreasureHuntRules.ChestReward(chest));

            RefreshGrid();
            RefreshSilhouettes();
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
                string label = string.Empty;
                if (!shown)
                {
                    color = UIEventMinigameFactory.TileHidden;
                }
                else if (owner < 0)
                {
                    color = UIEventMinigameFactory.TileEmpty;
                }
                else
                {
                    bool found = TreasureHuntRules.IsTreasureFound(board, revealed, owner);
                    color = found ? UIEventMinigameFactory.TileFound : UIEventMinigameFactory.TileTreasure;
                    label = ((char)('A' + owner)).ToString();
                }

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
                slotRect.anchoredPosition = new Vector2((slot - (remaining.Count - 1) / 2f) * 116f, 420f);

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

            Transform p = UIEventMinigameFactory.BakeFrame(page, root.transform, font, "보물찾기", false);

            // 목록이 없으니 상태 글자를 판 아래로 내린다.
            UIMissionUIFactory.SetRect(page.StatusTextInternal.rectTransform, new Vector2(840f, 150f), new Vector2(0f, -520f));

            page.stageText = UIMissionUIFactory.CreateText("StageInfo", p, string.Empty, 32f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.stageText.rectTransform, new Vector2(840f, 44f), new Vector2(0f, 548f));

            page.keyText = UIMissionUIFactory.CreateText("KeyInfo", p, string.Empty, 28f, TextAlignmentOptions.Center, UIEventMinigameFactory.MutedText, font);
            UIMissionUIFactory.SetRect(page.keyText.rectTransform, new Vector2(840f, 40f), new Vector2(0f, 504f));

            // 남은 보물 실루엣: 슬롯 7개 × 칸 5개.
            page.silhouetteSlots = new GameObject[SilhouetteSlots];
            page.silhouetteCells = new Image[SilhouetteSlots * CellsPerSilhouette];
            for (int slot = 0; slot < SilhouetteSlots; slot++)
            {
                GameObject slotRoot = UIMissionUIFactory.CreateRect("Silhouette" + slot, p);
                UIMissionUIFactory.SetRect(slotRoot.GetComponent<RectTransform>(), new Vector2(110f, 110f), new Vector2((slot - 3) * 116f, 420f));
                page.silhouetteSlots[slot] = slotRoot;

                for (int c = 0; c < CellsPerSilhouette; c++)
                {
                    Image cell = UIMissionUIFactory.CreateImage("Cell" + c, slotRoot.transform, UIEventMinigameFactory.TileTreasure);
                    UIMissionUIFactory.SetRect(cell.rectTransform, new Vector2(22f, 22f), Vector2.zero);
                    cell.raycastTarget = false;
                    page.silhouetteCells[slot * CellsPerSilhouette + c] = cell;
                }
            }

            // 판: 최대 8×8. 가로 칸 수는 판마다 GridLayoutGroup 의 열 수로 바꾼다.
            GameObject gridRoot = UIMissionUIFactory.CreateRect("Grid", p);
            UIMissionUIFactory.SetRect(gridRoot.GetComponent<RectTransform>(), new Vector2(740f, 740f), new Vector2(0f, -30f));
            page.grid = gridRoot.AddComponent<GridLayoutGroup>();
            page.grid.cellSize = new Vector2(88f, 88f);
            page.grid.spacing = new Vector2(4f, 4f);
            page.grid.childAlignment = TextAnchor.MiddleCenter;
            page.grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            page.grid.constraintCount = 8;

            page.tiles = new Button[MaxTiles];
            page.tileImages = new Image[MaxTiles];
            page.tileLabels = new TMP_Text[MaxTiles];
            for (int i = 0; i < MaxTiles; i++)
            {
                Image image = UIMissionUIFactory.CreateImage("Tile" + i, gridRoot.transform, UIEventMinigameFactory.TileHidden);
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;

                TMP_Text label = UIMissionUIFactory.CreateText(
                    "Label", image.transform, string.Empty, 44f, TextAlignmentOptions.Center, UIEventMinigameFactory.DarkText, font);
                UIMissionUIFactory.Stretch(label.rectTransform);

                page.tiles[i] = button;
                page.tileImages[i] = image;
                page.tileLabels[i] = label;
            }

            page.rewardText = UIMissionUIFactory.CreateText("RewardInfo", p, string.Empty, 26f, TextAlignmentOptions.Center, UIEventMinigameFactory.MutedText, font);
            page.rewardText.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(page.rewardText.rectTransform, new Vector2(840f, 120f), new Vector2(0f, -700f));

            return page;
        }
    }
}
