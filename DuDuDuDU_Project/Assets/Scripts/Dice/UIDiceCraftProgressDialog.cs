using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using OJ.DI;
using OJ.Hunting;
using OJ.UI;

namespace OJ.Dice
{
    public class UIDiceCraftProgressDialog : DialogBase
    {
        // 8.3b: 전투 매니저로 가는 창구.
        //
        // 이 다이얼로그는 씬에 상주하지 않는다(10.4 에서 프리팹으로 빠졌고 UIService 가 찍는다).
        // 그래서 BattleScope 의 씬 순회로는 닿지 않고, <b>찍는 쪽이 리졸버를 태워야</b> 채워진다.
        [Inject] private IBattleRefs battle;

        // 8.3b: 이 다이얼로그는 진행도 항목을 런타임에 찍는다. 그냥 Instantiate 하면
        // UIDiceCraftProgressItem 의 [Inject] 가 빈 채로 남는다 — 그 파일 주석이 말하는
        // "생성부의 Instantiate 가 채워 준다" 는 자리가 여기다.
        //
        // <b>주입은 Awake 보다 먼저다.</b> VContainer 의 부모 있는 Instantiate 는 프리팹을
        // SetActive(false) 로 껐다 찍고, 주입한 뒤에 켠다(ObjectResolverUnityExtensions.cs:78-91).
        // 그래서 DialogBase.Awake 가 부르는 OnLoad 시점에도 이미 채워져 있다.
        // 그래도 읽기를 뒤로 미뤄 뒀다 — 씬에 놓인 컴포넌트는 반대(자기 Awake 뒤)라
        // 두 규칙을 섞어 기억하는 것이 사고의 원천이고, 늦게 읽어서 손해 볼 것이 없다.
        [Inject] private IObjectResolver resolver;

