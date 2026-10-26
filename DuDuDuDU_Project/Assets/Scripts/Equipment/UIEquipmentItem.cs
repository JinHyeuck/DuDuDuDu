using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Equipment
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

        /// <summary>어떤 부위의 칸인가. 로비 가이드가 무기 칸을 찾는 데 쓴다.</summary>
        public EquipmentType Type => equipmentType;

        /// <summary>칸을 누르는 버튼. 로비 가이드가 손가락으로 가리키고 눌림을 기다린다.</summary>
        public Button ClickButton => clickButton;
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

        private GameObject upgradeRedDot;

        /// <summary>"강화할 수 있다" 레드닷(<see cref="OJ.UI.UpgradeRedDot"/>, 코드로 만든다).</summary>
        public void SetUpgradeDot(bool on)
        {
            // 카드 그림(아이콘) 모서리에 붙인다 — 칸 루트에 붙이면 슬롯 줄까지 포함한 큰 사각형의 모서리라 카드 밖에 뜬다.
            OJ.UI.UpgradeRedDot.Set(ref upgradeRedDot, iconImage != null ? iconImage.transform : transform, on, 50f, new Vector2(-34f, -34f));
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
                levelText.SetText("{0}", level);
            if (attackText != null)
                attackText.SetText("{0}", attack);

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
