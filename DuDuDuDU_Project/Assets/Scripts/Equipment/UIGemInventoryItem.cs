using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIGemInventoryItem : MonoBehaviour
    {
        [SerializeField] private Button clickButton;
        [SerializeField] private List<Image> backgroundImage;
        [SerializeField] private Image gemIconImage;
        [SerializeField] private Image equipTypeIconImage;
        [SerializeField] private Image selectedFrame;

        private string gemId;
        private System.Action<string> clickCallback;
        private bool isClickBound;

        private void Awake()
        {
            if (backgroundImage == null)
                backgroundImage = new List<Image> { GetComponent<Image>() };

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

        public void Refresh(GemDefinition definition, bool selected, bool interactable)
        {
            if (definition != null)
            {
                foreach (var img in backgroundImage)
                {
                    if (img != null)
                        SetImage(img, UIEquipmentSpriteResolver.GetGemFrameSprite(definition.rarity), true);
                }
                SetImage(gemIconImage, UIEquipmentSpriteResolver.GetGemIconSprite(definition.rarity), true);
                SetImage(equipTypeIconImage, UIEquipmentSpriteResolver.GetEquipmentSmallIconSprite(definition.equipableType), true);
            }
            else
            {
                SetImage(gemIconImage, null, false);
                SetImage(equipTypeIconImage, null, false);
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
