using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIGemInventoryItem : MonoBehaviour
    {
        [SerializeField] private Button clickButton;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text rarityText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private Image selectedFrame;

        private string gemId;
        private System.Action<string> clickCallback;
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
            TMP_Text runtimeNameText,
            TMP_Text runtimeRarityText,
            TMP_Text runtimeCountText,
            TMP_Text runtimeDescText,
            Image runtimeSelectedFrame)
        {
            clickButton = runtimeClickButton;
            nameText = runtimeNameText;
            rarityText = runtimeRarityText;
            countText = runtimeCountText;
            descText = runtimeDescText;
            selectedFrame = runtimeSelectedFrame;

            TryBindClick();
        }

        public void Bind(string id, System.Action<string> onClick)
        {
            gemId = id;
            clickCallback = onClick;
        }

        public void Refresh(GemDefinition definition, int count, bool selected, bool interactable)
        {
            if (definition != null)
            {
                if (nameText != null) nameText.SetText(definition.displayName);
                if (rarityText != null) rarityText.SetText(definition.rarity.ToString());
                if (descText != null) descText.SetText(UIEquipmentEffectTextFormatter.BuildGemDescription(definition));
            }
            else
            {
                if (nameText != null) nameText.SetText(gemId);
                if (rarityText != null) rarityText.SetText("Unknown");
                if (descText != null) descText.SetText("효과 없음");
            }

            if (countText != null)
            {
                countText.SetText("x{0}", count);
                countText.gameObject.SetActive(count > 0);
            }

            if (selectedFrame != null)
                selectedFrame.enabled = selected;

            if (clickButton != null)
                clickButton.interactable = interactable;
        }

        private void OnClick()
        {
            clickCallback?.Invoke(gemId);
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
