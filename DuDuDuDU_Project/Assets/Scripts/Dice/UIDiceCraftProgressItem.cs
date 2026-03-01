using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIDiceCraftProgressItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private GameObject selectedFrame;

        private DiceType mythicType;
        private System.Func<DiceType, int> percentProvider;
        private System.Action<DiceType> clickCallback;
        private bool hideWhenZero = true;
        public DiceType MythicType => mythicType;

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

        public void Bind(
            DiceType type,
            System.Func<DiceType, int> getPercent,
            System.Action<DiceType> onClick,
            bool hideZero = true)
        {
            mythicType = type;
            percentProvider = getPercent;
            clickCallback = onClick;
            hideWhenZero = hideZero;
            Refresh();
        }

        public void Refresh()
        {
            int percent = percentProvider != null ? percentProvider(mythicType) : 0;
            bool visible = !hideWhenZero || percent > 0;
            gameObject.SetActive(visible);
            if (!visible)
                return;

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(mythicType);

            if (percentText != null)
                percentText.SetText("{0}%", percent);
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        private void HandleClick()
        {
            clickCallback?.Invoke(mythicType);
        }
    }
}
