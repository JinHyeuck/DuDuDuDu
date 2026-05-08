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
        [SerializeField] private UIEquipmentDetailDialog detailDialog;
        [SerializeField] private UIEquipmentConfirmDialog confirmDialog;
        [SerializeField] private LobbyLayoutController lobbyLayoutController;

        public event Action OnDataChanged;

        private readonly List<UIEquipmentItem> equipmentItems = new List<UIEquipmentItem>();
        private readonly List<UIGemInventoryItem> gemInventoryItems = new List<UIGemInventoryItem>();
        private readonly List<EquipmentType> equipmentTypes = new List<EquipmentType>();

        private EquipmentType selectedEquipmentType = EquipmentType.Weapon;
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
        }

        protected override void OnEnter()
        {
            ValidateSceneReferences();
            Subscribe();
            BuildIfNeeded();
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
            RefreshGemInventory();
            NotifyChanged();
        }

        private void ValidateSceneReferences()
        {
            if (totalAttackText != null &&
                (manualEquipmentItems.Count > 0 || equipmentListRoot != null) &&
                gemInventoryRoot != null &&
                gemInventoryItemPrefab != null &&
                detailDialog != null &&
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
                {
                    equipmentItems.AddRange(manualEquipmentItems);
                }

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
                return;
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
            int itemIndex = 0;

            for (int i = 0; i < gemDefinitions.Count; i++)
            {
                GemDefinition definition = gemDefinitions[i];
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
            {
                gemInventoryItems[i].gameObject.SetActive(false);
            }
        }

        private void OnClickEquipmentItem(EquipmentType equipmentType)
        {
            selectedEquipmentType = equipmentType;
            RefreshEquipmentList();
            OpenDetailDialog();
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

            // TODO: Implement TryMergeAllGems in EquipmentManager
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

            if (levelUpAllButton != null)
                levelUpAllButton.onClick.AddListener(OnClickLevelUpAll);
            if (unEquipAllGemButton != null)
                unEquipAllGemButton.onClick.AddListener(OnClickUnEquipAllGems);
            if (mergeAllButton != null)
                mergeAllButton.onClick.AddListener(OnClickMergeAll);

            buttonsBound = levelUpAllButton != null || unEquipAllGemButton != null || mergeAllButton != null;
        }
    }
}
