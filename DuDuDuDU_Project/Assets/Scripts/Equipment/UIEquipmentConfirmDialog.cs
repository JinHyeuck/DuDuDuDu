using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIEquipmentConfirmDialog : IDialog
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;

        private System.Action confirmAction;
        private bool buttonsBound;

        protected override void OnLoad()
        {
            TryBindButtons();
        }

        protected override void OnUnload()
        {
            if (buttonsBound && cancelButton != null)
                cancelButton.onClick.RemoveListener(OnClickCancel);
            if (buttonsBound && confirmButton != null)
                confirmButton.onClick.RemoveListener(OnClickConfirm);
        }

        public void ConfigureRuntime(TMP_Text runtimeMessageText, Button runtimeCancelButton, Button runtimeConfirmButton)
        {
            messageText = runtimeMessageText;
            cancelButton = runtimeCancelButton;
            confirmButton = runtimeConfirmButton;

            TryBindButtons();
        }

        public void Open(string message, System.Action onConfirm)
        {
            confirmAction = onConfirm;
            if (messageText != null)
                messageText.SetText(message);
            Enter();
        }

        private void OnClickCancel()
        {
            confirmAction = null;
            Exit();
        }

        private void OnClickConfirm()
        {
            System.Action callback = confirmAction;
            confirmAction = null;
            Exit();
            callback?.Invoke();
        }

        private void TryBindButtons()
        {
            if (buttonsBound)
                return;

            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnClickCancel);
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnClickConfirm);

            buttonsBound = cancelButton != null || confirmButton != null;
        }
    }
}
