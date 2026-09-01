using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Dice
{
    public class UIMilestoneElement : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image lockImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private Color unlockedColor;
        [SerializeField] private Color lockedColor;
        

        public void Bind(int level, string description, bool unlocked)
        {
            if (backgroundImage != null)
                backgroundImage.color = unlocked ? unlockedColor : lockedColor;

            if (levelText != null)
            {
                levelText.SetText($"Lv.{level}");
                levelText.color = new Color(1f, 0.9f, 0.1f, 1f);
                levelText.fontStyle = FontStyles.Bold;
            }

            if (descText != null)
            {
                descText.SetText(description);
                descText.color = unlocked ? Color.white : new Color(0.72f, 0.72f, 0.78f, 1f);
                descText.fontStyle = FontStyles.Bold;
            }

            if (lockImage != null)
                lockImage.gameObject.SetActive(!unlocked);
        }
    }
}
