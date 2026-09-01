using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.UI;

namespace OJ.Equipment
{
    public class UIMergePopup : DialogBase
    {
        private class MergeMaterial
        {
            public string gemId;
            public GemDefinition definition;
            public bool selected;
        }

        [Header("Materials")]
        [SerializeField] private Transform materialRoot;
        [SerializeField] private UIGemInventoryItem materialItemPrefab;
        [SerializeField] private TMP_Text emptyText;

        [Header("Buttons")]
        [SerializeField] private Button mergeButton;

        [Header("Gem Info Popup")]
        [SerializeField] private GameObject gemInfoPopup;
        [SerializeField] private UIGemInventoryItem gemInfoItem;
        [SerializeField] private TMP_Text gemInfoNameText;
        [SerializeField] private TMP_Text gemInfoEquipTypeText;
        [SerializeField] private TMP_Text gemInfoEffectText;
        [SerializeField] private Button gemInfoCloseButton;

        private readonly List<MergeMaterial> materials = new List<MergeMaterial>();
        private readonly List<UIGemInventoryItem> materialItems = new List<UIGemInventoryItem>();

        private EquipmentType equipmentType;
        private Action<IReadOnlyList<string>> mergedCallback;
        private bool buttonsBound;

        protected override void OnLoad()
        {
            TryBindButtons();
            HideGemInfo();
        }

        protected override void OnUnload()
        {
            if (buttonsBound && mergeButton != null)
                mergeButton.onClick.RemoveListener(OnClickMerge);
            if (buttonsBound && gemInfoCloseButton != null)
                gemInfoCloseButton.onClick.RemoveListener(HideGemInfo);
        }

        protected override void OnExit()
        {
            HideGemInfo();
        }

        public void Open(EquipmentType type, Action<IReadOnlyList<string>> onMerged)
        {
            if (!_isLoaded)
                Load();

            equipmentType = type;
            mergedCallback = onMerged;

            RefreshMaterials();
            Enter();
        }

        private void RefreshMaterials()
        {
            materials.Clear();

            if (EquipmentManager.Instance != null)
            {
                IReadOnlyList<GemDefinition> definitions = EquipmentManager.Instance.GetGemDefinitions();
                Dictionary<Rarity, List<int>> indexesByRarity = new Dictionary<Rarity, List<int>>();

                for (int i = 0; i < definitions.Count; i++)
                {
                    GemDefinition definition = definitions[i];
                    if (definition == null ||
                        definition.equipableType != equipmentType ||
                        definition.rarity == Rarity.Mythic)
                    {
                        continue;
                    }

                    int count = EquipmentManager.Instance.GetGemCount(definition.gemId);
                    for (int countIndex = 0; countIndex < count; countIndex++)
                    {
                        int materialIndex = materials.Count;
                        materials.Add(new MergeMaterial
                        {
                            gemId = definition.gemId,
                            definition = definition,
                            selected = false
                        });

                        if (!indexesByRarity.TryGetValue(definition.rarity, out List<int> indexes))
                        {
                            indexes = new List<int>();
                            indexesByRarity[definition.rarity] = indexes;
                        }

                        indexes.Add(materialIndex);
                    }
                }

                foreach (var pair in indexesByRarity)
                {
                    int selectCount = (pair.Value.Count / 4) * 4;
                    for (int i = 0; i < selectCount; i++)
                        materials[pair.Value[i]].selected = true;
                }
            }

            EnsureMaterialItems(materials.Count);

            for (int i = 0; i < materialItems.Count; i++)
            {
                UIGemInventoryItem item = materialItems[i];
                if (item == null)
                    continue;

                bool active = i < materials.Count;
                item.gameObject.SetActive(active);
                if (!active)
                    continue;

                int index = i;
                MergeMaterial material = materials[i];
                item.Bind(material.gemId, _ => ToggleMaterial(index), ShowGemInfo);
                item.Refresh(material.definition, false, true);
                item.SetChecked(material.selected);
            }

            if (emptyText != null)
                emptyText.gameObject.SetActive(materials.Count <= 0);

            RefreshMergeButton();
        }

