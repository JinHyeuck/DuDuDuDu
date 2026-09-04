using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Point;
using OJ.Utils;
using VContainer;

namespace OJ.Element
{
    public class UIElementUpgradeItem : MonoBehaviour
    {
        // 8.3b: 이 아이템은 씬에 놓여 있지 않고 UIElementUpgradePanel 이 런타임에 프리팹으로
        // 찍는다. 배틀 스코프의 씬 순회는 이 오브젝트가 태어나기 전에 이미 끝났으므로
        // 순회로는 절대 채워지지 않는다 — 생성부가 resolver.Instantiate 로 찍어 주입한다.
        [Inject] private IBattleRefs battle;
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
            // 주입은 Instantiate 호출 안에서 Awake 가 끝난 뒤에 일어난다. 그래서 여기서는
            // 창구를 건드리지 않고 버튼 배선만 한다. 창구를 읽는 Refresh 는 생성부가
            // Instantiate 를 마치고 부르는 Bind 를 통해서만 들어오므로 그때는 이미 채워져 있다.
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

            // 이 아이템을 담는 패널은 ElementUpgradeManager 자신이 여는 것이라 매니저 없이는
            // 존재조차 못 한다. 그러니 창구가 비어 있으면 그건 표시할 값이 없는 상황이 아니라
            // 주입이 빠진 사고다 — 예전의 "null 이면 0레벨·1코인" 대체값은 그 사고를
            // 정상 화면처럼 그려서 덮어 버리므로 남기지 않는다.
            int level = battle.ElementUpgrade.GetLevel(elementType);
            int nextCost = battle.ElementUpgrade.GetNextUpgradeCost(elementType);
            int ownedCoin = PointManager.Instance != null ? PointManager.Instance.Get(PointType.BattleEnhanceStone) : 0;
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
                PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(PointType.BattleEnhanceStone);
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
