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
        [SerializeField] private Image selectedFrame;

        private int slotIndex;
        private System.Action<int> clickCallback;

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

        public void Bind(int index, System.Action<int> onClick)
        {
            slotIndex = index;
            clickCallback = onClick;
        }

        public void Refresh(bool unlocked, int unlockLevel, string gemName, string gemDesc, bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.enabled = selected;

            if (titleText != null)
                titleText.SetText($"슬롯 {slotIndex + 1} - {(string.IsNullOrEmpty(gemName) ? "비어 있음" : gemName)}");

            if (descText != null)
                descText.SetText(unlocked
                    ? (string.IsNullOrEmpty(gemDesc) ? "보석을 장착하세요." : gemDesc)
                    : "잠금 상태");

            if (lockText != null)
                lockText.SetText(unlocked ? string.Empty : $"Lv.{unlockLevel} 해금");

            if (clickButton != null)
                clickButton.interactable = unlocked;
        }

        private void OnClick()
        {
            clickCallback?.Invoke(slotIndex);
        }
    }
}