        private void ShowGemInfo(string gemId)
        {
            if (string.IsNullOrEmpty(gemId) ||
                EquipmentManager.Instance == null ||
                !EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition definition) ||
                definition == null)
            {
                return;
            }

            if (gemInfoPopup != null)
                gemInfoPopup.SetActive(true);

            if (gemInfoItem != null)
            {
                gemInfoItem.gameObject.SetActive(true);
                gemInfoItem.Bind(definition.gemId, null);
                gemInfoItem.Refresh(definition, false, false);
            }

            if (gemInfoNameText != null)
                gemInfoNameText.SetText(definition.displayName);
            if (gemInfoEquipTypeText != null)
                gemInfoEquipTypeText.SetText(UIEquipmentText.GetEquipmentName(definition.equipableType));
            if (gemInfoEffectText != null)
                gemInfoEffectText.SetText(UIEquipmentEffectTextFormatter.BuildGemDescription(definition));
        }

        private void HideGemInfo()
        {
            if (gemInfoPopup != null)
                gemInfoPopup.SetActive(false);
        }

        private void EnsureMaterialItems(int count)
        {
            if (materialRoot == null || materialItemPrefab == null)
                return;

            while (materialItems.Count < count)
            {
                UIGemInventoryItem item = Instantiate(materialItemPrefab, materialRoot);
                item.gameObject.SetActive(true);
                materialItems.Add(item);
            }
        }

        private void ToggleMaterial(int index)
        {
            if (index < 0 || index >= materials.Count)
                return;

            materials[index].selected = !materials[index].selected;

            if (index < materialItems.Count && materialItems[index] != null)
                materialItems[index].SetChecked(materials[index].selected);

            RefreshMergeButton();
        }

        private void RefreshMergeButton()
        {
            if (mergeButton != null)
                mergeButton.interactable = BuildAdjustedMaterialGemIds().Count > 0;
        }

        private void OnClickMerge()
        {
            if (EquipmentManager.Instance == null)
                return;

            List<string> materialGemIds = BuildAdjustedMaterialGemIds();
            if (materialGemIds.Count <= 0)
                return;

            if (EquipmentManager.Instance.TryMergeGems(equipmentType, materialGemIds, out List<string> resultGemIds))
            {
                mergedCallback?.Invoke(resultGemIds);
                Exit();
            }
        }

        private List<string> BuildAdjustedMaterialGemIds()
        {
            List<string> materialGemIds = new List<string>();
            Dictionary<Rarity, List<int>> selectedIndexesByRarity = new Dictionary<Rarity, List<int>>();

            for (int i = 0; i < materials.Count; i++)
            {
                MergeMaterial material = materials[i];
                if (material == null || !material.selected || material.definition == null)
                    continue;

                Rarity rarity = material.definition.rarity;
                if (!selectedIndexesByRarity.TryGetValue(rarity, out List<int> indexes))
                {
                    indexes = new List<int>();
                    selectedIndexesByRarity[rarity] = indexes;
                }

                indexes.Add(i);
            }

            foreach (var pair in selectedIndexesByRarity)
            {
                int useCount = (pair.Value.Count / 4) * 4;
                for (int i = 0; i < useCount; i++)
                    materialGemIds.Add(materials[pair.Value[i]].gemId);
            }

            return materialGemIds;
        }

        private void TryBindButtons()
        {
            if (buttonsBound)
                return;

            if (mergeButton != null)
                mergeButton.onClick.AddListener(OnClickMerge);
            if (gemInfoCloseButton != null)
                gemInfoCloseButton.onClick.AddListener(HideGemInfo);

            buttonsBound = mergeButton != null || gemInfoCloseButton != null;
        }
    }
}
