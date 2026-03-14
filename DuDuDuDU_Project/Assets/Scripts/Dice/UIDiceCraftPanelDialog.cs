using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIDiceCraftPanelDialog : IDialog
    {
        [Header("List")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIDiceCraftProgressItem listItemPrefab;

        [Header("Detail Header")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image iconImage;

        [Header("Detail Materials")]
        [SerializeField] private Transform materialRoot;
        [SerializeField] private UIDiceCraftMaterialStatusItem materialItemPrefab;

        [Header("Detail Action")]
        [SerializeField] private Button summonButton;
        [SerializeField] private TMP_Text summonButtonText;

        private readonly List<UIDiceCraftProgressItem> items = new List<UIDiceCraftProgressItem>();
        private readonly List<UIDiceCraftMaterialStatusItem> materialItems = new List<UIDiceCraftMaterialStatusItem>();
        private readonly Dictionary<DiceType, UIDiceCraftProgressItem> itemMap = new Dictionary<DiceType, UIDiceCraftProgressItem>();
        private readonly List<UIDice> consumeBuffer = new List<UIDice>();
        private readonly List<DiceType> mythicTypes = new List<DiceType>();

        private DiceType selectedMythicType = DiceType.KingNormal;
        private bool hasSelection;

        protected override void OnLoad()
        {
            BuildIfNeeded();

            if (summonButton != null)
                summonButton.onClick.AddListener(OnClickSummon);

            if (DiceTypeStarManager.Instance != null)
                DiceTypeStarManager.Instance.OnDiceInventoryChanged += HandleDiceInventoryChanged;
        }

        protected override void OnUnload()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(OnClickSummon);

            if (DiceTypeStarManager.Instance != null)
                DiceTypeStarManager.Instance.OnDiceInventoryChanged -= HandleDiceInventoryChanged;
        }

        protected override void OnEnter()
        {
            BuildIfNeeded();

            if (DiceTypeStarManager.Instance != null)
            {
                DiceTypeStarManager.Instance.OnDiceInventoryChanged -= HandleDiceInventoryChanged;
                DiceTypeStarManager.Instance.OnDiceInventoryChanged += HandleDiceInventoryChanged;
            }

            RefreshAll();
        }

        public void Open(DiceType mythicType)
        {
            if (!hasSelection || mythicTypes.Contains(mythicType))
            {
                selectedMythicType = mythicType;
                hasSelection = true;
            }

            Enter();
        }

        public void RefreshAll()
        {
            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();

            SortItemsByPercentDesc();

            if (!hasSelection || !mythicTypes.Contains(selectedMythicType))
                SelectBestItem();

            RefreshSelectionState();
            RefreshDetail();
        }

        private void BuildIfNeeded()
        {
            if (listItemPrefab == null || listRoot == null || items.Count > 0)
                return;

            mythicTypes.Clear();
            mythicTypes.AddRange(DiceMetaDataProvider.GetMythicTypes());

            for (int i = 0; i < mythicTypes.Count; i++)
            {
                DiceType type = mythicTypes[i];
                UIDiceCraftProgressItem item = Instantiate(listItemPrefab, listRoot);
                item.Bind(type, GetRecipeProgressPercent, OnClickListItem, false);
                items.Add(item);
                itemMap[type] = item;
            }
        }

        private void OnClickListItem(DiceType mythicType)
        {
            selectedMythicType = mythicType;
            hasSelection = true;
            RefreshSelectionState();
            RefreshDetail();
        }

        private void RefreshSelectionState()
        {
            foreach (var pair in itemMap)
                pair.Value.SetSelected(pair.Key == selectedMythicType);
        }

        private void SelectBestItem()
        {
            if (mythicTypes.Count == 0)
                return;

            DiceType best = mythicTypes[0];
            int bestPercent = GetRecipeProgressPercent(best);

            for (int i = 1; i < mythicTypes.Count; i++)
            {
                DiceType type = mythicTypes[i];
                int percent = GetRecipeProgressPercent(type);
                if (percent > bestPercent)
                {
                    best = type;
                    bestPercent = percent;
                }
            }

            selectedMythicType = best;
            hasSelection = true;
        }

        private void RefreshDetail()
        {
            if (!hasSelection)
                return;

            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(selectedMythicType);
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(selectedMythicType);

            if (nameText != null)
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : selectedMythicType.ToString());

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(selectedMythicType);

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(selectedMythicType) : 1;
            if (levelText != null)
                levelText.SetText("Lv.{0}", level);

            bool canCraft = DiceTypeStarManager.Instance != null && DiceTypeStarManager.Instance.CanCraft(recipe);
            if (summonButton != null)
                summonButton.interactable = canCraft;
            if (summonButtonText != null)
                summonButtonText.SetText(canCraft ? "소환" : "재료 부족");

            RefreshMaterialSlots(recipe);
        }

        private void RefreshMaterialSlots(IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe)
        {
            int visibleCount = 0;
            if (recipe != null)
            {
                for (int i = 0; i < recipe.Count; i++)
                {
                    DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                    int have = DiceTypeStarManager.Instance != null
                        ? DiceTypeStarManager.Instance.GetTypeStarCount(req.diceType, req.star)
                        : 0;

                    for (int unit = 0; unit < req.count; unit++)
                    {
                        bool owned = have > unit;
                        UIDiceCraftMaterialStatusItem item = GetOrCreateMaterialItem(visibleCount);
                        if (item == null)
                            continue;
                        item.gameObject.SetActive(true);
                        item.Bind(req.diceType, req.star, owned);
                        visibleCount++;
                    }
                }
            }

            HideMaterialSlotsFrom(visibleCount);
        }

        private void HideMaterialSlotsFrom(int start)
        {
            for (int i = start; i < materialItems.Count; i++)
            {
                if (materialItems[i] != null)
                    materialItems[i].gameObject.SetActive(false);
            }
        }

        private UIDiceCraftMaterialStatusItem GetOrCreateMaterialItem(int index)
        {
            if (index < materialItems.Count && materialItems[index] != null)
                return materialItems[index];

            if (materialItemPrefab == null || materialRoot == null)
                return null;

            UIDiceCraftMaterialStatusItem created = Instantiate(materialItemPrefab, materialRoot);
            materialItems.Add(created);
            return created;
        }

        private void SortItemsByPercentDesc()
        {
            items.Sort((a, b) =>
            {
                int pa = GetRecipeProgressPercent(a.MythicType);
                int pb = GetRecipeProgressPercent(b.MythicType);
                return pb.CompareTo(pa);
            });

            for (int i = 0; i < items.Count; i++)
                items[i].transform.SetSiblingIndex(i);
        }

        private int GetRecipeProgressPercent(DiceType mythicType)
        {
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (DiceTypeStarManager.Instance == null || recipe == null || recipe.Count == 0)
                return 0;

            return DiceTypeStarManager.Instance.GetRecipeProgressPercent(recipe);
        }

        private void OnClickSummon()
        {
            if (!hasSelection)
                return;

            if (!TryCraft(selectedMythicType))
                return;

            Exit();
        }

        private bool TryCraft(DiceType mythicType)
        {
            if (DiceMetaDataProvider.IsSummonable(mythicType))
                return false;

            if (UIBoard.Instance == null || UIBoard.Instance.diceMap == null)
                return false;

            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (recipe == null || recipe.Count == 0)
                return false;

            if (DiceTypeStarManager.Instance == null || !DiceTypeStarManager.Instance.CanCraft(recipe))
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

                if (dice.SlotIndex >= 0 && dice.SlotIndex < map.Length && map[dice.SlotIndex] == dice)
                    map[dice.SlotIndex] = null;

                DiceTypeStarManager.Instance.OnDiceRemove(dice.Type, dice.Star);
                Destroy(dice.gameObject);
            }

            int slotIndex = GetFirstEmptySlot();
            if (slotIndex < 0)
                return false;

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

        private void HandleDiceInventoryChanged()
        {
            if (!isEnter)
                return;

            RefreshAll();
        }
    }
}
