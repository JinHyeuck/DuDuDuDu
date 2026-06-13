using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentGemSlotItem : MonoBehaviour
    {
        [SerializeField] private Button clickButton;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private TMP_Text lockText;
        [SerializeField] private Image slotStateImage;
        [SerializeField] private Color slotLockedStateColor;
        [SerializeField] private Color slotUnlockedStateColor;

        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject gemIconObject;
        [SerializeField] private Image gemIconImage;
        [SerializeField] private Image lockImage;
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

        public void Bind(int index, System.Action<int> onClick)
        {
            slotIndex = index;
            clickCallback = onClick;
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
            if (slotStateImage != null)
                slotStateImage.color = unlocked ? slotUnlockedStateColor : slotLockedStateColor;

            if (lockImage != null)
                lockImage.gameObject.SetActive(!unlocked);

            if (gemIconObject != null)
                gemIconObject.gameObject.SetActive(unlocked && gemDefinition != null);

            SetImage(backgroundImage, gemDefinition != null ? UIEquipmentSpriteResolver.GetGemFrameSprite(gemDefinition.rarity) : null, false);
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
