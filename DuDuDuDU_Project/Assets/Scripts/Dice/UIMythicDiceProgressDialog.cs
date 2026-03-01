using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIMythicDiceProgressDialog : IDialog
    {
        [Header("UI")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIMythicDiceProgressItem itemPrefab;
        [SerializeField] private Button openDetailButton;
        [SerializeField] private UIMythicDiceCraftPanel detailDialog;
        [SerializeField] private TMP_Text stateText;

        [Header("Behavior")]
        [SerializeField] private bool showOnlyInSetting = true;
        [SerializeField] private float refreshInterval = 0.25f;

        private readonly List<UIMythicDiceProgressItem> items = new List<UIMythicDiceProgressItem>();
        private float nextRefreshTime;

        private void Update()
        {
            SyncVisibility();

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
            if (openDetailButton != null)
                openDetailButton.onClick.AddListener(OpenDetailDialog);
            RefreshStateText();
        }

        protected override void OnUnload()
        {
            if (openDetailButton != null)
                openDetailButton.onClick.RemoveListener(OpenDetailDialog);
        }

        protected override void OnEnter()
        {
            BuildIfNeeded();
            RefreshAll();
        }

        private void SyncVisibility()
        {
            bool shouldOpen = !showOnlyInSetting || (GameManager.Instance != null && GameManager.Instance.inGameState == InGameState.Setting);
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
                UIMythicDiceProgressItem item = Instantiate(itemPrefab, listRoot);
                item.Bind(mythics[i], GetRecipeProgressPercent, HandleClickMythic);
                items.Add(item);
            }
        }

        private void RefreshAll()
        {
            RefreshStateText();

            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();

            SortItemsByPercentDesc();
        }

        private void RefreshStateText()
        {
            if (stateText == null)
                return;

            InGameState state = GameManager.Instance != null ? GameManager.Instance.inGameState : InGameState.None;
            stateText.SetText($"조합 진행도 - {state}");
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

            detailDialog.Enter();
            detailDialog.RefreshAll();
        }

    }
}
