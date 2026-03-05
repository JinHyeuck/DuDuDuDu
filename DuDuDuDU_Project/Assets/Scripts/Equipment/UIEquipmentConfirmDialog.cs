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

        protected override void OnLoad()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnClickCancel);
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnClickConfirm);
        }

        protected override void OnUnload()
        {
            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(OnClickCancel);
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(OnClickConfirm);
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
    }
}
