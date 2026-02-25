using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIDiceGrowthItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image bgImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject hasNewMilestoneDot;

        private DiceType diceType;
        private System.Action<DiceType> onClick;

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

        public void Bind(DiceType type, System.Action<DiceType> clickCallback)
        {
            diceType = type;
            onClick = clickCallback;
            Refresh();
        }

        public void Refresh()
        {
            var meta = DiceMetaDataProvider.GetMeta(diceType);
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(diceType) : 1;

            if (bgImage != null)
                bgImage.color = DiceMetaDataProvider.GetColor(diceType);

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(diceType);

            if (nameText != null)
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : diceType.ToString());

            if (levelText != null)
                levelText.SetText("Lv.{0}", level);

            if (hasNewMilestoneDot != null)
                hasNewMilestoneDot.SetActive(HasMilestoneAtNextLevel(meta, level));
        }

        private void HandleClick()
        {
            onClick?.Invoke(diceType);
        }

        private static bool HasMilestoneAtNextLevel(DiceMetaDataDatabase.DiceMeta meta, int currentLevel)
        {
            if (meta == null || meta.milestones == null)
                return false;

            int nextLevel = currentLevel + 1;
            for (int i = 0; i < meta.milestones.Count; i++)
            {
                if (meta.milestones[i].level == nextLevel)
                    return true;
            }

            return false;
        }
    }
}
