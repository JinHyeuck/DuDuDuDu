using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentDetailDialog : IDialog
    {
        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private TMP_Text levelText;

        [Header("Slots")]
        [SerializeField] private Transform slotRoot;
        [SerializeField] private UIEquipmentGemSlotItem slotItemPrefab;

        [Header("Cost")]
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private TMP_Text scrollCostText;

        [Header("Buttons")]
        [SerializeField] private Button levelUpButton;
        [SerializeField] private Button levelUpAllButton;

        [Header("Sub Dialog")]
        [SerializeField] private UIEquipmentGemSelectDialog gemSelectDialog;
        [SerializeField] private UIEquipmentConfirmDialog confirmDialog;

        private readonly List<UIEquipmentGemSlotItem> slotItems = new List<UIEquipmentGemSlotItem>();

        private EquipmentType equipmentType;
        private System.Action onChanged;
        private bool buttonsBound;

        protected override void OnLoad()
        {
            TryBindButtons();
        }

        protected override void OnUnload()
        {
            if (buttonsBound && levelUpButton != null)
                levelUpButton.onClick.RemoveListener(OnClickLevelUp);
            if (buttonsBound && levelUpAllButton != null)
                levelUpAllButton.onClick.RemoveListener(OnClickLevelUpAll);
        }

        public void ConfigureRuntime(
            TMP_Text runtimeTitleText,
            TMP_Text runtimeAttackText,
            TMP_Text runtimeLevelText,
            Transform runtimeSlotRoot,
            UIEquipmentGemSlotItem runtimeSlotItemPrefab,
            TMP_Text runtimeGoldCostText,
            TMP_Text runtimeScrollCostText,
            Button runtimeLevelUpButton,
            Button runtimeLevelUpAllButton,
            Button runtimeCloseButton,
            UIEquipmentGemSelectDialog runtimeGemSelectDialog,
            UIEquipmentConfirmDialog runtimeConfirmDialog)
        {
            titleText = runtimeTitleText;
            attackText = runtimeAttackText;
            levelText = runtimeLevelText;
            slotRoot = runtimeSlotRoot;
            slotItemPrefab = runtimeSlotItemPrefab;
            goldCostText = runtimeGoldCostText;
            scrollCostText = runtimeScrollCostText;
            levelUpButton = runtimeLevelUpButton;
            levelUpAllButton = runtimeLevelUpAllButton;
            gemSelectDialog = runtimeGemSelectDialog;
            confirmDialog = runtimeConfirmDialog;

            TryBindButtons();
        }

        public void Open(EquipmentType type, System.Action changedCallback)
        {
            equipmentType = type;
            onChanged = changedCallback;

            Enter();
            Refresh();
        }

        public void Refresh()
        {
            if (EquipmentManager.Instance == null)
                return;

            int level = EquipmentManager.Instance.GetLevel(equipmentType);
            int attack = EquipmentManager.Instance.GetEquipmentAttack(equipmentType);
            (int goldCost, int scrollCost) = EquipmentManager.Instance.GetNextUpgradeCost(equipmentType);

            if (titleText != null)
                titleText.SetText(UIEquipmentText.GetEquipmentName(equipmentType));
            if (attackText != null)
                attackText.SetText("공격력 {0}", attack);
            if (levelText != null)
                levelText.SetText("레벨 {0}", level);
            if (goldCostText != null)
                goldCostText.SetText("Gold {0}/{1}", PointManager.Instance != null ? PointManager.Instance.Get(PointType.Gold) : 0, goldCost);

            PointType scrollType = PointManager.ToEquipmentScrollType(equipmentType);
            if (scrollCostText != null)
                scrollCostText.SetText("Scroll {0}/{1}", PointManager.Instance != null ? PointManager.Instance.Get(scrollType) : 0, scrollCost);

            BuildSlotIfNeeded();
            RefreshSlots();
        }

        private void BuildSlotIfNeeded()
        {
            if (slotItemPrefab == null || slotRoot == null)
                return;

            while (slotItems.Count < Define.MaxEquipmentSlot)
            {
                int newIndex = slotItems.Count;
                UIEquipmentGemSlotItem item = Instantiate(slotItemPrefab, slotRoot);
                item.gameObject.SetActive(true);
                item.Bind(newIndex, OnClickSlot);
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
                bool unlocked = EquipmentManager.Instance.IsSlotUnlocked(equipmentType, i);
                int unlockLevel = EquipmentManager.Instance.GetSlotUnlockLevel(i);
                string gemId = EquipmentManager.Instance.GetEquippedGemId(equipmentType, i);

                string gemName = string.Empty;
                string gemDesc = string.Empty;
                if (!string.IsNullOrEmpty(gemId) && EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition def))
                {
                    gemName = def.displayName;
                    gemDesc = UIEquipmentEffectTextFormatter.BuildGemDescription(def);
                }

                item.Refresh(unlocked, unlockLevel, gemName, gemDesc, false);
            }
        }

        private void OnClickSlot(int slotIndex)
        {
            if (gemSelectDialog == null)
                return;

            gemSelectDialog.Open(equipmentType, slotIndex, () =>
            {
                Refresh();
                onChanged?.Invoke();
            });
        }

        private void OnClickLevelUp()
        {
            if (EquipmentManager.Instance == null)
                return;

            if (EquipmentManager.Instance.TryLevelUp(equipmentType))
            {
                Refresh();
                onChanged?.Invoke();
            }
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
            {
                Refresh();
                onChanged?.Invoke();
            }
        }

        private void TryBindButtons()
        {
            if (buttonsBound)
                return;

            if (levelUpButton != null)
                levelUpButton.onClick.AddListener(OnClickLevelUp);
            if (levelUpAllButton != null)
                levelUpAllButton.onClick.AddListener(OnClickLevelUpAll);

            buttonsBound = levelUpButton != null || levelUpAllButton != null;
        }
    }
}
