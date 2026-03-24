using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIElementUpgradeItem : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text bonusText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Image costIconImage;

        private ElementType elementType = ElementType.Max;
        private System.Action<ElementType> clickCallback;

        private void Awake()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OnClickUpgrade);
        }

        private void OnDestroy()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnClickUpgrade);
        }

        public void Bind(ElementType type, System.Action<ElementType> onClick)
        {
            elementType = type;
            clickCallback = onClick;
            Refresh();
        }

        public void Refresh()
        {
            if (elementType == ElementType.Max)
                return;

            int level = ElementUpgradeManager.Instance != null ? ElementUpgradeManager.Instance.GetLevel(elementType) : 0;
            int nextCost = ElementUpgradeManager.Instance != null ? ElementUpgradeManager.Instance.GetNextUpgradeCost(elementType) : 1;
            int ownedCoin = PointManager.Instance != null ? PointManager.Instance.Get(PointType.Coin) : 0;
            ElementResource resource = StaticResource.Instance != null ? StaticResource.Instance.GetElementResource(elementType) : null;

            if (iconImage != null)
            {
                iconImage.sprite = resource != null ? resource.Icon : null;
                iconImage.color = resource != null ? resource.Color : Color.white;
            }

            if (nameText != null)
                nameText.SetText(GetDisplayName(elementType));

            if (bonusText != null)
                bonusText.SetText(BuildBonusText(elementType, level));

            if (levelText != null)
                levelText.SetText("Lv.{0}", level);

            if (costText != null)
                costText.SetText("{0}", nextCost);

            if (costIconImage != null && StaticResource.Instance != null && StaticResource.Instance.PointMetadataDatabase != null)
            {
                PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(PointType.Coin);
                costIconImage.sprite = metadata != null ? metadata.icon : null;
            }

            if (upgradeButton != null)
                upgradeButton.interactable = ownedCoin >= nextCost;
        }

        private void OnClickUpgrade()
        {
            clickCallback?.Invoke(elementType);
        }

        private static string BuildBonusText(ElementType elementType, int level)
        {
            float bonusPercent = level * 10f;
            if (elementType == ElementType.Water)
                return $"+{bonusPercent:0}%";

            if (elementType == ElementType.Light)
                return $"+{bonusPercent:0}%";

            return $"+{bonusPercent:0}%";
        }

        private static string GetDisplayName(ElementType elementType)
        {
            switch (elementType)
            {
                case ElementType.Normal:
                    return "Normal";
                case ElementType.Fire:
                    return "Fire";
                case ElementType.Water:
                    return "Water";
                case ElementType.Light:
                    return "Light";
                case ElementType.Dark:
                    return "Dark";
                default:
                    return elementType.ToString();
            }
        }
    }
}
