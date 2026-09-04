using System.Collections.Generic;
using System.Text;
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

        // 조합식 칸이던 자리다. 프리팹의 필드 이름·배선은 그대로 두고 <b>보여 주는 것만</b>
        // 바꿨다 — 이름을 바꾸면 프리팹의 직렬화 참조가 끊어져 에디터에서 다시 끌어다
        // 놓아야 하고, 이 화면은 로비에 이미 배선이 끝나 있다.
        [Header("Evolve Path")]
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

            RefreshEvolvePath();
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

        /// <summary>
        /// 진화 경로 칸. 예전에는 조합식 재료를 늘어놓던 자리다.
        ///
        /// 조합식이 사라지면서 보여 줄 것이 <b>재료 목록에서 한 줄짜리 계보</b>로 바뀌었다.
        /// 이 다이스가 무엇에서 왔고 무엇이 되는지, 아이콘 두 칸으로 말한다.
        ///
        /// <list type="bullet">
        /// <item>기본 다이스: [4성 자기 자신] → [특수]</item>
        /// <item>특수 다이스: [특수 자기 자신] → [킹]</item>
        /// <item>킹 다이스: 최종이라 칸을 통째로 숨긴다</item>
        /// </list>
        ///
        /// <b>"보유" 표시를 끈다</b>(showState: false). 재고를 세어 조합 가능 여부를 말하던
        /// 칸이었지만, 진화는 보드 위의 그 다이스 하나와 재화만 보므로 로비에서 셀 재고가
        /// 없다. 켜 두면 항상 "미보유"라고 빨갛게 거짓말을 한다.
        /// </summary>
        private void RefreshEvolvePath()
        {
            bool hasPath = DiceEvolution.TryGetEvolveTarget(currentDiceType, out DiceType evolveTarget);

            if (recipeSectionRoot != null)
                recipeSectionRoot.SetActive(hasPath);

            if (!hasPath)
            {
                HideRecipeSlotsFrom(0);
                return;
            }

            // 진화의 재료는 자기 자신이다. 기본 다이스만 성급 조건이 붙는다.
            int fromStar = DiceEvolution.GetTier(currentDiceType) == DiceTier.Base
                ? DiceEvolution.EvolveRequiredStar
                : 1;

            BindEvolveSlot(0, currentDiceType, fromStar);
            BindEvolveSlot(1, evolveTarget, 1);
            HideRecipeSlotsFrom(2);
        }

        private void BindEvolveSlot(int index, DiceType diceType, int star)
        {
            UIDiceCraftMaterialStatusItem item = GetOrCreateRecipeItem(index);
            if (item == null)
                return;

            item.gameObject.SetActive(true);
            item.Bind(diceType, star, true, false);
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

        /// <summary>
        /// 설명 칸. <b>문장은 에셋에서, 숫자는 공식에서</b> 온다.
        ///
        /// 예전에는 에셋의 <c>description</c> 안에 "적 1명에게 110 + (레벨 x 22) 대미지"처럼
        /// 수치가 박혀 있었다. 그래서 밸런스를 만질 때마다 그 문장이 조용히 낡았고, 실제로
        /// 킹 4종의 설명이 <c>baseAttack</c> 과 어긋난 채 한동안 떠 있었다. 문자열은
        /// 컴파일러도 테스트도 검사하지 않으므로 그 어긋남은 눈으로 찾을 수밖에 없다.
        ///
        /// 이제 <c>description</c> 은 <b>동작만</b> 말하고 수치를 담지 않는다. 공격력은
        /// 아래에서 실제 데미지 공식(<see cref="DiceMetaDataProvider.CalculateDamage"/>)으로
        /// 뽑고, 효과 수치는 <see cref="DiceTraitText"/> 가 각 공식을 불러 채운다.
        /// 그러면 밸런스를 고치는 순간 화면이 따라온다.
        ///
        /// 공격력을 <b>1성 기준</b>으로 뽑는 이유는 이 화면이 쿨타임도 그렇게 보여 주기
        /// 때문이다(<c>GetCooldown(type, 1)</c>). 성급은 보드 위에서 정해지는 값이라
        /// 로비에는 없다. 장비 보정은 들어간다 — 장비는 전투 밖에서도 끼고 있다.
        /// </summary>
        private string BuildDescriptionText(DiceMetaDataDatabase.DiceMeta meta, int level)
        {
            if (meta == null)
                return string.Empty;

            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(meta.description))
                builder.AppendLine(meta.description);

            const int lobbyStar = 1;
            builder.AppendFormat("공격력 {0}",
                DiceMetaDataProvider.CalculateDamage(currentDiceType, lobbyStar, level));

            string trait = DiceTraitText.Detailed(currentDiceType, level, battle);
            if (!string.IsNullOrEmpty(trait))
            {
                builder.AppendLine();
                builder.Append(trait);
            }

            return builder.ToString();
        }
    }
}
