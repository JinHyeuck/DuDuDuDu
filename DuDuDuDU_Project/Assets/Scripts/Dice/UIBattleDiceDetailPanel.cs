using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Battle;
using OJ.DI;
using OJ.Point;
using OJ.UI;
using OJ.Utils;

namespace OJ.Dice
{
    /// <summary>
    /// 보드의 다이스를 눌렀을 때 뜨는 <b>작은</b> 상세창. (진화 개편)
    ///
    /// <b>예전에는 전체 화면 창이었다.</b> 딤드를 깔고 그 위에 이름·레벨·원소 5칸·
    /// 몬스터 기준 피해·효과 적용 후 피해·마일스톤 4줄을 늘어놓았다. 그 화면이 하던 일은
    /// "이 다이스에 돈을 더 쓸지" 판단을 돕는 것이었는데, 그 판단은 전투 중이 아니라
    /// 로비의 성장 화면(<see cref="UIDiceGrowthDetailPanel"/>)에서 한다. 전투 중에
    /// 필요한 것은 <b>이걸 진화시킬까 교환할까</b> 하나뿐이다.
    ///
    /// 그래서 남긴 것은 다섯 가지다 — 아이콘 · 성급 · 특성 한 줄 · 쿨타임 · 속성.
    /// 피해 계산 · 마일스톤 목록 · 원소 다중 표시는 통째로 걷어냈다.
    ///
    /// <b>딤드도 걷어냈다.</b> 대신 화면 전체를 덮는 <b>투명한</b> 버튼을 깔고, 그것을
    /// 누르면 닫는다. 딤드는 "이 창이 전부다"라고 말하는 장치인데 이 창은 보드 위에
    /// 잠깐 떴다 사라지는 말풍선에 가깝다 — 뒤가 보여야 어느 슬롯을 눌렀는지 안다.
    ///
    /// <b>이 프리팹은 코드로 굽는다.</b> <see cref="Create"/> 가 정본이고
    /// <c>OJ/개발/전투 다이스 상세창 프리팹 굽기</c> 가 그것을 같은 경로에 저장한다.
    /// 경로가 같으므로 GUID 가 유지되고 <c>DialogCatalog</c> 참조가 안 끊긴다.
    /// </summary>
    public class UIBattleDiceDetailPanel : DialogBase
    {
        [Header("Card")]
        [SerializeField] private RectTransform card;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image elementIcon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text starText;
        [SerializeField] private TMP_Text coolTimeText;
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text traitText;

        [Header("Requirement")]
        [SerializeField] private TMP_Text requirementText;

        [Header("Actions")]
        [SerializeField] private Button evolveButton;
        [SerializeField] private TMP_Text evolveCostText;

        [Tooltip("진화하면 무엇이 되는지 보여 주는 아이콘. 진화 버튼 왼쪽에 놓인다.")]
        [SerializeField] private Image evolveTargetIcon;
        [SerializeField] private Button exchangeButton;
        [SerializeField] private TMP_Text exchangeCostText;

        [Header("Dismiss")]
        [SerializeField] private Button outsideButton;

        private DiceType currentDiceType = DiceType.Normal;
        private int currentDiceStar = 1;
        private UIDice currentDice;

        /// <summary>
        /// 배틀 씬 매니저로 가는 창구. (8.3b)
        ///
        /// <b>이 창은 씬에 없다.</b> UIService 가 카탈로그에서 꺼내 런타임에 찍는 프리팹이라,
        /// BattleScope 가 빌드 직후 씬 루트를 훑는 그 순회에는 아직 태어나지도 않았다.
        /// 그래서 주입은 <c>resolver.Instantiate</c> 가 찍는 그 순간에 일어난다.
        ///
        /// <b>그 순간은 <c>Awake</c> 보다 뒤다.</b> 그러므로 <c>Awake</c>(여기서는 DialogBase 의
        /// <c>Awake</c> → <c>Load</c> → <c>OnLoad</c>) 에서는 이 필드를 읽으면 안 된다.
        /// 실제로 읽는 곳은 <c>Open</c> 이 부른 <c>Refresh</c> 와 <c>isEnter</c> 가 선 뒤의
        /// <c>Update</c> 뿐이고, 둘 다 찍기가 끝난 다음이다.
        /// </summary>
        [Inject] private IBattleRefs battle;

        /// <summary>
        /// 진화 대상과 교환 후보를 보유로 거르기 위한 판정.
        /// 이 창은 <c>resolver.Instantiate</c> 로 만들어지므로 주입이 채워진다.
        /// </summary>
        [Inject] private DiceOwnershipManager ownership;

        protected override void OnLoad()
        {
            // 버튼 연결은 인스펙터 참조만 쓰므로 주입과 무관하다 — Awake 여기서 해도 된다.
            if (evolveButton != null)
                evolveButton.onClick.AddListener(OnClickEvolve);
            if (exchangeButton != null)
                exchangeButton.onClick.AddListener(OnClickExchange);
            if (outsideButton != null)
                outsideButton.onClick.AddListener(Exit);
        }

