using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentPage : IDialog
    {
        private enum EquipmentPageTab
        {
            EquippedEffects,
            Gems
        }

        [Header("Summary")]
        [SerializeField] private TMP_Text totalAttackText;

        [Header("Tabs")]
        [SerializeField] private Button equippedEffectTabButton;
        [SerializeField] private Button gemTabButton;
        [SerializeField] private GameObject equippedEffect_UnselectedTabIndicator;
        [SerializeField] private GameObject gem_UnselectedTabIndicator;
        [SerializeField] private GameObject equippedEffectView;
        [SerializeField] private GameObject gemInventoryView;

        [Header("Equipped Effects")]
        [SerializeField] private TMP_Text emptyEquippedEffectText;
        [SerializeField] private Transform equippedEffectRoot;
        [SerializeField] private TMP_Text equippedEffectItemPrefab;

        [Header("Equipment List")]
        [SerializeField] private List<UIEquipmentItem> manualEquipmentItems = new List<UIEquipmentItem>();
        [SerializeField] private Transform equipmentListRoot;
        [SerializeField] private UIEquipmentItem equipmentItemPrefab;

        [Header("Gem Inventory")]
        [SerializeField] private Transform gemInventoryRoot;
        [SerializeField] private UIGemInventoryItem gemInventoryItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button levelUpAllButton;
        [SerializeField] private Button unEquipAllGemButton;
        [SerializeField] private Button mergeAllButton;

        [Header("Dialogs")]
        [SerializeField] private UIEquipmentConfirmDialog confirmDialog;
        [SerializeField] private LobbyLayoutController lobbyLayoutController;

        public event Action OnDataChanged;

        private readonly List<UIEquipmentItem> equipmentItems = new List<UIEquipmentItem>();
        private readonly List<UIGemInventoryItem> gemInventoryItems = new List<UIGemInventoryItem>();
        private readonly List<TMP_Text> equippedEffectItems = new List<TMP_Text>();
        private readonly List<EquipmentType> equipmentTypes = new List<EquipmentType>();

        private EquipmentType selectedEquipmentType = EquipmentType.Weapon;
        private EquipmentPageTab currentTab = EquipmentPageTab.EquippedEffects;
        private bool buttonsBound;
        private bool missingReferenceWarned;

        protected override void OnLoad()
        {
            TryBindButtons();
        }

        protected override void OnUnload()
        {
            if (buttonsBound && levelUpAllButton != null)
                levelUpAllButton.onClick.RemoveListener(OnClickLevelUpAll);
            if (buttonsBound && unEquipAllGemButton != null)
                unEquipAllGemButton.onClick.RemoveListener(OnClickUnEquipAllGems);
            if (buttonsBound && mergeAllButton != null)
                mergeAllButton.onClick.RemoveListener(OnClickMergeAll);
            if (buttonsBound && equippedEffectTabButton != null)
                equippedEffectTabButton.onClick.RemoveListener(OnClickEquippedEffectTab);
            if (buttonsBound && gemTabButton != null)
                gemTabButton.onClick.RemoveListener(OnClickGemTab);
        }

        protected override void OnEnter()
        {
            ValidateSceneReferences();
            Subscribe();
            BuildIfNeeded();
            SelectTab(EquipmentPageTab.EquippedEffects, false);
            RefreshAll();
        }

        protected override void OnExit()
        {
            Unsubscribe();
        }

        public override void BackKeyCall()
        {
            lobbyLayoutController?.ShowTab(LobbyTab.Home);
        }

        public void RefreshAll()
        {
            RefreshSummary();
            RefreshEquipmentList();
            RefreshEquippedEffects();
            RefreshGemInventory();
            RefreshTabViews();
            NotifyChanged();
        }

        private void ValidateSceneReferences()
        {
            if (totalAttackText != null &&
                (manualEquipmentItems.Count > 0 || equipmentListRoot != null) &&
                equippedEffectRoot != null &&
                equippedEffectItemPrefab != null &&
                gemInventoryRoot != null &&
                gemInventoryItemPrefab != null &&
                confirmDialog != null)
            {
                return;
            }

            if (missingReferenceWarned)
                return;

            missingReferenceWarned = true;
            Debug.LogWarning("UIEquipmentPage: Scene UI references are missing. Assign the equipment UI fields in the Inspector.");
        }

        private void BuildIfNeeded()
        {
            if (equipmentTypes.Count <= 0)
            {
                foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
                    equipmentTypes.Add(equipmentType);
            }

            if (manualEquipmentItems != null && manualEquipmentItems.Count > 0)
            {
                if (equipmentItems.Count == 0)
                    equipmentItems.AddRange(manualEquipmentItems);

                for (int i = 0; i < equipmentItems.Count; i++)
                {
                    if (i < equipmentTypes.Count)
                    {
                        equipmentItems[i].gameObject.SetActive(true);
                        equipmentItems[i].Bind(equipmentTypes[i], OnClickEquipmentItem);
                    }
                    else
                    {
                        equipmentItems[i].gameObject.SetActive(false);
                    }
                }
            }
            else if (equipmentItemPrefab != null && equipmentListRoot != null)
            {
                while (equipmentItems.Count < equipmentTypes.Count)
                {
                    UIEquipmentItem item = Instantiate(equipmentItemPrefab, equipmentListRoot);
                    item.gameObject.SetActive(true);
                    equipmentItems.Add(item);
                }

                for (int i = 0; i < equipmentItems.Count; i++)
                {
                    if (i >= equipmentTypes.Count)
                    {
                        equipmentItems[i].gameObject.SetActive(false);
                        continue;
                    }

                    equipmentItems[i].gameObject.SetActive(true);
                    equipmentItems[i].Bind(equipmentTypes[i], OnClickEquipmentItem);
                }
            }

            if (gemInventoryItemPrefab != null && gemInventoryRoot != null && EquipmentManager.Instance != null)
            {
                IReadOnlyList<GemDefinition> gemDefinitions = EquipmentManager.Instance.GetGemDefinitions();
                while (gemInventoryItems.Count < gemDefinitions.Count)
                {
                    UIGemInventoryItem item = Instantiate(gemInventoryItemPrefab, gemInventoryRoot);
                    item.gameObject.SetActive(true);
                    gemInventoryItems.Add(item);
                }
            }
        }

        private void RefreshSummary()
        {
            if (totalAttackText == null || EquipmentManager.Instance == null)
                return;

            totalAttackText.SetText("{0}", EquipmentManager.Instance.GetTotalEquipmentAttack());
        }

        private void RefreshEquipmentList()
        {
            if (EquipmentManager.Instance == null)
                return;

            for (int i = 0; i < equipmentItems.Count; i++)
            {
                if (i >= equipmentTypes.Count)
                    continue;

                EquipmentType equipmentType = equipmentTypes[i];
                int level = EquipmentManager.Instance.GetLevel(equipmentType);
                int attack = EquipmentManager.Instance.GetEquipmentAttack(equipmentType);
                bool selected = selectedEquipmentType == equipmentType;
                List<EquipmentSlotVisualState> slotStates = BuildSlotStates(equipmentType);
                equipmentItems[i].Refresh(level, attack, selected, slotStates);
            }
        }

        private List<EquipmentSlotVisualState> BuildSlotStates(EquipmentType equipmentType)
        {
            List<EquipmentSlotVisualState> states = new List<EquipmentSlotVisualState>(Define.MaxEquipmentSlot);
            if (EquipmentManager.Instance == null)
                return states;

            for (int slotIndex = 0; slotIndex < Define.MaxEquipmentSlot; slotIndex++)
            {
                if (!EquipmentManager.Instance.IsSlotUnlocked(equipmentType, slotIndex))
                {
                    states.Add(EquipmentSlotVisualState.Locked);
                    continue;
                }

                string gemId = EquipmentManager.Instance.GetEquippedGemId(equipmentType, slotIndex);
                states.Add(string.IsNullOrEmpty(gemId) ? EquipmentSlotVisualState.Empty : EquipmentSlotVisualState.Equipped);
            }

            return states;
        }

        private void RefreshEquippedEffects()
        {
            List<string> effectLines = BuildEquippedEffectLines();

            RefreshEquippedEffectRows(effectLines);

            if (emptyEquippedEffectText != null)
            {
                emptyEquippedEffectText.gameObject.SetActive(effectLines.Count <= 0);
                if (effectLines.Count <= 0)
                    emptyEquippedEffectText.SetText("장착한 보석 효과가 없습니다.");
            }
        }

        private List<string> BuildEquippedEffectLines()
        {
            List<string> lines = new List<string>();
            if (EquipmentManager.Instance == null)
                return lines;

            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                for (int slotIndex = 0; slotIndex < Define.MaxEquipmentSlot; slotIndex++)
                {
                    string gemId = EquipmentManager.Instance.GetEquippedGemId(equipmentType, slotIndex);
                    if (string.IsNullOrEmpty(gemId))
                        continue;

                    if (!EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition definition) || definition == null)
                        continue;

                    string prefix = $"{UIEquipmentText.GetEquipmentName(equipmentType)} {slotIndex + 1} - {definition.displayName}";
                    if (definition.effects == null || definition.effects.Count <= 0)
                    {
                        lines.Add($"{prefix}: 효과 없음");
                        continue;
                    }

                    for (int effectIndex = 0; effectIndex < definition.effects.Count; effectIndex++)
                        lines.Add($"{prefix}: {UIEquipmentEffectTextFormatter.BuildEffectText(definition.effects[effectIndex])}");
                }
            }

            return lines;
        }

        private void RefreshEquippedEffectRows(IReadOnlyList<string> effectLines)
        {
            if (equippedEffectRoot == null || equippedEffectItemPrefab == null || effectLines == null)
                return;

            while (equippedEffectItems.Count < effectLines.Count)
            {
                TMP_Text item = Instantiate(equippedEffectItemPrefab, equippedEffectRoot);
                item.gameObject.SetActive(true);
                equippedEffectItems.Add(item);
            }

            for (int i = 0; i < equippedEffectItems.Count; i++)
            {
                TMP_Text item = equippedEffectItems[i];
                if (item == null)
                    continue;

                bool active = i < effectLines.Count;
                item.gameObject.SetActive(active);
                if (active)
                    item.SetText(effectLines[i]);
            }
        }

        private void RefreshGemInventory()
        {
            if (EquipmentManager.Instance == null)
                return;

            IReadOnlyList<GemDefinition> gemDefinitions = EquipmentManager.Instance.GetGemDefinitions();
            int itemIndex = 0;

            for (int i = 0; i < gemDefinitions.Count; i++)
            {
                GemDefinition definition = gemDefinitions[i];
                if (definition == null || definition.equipableType != selectedEquipmentType)
                    continue;

                int count = EquipmentManager.Instance.GetGemCount(definition.gemId);
                if (count <= 0)
                    continue;

                while (gemInventoryItems.Count <= itemIndex)
                {
                    if (gemInventoryItemPrefab == null || gemInventoryRoot == null)
                        break;

                    UIGemInventoryItem newItem = Instantiate(gemInventoryItemPrefab, gemInventoryRoot);
                    gemInventoryItems.Add(newItem);
                }

                if (itemIndex < gemInventoryItems.Count)
                {
                    gemInventoryItems[itemIndex].gameObject.SetActive(true);
                    gemInventoryItems[itemIndex].Bind(definition.gemId, OnClickGemInventoryItem);
                    gemInventoryItems[itemIndex].Refresh(definition, count, false, true);
                    itemIndex++;
                }
            }

            for (int i = itemIndex; i < gemInventoryItems.Count; i++)
                gemInventoryItems[i].gameObject.SetActive(false);
        }

        private void OnClickEquipmentItem(EquipmentType equipmentType)
        {
            selectedEquipmentType = equipmentType;
            RefreshEquipmentList();
            RefreshGemInventory();
            OpenEquipmentDialog(string.Empty);
        }

        private void OnClickGemInventoryItem(string gemId)
        {
            if (string.IsNullOrEmpty(gemId) || EquipmentManager.Instance == null)
                return;

            if (EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition definition))
            {
                selectedEquipmentType = definition.equipableType;
                RefreshEquipmentList();
                RefreshGemInventory();
            }

            OpenEquipmentDialog(gemId);
        }

        private void OpenEquipmentDialog(string selectedGemId)
        {
            if (confirmDialog == null)
            {
                Debug.LogWarning("UIEquipmentPage: UIEquipmentConfirmDialog is missing.");
                return;
            }

            confirmDialog.Open(selectedEquipmentType, selectedGemId, RefreshAll);
        }

        private void OnClickLevelUpAll()
        {
            if (confirmDialog == null)
            {
                DoLevelUpAll();
                return;
            }

            confirmDialog.Open(UIEquipmentText.GetLevelUpAllConfirmMessage(), DoLevelUpAll);
        }

        private void DoLevelUpAll()
        {
            if (EquipmentManager.Instance == null)
                return;

            int upgraded = EquipmentManager.Instance.TryLevelUpAll();
            if (upgraded > 0)
                RefreshAll();
        }

        private void OnClickUnEquipAllGems()
        {
            if (EquipmentManager.Instance == null)
                return;

            bool changed = false;
            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                for (int i = 0; i < Define.MaxEquipmentSlot; i++)
                {
                    if (EquipmentManager.Instance.UnequipGem(equipmentType, i))
                        changed = true;
                }
            }

            if (changed)
                RefreshAll();
        }

        private void OnClickMergeAll()
        {
            if (EquipmentManager.Instance == null)
                return;

            // TODO: Implement TryMergeAllGems in EquipmentManager.
            RefreshAll();
        }

        private void OnClickEquippedEffectTab()
        {
            SelectTab(EquipmentPageTab.EquippedEffects, true);
        }

        private void OnClickGemTab()
        {
            SelectTab(EquipmentPageTab.Gems, true);
        }

        private void SelectTab(EquipmentPageTab tab, bool refresh)
        {
            currentTab = tab;
            RefreshTabViews();

            if (refresh)
                RefreshAll();
        }

        private void RefreshTabViews()
        {
            bool showEquippedEffects = currentTab == EquipmentPageTab.EquippedEffects;

            if (equippedEffectView != null)
                equippedEffectView.SetActive(showEquippedEffects);
            if (gemInventoryView != null)
                gemInventoryView.SetActive(!showEquippedEffects);

            if (equippedEffect_UnselectedTabIndicator != null)
                equippedEffect_UnselectedTabIndicator.SetActive(!showEquippedEffects);
            if (gem_UnselectedTabIndicator != null)
                gem_UnselectedTabIndicator.SetActive(showEquippedEffects);
        }

        private void Subscribe()
        {
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnEquipmentChanged -= OnEquipmentChanged;
                EquipmentManager.Instance.OnGemChanged -= OnGemChanged;
                EquipmentManager.Instance.OnEquipmentChanged += OnEquipmentChanged;
                EquipmentManager.Instance.OnGemChanged += OnGemChanged;
            }

            if (PointManager.Instance != null)
            {
                PointManager.Instance.OnPointChanged -= OnPointChanged;
                PointManager.Instance.OnPointChanged += OnPointChanged;
            }
        }

        private void Unsubscribe()
        {
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnEquipmentChanged -= OnEquipmentChanged;
                EquipmentManager.Instance.OnGemChanged -= OnGemChanged;
            }

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        private void OnEquipmentChanged(EquipmentType equipmentType)
        {
            RefreshAll();
        }

        private void OnGemChanged()
        {
            RefreshAll();
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            RefreshAll();
        }

        private void NotifyChanged()
        {
            OnDataChanged?.Invoke();
        }

        private void TryBindButtons()
        {
            if (buttonsBound)
                return;

            if (levelUpAllButton != null)
                levelUpAllButton.onClick.AddListener(OnClickLevelUpAll);
            if (unEquipAllGemButton != null)
                unEquipAllGemButton.onClick.AddListener(OnClickUnEquipAllGems);
            if (mergeAllButton != null)
                mergeAllButton.onClick.AddListener(OnClickMergeAll);
            if (equippedEffectTabButton != null)
                equippedEffectTabButton.onClick.AddListener(OnClickEquippedEffectTab);
            if (gemTabButton != null)
                gemTabButton.onClick.AddListener(OnClickGemTab);

            buttonsBound = levelUpAllButton != null ||
                           unEquipAllGemButton != null ||
                           mergeAllButton != null ||
                           equippedEffectTabButton != null ||
                           gemTabButton != null;
        }
    }
}
