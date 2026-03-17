using System.Collections.Generic;
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
        [SerializeField] private TMP_Text descText;

        [Header("Milestones")]
        [SerializeField] private UIMilestoneElement milestoneElementPrefab;

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
        private readonly List<UIMilestoneElement> milestoneElements = new List<UIMilestoneElement>();
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
            int damage = meta != null ? meta.baseAttack + (level * meta.levelUpAttackIncrease) : 0;
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

            if (goldCostText != null) goldCostText.SetText("{0}/{1}", cost.goldCost, PointManager.Instance.Get(PointType.Gold));

            PointType scrollType = PointManager.ToScrollType(currentDiceType);
            if (scrollCostText != null) scrollCostText.SetText("{0}/{1}", cost.scrollCost, PointManager.Instance.Get(scrollType));

            PointMetadataDatabase db = StaticResource.Instance.PointMetadataDatabase;
            PointMetadataDatabase.PointMetadata metadata = db != null ? db.Get(scrollType) : null;
            if (scrollIcon != null) scrollIcon.sprite = metadata != null ? metadata.icon : null;
            RefreshMilestoneRows(meta, level);

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

        private void RefreshMilestoneRows(DiceMetaDataDatabase.DiceMeta meta, int currentLevel)
        {
            int count = meta != null && meta.milestones != null ? meta.milestones.Count : 0;
            for (int i = 0; i < count; i++)
            {
                UIMilestoneElement row = GetOrCreateMilestoneElement(i);
                if (row == null)
                    continue;

                bool unlocked = currentLevel >= meta.milestones[i].level;
                row.gameObject.SetActive(true);
                row.Bind(meta.milestones[i].level, meta.milestones[i].description, unlocked);
                RectTransform rowRect = row.transform as RectTransform;
                if (rowRect != null)
                {
                    rowRect.anchorMin = new Vector2(0.06f, 0.46f);
                    rowRect.anchorMax = new Vector2(0.94f, 0.46f);
                    rowRect.pivot = new Vector2(0.5f, 1f);
                    rowRect.anchoredPosition = new Vector2(0f, -(i * 62f));
                    rowRect.sizeDelta = new Vector2(0f, 54f);
                }
            }

            for (int i = count; i < milestoneElements.Count; i++)
            {
                if (milestoneElements[i] != null)
                    milestoneElements[i].gameObject.SetActive(false);
            }
        }

        private UIMilestoneElement GetOrCreateMilestoneElement(int index)
        {
            if (index < milestoneElements.Count && milestoneElements[index] != null)
                return milestoneElements[index];

            if (milestoneElementPrefab == null || dialogView == null)
                return null;

            UIMilestoneElement created = Instantiate(milestoneElementPrefab, dialogView.transform);
            milestoneElements.Add(created);
            return created;
        }
    }
}
