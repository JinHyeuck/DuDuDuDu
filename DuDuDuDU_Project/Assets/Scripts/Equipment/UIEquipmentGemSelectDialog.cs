using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentGemSelectDialog : IDialog
    {
        [Header("Header")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text selectedGemDescText;

        [Header("List")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIGemInventoryItem gemItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button equipButton;
        [SerializeField] private Button unequipButton;
        [SerializeField] private Button closeButton;

        private readonly List<UIGemInventoryItem> items = new List<UIGemInventoryItem>();
        private readonly List<GemDefinition> filteredGems = new List<GemDefinition>();

        private EquipmentType equipmentType;
        private int slotIndex;
        private string selectedGemId;
        private System.Action onChanged;

        protected override void OnLoad()
        {
            if (equipButton != null)
                equipButton.onClick.AddListener(OnClickEquip);
            if (unequipButton != null)
                unequipButton.onClick.AddListener(OnClickUnequip);
            if (closeButton != null)
                closeButton.onClick.AddListener(Exit);
        }

        protected override void OnUnload()
        {
            if (equipButton != null)
                equipButton.onClick.RemoveListener(OnClickEquip);
            if (unequipButton != null)
                unequipButton.onClick.RemoveListener(OnClickUnequip);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Exit);
        }

        public void Open(EquipmentType type, int slot, System.Action changedCallback)
        {
            equipmentType = type;
            slotIndex = slot;
            onChanged = changedCallback;
            selectedGemId = string.Empty;

            Enter();
            Refresh();
        }

        public void Refresh()
        {
            if (titleText != null)
                titleText.SetText($"{GetEquipmentName(equipmentType)} 슬롯 {slotIndex + 1} 보석");

            BuildGemList();
            RefreshGemList();
            RefreshSelectedDescription();
        }

        private void BuildGemList()
        {
            filteredGems.Clear();
            if (EquipmentManager.Instance == null)
                return;

            IReadOnlyList<GemDefinition> all = EquipmentManager.Instance.GetGemDefinitions();
            for (int i = 0; i < all.Count; i++)
            {
                GemDefinition gem = all[i];
                if (gem == null || gem.equipableType != equipmentType)
                    continue;

                filteredGems.Add(gem);
            }

            while (items.Count < filteredGems.Count)
            {
                if (gemItemPrefab == null || listRoot == null)
                    break;

                UIGemInventoryItem created = Instantiate(gemItemPrefab, listRoot);
                items.Add(created);
            }
        }

        private void RefreshGemList()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (i >= filteredGems.Count)
                {
                    items[i].gameObject.SetActive(false);
                    continue;
                }

                GemDefinition definition = filteredGems[i];
                int count = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetGemCount(definition.gemId) : 0;
                bool selected = selectedGemId == definition.gemId;

                items[i].gameObject.SetActive(true);
                items[i].Bind(definition.gemId, OnClickGemItem);
                items[i].Refresh(definition, count, selected, count > 0 || selected);
            }

            if (equipButton != null)
                equipButton.interactable = !string.IsNullOrEmpty(selectedGemId);
        }

        private void RefreshSelectedDescription()
        {
            if (selectedGemDescText == null)
                return;

            if (string.IsNullOrEmpty(selectedGemId))
            {
                selectedGemDescText.SetText("장착할 보석을 선택하세요.");
                return;
            }

            if (EquipmentManager.Instance != null && EquipmentManager.Instance.TryGetGemDefinition(selectedGemId, out GemDefinition definition))
            {
                selectedGemDescText.SetText(UIEquipmentEffectTextFormatter.BuildGemDescription(definition));
                return;
            }

            selectedGemDescText.SetText("보석 정보를 찾을 수 없습니다.");
        }

        private void OnClickGemItem(string gemId)
        {
            selectedGemId = gemId;
            RefreshGemList();
            RefreshSelectedDescription();
        }

        private void OnClickEquip()
        {
            if (EquipmentManager.Instance == null || string.IsNullOrEmpty(selectedGemId))
                return;

            if (EquipmentManager.Instance.TryEquipGem(equipmentType, slotIndex, selectedGemId))
            {
                onChanged?.Invoke();
                Exit();
            }
        }

        private void OnClickUnequip()
        {
            if (EquipmentManager.Instance == null)
                return;

            if (EquipmentManager.Instance.UnequipGem(equipmentType, slotIndex))
            {
                onChanged?.Invoke();
                Exit();
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
