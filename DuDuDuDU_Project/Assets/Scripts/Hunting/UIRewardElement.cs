using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIRewardElement : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;

        public void Construct(Image icon, TMP_Text amount)
        {
            iconImage = icon;
            amountText = amount;
        }

        public void Bind(Sprite iconSprite, int amount)
        {
            Bind(iconSprite, amount, "{0:#,##0}");
        }

        public void Bind(Sprite iconSprite, int amount, string amountFormat)
        {
            if (iconImage != null)
                iconImage.sprite = iconSprite;

            if (amountText != null)
            {
                if (string.IsNullOrEmpty(amountFormat))
                    amountText.SetText("{0:#,##0}", amount);
                else
                    amountText.SetText(amountFormat, amount);
            }
        }
    }
}