        protected override void OnUnload()
        {
            if (evolveButton != null)
                evolveButton.onClick.RemoveListener(OnClickEvolve);
            if (exchangeButton != null)
                exchangeButton.onClick.RemoveListener(OnClickExchange);
            if (outsideButton != null)
                outsideButton.onClick.RemoveListener(Exit);
        }

        protected override void OnEnter()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged += OnDiceLevelChanged;

            // 재화가 바뀌면 버튼의 켜짐/꺼짐이 바뀐다. 웨이브 클리어 보상이 이 창을
            // 열어 둔 채로 들어올 수 있으므로 구독해서 그 자리에서 갱신한다.
            if (battle.BattlePoints != null)
                battle.BattlePoints.OnBattlePointChanged += OnBattlePointChanged;
        }

        protected override void OnExit()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged -= OnDiceLevelChanged;
            if (battle.BattlePoints != null)
                battle.BattlePoints.OnBattlePointChanged -= OnBattlePointChanged;
        }

        private void Update()
        {
            if (!isEnter)
                return;

            if (currentDice == null || battle.Game == null)
            {
                Exit();
                return;
            }

            InGameState state = battle.Game.inGameState;
            if (state != InGameState.Wave && state != InGameState.Setting)
            {
                Exit();
                return;
            }

            // 보드가 움직이지는 않지만 다이스는 드래그로 슬롯을 옮긴다. 매 프레임
            // 따라붙이는 편이 "옮겼더니 말풍선만 남았다"를 막는 가장 싼 방법이다.
            PlaceCardNearDice();
        }

        /// <summary>지금 보고 있는 다이스. 닫혀 있으면 의미가 없다.</summary>
        public UIDice CurrentDice => currentDice;

        /// <summary>진화 버튼. 튜토리얼이 그 자리만 열어 둔다.</summary>
        public RectTransform EvolveButtonRect => evolveButton != null ? evolveButton.transform as RectTransform : null;

        public void Open(UIDice dice)
        {
            if (dice == null)
                return;

            currentDice = dice;
            currentDiceType = dice.Type;
            currentDiceStar = Mathf.Max(1, dice.Star);

            Enter();

            // Enter 뒤에 찾는다. 창이 꺼져 있는 동안에도 부모는 같지만, 팝업 캔버스는
            // UIService 가 만든 것이라 이 창이 처음 찍힐 때에야 부모로 붙는다.
            diceCanvas = dice.GetComponentInParent<Canvas>();
            if (popupCanvas == null)
                popupCanvas = GetComponentInParent<Canvas>();

            Refresh();
            PlaceCardNearDice();
        }

        public void Refresh()
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(currentDiceType);
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(currentDiceType) : 1;

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(currentDiceType, currentDiceStar);

            RefreshElementIcon(meta);

            if (nameText != null)
            {
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName)
                    ? meta.displayName
                    : currentDiceType.ToString());
            }

            // 성급은 <b>기본 다이스에만</b> 있다. 특수·킹은 showStarUI 가 false 이고
            // 항상 1 성이라, 켜 두면 "x1" 이 늘 붙어 다니며 4성 기본과 헷갈린다.
            if (starText != null)
            {
                bool showStar = DiceMetaDataProvider.ShowStarUI(currentDiceType);
                starText.gameObject.SetActive(showStar);
                if (showStar)
                    starText.SetText("{0}성", currentDiceStar);
            }

            if (coolTimeText != null)
                coolTimeText.SetText(string.Format("쿨타임 {0:0.#}초", GetBattleCooldown(currentDiceType, currentDiceStar)));

            // 보드 위의 <b>실제 성급</b>으로 계산한다. 성급은 데미지 공식의 pip 이라
            // 4성 노말과 1성 노말은 네 배 차이가 난다 — 로비의 1성 기준 표시와 다른
            // 숫자가 나오는 것이 정상이고, 여기서 알고 싶은 것은 "지금 이 놈이 얼마나
            // 때리는가"다.
            //
            // 비율을 괄호로 같이 적는다. 다이스 공격력이 장비 공격력의 퍼센트가 된 뒤에는
            // 숫자 하나만으로는 "이 다이스가 센 것"과 "장비가 센 것"이 구분되지 않는다.
            // 비율은 성급과 무관하다 — 성급은 뒤에서 pip 으로 곱해지므로 로비 표시와 같은 값이다.
            if (damageText != null)
            {
                // string.Format 으로 미리 만들어 SetText(string) 에 넘긴다. TMP 의
                // SetText(string, float, ...) 포맷 파서는 "{1:0.#}" 의 '#' 를 모른다 —
                // 자리 수를 생략하는 표기는 .NET 쪽에만 있다.
                damageText.SetText(string.Format("공격력 {0} ({1:0.#}%)",
                    DiceMetaDataProvider.CalculateDamage(currentDiceType, currentDiceStar, level),
                    DiceMetaDataProvider.GetAttackPercent(currentDiceType, level) * 100f));
            }

            if (traitText != null)
                traitText.SetText(DiceTraitText.Short(currentDiceType, level, battle));

            RefreshActionButtons();
        }

        private void RefreshElementIcon(DiceMetaDataDatabase.DiceMeta meta)
        {
            if (elementIcon == null)
                return;

            // 원소는 이제 다이스마다 하나다(진화 개편). 예전에는 특수 다이스가 두 개를
            // 들고 있어 아이콘 슬롯이 5칸이었다. 배열이 비는 경우는 에셋 사고뿐이라
            // 그때는 칸을 숨긴다 — 흰 네모를 남기면 그것이 원소인 줄 안다.
            bool hasElement = meta != null && meta.elementType != null && meta.elementType.Length > 0;
            elementIcon.gameObject.SetActive(hasElement);
            if (!hasElement)
                return;

            ElementResource elementResource = StaticResource.Instance.GetElementResource(meta.elementType[0]);
            elementIcon.sprite = elementResource != null ? elementResource.Icon : null;
            elementIcon.color = elementResource != null ? elementResource.Color : Color.white;
        }

        /// <summary>
        /// 진화·교환 버튼의 표시와 켜짐. <b>세 가지가 따로 논다</b> — 보임/비침, 비용 문구,
        /// 누를 수 있음. 킹은 최종이라 둘 다 감추고, 관리 단계가 아니면 눌리지 않는다.
        /// </summary>
        private void RefreshActionButtons()
        {
            bool canAct = battle.Game != null && battle.Game.CanManageBoard;
            int owned = battle.BattlePoints != null
                ? battle.BattlePoints.Get(BattlePointType.EnhanceStone)
                : 0;

            bool hasEvolvePath = DiceEvolution.TryGetEvolveTarget(currentDiceType, out DiceType evolveTarget);
            bool starOk = DiceEvolution.CanEvolve(currentDiceType, currentDiceStar);
            int evolveCost = DiceEvolution.GetEvolveCost(currentDiceType);

            // 보유 판정을 CanEvolve 안에 접지 않고 따로 둔다. 접으면 "성급이 모자라다" 와
            // "그 다이스를 아직 못 얻었다" 가 한 값으로 뭉쳐져, 아래 안내가 둘을 구분해
            // 말할 수 없게 된다 — 유저가 다음에 할 일이 완전히 다른 두 상황이다.
            bool targetOwned = !hasEvolvePath || ownership == null || ownership.IsOwned(evolveTarget);

            if (evolveButton != null)
            {
                // <b>감추지 않는다.</b> 감추면 "진화 경로가 있다" 는 사실 자체가 안 보이고,
                // 그러면 잠긴 다이스를 목표로 삼을 수가 없다.
                evolveButton.gameObject.SetActive(hasEvolvePath);
                if (hasEvolvePath)
                {
                    // 진화하면 무엇이 되는지 아이콘으로 보여 준다. 아직 보유하지 않은 다이스는 어둡게 —
                    // "그 다이스를 먼저 얻어야 한다" 는 안내 문구와 같은 뜻을 눈으로 잇는다.
                    if (evolveTargetIcon != null)
                    {
                        evolveTargetIcon.sprite = DiceMetaDataProvider.GetIcon(evolveTarget, 1);
                        evolveTargetIcon.enabled = evolveTargetIcon.sprite != null;
                        evolveTargetIcon.color = targetOwned ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
                    }

                    evolveButton.interactable = canAct && starOk && targetOwned && owned >= evolveCost;
                    if (evolveCostText != null)
                    {
                        evolveCostText.SetText("{0}", evolveCost);
                        evolveCostText.color = owned >= evolveCost ? CostTextColor : LackingColor;
                    }
                }
            }

            // 교환은 후보가 하나도 안 남으면 아예 없는 동작이다 — 킹이 교환 대상이 아닌 것과
            // 같은 뜻이 되므로 버튼도 같이 감춘다. MergeSystem 도 같은 판정으로 막는다.
            bool canExchange = DiceEvolution.CanExchange(currentDiceType)
                               && DiceEvolution.TryGetExchangeCandidates(
                                      currentDiceType, exchangeBuffer,
                                      ownership != null ? (System.Func<DiceType, bool>)ownership.IsOwned : null);
            int exchangeCost = DiceEvolution.GetExchangeCost(currentDiceType);

            if (exchangeButton != null)
            {
                exchangeButton.gameObject.SetActive(canExchange);
                if (canExchange)
                {
                    exchangeButton.interactable = canAct && owned >= exchangeCost;
                    if (exchangeCostText != null)
                    {
                        exchangeCostText.SetText("{0}", exchangeCost);
                        exchangeCostText.color = owned >= exchangeCost ? CostTextColor : LackingColor;
                    }
                }
            }

            RefreshRequirementText(hasEvolvePath, starOk, targetOwned, evolveTarget,
                canAct, owned, evolveCost, canExchange, exchangeCost);
        }

        /// <summary>교환 후보를 담는 재사용 버퍼. 이 창은 하나뿐이라 재진입이 없다.</summary>
        private readonly List<DiceType> exchangeBuffer = new List<DiceType>(5);

        /// <summary>
        /// 버튼이 왜 안 눌리는지 <b>한 줄로 말한다.</b>
        ///
        /// <b>왜 딤드만으로는 부족한가.</b> 회색 버튼은 "지금은 안 된다"까지만 말하고
        /// <b>무엇을 하면 되는지</b>는 말하지 않는다. 진화의 조건은 성급인데 그 규칙
        /// (★4 부터)은 화면 어디에도 적혀 있지 않아서, 유저가 4성을 만들어 보기 전에는
        /// 알 방법이 없다. 조합식을 걷어낸 이유가 "허들을 낮추는 것"이었으니 그 자리에
        /// 새 허들을 말없이 세우면 같은 실수를 반복하는 셈이다.
        ///
        /// 이유는 <b>하나만</b> 보여 준다. 여러 개를 나열하면 카드가 넘치고, 유저가 다음에
        /// 할 일은 어차피 하나다. 순서는 "고치기 어려운 것부터" — 웨이브는 기다리면 끝나고,
        /// 성급은 머지가 필요하며, 재화는 그중 가장 오래 걸린다.
        /// </summary>
        /// <summary>
        /// 인게임과 같은 이름. 위 <c>Refresh</c> 가 쓰는 식과 같은 것이라 여기 모아 둔다.
        /// </summary>
        private static string NameOf(DiceType diceType)
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(diceType);
            return meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : diceType.ToString();
        }

        private void RefreshRequirementText(
            bool hasEvolvePath, bool starOk, bool targetOwned, DiceType evolveTarget, bool canAct,
            int owned, int evolveCost, bool canExchange, int exchangeCost)
        {
            if (requirementText == null)
                return;

            // 킹은 최종이라 버튼이 둘 다 없다. 그때는 안내가 아니라 상태를 말한다.
            if (!hasEvolvePath && !canExchange)
            {
                ShowRequirement("최종 단계", MutedTextColor);
                return;
            }

            if (!canAct)
            {
                ShowRequirement("웨이브 중에는 바꿀 수 없습니다", MutedTextColor);
                return;
            }

            // <b>미보유가 성급보다 앞이다.</b> 이것은 다른 조건을 전부 무의미하게 만드는
            // 조건이라, 뒤에 두면 유저가 4성을 만들고 마석까지 모은 <b>뒤에야</b>
            // "사실 못 쓴다" 를 알게 된다.
            if (hasEvolvePath && !targetOwned)
            {
                ShowRequirement(NameOf(evolveTarget) + " 을(를) 아직 보유하지 않았습니다 (로비 → 다이스 성장)",
                    LackingColor);
                return;
            }

            if (hasEvolvePath && !starOk)
            {
                // ★ 는 BM HANNA 에서 빈칸으로 찍힌다(Tools/ui/PORTING.md) — 성급은 이 창의 다른 곳처럼 "n성" 으로 적는다.
                ShowRequirement(string.Format("{0}성부터 진화할 수 있습니다 (현재 {1}성)",
                    DiceEvolution.EvolveRequiredStar, currentDiceStar), WarnColor);
                return;
            }

            // 진화가 막혔는지 교환이 막혔는지에 따라 필요한 액수가 다르다.
            // 더 싼 쪽(교환)조차 못 내면 그 숫자를 말하는 편이 도움이 된다.
            int cheapest = canExchange ? exchangeCost : evolveCost;
            if (owned < cheapest)
            {
                ShowRequirement(string.Format("마석이 {0}개 더 필요합니다", cheapest - owned), LackingColor);
                return;
            }

            if (hasEvolvePath && owned < evolveCost)
            {
                ShowRequirement(string.Format("진화하려면 마석 {0}개가 더 필요합니다", evolveCost - owned), LackingColor);
                return;
            }

            requirementText.gameObject.SetActive(false);
        }

        private void ShowRequirement(string message, Color color)
        {
            requirementText.gameObject.SetActive(true);
            requirementText.SetText(message);
            requirementText.color = color;
        }

        private void OnClickEvolve()
        {
            if (currentDice == null || !battle.Merge.TryEvolve(currentDice))
                return;

            // 같은 오브젝트가 타입만 바뀌어 남는다(MergeSystem.ReplaceInPlace). 그래서
            // 창을 닫지 않고 새 다이스 기준으로 다시 그린다 — 진화 직후 무엇이 됐는지
            // 보는 것이 이 창의 목적이고, 특수 → 킹 진화를 이어서 누를 수도 있다.
            currentDiceType = currentDice.Type;
            currentDiceStar = Mathf.Max(1, currentDice.Star);
            Refresh();
        }

        private void OnClickExchange()
        {
            if (currentDice == null || !battle.Merge.TryExchange(currentDice))
                return;

            currentDiceType = currentDice.Type;
            currentDiceStar = Mathf.Max(1, currentDice.Star);
            Refresh();
        }

        private void OnDiceLevelChanged(DiceType diceType, int level)
        {
            if (diceType == currentDiceType)
                Refresh();
        }

        private void OnBattlePointChanged(BattlePointType battlePointType, int value)
        {
            RefreshActionButtons();
        }

        /// <summary>
        /// 카드를 누른 다이스 <b>바로 위</b>에 놓는다.
        ///
        /// <b>캔버스를 두 개 건넌다. 둘의 렌더 모드가 다르다.</b>
        /// <list type="bullet">
        /// <item>다이스가 사는 배틀 캔버스는 <c>ScreenSpaceCamera</c> 다. 그래서
        ///       <c>transform.position</c> 이 <b>월드 좌표</b>이고, 화면 픽셀로 바꾸려면
        ///       그 캔버스의 카메라를 넘겨야 한다.</item>
        /// <item>이 창이 사는 팝업 캔버스는 <c>UIService</c> 가 만든 <c>ScreenSpaceOverlay</c> 다.
        ///       역변환에는 카메라가 <c>null</c> 이어야 한다.</item>
        /// </list>
        ///
        /// <b>처음에는 양쪽 다 null 을 넘겼고, 그것이 버그였다.</b> 카메라 없는
        /// <c>WorldToScreenPoint</c> 는 입력의 x·y 를 그대로 돌려주므로, 월드 좌표
        /// (2.5, -1.3) 이 화면 픽셀 (2.5, -1.3) 로 읽혀 카드가 늘 좌하단 구석에 박혔다.
        /// 두 캔버스의 렌더 모드가 우연히 같아지면 조용히 나아 보였다가, 씬을
        /// 손보는 순간 다시 어긋나는 종류다 — 그래서 모드를 <b>물어서</b> 정한다.
        ///
        /// 화면 밖으로 나가지 않게 가둔다. 맨 윗줄 다이스를 누르면 카드가 화면 위로
        /// 넘어가는데, 그러면 <b>버튼이 손가락 밖</b>이 된다. 그때는 아래로 뒤집는다.
        /// </summary>
        private void PlaceCardNearDice()
        {
            if (card == null || currentDice == null)
                return;

            RectTransform parentRect = card.parent as RectTransform;
            if (parentRect == null)
                return;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                EventCameraOf(diceCanvas), currentDice.transform.position);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPoint, EventCameraOf(popupCanvas), out Vector2 local))
            {
                return;
            }

            Rect parentBounds = parentRect.rect;
            Vector2 half = card.rect.size * 0.5f;

            // 다이스 위쪽에 띄우고, 위가 모자라면 아래로 내린다.
            float above = local.y + half.y + CardGapFromDice;
            float below = local.y - half.y - CardGapFromDice;
            float y = above + half.y <= parentBounds.yMax ? above : below;

            float x = Mathf.Clamp(local.x, parentBounds.xMin + half.x, parentBounds.xMax - half.x);
            y = Mathf.Clamp(y, parentBounds.yMin + half.y, parentBounds.yMax - half.y);

            card.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>
        /// 좌표 변환에 넘길 카메라. <c>ScreenSpaceOverlay</c> 는 <b>반드시 null</b> 이어야
        /// 한다 — 그 모드에서는 캔버스에 카메라가 붙어 있어도 좌표계가 화면 픽셀이라,
        /// 카메라를 넘기면 한 번 더 투영되어 값이 어긋난다.
        /// </summary>
        private static Camera EventCameraOf(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }

        private const float CardGapFromDice = 90f;

        // 캔버스는 다이스마다 바뀌지 않는다. Update 가 매 프레임 부르는 자리라
        // GetComponentInParent 를 거기서 돌리지 않도록 Open 에서 한 번만 찾아 둔다.
        private Canvas diceCanvas;
        private Canvas popupCanvas;

        /// <summary>
        /// 화면에 뜨는 쿨타임. 발사 딜레이(<c>fireRate</c>)가 더해진 <b>체감값</b>이다.
        /// </summary>
        private float GetBattleCooldown(DiceType diceType, int star)
        {
            // 아래 null 검사는 원래 있던 것을 그대로 둔다. 창구를 막는 방어가 아니라,
            // 전투가 끝나 스코프가 비워진 프레임에 Refresh 가 한 번 더 불릴 때
            // 발사 딜레이를 0 으로 치던 기존 동작이다 — 지우면 동작이 바뀐다.
            float shotDelay = battle.Player != null ? Mathf.Max(0f, battle.Player.fireRate) : 0f;
            return shotDelay + DiceMetaDataProvider.GetCooldown(diceType, star);
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 <b>에디터 굽기 전용</b>이다. 런타임에 부르지 않는다.
        //
        // 손으로 프리팹을 짜지 않는 이유는 UIConfirmDialog 와 같다 — 위치·색·크기를
        // 인스펙터에 옮겨 적으면 오차가 생기고, 값을 아는 것은 코드다.
        // <c>UIBattleDiceDetailPanelBaker</c> 가 이것을 불러 같은 경로에 저장한다.
        // ──────────────────────────────────────────────────────────────

        // 시안 Art/Layout/Ingame_NewLayour.png · Art/0PSD/인게임관련.psd (2026-10-27).
        // 좌표는 카드 판(보이는 x 271~937, y 1297~1720)의 가운데 기준, 위가 +.
        // 크기는 PSD 레이어의 <b>불투명한 픽셀</b>로 잰 것이다 — 주석의 622x212 · 229x172 는 아래로 번진
        // 반투명 그림자까지 든 bbox 라, 그대로 쓰면 버튼이 판 밖으로 70px 내려온다.
        //   판        Merge_Bg x2        보이는 666x423
        //   정보 상자 Merge_Btn_Bg 575d7e 보이는 622x148
        //   진화/교환 Merge_Btn_Bg ffba00 / 04bafe 보이는 229x102 ("머지 때 쓰던 비율 그대로" — 보드 칸과 같은 x2.857)
        //   글자 30pt(버튼 이름 28pt), 성급·비용 78fe04, 공격력 ffea00

        private const string FrameSpritePath = "Art/Ingame/Merge_Bg";
        private const string BoxSpritePath = "Art/Ingame/Merge_Btn_Bg";
        private const string StoneSpritePath = "Art/Gem/LevelStone";

        private static readonly Color InfoBoxColor = Hex(0x575d7e);
        private static readonly Color EvolveColor = Hex(0xffba00);
        private static readonly Color ExchangeColor = Hex(0x04bafe);
        private static readonly Color CostTextColor = Hex(0x78fe04);
        private static readonly Color DamageTextColor = Hex(0xffea00);

        /// <summary>안내 줄의 담담한 색(최종 단계 · 웨이브 중).</summary>
        private static readonly Color MutedTextColor = new Color(0.78f, 0.84f, 0.94f, 1f);

        // 안내 줄의 두 색. 성급이 모자란 것은 <b>지금 못 한다</b>가 아니라 <b>더 키우면
        // 된다</b>이므로 경고색(노랑)이고, 재화 부족은 붉게 둔다 — 비용 숫자가 붉어지는
        // 것과 같은 색이라 눈이 두 곳을 잇는다.
        private static readonly Color WarnColor = new Color(1f, 0.80f, 0.35f, 1f);
        private static readonly Color LackingColor = new Color(1f, 0.45f, 0.42f, 1f);

        /// <summary>카드 판의 크기. PlaceCardNearDice 가 이 크기로 화면 안에 가둔다.</summary>
        private const float CardWidth = 666f;
        private const float CardHeight = 423f;

        /// <summary>보드 칸과 같은 Merge_Btn_Bg 배율(보이는 42 → 120). 여백 11px 이라 그림은 보이는 것보다 +63.</summary>
        private const float BoxScale = 120f / 42f;
        private const float BoxMargin = 11f * 2f * BoxScale;

        private const float ButtonWidth = 229f;
        private const float ButtonHeight = 102f;

        /// <summary>에디터 굽기 전용. <c>UIBattleDiceDetailPanelBaker</c> 만 부른다.</summary>
        public static UIBattleDiceDetailPanel Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = CreateRect("UIBattleDiceDetailPanel", parent);
            Stretch(root.GetComponent<RectTransform>());

            GameObject view = CreateRect("DialogView", root.transform);
            Stretch(view.GetComponent<RectTransform>());

            var panel = root.AddComponent<UIBattleDiceDetailPanel>();
            panel.dialogView = view;
            panel.UseBackBtn = true;

            // 화면 전체를 덮는 <b>투명한</b> 버튼. 딤드를 대신한다.
            // alpha 0 인 Image 는 그려지지 않지만 레이캐스트는 받는다 — 그래서
            // "뒤가 다 보이는데 바깥을 누르면 닫힌다"가 성립한다. Image 를 아예 빼면
            // 레이캐스트 대상이 없어져 클릭이 뒤로 통과한다.
            Image blocker = CreateImage("OutsideCatcher", view.transform, new Color(0f, 0f, 0f, 0f));
            Stretch(blocker.rectTransform);
            panel.outsideButton = blocker.gameObject.AddComponent<Button>();
            panel.outsideButton.targetGraphic = blocker;
            panel.outsideButton.transition = Selectable.Transition.None;

            // 카드와 버튼을 한 덩어리로 옮기기 위해 묶는다. PlaceCardNearDice 는
            // 이 RectTransform 하나만 움직인다.
            //
            // <b>여기 붙은 투명 Image 는 지우면 안 된다.</b> 위의 OutsideCatcher 가 화면
            // 전체를 먹고 있어서, 카드 영역에도 레이캐스트를 받는 것이 없으면 카드를 누른
            // 손가락이 그대로 "바깥"으로 판정돼 창이 닫힌다. alpha 0 이라 그려지지는 않고
            // 클릭만 막는다 — 그것이 이 Image 의 유일한 일이다.
            Image cardBlocker = CreateImage("Card", view.transform, new Color(0f, 0f, 0f, 0f));
            panel.card = cardBlocker.rectTransform;
            SetRect(panel.card, new Vector2(CardWidth, CardHeight), Vector2.zero);
            Transform c = panel.card;

            // 판 — Merge_Bg x2, 보이는 (271,1297)~(937,1720). 그림 여백 7px x2 → +28.
            Image frame = Sliced("Frame", c, FrameSpritePath, 2f, Color.white);
            SetRect(frame.rectTransform, new Vector2(666f + 28f, 423f + 28f), L(604f, 1508.5f));
            frame.raycastTarget = false;

            // 다이스 아이콘 — 보이는 102(그림 64 안에 45 → x2.27).
            panel.iconImage = CreateImage("Icon", c, Color.white);
            SetRect(panel.iconImage.rectTransform, new Vector2(145f, 145f), L(340f, 1366f));
            panel.iconImage.preserveAspect = true;
            panel.iconImage.raycastTarget = false;

            // 속성 아이콘은 시안에 없다. 다이스 아이콘 오른쪽 아래에 작은 배지로만 남긴다 — 빼면
            // 전투 중에 속성을 볼 곳이 없다.
            panel.elementIcon = CreateImage("Element", c, Color.white);
            SetRect(panel.elementIcon.rectTransform, new Vector2(44f, 44f), L(380f, 1404f));
            panel.elementIcon.preserveAspect = true;
            panel.elementIcon.raycastTarget = false;

            // 이름 · 성급 — 한 줄(가운데 y 1343). 이름 왼쪽 끝 402, 성급 오른쪽 끝 904.
            panel.nameText = CreateText("Name", c, "Dice", 30f, TextAlignmentOptions.Left, Color.white, font);
            SetRect(panel.nameText.rectTransform, new Vector2(340f, 44f), L(402f + 170f, 1343f));

            panel.starText = CreateText("Star", c, "1성", 30f, TextAlignmentOptions.Right, CostTextColor, font);
            SetRect(panel.starText.rectTransform, new Vector2(120f, 44f), L(904f - 60f, 1346f));

            // 특성 · 쿨타임 — 한 줄(가운데 y 1393). 특성 왼쪽 끝 405, 쿨타임 오른쪽 끝 914.
            panel.traitText = CreateFittedText("Trait", c, "특성", 30f, 20f, TextAlignmentOptions.Left, Color.white, font);
            SetRect(panel.traitText.rectTransform, new Vector2(360f, 44f), L(405f + 180f, 1393f));

            panel.coolTimeText = CreateText("CoolTime", c, "쿨타임 0초", 30f, TextAlignmentOptions.Right, Color.white, font);
            SetRect(panel.coolTimeText.rectTransform, new Vector2(170f, 44f), L(914f - 85f, 1394f));

            // 정보 상자 — 575d7e, 보이는 (291,1425)~(913,1573). 공격력 한 줄 + 안내 한 줄.
            Image infoBox = Sliced("InfoBox", c, BoxSpritePath, BoxScale, InfoBoxColor);
            SetRect(infoBox.rectTransform, new Vector2(622f + BoxMargin, 148f + BoxMargin), L(602f, 1499f));
            infoBox.raycastTarget = false;

            panel.damageText = CreateText("Damage", c, "공격력 0", 30f, TextAlignmentOptions.Left, DamageTextColor, font);
            SetRect(panel.damageText.rectTransform, new Vector2(590f, 40f), L(307f + 295f, 1454f));

            // 안내 줄. 문장이 길어질 수 있어("4성부터 진화할 수 있습니다 (현재 1성)") 줄이지 않고 두 줄까지 접는다.
            // 안내가 필요 없을 때는 이 줄만 꺼진다. <b>카드 크기는 그대로 둔다</b> — 이 카드는 매 프레임
            // 다이스를 따라다니므로, 높이가 오르내리면 위/아래 뒤집기 판정이 같이 흔들려 화면에서 떨린다.
            panel.requirementText = CreateText("Requirement", c, "", 28f, TextAlignmentOptions.TopLeft, WarnColor, font);
            SetRect(panel.requirementText.rectTransform, new Vector2(590f, 64f), L(306f + 295f, 1512f));
            panel.requirementText.textWrappingMode = TextWrappingModes.Normal;

            // 진화 — ffba00, 보이는 (358,1585)~(587,1687). 왼쪽에 진화하면 되는 다이스, 오른쪽에 "진화" + 마석 비용.
            // 정보 상자 아래 끝(1573)과 판 아래 끝(1720) 사이에 든다.
            panel.evolveButton = CreateActionButton("EvolveButton", c, "진화", EvolveColor, font,
                L(472.5f, 1636f), new Vector2(56.5f, 20.5f), new Vector2(28.5f, -20.5f), 59.5f, out panel.evolveCostText);

            panel.evolveTargetIcon = CreateImage("TargetIcon", panel.evolveButton.transform, Color.white);
            SetRect(panel.evolveTargetIcon.rectTransform, new Vector2(117f, 117f), new Vector2(-67.5f, 1f));
            panel.evolveTargetIcon.preserveAspect = true;
            panel.evolveTargetIcon.raycastTarget = false;

            // 교환 — 04bafe, 보이는 (628,1587)~(857,1689).
            panel.exchangeButton = CreateActionButton("ExchangeButton", c, "교환", ExchangeColor, font,
                L(742.5f, 1638f), new Vector2(1f, 22.5f), new Vector2(-20.5f, -20.5f), 12.5f, out panel.exchangeCostText);

            return panel;
        }

        /// <summary>시안 화면 좌표 → 카드 판(가운데 (604, 1508.5)) 안의 anchoredPosition.</summary>
        private static Vector2 L(float x, float y)
        {
            return new Vector2(x - 604f, 1508.5f - y);
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>9슬라이스 그림. 테두리는 스프라이트 .meta 의 것, 여기서는 배율만 준다. 그림이 없으면 멈춘다.</summary>
        private static Image Sliced(string name, Transform parent, string path, float pixelScale, Color color)
        {
            Image image = CreateImage(name, parent, color);
            image.sprite = LoadSprite(path);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / pixelScale;
            return image;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
                throw new System.InvalidOperationException("[다이스 상세 굽기] 스프라이트가 없다: Resources/" + path);
            return sprite;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(
            string name, Transform parent, string text, float size,
            TextAlignmentOptions align, Color color, TMP_FontAsset font)
        {
            TextMeshProUGUI label = CreateRect(name, parent).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.font = font;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 이름 위 · 마석 아이콘 + 비용 아래로 얹은 버튼(시안). 비용은 <b>따로 두</b>어야 갱신할 때
        /// 문자열을 다시 조립하지 않는다. 좌표는 버튼 가운데 기준 — 이름 가운데, 마석 가운데, 비용 글자 왼쪽 끝.
        /// </summary>
        private static Button CreateActionButton(
            string name, Transform parent, string text, Color color, TMP_FontAsset font,
            Vector2 position, Vector2 labelCenter, Vector2 stoneCenter, float costLeft, out TMP_Text costText)
        {
            Image background = Sliced(name, parent, BoxSpritePath, BoxScale, color);
            SetRect(background.rectTransform, new Vector2(ButtonWidth + BoxMargin, ButtonHeight + BoxMargin), position);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            TMP_Text label = CreateText("Label", background.transform, text, 28f,
                TextAlignmentOptions.Center, Color.white, font);
            SetRect(label.rectTransform, new Vector2(120f, 40f), labelCenter);

            // 마석 — 보이는 30x35(그림 64 안에 23x27 → x1.3).
            Image stone = CreateImage("Stone", background.transform, Color.white);
            stone.sprite = LoadSprite(StoneSpritePath);
            stone.preserveAspect = true;
            stone.raycastTarget = false;
            SetRect(stone.rectTransform, new Vector2(83f, 83f), stoneCenter);

            costText = CreateText("Cost", background.transform, "0", 30f,
                TextAlignmentOptions.Left, CostTextColor, font);
            SetRect(costText.rectTransform, new Vector2(100f, 40f), new Vector2(costLeft + 50f, stoneCenter.y));

            return button;
        }

        private static TMP_Text CreateFittedText(
            string name, Transform parent, string text, float maxSize, float minSize,
            TextAlignmentOptions align, Color color, TMP_FontAsset font)
        {
            TMP_Text label = CreateText(name, parent, text, maxSize, align, color, font);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = maxSize;
            label.fontSizeMin = minSize;
            label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
