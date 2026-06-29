using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentEffectGroupItem : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private Image equipmentIconImage;
        [SerializeField] private TMP_Text equipmentNameText;

        [Header("Rows")]
        [SerializeField] private Transform rowRoot;
        [SerializeField] private UIEquipmentEffectRowItem rowItemPrefab;

        private readonly List<UIEquipmentEffectRowItem> rowItems = new List<UIEquipmentEffectRowItem>();

        public void Refresh(EquipmentType equipmentType, IReadOnlyList<string> gemNames, IReadOnlyList<string> effectTexts)
        {
            SetImage(equipmentIconImage, UIEquipmentSpriteResolver.GetEquipmentSmallIconSprite(equipmentType), false);

            if (equipmentNameText != null)
                equipmentNameText.SetText(UIEquipmentText.GetEquipmentName(equipmentType) + " 효과");

            int rowCount = gemNames != null ? gemNames.Count : 0;
            EnsureRows(rowCount);

            for (int i = 0; i < rowItems.Count; i++)
            {
                UIEquipmentEffectRowItem rowItem = rowItems[i];
                if (rowItem == null)
                    continue;

                bool active = i < rowCount;
                rowItem.gameObject.SetActive(active);
                if (!active)
                    continue;

                string gemName = gemNames[i];
                string effectText = effectTexts != null && i < effectTexts.Count ? effectTexts[i] : string.Empty;
                rowItem.Refresh(gemName, effectText);
            }

            RebuildLayout();
        }

        private void OnEnable()
        {
            RebuildLayout();
        }

        private void EnsureRows(int count)
        {
            if (rowRoot == null || rowItemPrefab == null)
                return;

            while (rowItems.Count < count)
            {
                UIEquipmentEffectRowItem rowItem = Instantiate(rowItemPrefab, rowRoot);
                rowItem.gameObject.SetActive(true);
                rowItems.Add(rowItem);
            }
        }

        public void RebuildLayout()
        {
            Canvas.ForceUpdateCanvases();
            Rebuild(rowRoot);
            Rebuild(transform);
        }

        private static void Rebuild(Transform target)
        {
            if (target is RectTransform rectTransform)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        private static void SetImage(Image image, Sprite sprite, bool enabledWhenNull)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null || enabledWhenNull;
        }
    }
}
