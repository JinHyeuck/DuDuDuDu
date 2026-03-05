using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace OJ
{
    public enum EquipmentSlotVisualState
    {
        Locked = 0,
        Empty,
        Uncommon,
        Common,
        Normal,
        Rare,
        Epic,
        Mythic
    }

    public class UIEquipmentItem : MonoBehaviour
    {
        [SerializeField] private Button clickButton;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private Image selectedFrame;
        [Header("Slot Visual")]
        [SerializeField] private List<Image> slotStateImages;
        [SerializeField] private Sprite unlockedSlotSprite;
        [SerializeField] private Sprite lockedSlotSprite;
        [SerializeField] private Color lockedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [SerializeField] private Color uncommonColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] private Color commonColor = new Color(0.20f, 0.55f, 1.0f, 1f);
        [SerializeField] private Color normalColor = new Color(0.20f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color rareColor = new Color(1.0f, 0.55f, 0.0f, 1f);
        [SerializeField] private Color epicColor = new Color(0.75f, 0.35f, 1.0f, 1f);
        [SerializeField] private Color mythicColor = new Color(1.0f, 0.2f, 0.2f, 1f);

        private EquipmentType equipmentType;
        private System.Action<EquipmentType> clickCallback;

        private void Awake()
        {
            if (clickButton != null)
                clickButton.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (clickButton != null)
                clickButton.onClick.RemoveListener(OnClick);
        }

        public void Bind(EquipmentType type, System.Action<EquipmentType> onClick)
        {
            equipmentType = type;
            clickCallback = onClick;
        }

        public void Refresh(
            int level,
            int attack,
            bool selected,
            IReadOnlyList<EquipmentSlotVisualState> slotStates)
        {
            if (nameText != null)
                nameText.SetText(GetDisplayName(equipmentType));
            if (levelText != null)
                levelText.SetText("Lv.{0}", level);
            if (attackText != null)
                attackText.SetText("ATK {0}", attack);
            if (selectedFrame != null)
                selectedFrame.enabled = selected;

            RefreshSlotVisual(slotStates);
        }

        private void OnClick()
        {
            clickCallback?.Invoke(equipmentType);
        }

        private static string GetDisplayName(EquipmentType type)
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

        private void RefreshSlotVisual(IReadOnlyList<EquipmentSlotVisualState> slotStates)
        {
            if (slotStateImages == null || slotStateImages.Count <= 0 || slotStates == null)
                return;

            int count = Mathf.Min(slotStateImages.Count, slotStates.Count);
            for (int i = 0; i < slotStateImages.Count; i++)
            {
                Image slotImage = slotStateImages[i];
                if (slotImage == null)
                    continue;

                if (i >= count)
                {
                    slotImage.enabled = false;
                    continue;
                }

                slotImage.enabled = true;
                EquipmentSlotVisualState state = slotStates[i];
                slotImage.sprite = state == EquipmentSlotVisualState.Locked ? lockedSlotSprite : unlockedSlotSprite;
                slotImage.color = GetStateColor(state);
            }
        }

        private Color GetStateColor(EquipmentSlotVisualState state)
        {
            switch (state)
            {
                case EquipmentSlotVisualState.Locked: return lockedColor;
                case EquipmentSlotVisualState.Empty: return emptyColor;
                case EquipmentSlotVisualState.Uncommon: return uncommonColor;
                case EquipmentSlotVisualState.Common: return commonColor;
                case EquipmentSlotVisualState.Normal: return normalColor;
                case EquipmentSlotVisualState.Rare: return rareColor;
                case EquipmentSlotVisualState.Epic: return epicColor;
                case EquipmentSlotVisualState.Mythic: return mythicColor;
                default: return emptyColor;
            }
        }
    }
}
