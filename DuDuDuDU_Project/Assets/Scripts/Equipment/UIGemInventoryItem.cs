using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OJ
{
    public class UIGemInventoryItem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Button clickButton;
        [SerializeField] private List<Image> backgroundImage;
        [SerializeField] private Image gemIconImage;
        [SerializeField] private Image equipTypeIconImage;
        [SerializeField] private Image selectedFrame;
        [SerializeField] private GameObject checkObject;
        [SerializeField] private float longPressSeconds = 0.45f;

        private string gemId;
        private System.Action<string> clickCallback;
        private System.Action<string> longPressCallback;
        private bool isClickBound;
        private bool pointerDown;
        private bool longPressTriggered;
        private bool suppressClick;
        private float pointerDownTime;

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
            Bind(id, onClick, null);
        }

        public void Bind(string id, System.Action<string> onClick, System.Action<string> onLongPress)
        {
            gemId = id;
            clickCallback = onClick;
            longPressCallback = onLongPress;
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
            if (checkObject != null)
                checkObject.SetActive(false);

            if (clickButton != null)
                clickButton.interactable = interactable;
        }

        public void SetChecked(bool isChecked)
        {
            if (checkObject != null)
            {
                checkObject.SetActive(isChecked);
                return;
            }

            if (selectedFrame != null)
                selectedFrame.enabled = isChecked;
        }

        private void OnClick()
        {
            if (suppressClick)
            {
                suppressClick = false;
                return;
            }

            clickCallback?.Invoke(gemId);
        }

        private void Update()
        {
            if (!pointerDown || longPressTriggered || longPressCallback == null)
                return;

            if (Time.unscaledTime - pointerDownTime < longPressSeconds)
                return;

            longPressTriggered = true;
            suppressClick = true;
            longPressCallback.Invoke(gemId);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDown = true;
            longPressTriggered = false;
            suppressClick = false;
            pointerDownTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerDown = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerDown = false;
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
