using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Point;
using OJ.UI;
using OJ.Utils;
using VContainer;

namespace OJ.Dice
{
    public class UIDiceGrowthDetailPanel : DialogBase
    {
        [Header("Header")]
        [SerializeField] private Image iconImage;
        [SerializeField] private List<Image> elementIcons;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;

        [Header("Stats")]
        [SerializeField] private TMP_Text coolTimeText;
        [SerializeField] private TMP_Text descText;

        [Header("Milestones")]
        [SerializeField] private Transform milestoneListRoot;
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

        // 8.3b: 이 패널은 UIService 가 런타임에 프리팹으로 찍는다. BattleScope 의 씬 루트
        // 순회는 sceneLoaded 때 한 번뿐이라 그 뒤에 태어나는 이 오브젝트에는 닿지 않고,
        // 대신 resolver.Instantiate 가 호출 안에서 채워 준다.
        // <b>주입은 Awake 보다 먼저다.</b> VContainer 의 부모 있는 Instantiate 는 프리팹을
        // SetActive(false) 로 껐다 찍고, 주입한 뒤에 켠다(ObjectResolverUnityExtensions.cs:78-91).
        // 그래서 DialogBase.Awake 가 부르는 OnLoad 시점에도 이미 채워져 있다.
        // 그래도 읽기를 뒤로 미뤄 뒀다 — 씬에 놓인 컴포넌트는 반대(자기 Awake 뒤)라
        // 두 규칙을 섞어 기억하는 것이 사고의 원천이고, 늦게 읽어서 손해 볼 것이 없다.
        [Inject] private IBattleRefs battle;

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
            float cooldown = DiceMetaDataProvider.GetCooldown(currentDiceType, 1);
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
            if (coolTimeText != null) coolTimeText.SetText("{0:0.0}", cooldown);
            if (descText != null) descText.SetText(BuildDescriptionText(meta, level));

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
                // 8.3b: 예전 DiceTypeStarManager.Instance == null 검사를 그대로 옮긴 것이다.
                // 그 매니저는 전투 씬에만 사는 물건이라 "Instance 가 null" 은 곧 "전투가
                // 없다"는 뜻이었고, 창구에서 같은 것을 묻는 이름이 IsActive 다. 이 패널은
                // 로비에서도 열리므로 그때 보유 개수가 0 으로 보이던 동작이 그대로 산다.
                //
                // battle 이나 battle.DiceStars 를 대신 검사하지 않았다. UIService 가
                // 루트 리졸버로 찍으므로 창구 자체는 항상 채워지고, 전투 중에 그 뒤가
                // null 이면 조용히 0 을 그릴 상황이 아니라 주입이 빠진 사고다.
                int have = battle.IsActive
                    ? battle.DiceStars.GetTypeStarCount(req.diceType, req.star)
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

            if (milestoneElementPrefab == null || milestoneListRoot == null)
                return null;

            UIMilestoneElement created = Instantiate(milestoneElementPrefab, milestoneListRoot);
            milestoneElements.Add(created);
            return created;
        }

        private string BuildDescriptionText(DiceMetaDataDatabase.DiceMeta meta, int level)
        {
            if (meta == null)
                return string.Empty;

            if (currentDiceType == DiceType.Poison || currentDiceType == DiceType.KingPoison)
            {
                float poisonMultiplier = DiceMetaDataProvider.GetPoisonDamageMultiplier(DiceType.Poison, level);
                float poisonDuration = DiceMetaDataProvider.GetPoisonDuration(DiceType.Poison);
                return $"{meta.description}\n중독: 0.5초마다 현재 체력의 10% x {poisonMultiplier:0.##} 피해 ({poisonDuration:0.#}초)";
            }

            return meta.description;
        }
    }
}
