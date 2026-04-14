using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIRewardElement : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;

        public void Bind(Sprite iconSprite, int amount)
        {
            if (iconImage != null)
                iconImage.sprite = iconSprite;

            if (amountText != null)
                amountText.SetText(amount.ToString("#,##0"));
        }
    }
}
