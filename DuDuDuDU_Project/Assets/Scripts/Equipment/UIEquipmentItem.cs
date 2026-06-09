using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public enum EquipmentSlotVisualState
    {
        Locked = 0,
        Empty,
        Equipped,
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
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private Image iconImage;
        [Header("Slot Visual")]
        [SerializeField] private List<Image> slotStateImages;
        [SerializeField] private Sprite unlockedSlotSprite;
        [SerializeField] private Sprite equippedSlotSprite;
        [SerializeField] private Vector2 unLockedSlotSpriteSize = new Vector2(80f, 80f);
        [SerializeField] private Sprite lockedSlotSprite;
        [SerializeField] private Vector2 lockedSlotSpriteSize = new Vector2(40f, 40f);

        private EquipmentType equipmentType;
        private System.Action<EquipmentType> clickCallback;
        private bool isClickBound;

        private void Awake()
        {
            TryBindClick();
        }

        private void OnDestroy()
        {
            if (isClickBound && clickButton != null)
                clickButton.onClick.RemoveListener(OnClick);
        }

        public void Bind(EquipmentType type, System.Action<EquipmentType> onClick)
        {
            equipmentType = type;
            clickCallback = onClick;
            Sprite icon = UIEquipmentSpriteResolver.GetEquipmentLargeIconSprite(type);
            iconImage.sprite = icon;
        }

        public void Refresh(
            int level,
            int attack,
            bool selected,
            IReadOnlyList<EquipmentSlotVisualState> slotStates)
        {
            if (levelText != null)
                levelText.SetText("Lv.{0}", level);
            if (attackText != null)
                attackText.SetText("ATK {0}", attack);

            RefreshSlotVisual(slotStates);
        }

        private void OnClick()
        {
            clickCallback?.Invoke(equipmentType);
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
                slotImage.sprite = GetSlotSprite(state);
                slotImage.rectTransform.sizeDelta = GetSlotSpriteSize(state);
                slotImage.color = Color.white;
            }
        }

        private Sprite GetSlotSprite(EquipmentSlotVisualState state)
        {
            switch (state)
            {
                case EquipmentSlotVisualState.Locked:
                    return lockedSlotSprite != null
                        ? lockedSlotSprite
                        : UIEquipmentSpriteResolver.GetLockedSlotSprite();
                case EquipmentSlotVisualState.Empty:
                    return unlockedSlotSprite != null
                        ? unlockedSlotSprite
                        : UIEquipmentSpriteResolver.GetEmptySlotSprite();
                default:
                    return equippedSlotSprite != null
                        ? equippedSlotSprite
                        : UIEquipmentSpriteResolver.GetEquippedSlotSprite();
            }
        }

        private Vector2 GetSlotSpriteSize(EquipmentSlotVisualState state)
        {
            switch (state)
            {
                case EquipmentSlotVisualState.Locked:
                    return lockedSlotSpriteSize;
                default:
                    return unLockedSlotSpriteSize;
            }
        }


        private void TryBindClick()
        {
            if (isClickBound || clickButton == null)
                return;

            clickButton.onClick.AddListener(OnClick);
            isClickBound = true;
        }
    }
}
