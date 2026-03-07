using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIDiceGrowthDetailPanel : IDialog
    {
        [Header("Header")]
        [SerializeField] private Image iconImage;
        [SerializeField] private List<Image> elementIcons;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;

        [Header("Stats")]
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text levelUpGainText;
        [SerializeField] private TMP_Text pipFactorText;
        [SerializeField] private TMP_Text descText;

        [Header("Milestones")]
        [SerializeField] private TMP_Text milestoneText;

        [Header("Craft Recipe")]
        [SerializeField] private GameObject recipeSectionRoot;
        [SerializeField] private Transform recipeListRoot;
        [SerializeField] private UIDiceCraftMaterialStatusItem recipeItemPrefab;

        [Header("Cost")]
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private TMP_Text scrollCostText;
        [SerializeField] private Image scrollIcon;

        [Header("Buttons")]
        [SerializeField] private Button upgradeButton;

        private readonly List<UIDiceCraftMaterialStatusItem> recipeItems = new List<UIDiceCraftMaterialStatusItem>();
        private DiceType currentDiceType = DiceType.Normal;
        private System.Action onChanged;

        protected override void OnLoad()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OnClickUpgrade);
        }

        protected override void OnUnload()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnClickUpgrade);
        }

        protected override void OnEnter()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged += OnDiceLevelChanged;
        }

        protected override void OnExit()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged -= OnDiceLevelChanged;
        }

        public void Open(DiceType diceType, System.Action changedCallback)
        {
            currentDiceType = diceType;
            onChanged = changedCallback;
            Enter();
            Refresh();
        }

        public void Refresh()
        {
            var meta = DiceMetaDataProvider.GetMeta(currentDiceType);
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(currentDiceType) : 1;
            int samplePip = Mathf.Max(1, DiceTypeStarManager.Instance != null ? DiceTypeStarManager.Instance.GetTypeStars(currentDiceType) : 1);
            int damage = DiceMetaDataProvider.CalculateDamage(currentDiceType, samplePip, level);
            var cost = DiceLevelManager.Instance != null
                ? DiceLevelManager.Instance.GetNextUpgradeCost(currentDiceType)
                : DiceMetaDataProvider.GetUpgradeCost(currentDiceType, level);

            if (iconImage != null) iconImage.sprite = DiceMetaDataProvider.GetIcon(currentDiceType);
            for(int i = 0; i < meta.elementType.Length; i++)
            {
                if (elementIcons[i] != null)
                {
                    var elementResource = StaticResource.Instance.GetElementResource(meta.elementType[i]);
                    elementIcons[i].sprite = elementResource != null ? elementResource.Icon : null;
                    elementIcons[i].color = elementResource != null ? elementResource.Color : Color.white;
                    elementIcons[i].gameObject.SetActive(true);
                }
            }

            for(int i = meta.elementType.Length; i < elementIcons.Count; i++)
            {
                if (elementIcons[i] != null)
                    elementIcons[i].gameObject.SetActive(false);
            }

            if (nameText != null) nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : currentDiceType.ToString());
            if (levelText != null) levelText.SetText("Lv. {0}", level);
            if (damageText != null) damageText.SetText("{0}", damage);
            if (descText != null) descText.SetText(meta != null ? meta.description : string.Empty);
            if (levelUpGainText != null) levelUpGainText.SetText("+{0}", meta != null ? meta.levelUpAttackIncrease : 0);
            if (pipFactorText != null) pipFactorText.SetText(string.Format("x{0:0.##}", meta != null ? meta.dicePipAttackFactor : 1f));

            if (goldCostText != null) goldCostText.SetText("{0}/{1}", PointManager.Instance.Get(PointType.Gold), cost.goldCost);

            PointType scrollType = PointManager.ToScrollType(currentDiceType);
            if (scrollCostText != null) scrollCostText.SetText("{0}/{1}", PointManager.Instance.Get(scrollType), cost.scrollCost);

            PointMetadataDatabase db = StaticResource.Instance.PointMetadataDatabase;
            PointMetadataDatabase.PointMetadata metadata = db != null ? db.Get(scrollType) : null;
            if (scrollIcon != null) scrollIcon.sprite = metadata != null ? metadata.icon : null;
            if (milestoneText != null) milestoneText.SetText(BuildMilestoneText(meta, level));

            RefreshRecipeSection();
        }

        private void OnClickUpgrade()
        {
            if (DiceLevelManager.Instance == null)
                return;

            if (DiceLevelManager.Instance.TryLevelUp(currentDiceType))
            {
                Refresh();
                onChanged?.Invoke();
            }
        }

        private void OnDiceLevelChanged(DiceType diceType, int level)
        {
            if (diceType == currentDiceType)
                Refresh();
        }

        private void RefreshRecipeSection()
        {
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(currentDiceType);
            bool hasRecipe = recipe != null && recipe.Count > 0;

            if (recipeSectionRoot != null)
                recipeSectionRoot.SetActive(hasRecipe);

            if (!hasRecipe)
            {
                HideRecipeSlotsFrom(0);
                return;
            }

            int slot = 0;
            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                int have = DiceTypeStarManager.Instance != null
                    ? DiceTypeStarManager.Instance.GetTypeStarCount(req.diceType, req.star)
                    : 0;

                for (int unit = 0; unit < req.count; unit++)
                {
                    UIDiceCraftMaterialStatusItem item = GetOrCreateRecipeItem(slot);
                    if (item == null)
                        continue;

                    item.gameObject.SetActive(true);
                    item.Bind(req.diceType, req.star, have > unit, false);
                    slot++;
                }
            }

            HideRecipeSlotsFrom(slot);
        }

        private UIDiceCraftMaterialStatusItem GetOrCreateRecipeItem(int index)
        {
            if (index < recipeItems.Count && recipeItems[index] != null)
                return recipeItems[index];

            if (recipeItemPrefab == null || recipeListRoot == null)
                return null;

            UIDiceCraftMaterialStatusItem created = Instantiate(recipeItemPrefab, recipeListRoot);
            recipeItems.Add(created);
            return created;
        }

        private void HideRecipeSlotsFrom(int start)
        {
            for (int i = start; i < recipeItems.Count; i++)
            {
                if (recipeItems[i] != null)
                    recipeItems[i].gameObject.SetActive(false);
            }
        }

        private static string BuildMilestoneText(DiceMetaDataDatabase.DiceMeta meta, int currentLevel)
        {
            if (meta == null || meta.milestones == null || meta.milestones.Count == 0)
                return "특수 효과 없음";

            StringBuilder sb = new StringBuilder(256);
            for (int i = 0; i < meta.milestones.Count; i++)
            {
                var m = meta.milestones[i];
                bool unlocked = currentLevel >= m.level;
                sb.Append(unlocked ? "[활성] " : "[잠금] ");
                sb.Append("Lv.");
                sb.Append(m.level);
                sb.Append(" - ");
                sb.Append(m.description);
                if (i < meta.milestones.Count - 1)
                    sb.Append('\n');
            }

            return sb.ToString();
        }
    }
}
