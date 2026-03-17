using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIMilestoneElement : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image lockImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text descText;

        public void Bind(int level, string description, bool unlocked)
        {
            if (backgroundImage != null)
                backgroundImage.color = unlocked ? new Color(0.56f, 0.30f, 0.70f, 0.95f) : new Color(0.18f, 0.18f, 0.26f, 0.95f);

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
