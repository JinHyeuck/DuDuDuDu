using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentPage : IDialog
    {
        [Header("Summary")]
        [SerializeField] private TMP_Text totalAttackText;

        [Header("Equipment List")]
        [SerializeField] private Transform equipmentListRoot;
        [SerializeField] private UIEquipmentItem equipmentItemPrefab;

        [Header("Gem Inventory")]
        [SerializeField] private Transform gemInventoryRoot;
        [SerializeField] private UIGemInventoryItem gemInventoryItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button openDetailButton;
        [SerializeField] private Button levelUpAllButton;
        [SerializeField] private Button removeAllGemButton;

        [Header("Dialogs")]
        [SerializeField] private UIEquipmentDetailDialog detailDialog;
        [SerializeField] private UIEquipmentConfirmDialog confirmDialog;

        public event Action OnDataChanged;

        private readonly List<UIEquipmentItem> equipmentItems = new List<UIEquipmentItem>();
        private readonly List<UIGemInventoryItem> gemInventoryItems = new List<UIGemInventoryItem>();
        private readonly List<EquipmentType> equipmentTypes = new List<EquipmentType>();

        private EquipmentType selectedEquipmentType = EquipmentType.Weapon;
        private bool buttonsBound;

        protected override void OnLoad()
        {
            TryBindButtons();
        }

        protected override void OnUnload()
        {
            if (buttonsBound && openDetailButton != null)
                openDetailButton.onClick.RemoveListener(OpenDetailDialog);
            if (buttonsBound && levelUpAllButton != null)
                levelUpAllButton.onClick.RemoveListener(OnClickLevelUpAll);
            if (buttonsBound && removeAllGemButton != null)
                removeAllGemButton.onClick.RemoveListener(OnClickRemoveAllGems);
        }

        protected override void OnEnter()
        {
            EnsureRuntimeUI();
            Subscribe();
            BuildIfNeeded();
            RefreshAll();
        }

        protected override void OnExit()
        {
            Unsubscribe();
        }

        public void ConfigureRuntime(
            TMP_Text runtimeTotalAttackText,
            Transform runtimeEquipmentListRoot,
            UIEquipmentItem runtimeEquipmentItemPrefab,
            Transform runtimeGemInventoryRoot,
            UIGemInventoryItem runtimeGemInventoryItemPrefab,
            Button runtimeOpenDetailButton,
            Button runtimeLevelUpAllButton,
            Button runtimeRemoveAllGemButton,
            UIEquipmentDetailDialog runtimeDetailDialog,
            UIEquipmentConfirmDialog runtimeConfirmDialog)
        {
            totalAttackText = runtimeTotalAttackText;
            equipmentListRoot = runtimeEquipmentListRoot;
            equipmentItemPrefab = runtimeEquipmentItemPrefab;
            gemInventoryRoot = runtimeGemInventoryRoot;
            gemInventoryItemPrefab = runtimeGemInventoryItemPrefab;
            openDetailButton = runtimeOpenDetailButton;
            levelUpAllButton = runtimeLevelUpAllButton;
            removeAllGemButton = runtimeRemoveAllGemButton;
            detailDialog = runtimeDetailDialog;
            confirmDialog = runtimeConfirmDialog;

            TryBindButtons();
        }

        public void RefreshAll()
        {
            RefreshSummary();
            RefreshEquipmentList();
            RefreshGemInventory();
            NotifyChanged();
        }

        private void EnsureRuntimeUI()
        {
            if (totalAttackText != null &&
                equipmentListRoot != null &&
                equipmentItemPrefab != null &&
                gemInventoryRoot != null &&
                gemInventoryItemPrefab != null &&
                openDetailButton != null &&
                levelUpAllButton != null &&
                removeAllGemButton != null &&
                detailDialog != null &&
                confirmDialog != null)
            {
                return;
            }

            UIEquipmentRuntimeBuilder.Build(this);
        }

        private void BuildIfNeeded()
        {
            if (equipmentTypes.Count <= 0)
            {
                foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
                    equipmentTypes.Add(equipmentType);
            }

            if (equipmentItemPrefab != null && equipmentListRoot != null)
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

            totalAttackText.SetText("총 장비 공격력 {0}", EquipmentManager.Instance.GetTotalEquipmentAttack());
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
                bool unlocked = EquipmentManager.Instance.IsSlotUnlocked(equipmentType, slotIndex);
                if (!unlocked)
                {
                    states.Add(EquipmentSlotVisualState.Locked);
                    continue;
                }

                string gemId = EquipmentManager.Instance.GetEquippedGemId(equipmentType, slotIndex);
                if (string.IsNullOrEmpty(gemId))
                {
                    states.Add(EquipmentSlotVisualState.Empty);
                    continue;
                }

                if (!EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition gemDefinition) || gemDefinition == null)
                {
                    states.Add(EquipmentSlotVisualState.Empty);
                    continue;
                }

                switch (gemDefinition.rarity)
                {
                    case Rarity.Uncommon:
                        states.Add(EquipmentSlotVisualState.Uncommon);
                        break;
                    case Rarity.Common:
                        states.Add(EquipmentSlotVisualState.Common);
                        break;
                    case Rarity.Normal:
                        states.Add(EquipmentSlotVisualState.Normal);
                        break;
                    case Rarity.Rare:
                        states.Add(EquipmentSlotVisualState.Rare);
                        break;
                    case Rarity.Epic:
                        states.Add(EquipmentSlotVisualState.Epic);
                        break;
                    case Rarity.Mythic:
                        states.Add(EquipmentSlotVisualState.Mythic);
                        break;
                    default:
                        states.Add(EquipmentSlotVisualState.Empty);
                        break;
                }
            }

            return states;
        }

        private void RefreshGemInventory()
        {
            if (EquipmentManager.Instance == null)
                return;

            IReadOnlyList<GemDefinition> gemDefinitions = EquipmentManager.Instance.GetGemDefinitions();
            for (int i = 0; i < gemInventoryItems.Count; i++)
            {
                if (i >= gemDefinitions.Count)
                {
                    gemInventoryItems[i].gameObject.SetActive(false);
                    continue;
                }

                GemDefinition definition = gemDefinitions[i];
                int count = EquipmentManager.Instance.GetGemCount(definition.gemId);

                gemInventoryItems[i].gameObject.SetActive(true);
                gemInventoryItems[i].Bind(definition.gemId, OnClickGemInventoryItem);
                gemInventoryItems[i].Refresh(definition, count, false, true);
            }
        }

        private void OnClickEquipmentItem(EquipmentType equipmentType)
        {
            selectedEquipmentType = equipmentType;
            RefreshEquipmentList();
        }

        private void OnClickGemInventoryItem(string gemId)
        {
            if (string.IsNullOrEmpty(gemId) || EquipmentManager.Instance == null)
                return;

            if (EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition definition))
            {
                selectedEquipmentType = definition.equipableType;
                RefreshEquipmentList();
            }

            OpenDetailDialog();
        }

        private void OpenDetailDialog()
        {
            if (detailDialog == null)
                return;

            detailDialog.Open(selectedEquipmentType, RefreshAll);
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

        private void OnClickRemoveAllGems()
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

            if (openDetailButton != null)
                openDetailButton.onClick.AddListener(OpenDetailDialog);
            if (levelUpAllButton != null)
                levelUpAllButton.onClick.AddListener(OnClickLevelUpAll);
            if (removeAllGemButton != null)
                removeAllGemButton.onClick.AddListener(OnClickRemoveAllGems);

            buttonsBound = openDetailButton != null || levelUpAllButton != null || removeAllGemButton != null;
        }
    }
}
