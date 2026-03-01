using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIDiceCraftProgressDialog : IDialog
    {
        [Header("UI")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIDiceCraftProgressItem itemPrefab;
        [SerializeField] private Button openDetailButton;
        [SerializeField] private UIDiceCraftPanelDialog detailDialog;

        [Header("Behavior")]
        [SerializeField] private bool showOnlyInSetting = true;

        private readonly List<UIDiceCraftProgressItem> items = new List<UIDiceCraftProgressItem>();
        private DiceType selectedMythicType = DiceType.KingNormal;
        private bool hasSelection;

        private void Update()
        {
            if (!showOnlyInSetting || GameManager.Instance == null)
                return;

            bool shouldOpen = GameManager.Instance.inGameState == InGameState.Setting;
            if (shouldOpen && !isEnter)
                Enter();
            else if (!shouldOpen && isEnter)
                Exit();
        }

        protected override void OnLoad()
        {
            BuildIfNeeded();
            if (openDetailButton != null)
                openDetailButton.onClick.AddListener(OpenDetailDialog);
            if (DiceTypeStarManager.Instance != null)
                DiceTypeStarManager.Instance.OnDiceInventoryChanged += HandleDiceInventoryChanged;
        }

        protected override void OnUnload()
        {
            if (openDetailButton != null)
                openDetailButton.onClick.RemoveListener(OpenDetailDialog);
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

        private void HandleDiceInventoryChanged()
        {
            if (!isEnter)
                return;

            RefreshAll();
        }

        private void BuildIfNeeded()
        {
            if (itemPrefab == null || listRoot == null || items.Count > 0)
                return;

            List<DiceType> mythics = DiceMetaDataProvider.GetMythicTypes();
            for (int i = 0; i < mythics.Count; i++)
            {
                UIDiceCraftProgressItem item = Instantiate(itemPrefab, listRoot);
                item.Bind(mythics[i], GetRecipeProgressPercent, HandleClickMythic, true);
                items.Add(item);
            }
        }

        private void RefreshAll()
        {
            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();

            SortItemsByPercentDesc();
        }

        private int GetRecipeProgressPercent(DiceType mythicType)
        {
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (DiceTypeStarManager.Instance == null || recipe == null || recipe.Count == 0)
                return 0;

            return DiceTypeStarManager.Instance.GetRecipeProgressPercent(recipe);
        }

        private void HandleClickMythic(DiceType mythicType)
        {
            if (detailDialog == null)
                return;

            detailDialog.Open(mythicType);

            return;

            selectedMythicType = mythicType;
            hasSelection = true;
            OpenDetailDialog();
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

        private void OpenDetailDialog()
        {
            if (detailDialog == null)
                return;

            if (!hasSelection && items.Count > 0)
                selectedMythicType = items[0].MythicType;

            detailDialog.Open(selectedMythicType);
        }

    }
}
