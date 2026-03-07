using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
        [SerializeField] private GameObject canUpgradeDot;
        [SerializeField] private Image scrollCountFillImage;
        [SerializeField] private TMP_Text scrollCountText;

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

            // if (bgImage != null)
            //     bgImage.color = DiceMetaDataProvider.GetColor(diceType);

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(diceType);

            if (nameText != null)
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : diceType.ToString());

            if (levelText != null)
                levelText.SetText("Lv.{0}", level);

            var cost = DiceLevelManager.Instance != null
                ? DiceLevelManager.Instance.GetNextUpgradeCost(diceType)
                : DiceMetaDataProvider.GetUpgradeCost(diceType, level);

            PointType scrollType = PointManager.ToScrollType(diceType);
            int ownedScroll = PointManager.Instance != null ? PointManager.Instance.Get(scrollType) : 0;
            int requiredScroll = Mathf.Max(0, cost.scrollCost);

            if (scrollCountText != null)
                scrollCountText.SetText("{0}/{1}", ownedScroll, requiredScroll);

            if (scrollCountFillImage != null)
            {
                float fill = requiredScroll <= 0 ? 1f : Mathf.Clamp01((float)ownedScroll / requiredScroll);
                scrollCountFillImage.fillAmount = fill;
            }

            if (canUpgradeDot != null)
            {
                int ownedGold = PointManager.Instance != null ? PointManager.Instance.Get(PointType.Gold) : 0;
                bool canUpgrade = ownedGold >= cost.goldCost && ownedScroll >= requiredScroll;
                canUpgradeDot.SetActive(canUpgrade);
            }
        }

        private void HandleClick()
        {
            onClick?.Invoke(diceType);
        }

    }
}
