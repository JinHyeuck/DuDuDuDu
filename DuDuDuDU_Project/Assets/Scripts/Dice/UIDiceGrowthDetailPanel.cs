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

        // ── 미보유(해금) 표시 ────────────────────────────────────────────
        //
        // 이 창은 <b>보유·미보유 둘 다</b> 맡는다. 미보유일 때 따로 팝업을 띄우지 않는 이유는,
        // 유저가 그때 알아야 할 것이 "이 다이스가 얼마나 센가"(이 창이 이미 그리는 것)와
        // "어떻게 얻나" 두 가지인데 그 둘을 갈라 놓으면 <b>비교할 수가 없기</b> 때문이다.
        //
        // <b>두 길은 <i>둘 중 하나</i>다.</b> 컨텐츠 미션과 재화 구매를 그냥 두 줄로 늘어놓으면
        // <b>둘 다 해야 열리는 것처럼</b> 읽힌다 — 실제로 그렇게 오해가 났다. 그래서 제목에
        // "두 가지 방법 중 진행" 을 박고, 두 줄 사이에 선을 그어 <b>갈래</b>임을 눈으로 말한다.

        /// <summary>비용 칸의 제목. 보유면 "필요 재화", 미보유면 두 갈래를 알리는 문구.</summary>
        [SerializeField] private TMP_Text priceTitleText;

        // 강화 비용 두 줄. <b>미보유일 때는 둘 다 끈다</b> — 해금 값은 아래 전용 줄이
        // 따로 그리므로, 켜 두면 <b>재화 아이콘이 화면에 두 개</b>가 된다. 실제로 그랬다.

        /// <summary>골드 줄(강화 비용).</summary>
        [SerializeField] private GameObject goldRow;

        /// <summary>스크롤 줄(강화 비용). <c>scrollIcon</c>·<c>scrollCostText</c> 가 이 안에 산다.</summary>
        [SerializeField] private GameObject scrollRow;

        /// <summary>미보유일 때만 켜지는 것들의 부모. 하나로 묶어야 껐다 켜는 자리가 하나가 된다.</summary>
        [SerializeField] private GameObject unlockRoot;

        /// <summary>"획득 미션 : 별 15개 모으기".</summary>
        [SerializeField] private TMP_Text unlockMissionText;

        /// <summary>미션 컨텐츠로 데려다 주는 버튼. 갈 곳이 없으면 꺼진다.</summary>
        [SerializeField] private Button unlockMissionButton;

        /// <summary>"즉시 구매 :" 줄의 재화 아이콘.</summary>
        [SerializeField] private Image unlockPriceIcon;

        /// <summary>보유/필요.</summary>
        [SerializeField] private TMP_Text unlockPriceText;

        /// <summary>재화로 여는 버튼. <b>바깥 LevelUp 버튼과 별개다</b> — 그쪽은 미보유일 때 잠긴다.</summary>
        [SerializeField] private Button unlockBuyButton;

        /// <summary>중복 획득이 재화로 돌아온다는 안내. 사기 전에 알아야 손해가 아니라고 읽힌다.</summary>
        [SerializeField] private TMP_Text unlockNoticeText;

        // 프리팹에 원래 적혀 있던 문구다. 코드가 갈아 끼우므로 되돌릴 값이 필요하다.
        private const string PriceTitleOwned = "필요 재화";
        private const string PriceTitleLocked = "획득 방법 (두 가지 방법 중 진행)";

        /// <summary>
        /// 중복 획득 안내. <b>사기 전에 보여야 뜻이 있다</b> — 컨텐츠 보상으로도 나오는
        /// 다이스를 재화로 먼저 사면 "그럼 나중에 그 보상은 날아가나" 가 걸리는데,
        /// 실제로는 가격만큼 재화로 돌아온다. 그 사실을 모르면 사지 않는다.
        /// </summary>
        private const string UnlockNotice = "(이미 획득한 다이스를 다시 얻으면 구매한 재화로 획득해요)";

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

            if (unlockMissionButton != null)
                unlockMissionButton.onClick.AddListener(OnClickUnlockMission);

            if (unlockBuyButton != null)
                unlockBuyButton.onClick.AddListener(OnClickUnlockBuy);
        }

        protected override void OnUnload()
        {
            if (upgradeButton != null)
                upgradeButton.onClick.RemoveListener(OnClickUpgrade);

            if (unlockMissionButton != null)
                unlockMissionButton.onClick.RemoveListener(OnClickUnlockMission);

            if (unlockBuyButton != null)
                unlockBuyButton.onClick.RemoveListener(OnClickUnlockBuy);
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
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            bool owned = ownership == null || ownership.IsOwned(currentDiceType);

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
            // 미보유에 "Lv. 1" 을 적으면 <b>이미 가진 것처럼</b> 읽힌다. 레벨 자체는 남아
            // 있지만(언락 전에도 올릴 수 있었다) 그 숫자가 지금 뜻하는 것은 없다.
            if (levelText != null)
            {
                if (owned)
                    levelText.SetText("Lv. {0}", level);
                else
                    levelText.SetText("미보유");
            }
            if (coolTimeText != null) coolTimeText.SetText("{0:0.0}", cooldown);
            if (descText != null) descText.SetText(BuildDescriptionText(meta, level));

            RefreshCostSection(owned, ownership, cost);
            RefreshMilestoneRows(meta, level);

            RefreshEvolvePath();
        }

        /// <summary>
        /// 비용 칸을 <b>강화용</b>과 <b>해금용</b> 사이에서 갈아 끼운다.
        ///
        /// 해금 쪽은 두 갈래를 <b>나란히</b> 놓는다 — 위가 컨텐츠 미션(+ 그리로 가는 버튼),
        /// 아래가 즉시 구매(+ 구매 버튼). 둘 사이의 선과 제목의 "두 가지 방법 중 진행" 이
        /// 그 둘이 <b>and 가 아니라 or</b> 임을 말하는 장치다.
        /// </summary>
        private void RefreshCostSection(bool owned, DiceOwnershipManager ownership, (int goldCost, int scrollCost) cost)
        {
            if (goldRow != null)
                goldRow.SetActive(owned);

            if (scrollRow != null)
                scrollRow.SetActive(owned);

            if (priceTitleText != null)
                priceTitleText.SetText(owned ? PriceTitleOwned : PriceTitleLocked);

            if (unlockRoot != null)
                unlockRoot.SetActive(!owned);

            // 재화 종류는 둘 다 ToScrollType 이 정한다 — 강화 스크롤과 해금 재화가
            // 같은 것이라, 아이콘을 어디에 꽂든 조회는 하나면 된다.
            PointType scrollType = PointManager.ToScrollType(currentDiceType);
            PointMetadataDatabase db = StaticResource.Instance.PointMetadataDatabase;
            PointMetadataDatabase.PointMetadata metadata = db != null ? db.Get(scrollType) : null;

            // 강화 줄의 아이콘. 미보유면 그 줄이 통째로 꺼지므로 꽂을 필요가 없다.
            if (owned && scrollIcon != null)
                scrollIcon.sprite = metadata != null ? metadata.icon : null;

            if (owned)
            {
                // <b>보유/필요 순서다.</b> 재화 표기는 프로젝트 전체가 이 순서를 쓴다 —
                // 앞이 "지금 얼마 있나", 뒤가 "얼마가 드는가".
                if (goldCostText != null)
                    goldCostText.SetText("{0}/{1}", PointManager.Instance.Get(PointType.Gold), cost.goldCost);

                if (scrollCostText != null)
                    scrollCostText.SetText("{0}/{1}", PointManager.Instance.Get(scrollType), cost.scrollCost);

                if (upgradeButton != null)
                    upgradeButton.interactable = true;

                return;
            }

            // <b>LevelUp 버튼은 잠근다.</b> 글자를 "구매" 로 바꾸지 않는다 — 구매는 아래
            // 전용 버튼이 맡고, 바깥 버튼까지 같은 일을 하면 누를 곳이 둘이 된다.
            if (upgradeButton != null)
                upgradeButton.interactable = false;

            RefreshUnlockRows(ownership, scrollType, metadata);
        }

        /// <summary>획득 미션 줄과 즉시 구매 줄을 채운다.</summary>
        private void RefreshUnlockRows(
            DiceOwnershipManager ownership, PointType scrollType, PointMetadataDatabase.PointMetadata metadata)
        {
            DiceUnlockDefinition definition = DiceUnlockDatabaseProvider.Database.Get(currentDiceType);
            int price = definition != null ? definition.price : 0;
            int held = PointManager.Instance != null ? PointManager.Instance.Get(scrollType) : 0;

            if (unlockMissionText != null)
            {
                string sources = DiceUnlockText.DescribeSources(definition);
                unlockMissionText.SetText(string.IsNullOrEmpty(sources)
                    ? "획득 미션 : 없음"
                    : "획득 미션 : " + sources);
            }

            // 갈 곳이 없으면 버튼을 감춘다. 눌러도 아무 일이 없는 버튼은 고장으로 읽힌다.
            if (unlockMissionButton != null)
                unlockMissionButton.gameObject.SetActive(DiceUnlockShortcut.CanGo(definition));

            if (unlockPriceIcon != null)
                unlockPriceIcon.sprite = metadata != null ? metadata.icon : null;

            if (unlockPriceText != null)
                unlockPriceText.SetText("{0}/{1}", held, price);

            // 가격이 없다는 것은 <b>살 수 없다</b>는 뜻이지 공짜가 아니다.
            if (unlockBuyButton != null)
                unlockBuyButton.interactable = price > 0 && ownership != null && ownership.CanAfford(currentDiceType);

            if (unlockNoticeText != null)
                unlockNoticeText.SetText(UnlockNotice);
        }

        private void OnClickUnlockMission()
        {
            DiceUnlockShortcut.Go(DiceUnlockDatabaseProvider.Database.Get(currentDiceType));
        }

        private void OnClickUnlockBuy()
        {
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            if (ownership == null)
                return;

            if (ownership.TryUnlockWithPoints(currentDiceType))
            {
                Refresh();
                onChanged?.Invoke();
            }
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
