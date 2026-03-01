using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIMythicDiceProgressItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text percentText;

        private DiceType mythicType;
        private System.Func<DiceType, int> percentProvider;
        private System.Action<DiceType> clickCallback;
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
            System.Action<DiceType> onClick)
        {
            mythicType = type;
            percentProvider = getPercent;
            clickCallback = onClick;
            Refresh();
        }

        public void Refresh()
        {
            int percent = percentProvider != null ? percentProvider(mythicType) : 0;
            gameObject.SetActive(percent > 0);
            if (percent <= 0)
                return;

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(mythicType);

            if (percentText != null)
                percentText.SetText("{0}%", percent);
        }

        private void HandleClick()
        {
            clickCallback?.Invoke(mythicType);
        }
    }
}