        [Header("UI")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIDiceCraftProgressItem itemPrefab;
        [SerializeField] private Button openDetailButton;

        [Header("Behavior")]
        [SerializeField] private bool showOnlyInSetting = true;

        private readonly List<UIDiceCraftProgressItem> items = new List<UIDiceCraftProgressItem>();
        private DiceType selectedMythicType = DiceType.KingNormal;
        private bool hasSelection;

        private void Update()
        {
            // 예전 GameManager.Instance == null 검사와 같은 뜻이다. 그 매니저는 전투 씬에만
            // 사는 물건이라 "Instance 가 없다" 는 곧 "전투가 없다" 였고, 창구에서 같은 것을
            // 묻는 이름이 IsActive 다.
            if (!showOnlyInSetting || !battle.IsActive)
                return;

            bool shouldOpen = battle.Game.inGameState == InGameState.Setting;
            if (shouldOpen && !isEnter)
                Enter();
            else if (!shouldOpen && isEnter)
                Exit();
        }

        /// <summary>
        /// <b>여기는 사실상 Awake 다.</b> <c>DialogBase.Awake</c> 가 <c>Load</c> 를 거쳐 부른다.
        ///
        /// 주입은 <c>Instantiate</c> 가 돌아온 뒤에 붙으므로 이 시점의 <c>battle</c>·<c>resolver</c>
        /// 는 아직 null 이다. 그래서 그 둘이 필요한 일 — 항목 찍기와 이벤트 구독 — 은
        /// <see cref="Start"/> 로 내렸다. 버튼 연결만 남는다. 버튼은 인스펙터가 들고 있는
        /// 참조라 주입과 무관하다.
        /// </summary>
        protected override void OnLoad()
        {
            if (openDetailButton != null)
                openDetailButton.onClick.AddListener(OpenDetailDialog);
        }

        /// <summary>
        /// <see cref="OnLoad"/> 에서 내려온 나머지. (8.3b)
        ///
        /// <b>구독이 -= 뒤에 += 인 이유.</b> <c>UIService.Show</c> 는 찍은 다음 줄에서 바로
        /// <c>Enter</c> 를 부르므로 <see cref="OnEnter"/> 가 이 <c>Start</c> 보다 먼저 돌 수 있다.
        /// 예전에는 <c>OnLoad</c>(Awake) 가 항상 먼저라 그냥 <c>+=</c> 여도 한 번이었는데,
        /// 내려온 지금 그대로 두면 두 번 걸려 갱신이 두 번 돈다.
        /// </summary>
        private void Start()
        {
            BuildIfNeeded();
            SubscribeDiceInventoryChanged();
        }

        protected override void OnUnload()
        {
            if (openDetailButton != null)
                openDetailButton.onClick.RemoveListener(OpenDetailDialog);

            // 창구가 비어 있으면 구독을 걸어 둔 상대도 이미 없다. 배틀 스코프가 먼저 치워지고
            // 이 다이얼로그가 나중에 파괴되는 순서가 실제로 생기는데, 예전
            // DiceTypeStarManager.Instance == null 검사가 걸러 주던 것이 바로 그 순간이다.
            if (battle.IsActive)
                battle.DiceStars.OnDiceInventoryChanged -= HandleDiceInventoryChanged;
        }

        protected override void OnEnter()
        {
            BuildIfNeeded();
            SubscribeDiceInventoryChanged();
            RefreshAll();
        }

        private void SubscribeDiceInventoryChanged()
        {
            if (!battle.IsActive)
                return;

            battle.DiceStars.OnDiceInventoryChanged -= HandleDiceInventoryChanged;
            battle.DiceStars.OnDiceInventoryChanged += HandleDiceInventoryChanged;
        }

        private void HandleDiceInventoryChanged()
        {
            if (!isEnter)
                return;

            RefreshAll();
        }

        private void BuildIfNeeded()
        {
            if (itemPrefab == null || listRoot == null || items.Count > 0)
                return;

            List<DiceType> mythics = DiceMetaDataProvider.GetMythicTypes();
            for (int i = 0; i < mythics.Count; i++)
            {
                // 리졸버로 찍어야 항목의 [Inject] 가 그 자리에서 채워진다.
                // 부모(listRoot)를 반드시 넘긴다 — 부모 없는 오버로드는 스코프 아래 만들었다가
                // 떼어내는 분기를 타서 항목이 엉뚱한 씬에 남는다.
                UIDiceCraftProgressItem item = resolver.Instantiate(itemPrefab, listRoot);
                item.Bind(mythics[i], GetRecipeProgressPercent, HandleClickMythic, true);
                items.Add(item);
            }
        }

        private void RefreshAll()
        {
            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();

            SortItemsByPercentDesc();
        }

        private int GetRecipeProgressPercent(DiceType mythicType)
        {
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);

            // 전투가 없으면 보유 별을 물어볼 곳이 없다 — 예전 DiceTypeStarManager.Instance == null
            // 검사가 서 있던 자리다. 레시피 검사는 창구와 무관해서 그대로 둔다.
            if (!battle.IsActive || recipe == null || recipe.Count == 0)
                return 0;

            return battle.DiceStars.GetRecipeProgressPercent(recipe);
        }

        /// <summary>
        /// 카탈로그에서 꺼내 띄운다. (10.4)
        ///
        /// 예전에는 씬 인스턴스를 <c>[SerializeField]</c> 로 직접 가리켰고, 그 참조가 <c>None</c> 이면
        /// <b>아무 로그 없이</b> 창이 안 열렸다. <see cref="UIService"/> 는 못 열면 사유를 로그로 남긴다.
        /// <c>Open</c> 이 선택 상태를 넣고 스스로 <c>Enter</c> 를 부르므로 <c>Show</c> 가 아니라 <c>Get</c> 이다.
        /// </summary>
        private void HandleClickMythic(DiceType mythicType)
        {
            UIDiceCraftPanelDialog detailDialog = GameContainer.UI?.Get<UIDiceCraftPanelDialog>();
            if (detailDialog == null)
                return;

            detailDialog.Open(mythicType);

            return;

            selectedMythicType = mythicType;
            hasSelection = true;
            OpenDetailDialog();
        }

        private void SortItemsByPercentDesc()
        {
            items.Sort((a, b) =>
            {
                int pa = GetRecipeProgressPercent(a.MythicType);
                int pb = GetRecipeProgressPercent(b.MythicType);
                return pb.CompareTo(pa);
            });

            for (int i = 0; i < items.Count; i++)
                items[i].transform.SetSiblingIndex(i);
        }

        private void OpenDetailDialog()
        {
            if (!hasSelection && items.Count > 0)
                selectedMythicType = items[0].MythicType;

            UIDiceCraftPanelDialog detailDialog = GameContainer.UI?.Get<UIDiceCraftPanelDialog>();
            if (detailDialog == null)
                return;

            detailDialog.Open(selectedMythicType);
        }

    }
}
