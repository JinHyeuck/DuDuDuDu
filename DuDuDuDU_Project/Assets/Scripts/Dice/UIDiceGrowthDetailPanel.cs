using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.Hunting;
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

        // 조합식 칸이던 자리다. 지금은 <b>승급 칸</b>이 대신 들어서서(BuildPromotionBox) 이 목록은 켜지 않는다.
        // 필드 이름·배선은 그대로 둔다 — 이름을 바꾸면 프리팹의 직렬화 참조가 끊어져 에디터에서 다시 끌어다
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

        /// <summary>해금 가이드가 강화 버튼을 가리킨다(<c>LobbyUnlockDirector</c>).</summary>
        public Button UpgradeButton => upgradeButton;

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

        // ── 레벨 초기화 버튼 ─────────────────────────────────────────────
        //
        // <b>코드로 만든다.</b> 이 창의 프리팹 YAML 을 건드리지 않고 강화 버튼 왼쪽에 붙이기 위해서다
        // (<c>UIBattleCurrencyBox</c> 와 같은 방식). 자리는 강화 버튼을 기준으로 잡는다.

        private static readonly Vector2 ResetButtonSize = new Vector2(220f, 110f);
        private const float ResetButtonGap = 24f;

        private Button resetButton;
        private TMP_Text resetLabel;

        private void BuildResetButton()
        {
            if (upgradeButton == null || levelText == null || resetButton != null)
                return;

            var upRect = (RectTransform)upgradeButton.transform;
            Transform parent = upRect.parent;

            GameObject root = UIBountyUIFactory.CreateRect("ResetButton", parent);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = upRect.anchorMin;
            rect.anchorMax = upRect.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ResetButtonSize;

            Vector2 upCenter = upRect.anchoredPosition +
                               Vector2.Scale(new Vector2(0.5f, 0.5f) - upRect.pivot, upRect.rect.size);
            float x = upCenter.x - upRect.rect.width * 0.5f - ResetButtonGap - ResetButtonSize.x * 0.5f;
            rect.anchoredPosition = new Vector2(x, upCenter.y);

            Image back = root.AddComponent<Image>();
            back.sprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Red");
            back.type = Image.Type.Sliced;
            back.color = new Color(0.9f, 0.5f, 0.5f, 1f);

            resetButton = root.AddComponent<Button>();
            resetButton.targetGraphic = back;
            ColorBlock colors = resetButton.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
            resetButton.colors = colors;

            resetLabel = UIBountyUIFactory.CreateText("Label", root.transform, "초기화", 38f,
                TextAlignmentOptions.Center, Color.white, levelText.font, levelText.fontSharedMaterial);
            UIBountyUIFactory.Stretch(resetLabel.rectTransform);
            resetLabel.textWrappingMode = TextWrappingModes.NoWrap;
            resetLabel.lineSpacing = -12f;

            resetButton.onClick.AddListener(OnClickReset);
        }

        private void RefreshResetButton(bool owned)
        {
            if (resetButton == null)
                return;

            resetButton.gameObject.SetActive(owned);
            if (!owned)
                return;

            DiceLevelManager levels = DiceLevelManager.Instance;
            bool can = levels != null && levels.CanReset(currentDiceType);
            resetButton.interactable = can;

            if (!can)
            {
                resetLabel.SetText("초기화");
                return;
            }

            int gem = levels.GetResetGemCost(currentDiceType);
            resetLabel.SetText(gem > 0
                ? "초기화\n<size=60%><color=#FFE27A>무료젬 " + gem + "</color></size>"
                : "초기화\n<size=60%><color=#CFFFD0>무료</color></size>");
        }

        private void OnClickReset()
        {
            DiceLevelManager levels = DiceLevelManager.Instance;
            if (levels == null || !levels.CanReset(currentDiceType))
                return;

            UIConfirmDialog confirm = GameContainer.UI?.Get<UIConfirmDialog>();
            if (confirm == null)
                return;

            DiceType target = currentDiceType;
            int level = levels.GetLevel(target);
            int gem = levels.GetResetGemCost(target);
            int held = PointManager.Instance != null ? PointManager.Instance.Get(PointType.FreeGem) : 0;

            var meta = DiceMetaDataProvider.GetMeta(target);
            string diceName = meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : target.ToString();

            var message = new StringBuilder();
            message.Append("<size=32>").Append(diceName).Append("의 레벨을 1로 되돌려요.\n");
            message.Append("레벨업에 쓴 재화는 모두 돌려받아요.</size>\n");
            message.Append("<size=12> </size>\n");
            message.Append("<size=28><color=#C9D6EE>")
                .Append(DiceLevelResetRules.FreeMaxLevel).Append("레벨까지 무료 / ")
                .Append(DiceLevelResetRules.FreeMaxLevel + 1).Append("레벨부터 무료젬 ")
                .Append(DiceLevelResetRules.GemCost).Append("개</color></size>\n");
            message.Append("<size=32><color=#FFE27A>");
            if (gem > 0)
                message.Append("지금 Lv.").Append(level).Append(" : 무료젬 ").Append(gem).Append("개 (보유 ").Append(held).Append(")");
            else
                message.Append("지금 Lv.").Append(level).Append(" : 무료로 초기화해요");
            message.Append("</color></size>");

            confirm.transform.SetAsLastSibling();
            confirm.Open("다이스 초기화", message.ToString(), "초기화", "취소", () => ConfirmReset(target));
        }

        private void ConfirmReset(DiceType target)
        {
            DiceLevelManager levels = DiceLevelManager.Instance;
            if (levels == null)
                return;

            var refund = new List<PointRewardEntry>();
            switch (levels.TryReset(target, refund))
            {
                case DiceResetResult.Done:
                    Refresh();
                    onChanged?.Invoke();
                    ShowResetRefund(refund);
                    break;

                case DiceResetResult.NotEnoughGems:
                    UIConfirmDialog alert = GameContainer.UI?.Get<UIConfirmDialog>();
                    if (alert != null)
                    {
                        int held = PointManager.Instance != null ? PointManager.Instance.Get(PointType.FreeGem) : 0;
                        alert.transform.SetAsLastSibling();
                        alert.Open("무료젬이 부족해요",
                            "초기화에는 무료젬 " + levels.GetResetGemCost(target) + "개가 필요해요.\n(보유 " + held + "개)",
                            "확인", null, null);
                    }
                    break;
            }
        }

        private static void ShowResetRefund(List<PointRewardEntry> refund)
        {
            if (refund.Count == 0)
                return;

            UIRewardResultDialog result = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (result == null)
                return;

            result.transform.SetAsLastSibling();
            result.Open(refund, "레벨업에 쓴 재화를 돌려받았어요.");
        }

        protected override void OnLoad()
        {
            BuildResetButton();
            BuildPromotionBox();

            // 선택권·이벤트 재화(승급 미션 B)를 바로 사는 "충전" 버튼(강화 버튼 오른쪽).
            if (levelText != null)
            {
                OJ.Shop.CurrencyPassPopup.AttachBeside(
                    upgradeButton, OJ.Shop.ContentPass.DiceGrowth, levelText.font, new Vector2(200f, 110f));
            }

            if (upgradeButton != null)
                upgradeButton.onClick.AddListener(OnClickUpgrade);

            if (unlockMissionButton != null)
                unlockMissionButton.onClick.AddListener(OnClickUnlockMission);

            if (unlockBuyButton != null)
                unlockBuyButton.onClick.AddListener(OnClickUnlockBuy);
        }

        protected override void OnUnload()
        {
            if (resetButton != null)
                resetButton.onClick.RemoveListener(OnClickReset);

            if (promotionButton != null)
                promotionButton.onClick.RemoveListener(OnClickPromotion);

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

            if (PromotionManager.Instance != null)
            {
                PromotionManager.Instance.OnChanged += Refresh;
                // 끝난 이벤트의 남은 재화를 정리한 뒤 화면을 그린다(날짜가 바뀐 프레임이 아직 안 왔을 수 있다).
                PromotionManager.Instance.Tick(System.DateTime.UtcNow);
            }
        }

        protected override void OnExit()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged -= OnDiceLevelChanged;

            if (PromotionManager.Instance != null)
                PromotionManager.Instance.OnChanged -= Refresh;
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
            bool maxLevel = owned && level >= Define.MaxDiceLevel;

            if (levelText != null)
            {
                if (!owned)
                    levelText.SetText("미보유");
                else if (maxLevel)
                    levelText.SetText("Lv. {0}  MAX", level);
                else
                    levelText.SetText("Lv. {0}", level);
            }
            if (coolTimeText != null) coolTimeText.SetText("{0:0.0}", cooldown);
            if (descText != null) descText.SetText(BuildDescriptionText(meta, level));

            RefreshCostSection(owned, ownership, cost);
            RefreshResetButton(owned);
            RefreshMilestoneRows(meta, level);

            RefreshPromotionBox(owned, level);
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
            // 레벨업에 골드가 들지 않는다(카피만). 골드 줄은 비용이 있을 때만 보인다.
            if (goldRow != null)
                goldRow.SetActive(owned && cost.goldCost > 0);

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
                // <b>만렙이면 비용 자리에 MAX 를 적는다.</b> 버튼만 잠그면 유저는 눌러 보고
                // 아무 일도 안 일어나는 것으로 알게 된다 — 이유가 화면에 있어야 한다.
                // 남은 비용을 그대로 두면 "돈은 있는데 왜 안 되지"가 된다.
                bool atMaxLevel = DiceLevelManager.Instance != null &&
                                  DiceLevelManager.Instance.IsMaxLevel(currentDiceType);

                if (atMaxLevel)
                {
                    if (goldCostText != null)
                        goldCostText.SetText("MAX");

                    if (scrollCostText != null)
                        scrollCostText.SetText("MAX");

                    if (upgradeButton != null)
                        upgradeButton.interactable = false;

                    return;
                }

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

        /// <summary>
        /// 획득 미션 컨텐츠로 보낸다.
        ///
        /// <b>보내고 나서 이 창을 닫는다.</b> 안 닫으면 컨텐츠 화면 <i>위에</i> 다이스 상세창이
        /// 그대로 떠 있어서, 보내 준 화면을 정작 볼 수가 없다.
        /// 못 갔으면 닫지 않는다 — 그때 닫으면 유저 앞에 아무것도 안 남는다.
        /// </summary>
        private void OnClickUnlockMission()
        {
            if (DiceUnlockShortcut.Go(DiceUnlockDatabaseProvider.Database.Get(currentDiceType)))
                Exit();
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


        /// <summary>줄마다 어느 레벨의 마일스톤인가. 가이드가 첫 마일스톤 줄을 짚는 데 쓴다.</summary>
        private readonly List<int> milestoneRowLevels = new List<int>();

        /// <summary>이 레벨의 마일스톤 줄. 안 보이거나 없으면 null.</summary>
        public RectTransform GetMilestoneRow(int level)
        {
            for (int i = 0; i < milestoneElements.Count && i < milestoneRowLevels.Count; i++)
            {
                if (milestoneRowLevels[i] == level && milestoneElements[i] != null && milestoneElements[i].gameObject.activeInHierarchy)
                    return milestoneElements[i].transform as RectTransform;
            }

            return null;
        }

        /// <summary>추가 효과 목록 한 줄. 마일스톤(레벨로 열림)과 승급(단계로 열림)이 섞여 레벨순으로 선다.</summary>
        private readonly struct EffectRow
        {
            public EffectRow(int level, string description, bool unlocked, bool promotion)
            {
                Level = level;
                Description = description;
                Unlocked = unlocked;
                Promotion = promotion;
            }

            public int Level { get; }
            public string Description { get; }
            public bool Unlocked { get; }
            public bool Promotion { get; }
        }

        private const float MilestoneRowPitch = 62f;
        private const float MilestoneRowHeight = 54f;

        /// <summary>
        /// 추가 효과 목록. <b>마일스톤 줄과 승급 줄을 한 목록에 합쳐</b> 레벨순으로 세운다 —
        /// 승급 효과는 "다이스를 키우면 생기는 것"이라 따로 칸을 두면 같은 종류의 정보가 두 곳에 갈라진다.
        /// 승급 줄은 레벨이 아니라 <b>승급 단계</b>로 열린다(레벨은 등록 조건일 뿐이다).
        /// </summary>
        private void RefreshMilestoneRows(DiceMetaDataDatabase.DiceMeta meta, int currentLevel)
        {
            var rows = new List<EffectRow>();

            if (meta != null && meta.milestones != null)
            {
                for (int i = 0; i < meta.milestones.Count; i++)
                {
                    rows.Add(new EffectRow(
                        meta.milestones[i].level,
                        meta.milestones[i].description,
                        currentLevel >= meta.milestones[i].level,
                        false));
                }
            }

            if (PromotionManager.IsPromotable(currentDiceType))
            {
                int maxTier = PromotionRules.MaxTier(PromotionManager.IsBasic(currentDiceType));
                int tier = PromotionManager.TierOf(currentDiceType);
                for (int promoTier = 1; promoTier <= maxTier; promoTier++)
                {
                    string text = PromotionText.Describe(currentDiceType, promoTier);
                    if (string.IsNullOrEmpty(text))
                        continue;

                    rows.Add(new EffectRow(
                        PromotionRules.RequiredDiceLevel(promoTier),
                        "<color=#7FE3FF>[승급]</color> " + text,
                        tier >= promoTier,
                        true));
                }
            }

            // 레벨순. 같은 레벨이면 마일스톤이 먼저다(승급 Lv30 과 기본 5종 Lv30 마일스톤이 겹치지는 않지만 순서를 못 박아 둔다).
            rows.Sort((x, y) =>
            {
                int byLevel = x.Level.CompareTo(y.Level);
                return byLevel != 0 ? byLevel : x.Promotion.CompareTo(y.Promotion);
            });

            milestoneRowLevels.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                UIMilestoneElement row = GetOrCreateMilestoneElement(i);
                // 승급 줄은 레벨이 겹칠 수 있어 가이드 조회에는 마일스톤만 싣는다.
                milestoneRowLevels.Add(rows[i].Promotion ? -1 : rows[i].Level);
                if (row == null)
                    continue;

                row.gameObject.SetActive(true);
                row.Bind(rows[i].Level, rows[i].Description, rows[i].Unlocked);
                RectTransform rowRect = row.transform as RectTransform;
                if (rowRect != null)
                {
                    // 위쪽 기준으로 세운다. 줄이 여섯 개까지 늘 수 있어 목록 높이를 줄 수에 맞춰야 스크롤이 된다.
                    rowRect.anchorMin = new Vector2(0.06f, 1f);
                    rowRect.anchorMax = new Vector2(0.94f, 1f);
                    rowRect.pivot = new Vector2(0.5f, 1f);
                    rowRect.anchoredPosition = new Vector2(0f, -(i * MilestoneRowPitch));
                    rowRect.sizeDelta = new Vector2(0f, MilestoneRowHeight);
                }
            }

            if (milestoneListRoot is RectTransform content)
            {
                content.sizeDelta = new Vector2(content.sizeDelta.x, rows.Count * MilestoneRowPitch);
            }

            for (int i = rows.Count; i < milestoneElements.Count; i++)
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

        // ── 승급 칸 ──────────────────────────────────────────────────────
        //
        // 예전에 조합식(그 뒤 진화 경로)을 보이던 칸 자리다. <b>코드로 만든다</b> — 강화 버튼 옆 초기화 버튼과
        // 같은 이유로, 이 창의 프리팹 YAML 을 건드리지 않고 기존 칸(Craft) 안에 붙이기 위해서다.
        // 칸 제목(CraftTitle)만 "승급" 으로 바꾸고, 옛 진화 경로 목록(recipeSectionRoot)은 켜지 않는다.

        private TMP_Text promotionStatusText;
        private Button promotionButton;
        private TMP_Text promotionButtonLabel;

        private static readonly Vector2 PromotionButtonSize = new Vector2(250f, 104f);

        private void BuildPromotionBox()
        {
            if (promotionButton != null || recipeSectionRoot == null || levelText == null)
                return;

            Transform craft = recipeSectionRoot.transform.parent;
            if (craft == null)
                return;

            recipeSectionRoot.SetActive(false);

            Transform title = craft.Find("CraftTitle");
            TMP_Text titleText = title != null ? title.GetComponentInChildren<TMP_Text>(true) : null;
            if (titleText != null)
                titleText.SetText("승급");

            // 옛 칸의 안내 문구("조합이 필요 없습니다")가 같은 자리에 남아 새 글자와 겹친다 — 글자만 끈다(배경은 칸 몫이다).
            Transform oldDesc = craft.Find("Desc");
            if (oldDesc != null)
            {
                foreach (TMP_Text old in oldDesc.GetComponentsInChildren<TMP_Text>(true))
                    old.enabled = false;
            }

            promotionStatusText = UIBountyUIFactory.CreateText("PromotionStatus", craft, string.Empty, 30f,
                TextAlignmentOptions.TopLeft, Color.white, levelText.font, levelText.fontSharedMaterial);
            RectTransform statusRect = promotionStatusText.rectTransform;
            statusRect.anchorMin = new Vector2(0.04f, 0.04f);
            statusRect.anchorMax = new Vector2(0.62f, 0.80f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            promotionStatusText.textWrappingMode = TextWrappingModes.Normal;
            promotionStatusText.raycastTarget = false;

            GameObject root = UIBountyUIFactory.CreateRect("PromotionButton", craft);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.80f, 0.40f);
            rect.anchorMax = new Vector2(0.80f, 0.40f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = PromotionButtonSize;
            rect.anchoredPosition = Vector2.zero;

            Image back = root.AddComponent<Image>();
            back.sprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green");
            back.type = Image.Type.Sliced;

            promotionButton = root.AddComponent<Button>();
            promotionButton.targetGraphic = back;
            ColorBlock colors = promotionButton.colors;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
            promotionButton.colors = colors;

            promotionButtonLabel = UIBountyUIFactory.CreateText("Label", root.transform, "승급 미션", 36f,
                TextAlignmentOptions.Center, Color.white, levelText.font, levelText.fontSharedMaterial);
            UIBountyUIFactory.Stretch(promotionButtonLabel.rectTransform);
            promotionButtonLabel.textWrappingMode = TextWrappingModes.NoWrap;

            promotionButton.onClick.AddListener(OnClickPromotion);
        }

        private void RefreshPromotionBox(bool owned, int level)
        {
            if (promotionStatusText == null || promotionButton == null)
                return;

            PromotionManager manager = PromotionManager.Instance;
            if (manager == null || !PromotionManager.IsPromotable(currentDiceType))
            {
                promotionStatusText.SetText(string.Empty);
                promotionButton.gameObject.SetActive(false);
                return;
            }

            int maxTier = PromotionRules.MaxTier(PromotionManager.IsBasic(currentDiceType));
            int tier = manager.GetTier(currentDiceType);
            bool queuedHere = manager.TryGetQueue(out PromotionQueueView queue) && queue.DiceType == currentDiceType;

            var text = new StringBuilder();
            text.Append("<b>승급 ").Append(tier).Append(" / ").Append(maxTier).Append("</b>  ");
            for (int i = 1; i <= maxTier; i++)
                text.Append(i <= tier ? "<color=#FFE27A>★</color>" : "<color=#7A7F8C>☆</color>");
            text.Append('\n');

            if (!owned)
            {
                text.Append("<color=#C9D6EE>미보유 다이스는 승급할 수 없어요</color>");
            }
            else if (tier >= maxTier)
            {
                text.Append("<color=#CFFFD0>승급을 모두 마쳤어요</color>");
            }
            else if (queuedHere)
            {
                text.Append("<color=#7FE3FF>승급 진행 중</color>  ")
                    .Append(CountMetMissions(manager, queue.TargetTier)).Append(" / ")
                    .Append(CountRequiredMissions(queue.TargetTier)).Append(" 미션");
            }
            else
            {
                int need = PromotionRules.RequiredDiceLevel(tier + 1);
                string levelNote = level >= need
                    ? "<color=#CFFFD0>등록할 수 있어요</color>"
                    : "<color=#C9D6EE>Lv." + need + " 부터 등록 (현재 Lv." + level + ")</color>";
                text.Append("다음 승급 ").Append(tier + 1).Append("단계  ").Append(levelNote);
            }

            promotionStatusText.SetText(text.ToString());

            promotionButton.gameObject.SetActive(true);
            promotionButton.interactable = owned && tier < maxTier;
            promotionButtonLabel.SetText(queuedHere ? "진행 보기" : "승급 미션");
        }

        private static int CountRequiredMissions(int targetTier)
        {
            int count = 0;
            for (int i = 0; i < PromotionRules.MissionCount; i++)
            {
                if (PromotionRules.IsRequired((PromotionMission)i, targetTier))
                    count++;
            }

            return count;
        }

        private static int CountMetMissions(PromotionManager manager, int targetTier)
        {
            int count = 0;
            for (int i = 0; i < PromotionRules.MissionCount; i++)
            {
                var mission = (PromotionMission)i;
                int need = PromotionRules.Requirement(mission, targetTier);
                if (need > 0 && manager.GetProgress(mission) >= need)
                    count++;
            }

            return count;
        }

        private void OnClickPromotion()
        {
            OpenPromotionPopup();
        }

        /// <summary>
        /// 승급 미션 창. <b>범용 확인 창을 빌려 쓴다</b> — 미션이 여덟 줄 안팎이라 목록 프리팹을 새로 만들 만큼은 아니고,
        /// 확인 버튼이 상황에 따라 "등록" / "이벤트 재화 투입" / "확인" 으로 바뀐다.
        /// </summary>
        private void OpenPromotionPopup()
        {
            PromotionManager manager = PromotionManager.Instance;
            UIConfirmDialog confirm = GameContainer.UI?.Get<UIConfirmDialog>();
            if (manager == null || confirm == null || !PromotionManager.IsPromotable(currentDiceType))
                return;

            System.DateTime now = System.DateTime.UtcNow;
            DiceType target = currentDiceType;
            var meta = DiceMetaDataProvider.GetMeta(target);
            string diceName = meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : target.ToString();

            bool queuedHere = manager.TryGetQueue(out PromotionQueueView queue) && queue.DiceType == target;
            int targetTier = queuedHere ? queue.TargetTier : manager.GetTier(target) + 1;
            int maxTier = PromotionRules.MaxTier(PromotionManager.IsBasic(target));
            if (targetTier > maxTier)
                return;

            var message = new StringBuilder();
            message.Append("<size=32>").Append(diceName).Append("  승급 ").Append(targetTier).Append("단계</size>\n");

            string effect = PromotionText.Describe(target, targetTier);
            message.Append("<size=26><color=#7FE3FF>").Append(effect).Append("</color>\n")
                .Append(PromotionText.CommonLine).Append("</size>\n<size=10> </size>\n");

            string confirmLabel;
            System.Action onConfirm = null;

            if (queuedHere)
            {
                AppendMissionLines(message, manager, targetTier);

                int need = PromotionRules.Requirement(PromotionMission.EventCurrency, targetTier);
                int missing = need - manager.GetProgress(PromotionMission.EventCurrency);
                PointType open = EventCurrencyRules.Resolve(EventCurrencyRules.DayIndex(now));
                int held = PointManager.Instance != null ? PointManager.Instance.Get(open) : 0;

                if (need > 0 && missing > 0)
                {
                    message.Append("<size=24><color=#C9D6EE>열린 이벤트 재화만 넣을 수 있어요 (보유 ").Append(held).Append("개)</color></size>");
                    confirmLabel = held > 0 ? "재화 투입" : "확인";
                    if (held > 0)
                    {
                        onConfirm = () =>
                        {
                            manager.DonateEventCurrency(held, System.DateTime.UtcNow);
                            Refresh();
                            OpenPromotionPopup();
                        };
                    }
                }
                else
                {
                    confirmLabel = "확인";
                }
            }
            else
            {
                int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(target) : 1;
                PromotionRegisterResult result = manager.CanRegister(target, now);

                AppendRequirementLines(message, targetTier);

                switch (result)
                {
                    case PromotionRegisterResult.LevelTooLow:
                        message.Append("<size=26><color=#FFB0B0>Lv.").Append(PromotionRules.RequiredDiceLevel(targetTier))
                            .Append(" 이 되면 등록할 수 있어요 (현재 Lv.").Append(level).Append(")</color></size>");
                        confirmLabel = "확인";
                        break;

                    case PromotionRegisterResult.ReplaceLocked:
                        System.TimeSpan left = manager.ReplaceLockRemaining(now);
                        message.Append("<size=26><color=#FFB0B0>다른 다이스를 승급 중이에요. ")
                            .Append(FormatLock(left)).Append(" 뒤에 바꿀 수 있어요</color></size>");
                        confirmLabel = "확인";
                        break;

                    default:
                        if (manager.TryGetQueue(out PromotionQueueView other))
                        {
                            message.Append("<size=26><color=#FFE27A>바꾸면 ").Append(DiceName(other.DiceType))
                                .Append("의 진행도가 사라져요</color></size>");
                        }
                        else
                        {
                            message.Append("<size=26><color=#CFFFD0>등록한 뒤에 쓴 분만 인정돼요</color></size>");
                        }

                        confirmLabel = "등록";
                        onConfirm = () =>
                        {
                            manager.TryRegister(target, System.DateTime.UtcNow);
                            Refresh();
                            OpenPromotionPopup();
                        };
                        break;
                }
            }

            confirm.transform.SetAsLastSibling();
            confirm.Open("다이스 승급", message.ToString(), confirmLabel, "닫기", onConfirm);
        }

        private static string DiceName(DiceType diceType)
        {
            var meta = DiceMetaDataProvider.GetMeta(diceType);
            return meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : diceType.ToString();
        }

        private static string FormatLock(System.TimeSpan left)
        {
            int hours = (int)left.TotalHours;
            int minutes = left.Minutes;
            return hours > 0 ? hours + "시간 " + minutes + "분" : minutes + "분";
        }

        /// <summary>진행도 줄. 채운 미션은 초록, 아닌 것은 흰색이다.</summary>
        private static void AppendMissionLines(StringBuilder message, PromotionManager manager, int targetTier)
        {
            message.Append("<size=26>");
            for (int i = 0; i < PromotionRules.MissionCount; i++)
            {
                var mission = (PromotionMission)i;
                int need = PromotionRules.Requirement(mission, targetTier);
                if (need <= 0)
                    continue;

                int done = manager.GetProgress(mission);
                bool met = done >= need;
                message.Append(met ? "<color=#CFFFD0>" : "<color=#FFFFFF>")
                    .Append(PromotionText.MissionName(mission)).Append("  ")
                    .Append(done).Append(" / ").Append(need)
                    .Append(met ? "  (완료)" : string.Empty)
                    .Append("</color>\n");
            }

            message.Append("</size>");
        }

        /// <summary>등록 전에 보이는 요구량 줄.</summary>
        private static void AppendRequirementLines(StringBuilder message, int targetTier)
        {
            message.Append("<size=26>");
            for (int i = 0; i < PromotionRules.MissionCount; i++)
            {
                var mission = (PromotionMission)i;
                int need = PromotionRules.Requirement(mission, targetTier);
                if (need > 0)
                    message.Append(PromotionText.MissionName(mission)).Append("  ").Append(need).Append('\n');
            }

            message.Append("</size>");
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
        ///
        /// <b>숫자 뒤에 비율을 괄호로 같이 적는다.</b> 다이스 공격력이 장비 공격력의
        /// 퍼센트가 된 뒤에는 숫자만 보여 주면 그것이 왜 그 값인지 — 장비를 올린 결과인지
        /// 다이스를 올린 결과인지 — 화면에서 알 수 없다. 숫자는 둘이 곱해진 결과이고,
        /// 괄호 안이 이 다이스가 가진 몫이다.
        /// </summary>
        private string BuildDescriptionText(DiceMetaDataDatabase.DiceMeta meta, int level)
        {
            if (meta == null)
                return string.Empty;

            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(meta.description))
                builder.AppendLine(meta.description);

            const int lobbyStar = 1;
            builder.AppendFormat("공격력 {0}  (장비 공격력의 {1:0.#}%)",
                DiceMetaDataProvider.CalculateDamage(currentDiceType, lobbyStar, level),
                DiceMetaDataProvider.GetAttackPercent(currentDiceType, level) * 100f);

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
