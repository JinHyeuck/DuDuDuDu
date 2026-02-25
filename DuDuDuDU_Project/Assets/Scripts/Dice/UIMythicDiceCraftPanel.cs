using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace OJ
{
    public class UIMythicDiceCraftPanel : IDialog
    {
        [Header("UI")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIMythicDiceCraftItem itemPrefab;
        [SerializeField] private TMP_Text stateText;

        [Header("Behavior")]
        [SerializeField] private bool autoToggleByGameState = true;
        [SerializeField] private float refreshInterval = 0.2f;

        private readonly List<UIMythicDiceCraftItem> items = new List<UIMythicDiceCraftItem>();
        private readonly Dictionary<(DiceType type, int star), int> materialCounts = new Dictionary<(DiceType type, int star), int>();
        private readonly List<UIDice> consumeBuffer = new List<UIDice>();
        private float nextRefreshTime;

        private void Update()
        {
            if (autoToggleByGameState)
                SyncVisibilityWithState();

            if (!isEnter)
                return;

            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            RefreshAll();
        }

        protected override void OnLoad()
        {
            BuildIfNeeded();
            RefreshStateText();
        }

        protected override void OnEnter()
        {
            BuildIfNeeded();
            RefreshAll();
        }

        private void SyncVisibilityWithState()
        {
            bool shouldOpen = GameManager.Instance != null && GameManager.Instance.inGameState == InGameState.Setting;
            if (shouldOpen == isEnter)
                return;

            if (shouldOpen)
                Enter();
            else
                Exit();
        }

        private void BuildIfNeeded()
        {
            if (itemPrefab == null || listRoot == null || items.Count > 0)
                return;

            List<DiceType> mythics = DiceMetaDataProvider.GetMythicTypes();
            for (int i = 0; i < mythics.Count; i++)
            {
                UIMythicDiceCraftItem item = Instantiate(itemPrefab, listRoot);
                item.Bind(mythics[i], TryCraftAndRefresh, GetMaterialCount);
                items.Add(item);
            }
        }

        public void RefreshAll()
        {
            BuildMaterialCounts();
            RefreshStateText();

            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();
        }

        private bool TryCraftAndRefresh(DiceType mythicType)
        {
            bool crafted = TryCraft(mythicType);
            RefreshAll();
            return crafted;
        }

        private void RefreshStateText()
        {
            if (stateText == null)
                return;

            InGameState state = GameManager.Instance != null ? GameManager.Instance.inGameState : InGameState.None;
            bool boardReady = UIBoard.Instance != null && UIBoard.Instance.diceMap != null;
            stateText.SetText($"State: {state} / Board: {(boardReady ? "Ready" : "Missing")}");
        }

        private void BuildMaterialCounts()
        {
            materialCounts.Clear();
            if (UIBoard.Instance == null || UIBoard.Instance.diceMap == null)
                return;

            UIDice[] map = UIBoard.Instance.diceMap;
            for (int i = 0; i < map.Length; i++)
            {
                UIDice dice = map[i];
                if (dice == null)
                    continue;

                var key = (dice.Type, dice.Star);
                materialCounts.TryGetValue(key, out int count);
                materialCounts[key] = count + 1;
            }
        }

        private int GetMaterialCount(DiceType type, int star)
        {
            materialCounts.TryGetValue((type, star), out int count);
            return count;
        }

        private bool TryCraft(DiceType mythicType)
        {
            if (DiceMetaDataProvider.IsSummonable(mythicType))
                return false;

            if (UIBoard.Instance == null || UIBoard.Instance.diceMap == null)
                return false;

            int slotIndex = GetFirstEmptySlot();
            if (slotIndex < 0)
                return false;

            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (recipe == null || recipe.Count == 0)
                return false;

            consumeBuffer.Clear();
            UIDice[] map = UIBoard.Instance.diceMap;
            bool[] used = new bool[map.Length];

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                int found = 0;
                for (int idx = 0; idx < map.Length; idx++)
                {
                    if (used[idx])
                        continue;

                    UIDice dice = map[idx];
                    if (dice == null)
                        continue;

                    if (dice.Type != req.diceType || dice.Star != req.star)
                        continue;

                    used[idx] = true;
                    consumeBuffer.Add(dice);
                    found++;
                    if (found >= req.count)
                        break;
                }

                if (found < req.count)
                {
                    consumeBuffer.Clear();
                    return false;
                }
            }

            for (int i = 0; i < consumeBuffer.Count; i++)
            {
                UIDice dice = consumeBuffer[i];
                if (dice == null)
                    continue;

                DiceTypeStarManager.Instance.OnDiceRemove(dice.Type, dice.Star);
                Destroy(dice.gameObject);
            }

            const int mythicStar = 1;
            DiceTypeStarManager.Instance.OnDiceSpawn(mythicType, mythicStar);
            UIBoard.Instance.SpawnDice(mythicType, mythicStar, slotIndex);
            return true;
        }

        private int GetFirstEmptySlot()
        {
            UIDice[] map = UIBoard.Instance.diceMap;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] == null)
                    return i;
            }

            return -1;
        }
    }

    public class UIMythicDiceCraftItem : MonoBehaviour
    {
        [SerializeField] private Button craftButton;
        [SerializeField] private TMP_Text craftButtonText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image bgImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text recipeText;
        [SerializeField] private TMP_Text progressText;

        private DiceType mythicType;
        private System.Func<DiceType, bool> craftCallback;
        private System.Func<DiceType, int, int> materialCountProvider;
        private readonly StringBuilder lineBuilder = new StringBuilder(256);

        private void Awake()
        {
            if (craftButton != null)
                craftButton.onClick.AddListener(HandleCraftClick);
        }

        private void OnDestroy()
        {
            if (craftButton != null)
                craftButton.onClick.RemoveListener(HandleCraftClick);
        }

        public void Bind(
            DiceType type,
            System.Func<DiceType, bool> onCraft,
            System.Func<DiceType, int, int> countProvider)
        {
            mythicType = type;
            craftCallback = onCraft;
            materialCountProvider = countProvider;
            Refresh();
        }

        public void Refresh()
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(mythicType);
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);

            if (bgImage != null)
                bgImage.color = DiceMetaDataProvider.GetColor(mythicType);

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(mythicType);

            if (nameText != null)
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : mythicType.ToString());

            bool canCraft = BuildRecipeTexts(recipe);

            if (craftButton != null)
                craftButton.interactable = canCraft;

            if (craftButtonText != null)
                craftButtonText.SetText(canCraft ? "소환" : "재료 부족");
        }

        private bool BuildRecipeTexts(IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe)
        {
            if (recipe == null || recipe.Count == 0)
            {
                if (recipeText != null)
                    recipeText.SetText("조합식 없음");
                if (progressText != null)
                    progressText.SetText(string.Empty);
                return false;
            }

            bool canCraft = true;
            lineBuilder.Clear();
            int readyCount = 0;

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                int have = materialCountProvider != null ? materialCountProvider(req.diceType, req.star) : 0;
                bool ok = have >= req.count;
                if (!ok)
                    canCraft = false;
                else
                    readyCount++;

                lineBuilder.Append(req.star);
                lineBuilder.Append("★ ");
                lineBuilder.Append(req.diceType);
                lineBuilder.Append(" x");
                lineBuilder.Append(req.count);
                lineBuilder.Append(" (");
                lineBuilder.Append(have);
                lineBuilder.Append("/");
                lineBuilder.Append(req.count);
                lineBuilder.Append(")");
                if (i < recipe.Count - 1)
                    lineBuilder.Append('\n');
            }

            if (recipeText != null)
                recipeText.SetText(lineBuilder.ToString());
            if (progressText != null)
                progressText.SetText("{0}/{1}", readyCount, recipe.Count);

            return canCraft;
        }

        private void HandleCraftClick()
        {
            craftCallback?.Invoke(mythicType);
        }
    }
}
