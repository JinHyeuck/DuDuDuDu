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
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image gemIconImage;
        [SerializeField] private Image equipTypeIconImage;
        [SerializeField] private Image selectedFrame;

        private string gemId;
        private System.Action<string> clickCallback;
        private bool isClickBound;

        private void Awake()
        {
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();

            TryBindClick();
        }

        private void OnDestroy()
        {
            if (isClickBound && clickButton != null)
                clickButton.onClick.RemoveListener(OnClick);
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
                if (rarityText != null) rarityText.SetText(UIEquipmentText.GetRarityName(definition.rarity));
                SetImage(backgroundImage, UIEquipmentSpriteResolver.GetGemFrameSprite(definition.rarity), true);
                SetImage(gemIconImage, UIEquipmentSpriteResolver.GetGemIconSprite(definition.rarity), true);
                SetImage(equipTypeIconImage, UIEquipmentSpriteResolver.GetEquipmentSmallIconSprite(definition.equipableType), true);
            }
            else
            {
                if (nameText != null) nameText.SetText(gemId);
                if (rarityText != null) rarityText.SetText("Unknown");
                SetImage(gemIconImage, null, false);
                SetImage(equipTypeIconImage, null, false);
            }

            if (descText != null)
            {
                descText.SetText(string.Empty);
                descText.gameObject.SetActive(false);
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
