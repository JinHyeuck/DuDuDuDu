using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentConfirmDialog : IDialog
    {
        [Header("Equipment Detail")]
        [SerializeField] private GameObject equipmentDetailView;
        [SerializeField] private TMP_Text equipmentTitleText;
        [SerializeField] private TMP_Text equipmentTypeText;
        [SerializeField] private TMP_Text equipmentLevelText;
        [SerializeField] private TMP_Text equipmentAttackText;
        [SerializeField] private Image equipmentIconImage;

        [Header("Equipment Effects")]
        [SerializeField] private TMP_Text equipmentEffectSummaryText;
        [SerializeField] private TMP_Text emptyEquipmentEffectText;
        [SerializeField] private List<TMP_Text> manualEquipmentEffectTexts = new List<TMP_Text>();
        [SerializeField] private Transform equipmentEffectRoot;
        [SerializeField] private TMP_Text equipmentEffectItemPrefab;

        [Header("Slots")]
        [SerializeField] private Transform slotRoot;
        [SerializeField] private UIEquipmentGemSlotItem slotItemPrefab;

        [Header("Gem Inventory")]
        [SerializeField] private Transform gemInventoryRoot;
        [SerializeField] private UIGemInventoryItem gemInventoryItemPrefab;
        [SerializeField] private TMP_Text emptyGemInventoryText;

        [Header("Selected Gem")]
        [SerializeField] private GameObject gemInfoEmptyView;
        [SerializeField] private GameObject gemInfoFilledView;
        [SerializeField] private TMP_Text selectedGemEmptyText;
        [SerializeField] private TMP_Text selectedGemNameText;
        [SerializeField] private TMP_Text selectedGemRarityText;
        [SerializeField] private TMP_Text selectedGemEquipTypeText;
        [SerializeField] private TMP_Text selectedGemCountText;
        [SerializeField] private TMP_Text selectedGemEffectText;
        [SerializeField] private Image selectedGemFrameImage;
        [SerializeField] private Image selectedGemIconImage;
        [SerializeField] private Image selectedGemEquipTypeIconImage;

        [Header("Cost")]
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private TMP_Text scrollCostText;
        [SerializeField] private Image scrollCostIconImage;

        [Header("Detail Buttons")]
        [SerializeField] private Button levelUpButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unequipButton;
        [SerializeField] private Button closeButton;

        private readonly List<UIEquipmentGemSlotItem> slotItems = new List<UIEquipmentGemSlotItem>();
        private readonly List<UIGemInventoryItem> gemInventoryItems = new List<UIGemInventoryItem>();
        private readonly List<TMP_Text> equipmentEffectItems = new List<TMP_Text>();

        private System.Action onChanged;
        private EquipmentType equipmentType;
        private string selectedGemId = string.Empty;
        private int selectedSlotIndex = -1;
        private bool buttonsBound;

        protected override void OnLoad()
        {
            TryBindButtons();
        }

        protected override void OnUnload()
        {
            if (buttonsBound && levelUpButton != null)
                levelUpButton.onClick.RemoveListener(OnClickLevelUp);
            if (buttonsBound && equipButton != null)
                equipButton.onClick.RemoveListener(OnClickEquip);
            if (buttonsBound && unequipButton != null)
                unequipButton.onClick.RemoveListener(OnClickUnequip);
            if (buttonsBound && closeButton != null)
                closeButton.onClick.RemoveListener(OnClickClose);
        }

        public void Open(EquipmentType type, string gemId, System.Action changedCallback)
        {
            equipmentType = type;
            selectedGemId = gemId ?? string.Empty;
            onChanged = changedCallback;
            selectedSlotIndex = FindDefaultSlotIndex();

            if (equipmentDetailView != null)
                equipmentDetailView.SetActive(true);

            Enter();
            RefreshEquipmentDetail();
        }

        private void RefreshEquipmentDetail()
        {
            if (EquipmentManager.Instance == null)
                return;

            int level = EquipmentManager.Instance.GetLevel(equipmentType);
            int attack = EquipmentManager.Instance.GetEquipmentAttack(equipmentType);
            (int goldCost, int scrollCost) = EquipmentManager.Instance.GetNextUpgradeCost(equipmentType);

            if (equipmentTitleText != null)
                equipmentTitleText.SetText(UIEquipmentText.GetEquipmentName(equipmentType));
            if (equipmentTypeText != null)
                equipmentTypeText.SetText(UIEquipmentText.GetEquipmentName(equipmentType));
            if (equipmentLevelText != null)
                equipmentLevelText.SetText("Lv.{0}", level);
            if (equipmentAttackText != null)
                equipmentAttackText.SetText("ATK {0}", attack);
            SetImage(equipmentIconImage, UIEquipmentSpriteResolver.GetEquipmentLargeIconSprite(equipmentType), false);

            if (goldCostText != null)
                goldCostText.SetText("Gold {0}/{1}", PointManager.Instance != null ? PointManager.Instance.Get(PointType.Gold) : 0, goldCost);

            PointType scrollType = PointManager.ToEquipmentScrollType(equipmentType);
            if (scrollCostText != null)
                scrollCostText.SetText("Scroll {0}/{1}", PointManager.Instance != null ? PointManager.Instance.Get(scrollType) : 0, scrollCost);
            SetImage(scrollCostIconImage, GetScrollCostIconSprite(scrollType), false);

            BuildSlotIfNeeded();
            RefreshSlots();
            RefreshGemInventory();
            RefreshEquipmentEffects();
            RefreshSelectedGemInfo();
            RefreshDetailButtons();
        }

        private void BuildSlotIfNeeded()
        {
            if (slotItemPrefab == null || slotRoot == null)
                return;

            while (slotItems.Count < Define.MaxEquipmentSlot)
            {
                int index = slotItems.Count;
                UIEquipmentGemSlotItem item = Instantiate(slotItemPrefab, slotRoot);
                item.gameObject.SetActive(true);
                item.Bind(index, OnClickSlot);
                slotItems.Add(item);
            }
        }

        private void RefreshSlots()
        {
            if (EquipmentManager.Instance == null)
                return;

            for (int i = 0; i < slotItems.Count; i++)
            {
                UIEquipmentGemSlotItem item = slotItems[i];
                if (item == null)
                    continue;

                bool unlocked = EquipmentManager.Instance.IsSlotUnlocked(equipmentType, i);
                int unlockLevel = EquipmentManager.Instance.GetSlotUnlockLevel(i);
                GemDefinition definition = null;

                string equippedGemId = EquipmentManager.Instance.GetEquippedGemId(equipmentType, i);
                if (!string.IsNullOrEmpty(equippedGemId))
                    EquipmentManager.Instance.TryGetGemDefinition(equippedGemId, out definition);

                item.gameObject.SetActive(true);
                item.Refresh(unlocked, unlockLevel, definition, i == selectedSlotIndex);
            }
        }

        private void RefreshGemInventory()
        {
            if (EquipmentManager.Instance == null)
                return;

            IReadOnlyList<GemDefinition> definitions = EquipmentManager.Instance.GetGemDefinitions();
            int itemIndex = 0;

            for (int i = 0; i < definitions.Count; i++)
            {
                GemDefinition definition = definitions[i];
                if (definition == null || definition.equipableType != equipmentType)
                    continue;

                int count = EquipmentManager.Instance.GetGemCount(definition.gemId);
                if (count <= 0)
                    continue;

                while (gemInventoryItems.Count <= itemIndex)
                {
                    if (gemInventoryRoot == null || gemInventoryItemPrefab == null)
                        break;

                    UIGemInventoryItem item = Instantiate(gemInventoryItemPrefab, gemInventoryRoot);
                    item.gameObject.SetActive(true);
                    gemInventoryItems.Add(item);
                }

                if (itemIndex < gemInventoryItems.Count)
                {
                    bool selected = selectedGemId == definition.gemId;

                    gemInventoryItems[itemIndex].gameObject.SetActive(true);
                    gemInventoryItems[itemIndex].Bind(definition.gemId, OnClickGemInventoryItem);
                    gemInventoryItems[itemIndex].Refresh(definition, count, selected, true);
                    itemIndex++;
                }
            }

            for (int i = itemIndex; i < gemInventoryItems.Count; i++)
                gemInventoryItems[i].gameObject.SetActive(false);

            if (emptyGemInventoryText != null)
            {
                emptyGemInventoryText.gameObject.SetActive(itemIndex <= 0);
                if (itemIndex <= 0)
                    emptyGemInventoryText.SetText("장착 가능한 보석이 없습니다.");
            }
        }

        private void RefreshEquipmentEffects()
        {
            List<string> lines = BuildEquipmentEffectLines();

            if (equipmentEffectSummaryText != null)
                equipmentEffectSummaryText.SetText(lines.Count > 0 ? string.Join("\n", lines) : "현재 효과 없음");

            RefreshEquipmentEffectRows(lines);

            if (emptyEquipmentEffectText != null)
            {
                emptyEquipmentEffectText.gameObject.SetActive(lines.Count <= 0 && equipmentEffectSummaryText == null);
                if (lines.Count <= 0)
                    emptyEquipmentEffectText.SetText("현재 효과 없음");
            }
        }

        private List<string> BuildEquipmentEffectLines()
        {
            List<string> lines = new List<string>();
            if (EquipmentManager.Instance == null)
                return lines;

            for (int slotIndex = 0; slotIndex < Define.MaxEquipmentSlot; slotIndex++)
            {
                if (!EquipmentManager.Instance.IsSlotUnlocked(equipmentType, slotIndex))
                {
                    lines.Add($"슬롯 {slotIndex + 1}: Lv.{EquipmentManager.Instance.GetSlotUnlockLevel(slotIndex)} 해금");
                    continue;
                }

                string gemId = EquipmentManager.Instance.GetEquippedGemId(equipmentType, slotIndex);
                if (string.IsNullOrEmpty(gemId))
                    continue;

                if (!EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition definition) || definition == null)
                    continue;

                if (definition.effects == null || definition.effects.Count <= 0)
                {
                    lines.Add($"슬롯 {slotIndex + 1}: 효과 없음");
                    continue;
                }

                for (int effectIndex = 0; effectIndex < definition.effects.Count; effectIndex++)
                    lines.Add($"슬롯 {slotIndex + 1}: {UIEquipmentEffectTextFormatter.BuildEffectText(definition.effects[effectIndex])}");
            }

            return lines;
        }

        private void RefreshEquipmentEffectRows(IReadOnlyList<string> lines)
        {
            if (manualEquipmentEffectTexts != null && manualEquipmentEffectTexts.Count > 0)
            {
                for (int i = 0; i < manualEquipmentEffectTexts.Count; i++)
                {
                    TMP_Text text = manualEquipmentEffectTexts[i];
                    if (text == null)
                        continue;

                    bool active = lines != null && i < lines.Count;
                    text.gameObject.SetActive(active);
                    if (active)
                        text.SetText(lines[i]);
                }

                return;
            }

            if (equipmentEffectRoot == null || equipmentEffectItemPrefab == null || lines == null)
                return;

            while (equipmentEffectItems.Count < lines.Count)
            {
                TMP_Text item = Instantiate(equipmentEffectItemPrefab, equipmentEffectRoot);
                item.gameObject.SetActive(true);
                equipmentEffectItems.Add(item);
            }

            for (int i = 0; i < equipmentEffectItems.Count; i++)
            {
                TMP_Text item = equipmentEffectItems[i];
                if (item == null)
                    continue;

                bool active = i < lines.Count;
                item.gameObject.SetActive(active);
                if (active)
                    item.SetText(lines[i]);
            }
        }

        private void RefreshSelectedGemInfo()
        {
            GemDefinition definition = null;
            bool hasGem = !string.IsNullOrEmpty(selectedGemId) &&
                          EquipmentManager.Instance != null &&
                          EquipmentManager.Instance.TryGetGemDefinition(selectedGemId, out definition) &&
                          definition != null;

            if (gemInfoEmptyView != null)
                gemInfoEmptyView.SetActive(!hasGem);
            if (gemInfoFilledView != null)
                gemInfoFilledView.SetActive(hasGem);

            if (!hasGem)
            {
                if (selectedGemEmptyText != null)
                    selectedGemEmptyText.SetText("Empty");
                if (selectedGemNameText != null)
                    selectedGemNameText.SetText("Empty");
                if (selectedGemRarityText != null)
                    selectedGemRarityText.SetText(string.Empty);
                if (selectedGemEquipTypeText != null)
                    selectedGemEquipTypeText.SetText(string.Empty);
                if (selectedGemCountText != null)
                    selectedGemCountText.SetText(string.Empty);
                if (selectedGemEffectText != null)
                    selectedGemEffectText.SetText(string.Empty);

                SetImage(selectedGemFrameImage, null, false);
                SetImage(selectedGemIconImage, null, false);
                SetImage(selectedGemEquipTypeIconImage, null, false);
                return;
            }

            if (selectedGemNameText != null)
                selectedGemNameText.SetText(definition.displayName);
            if (selectedGemRarityText != null)
                selectedGemRarityText.SetText(UIEquipmentText.GetRarityName(definition.rarity));
            if (selectedGemEquipTypeText != null)
                selectedGemEquipTypeText.SetText(UIEquipmentText.GetEquipmentName(definition.equipableType));
            if (selectedGemCountText != null)
                selectedGemCountText.SetText("x{0}", EquipmentManager.Instance.GetGemCount(definition.gemId));
            if (selectedGemEffectText != null)
                selectedGemEffectText.SetText(UIEquipmentEffectTextFormatter.BuildGemDescription(definition));

            SetImage(selectedGemFrameImage, UIEquipmentSpriteResolver.GetGemFrameSprite(definition.rarity), true);
            SetImage(selectedGemIconImage, UIEquipmentSpriteResolver.GetGemIconSprite(definition.rarity), true);
            SetImage(selectedGemEquipTypeIconImage, UIEquipmentSpriteResolver.GetEquipmentSmallIconSprite(definition.equipableType), false);
        }

        private void RefreshDetailButtons()
        {
            bool slotReady = selectedSlotIndex >= 0 &&
                             EquipmentManager.Instance != null &&
                             EquipmentManager.Instance.IsSlotUnlocked(equipmentType, selectedSlotIndex);
            bool hasSelectedGem = !string.IsNullOrEmpty(selectedGemId) &&
                                  EquipmentManager.Instance != null &&
                                  EquipmentManager.Instance.GetGemCount(selectedGemId) > 0;
            bool hasEquippedGem = slotReady &&
                                  !string.IsNullOrEmpty(EquipmentManager.Instance.GetEquippedGemId(equipmentType, selectedSlotIndex));

            if (levelUpButton != null)
                levelUpButton.interactable = EquipmentManager.Instance != null;
            if (equipButton != null)
                equipButton.interactable = slotReady && hasSelectedGem;
            if (unequipButton != null)
                unequipButton.interactable = hasEquippedGem;
        }

        private int FindDefaultSlotIndex()
        {
            if (EquipmentManager.Instance == null)
                return -1;

            if (!string.IsNullOrEmpty(selectedGemId))
            {
                for (int i = 0; i < Define.MaxEquipmentSlot; i++)
                {
                    if (!EquipmentManager.Instance.IsSlotUnlocked(equipmentType, i))
                        continue;

                    if (string.IsNullOrEmpty(EquipmentManager.Instance.GetEquippedGemId(equipmentType, i)))
                        return i;
                }
            }

            for (int i = 0; i < Define.MaxEquipmentSlot; i++)
            {
                if (EquipmentManager.Instance.IsSlotUnlocked(equipmentType, i))
                    return i;
            }

            return -1;
        }

        private void OnClickSlot(int slotIndex)
        {
            selectedSlotIndex = slotIndex;
            RefreshEquipmentDetail();
        }

        private void OnClickGemInventoryItem(string gemId)
        {
            if (string.IsNullOrEmpty(gemId) || EquipmentManager.Instance == null)
                return;

            if (!EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition definition) ||
                definition == null ||
                definition.equipableType != equipmentType)
            {
                return;
            }

            selectedGemId = gemId;
            RefreshGemInventory();
            RefreshSelectedGemInfo();
            RefreshDetailButtons();
        }

        private void OnClickLevelUp()
        {
            if (EquipmentManager.Instance == null)
                return;

            if (EquipmentManager.Instance.TryLevelUp(equipmentType))
            {
                RefreshEquipmentDetail();
                onChanged?.Invoke();
            }
        }

        private void OnClickEquip()
        {
            if (EquipmentManager.Instance == null || selectedSlotIndex < 0 || string.IsNullOrEmpty(selectedGemId))
                return;

            if (EquipmentManager.Instance.TryEquipGem(equipmentType, selectedSlotIndex, selectedGemId))
            {
                selectedGemId = string.Empty;
                RefreshEquipmentDetail();
                onChanged?.Invoke();
            }
        }

        private void OnClickUnequip()
        {
            if (EquipmentManager.Instance == null || selectedSlotIndex < 0)
                return;

            if (EquipmentManager.Instance.UnequipGem(equipmentType, selectedSlotIndex))
            {
                RefreshEquipmentDetail();
                onChanged?.Invoke();
            }
        }

        private void OnClickClose()
        {
            Exit();
        }

        private Sprite GetScrollCostIconSprite(PointType scrollType)
        {
            Sprite icon = PointRewardUtility.GetPointIcon(scrollType);
            if (icon != null)
                return icon;

            icon = Resources.Load<Sprite>($"Art/Gem/{scrollType}");
            if (icon != null)
                return icon;

            return UIEquipmentSpriteResolver.GetEquipmentSmallIconSprite(equipmentType);
        }

        private static void SetImage(Image image, Sprite sprite, bool enabledWhenNull)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null || enabledWhenNull;
        }

        private void TryBindButtons()
        {
            if (buttonsBound)
                return;

            if (levelUpButton != null)
                levelUpButton.onClick.AddListener(OnClickLevelUp);
            if (equipButton != null)
                equipButton.onClick.AddListener(OnClickEquip);
            if (unequipButton != null)
                unequipButton.onClick.AddListener(OnClickUnequip);
            if (closeButton != null)
                closeButton.onClick.AddListener(OnClickClose);

            buttonsBound = levelUpButton != null ||
                           equipButton != null ||
                           unequipButton != null ||
                           closeButton != null;
        }
    }
}
