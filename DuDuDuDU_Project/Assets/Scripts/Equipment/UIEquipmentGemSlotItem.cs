using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentGemSlotItem : MonoBehaviour
    {
        [SerializeField] private Button clickButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private TMP_Text lockText;
        [SerializeField] private Image slotStateImage;
        [SerializeField] private Image gemIconImage;
        [SerializeField] private Sprite emptySlotSprite;
        [SerializeField] private Sprite equippedSlotSprite;
        [SerializeField] private Sprite lockedSlotSprite;
        [SerializeField] private Image selectedFrame;

        private int slotIndex;
        private System.Action<int> clickCallback;
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

        public void ConfigureRuntime(
            Button runtimeClickButton,
            TMP_Text runtimeTitleText,
            TMP_Text runtimeDescText,
            TMP_Text runtimeLockText,
            Image runtimeSelectedFrame,
            Image runtimeSlotStateImage = null,
            Image runtimeGemIconImage = null,
            Sprite runtimeEmptySlotSprite = null,
            Sprite runtimeEquippedSlotSprite = null,
            Sprite runtimeLockedSlotSprite = null)
        {
            clickButton = runtimeClickButton;
            titleText = runtimeTitleText;
            descText = runtimeDescText;
            lockText = runtimeLockText;
            selectedFrame = runtimeSelectedFrame;
            slotStateImage = runtimeSlotStateImage;
            gemIconImage = runtimeGemIconImage;
            emptySlotSprite = runtimeEmptySlotSprite;
            equippedSlotSprite = runtimeEquippedSlotSprite;
            lockedSlotSprite = runtimeLockedSlotSprite;

            TryBindClick();
        }

        public void Bind(int index, System.Action<int> onClick)
        {
            slotIndex = index;
            clickCallback = onClick;
        }

        public void Refresh(bool unlocked, int unlockLevel, string gemName, string gemDesc, bool selected)
        {
            Refresh(unlocked, unlockLevel, gemName, gemDesc, selected, null);
        }

        public void Refresh(bool unlocked, int unlockLevel, GemDefinition gemDefinition, bool selected)
        {
            string gemName = gemDefinition != null ? gemDefinition.displayName : string.Empty;
            string gemDesc = gemDefinition != null ? UIEquipmentEffectTextFormatter.BuildGemDescription(gemDefinition) : string.Empty;
            Refresh(unlocked, unlockLevel, gemName, gemDesc, selected, gemDefinition);
        }

        private void Refresh(bool unlocked, int unlockLevel, string gemName, string gemDesc, bool selected, GemDefinition gemDefinition)
        {
            if (selectedFrame != null)
                selectedFrame.enabled = selected;

            if (titleText != null)
                titleText.SetText($"슬롯 {slotIndex + 1} - {(string.IsNullOrEmpty(gemName) ? "비어 있음" : gemName)}");

            if (descText != null)
            {
                descText.SetText(unlocked
                    ? (string.IsNullOrEmpty(gemDesc) ? "보석을 장착해 보세요." : gemDesc)
                    : "잠금 상태");
            }

            if (lockText != null)
                lockText.SetText(unlocked ? string.Empty : $"Lv.{unlockLevel} 해금");

            RefreshVisual(unlocked, gemDefinition);

            if (clickButton != null)
                clickButton.interactable = unlocked;
        }

        private void OnClick()
        {
            clickCallback?.Invoke(slotIndex);
        }

        private void RefreshVisual(bool unlocked, GemDefinition gemDefinition)
        {
            Sprite slotSprite;
            if (!unlocked)
                slotSprite = lockedSlotSprite != null ? lockedSlotSprite : UIEquipmentSpriteResolver.GetLockedSlotSprite();
            else if (gemDefinition != null)
                slotSprite = equippedSlotSprite != null ? equippedSlotSprite : UIEquipmentSpriteResolver.GetEquippedSlotSprite();
            else
                slotSprite = emptySlotSprite != null ? emptySlotSprite : UIEquipmentSpriteResolver.GetEmptySlotSprite();

            SetImage(slotStateImage, slotSprite, true);
            SetImage(gemIconImage, gemDefinition != null ? UIEquipmentSpriteResolver.GetGemIconSprite(gemDefinition.rarity) : null, false);
        }

        private static void SetImage(Image image, Sprite sprite, bool enabledWhenNull)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null || enabledWhenNull;
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
