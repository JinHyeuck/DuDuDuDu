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
        [SerializeField] private Button closeButton;

        [Header("Sub Dialog")]
        [SerializeField] private UIEquipmentGemSelectDialog gemSelectDialog;
        [SerializeField] private UIEquipmentConfirmDialog confirmDialog;

        private readonly List<UIEquipmentGemSlotItem> slotItems = new List<UIEquipmentGemSlotItem>();

        private EquipmentType equipmentType;
        private System.Action onChanged;

        protected override void OnLoad()
        {
            if (levelUpButton != null)
                levelUpButton.onClick.AddListener(OnClickLevelUp);
            if (levelUpAllButton != null)
                levelUpAllButton.onClick.AddListener(OnClickLevelUpAll);
            if (closeButton != null)
                closeButton.onClick.AddListener(Exit);
        }

        protected override void OnUnload()
        {
            if (levelUpButton != null)
                levelUpButton.onClick.RemoveListener(OnClickLevelUp);
            if (levelUpAllButton != null)
                levelUpAllButton.onClick.RemoveListener(OnClickLevelUpAll);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Exit);
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
                titleText.SetText(GetEquipmentName(equipmentType));
            if (attackText != null)
                attackText.SetText("공격력: {0}", attack);
            if (levelText != null)
                levelText.SetText("레벨: {0}", level);
            if (goldCostText != null)
                goldCostText.SetText("{0}/{1}", PointManager.Instance != null ? PointManager.Instance.Get(PointType.Gold) : 0, goldCost);

            PointType scrollType = PointManager.ToEquipmentScrollType(equipmentType);
            if (scrollCostText != null)
                scrollCostText.SetText("{0}/{1}", PointManager.Instance != null ? PointManager.Instance.Get(scrollType) : 0, scrollCost);

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

            confirmDialog.Open("장비 도면과 골드를 소모해 가능한 만큼 강화하시겠습니까?", DoLevelUpAll);
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

        private static string GetEquipmentName(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.Weapon: return "무기";
                case EquipmentType.Helmet: return "모자";
                case EquipmentType.Armor: return "갑옷";
                case EquipmentType.Ring: return "반지";
                case EquipmentType.Shoes: return "신발";
                case EquipmentType.Necklace: return "목걸이";
                default: return type.ToString();
            }
        }
    }
}
