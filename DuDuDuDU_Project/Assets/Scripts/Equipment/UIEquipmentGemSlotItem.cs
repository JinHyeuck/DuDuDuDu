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
            Image runtimeSelectedFrame)
        {
            clickButton = runtimeClickButton;
            titleText = runtimeTitleText;
            descText = runtimeDescText;
            lockText = runtimeLockText;
            selectedFrame = runtimeSelectedFrame;

            TryBindClick();
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
            {
                descText.SetText(unlocked
                    ? (string.IsNullOrEmpty(gemDesc) ? "보석을 장착해 보세요." : gemDesc)
                    : "잠금 상태");
            }

            if (lockText != null)
                lockText.SetText(unlocked ? string.Empty : $"Lv.{unlockLevel} 잠금");

            if (clickButton != null)
                clickButton.interactable = unlocked;
        }

        private void OnClick()
        {
            clickCallback?.Invoke(slotIndex);
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
