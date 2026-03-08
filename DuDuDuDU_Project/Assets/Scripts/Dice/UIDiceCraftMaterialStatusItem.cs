using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIDiceCraftMaterialStatusItem : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text starText;
        [SerializeField] private TMP_Text stateText;

        public void Bind(DiceType diceType, int star, bool owned, bool showState = true)
        {
            if (iconImage != null)
            {
                iconImage.sprite = DiceMetaDataProvider.GetIcon(diceType);
                iconImage.color = Color.white;
            }

            if (starText != null)
            {
                bool showStarUI = DiceMetaDataProvider.ShowStarUI(diceType);
                starText.gameObject.SetActive(showStarUI);
                if (showStarUI)
                    starText.SetText($"{star}★");
            }

            if (stateText != null)
            {
                stateText.gameObject.SetActive(showState);
                if (showState)
                {
                    stateText.SetText(owned ? "보유" : "미보유");
                    stateText.color = owned ? new Color(0.2f, 0.9f, 0.4f) : new Color(1f, 0.35f, 0.35f);
                }
            }
        }
    }
}
