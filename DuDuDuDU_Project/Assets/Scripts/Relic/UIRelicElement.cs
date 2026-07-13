using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIRelicElement : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text unknownText;
        [SerializeField] private GameObject selectedFrame;

        private RelicDefinition definition;
        private System.Action<RelicDefinition> clickCallback;

        public RelicDefinition Definition => definition;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void Bind(RelicDefinition relicDefinition, System.Action<RelicDefinition> onClick)
        {
            definition = relicDefinition;
            clickCallback = onClick;
            Refresh();
        }

        public void Refresh()
        {
            if (definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            int level = RelicManager.Instance != null ? RelicManager.Instance.GetLevel(definition.relicId) : 0;
            bool owned = level > 0;

            if (backgroundImage != null && RelicManager.Instance != null)
                backgroundImage.sprite = RelicManager.Instance.GetBackground(definition.rarity);

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(owned);
                iconImage.sprite = definition.icon;
            }

            if (nameText != null)
            {
                nameText.gameObject.SetActive(owned);
                nameText.SetText(definition.displayName);
            }

            if (levelText != null)
            {
                levelText.gameObject.SetActive(owned);
                levelText.SetText("Lv.{0}", level);
            }

            if (unknownText != null)
            {
                unknownText.gameObject.SetActive(!owned);
                unknownText.SetText("?");
            }
        }

        public void VisibleName(bool visible)
        {
            if (nameText != null)
                nameText.gameObject.SetActive(visible);
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        private void HandleClick()
        {
            if (definition != null)
                clickCallback?.Invoke(definition);
        }
    }
}
