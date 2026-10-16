using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Bounty;
using OJ.DI;
using OJ.Hunting;
using OJ.UI;

namespace OJ.Dice
{
    /// <summary>
    /// 조합식 카드 상점 — 카드 3장 · [조합식] · 리롤 · 접기. <b>카드 모드에서만 뜬다.</b>
    ///
    /// <b>여기에 규칙을 두지 않는다.</b> 상태는 <c>GameManager.Shop</c>, 구매·리롤은
    /// <c>GameManager.TryBuyShopCard / TryRerollShop</c> 이 한다. 이 창은 그 상태를 그리고
    /// 눌림을 넘길 뿐이다 — 규칙이 두 곳에 있으면 한쪽만 고치는 사고가 난다.
    ///
    /// <b>왜 다이얼로그인가.</b> 씬 <c>.unity</c> 를 손으로 편집하지 않는 규칙(AGENTS 절대 규칙 3)이라
    /// <c>UIBountyBanner</c>·<c>UIWaveRewindButton</c> 과 같은 방식으로 프리팹을 구워 카탈로그에 올린다.
    ///
    /// <b>루트를 화면 전체로 늘리지 않는다.</b> 늘리면 그 위의 <c>GraphicRaycaster</c> 가 보드 클릭을 먹는다.
    /// 칸 상태(살 수 있나·재화가 되나)는 <b>매 프레임 다시 그린다</b> — 보드 위의 다이스가 옮겨지거나
    /// SP 가 들어오는 것을 일일이 구독하는 것보다 싸고 빠뜨릴 곳이 없다(테스트 모드 전용 창이다).
    /// </summary>
    public sealed class UICardShopPanel : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private UIShopCardSlot[] slots;
        [SerializeField] private Button recipeButton;
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMP_Text rerollLabel;
        [SerializeField] private Button collapseButton;
        [SerializeField] private TMP_Text collapseLabel;
        [SerializeField] private GameObject cardRow;

        private CardShop subscribed;
        private bool collapsed;

        protected override void OnLoad()
        {
            recipeButton.onClick.AddListener(OnRecipe);
            rerollButton.onClick.AddListener(OnReroll);
            collapseButton.onClick.AddListener(OnCollapse);

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].SetIndex(i);
                slots[i].BuyClicked += OnBuy;
                slots[i].LockClicked += OnLock;
            }
        }

        protected override void OnEnter()
        {
            Subscribe();
            Refresh();
        }

        protected override void OnExit()
        {
            Unsubscribe();
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        private void Update()
        {
            if (_isEnter)
                Refresh();
        }

        private void Subscribe()
        {
            CardShop shop = battle != null && battle.Game != null ? battle.Game.Shop : null;
            if (shop == null || subscribed == shop)
                return;

            Unsubscribe();
            shop.Changed += Refresh;
            subscribed = shop;
        }

        private void Unsubscribe()
        {
            if (subscribed == null)
                return;

            subscribed.Changed -= Refresh;
            subscribed = null;
        }

        private void Refresh()
        {
            GameManager game = battle != null ? battle.Game : null;
            if (game == null || slots == null)
                return;

            CardShop shop = game.Shop;
            UIBoard board = battle.Board;

            for (int i = 0; i < slots.Length && i < shop.Count; i++)
            {
                ShopCard card = shop[i];
                bool canBuy = !card.IsEmpty && HasMaterials(card, board);
                bool affordable = !card.IsEmpty && CanAfford(card);
                slots[i].Bind(card, canBuy, affordable);
            }

            int sp = battle.BattlePoints != null ? battle.BattlePoints.Get(OJ.BattlePointType.SummonPoint) : 0;
            rerollLabel.text = "리롤 " + DiceRecipes.RerollCostSp + "SP";
            rerollButton.interactable = sp >= DiceRecipes.RerollCostSp;
            collapseLabel.text = collapsed ? "펼치기" : "접기";
            cardRow.SetActive(!collapsed);
        }

        private static bool HasMaterials(ShopCard card, UIBoard board)
        {
            if (board == null)
                return false;

            switch (card.Kind)
            {
                case ShopCardKind.GrantDice:
                    return board.HasEmptyUnlockedSlot();
                case ShopCardKind.MergeDice:
                    return board.CountDice(card.MainType, card.MainStar) >= 2;
                default:
                    return board.CountDice(card.MainType, card.MainStar) >= 1
                        && board.CountDice(card.SubType, card.SubStar) >= 1;
            }
        }

        private bool CanAfford(ShopCard card)
        {
            if (battle.BattlePoints == null)
                return false;

            return battle.BattlePoints.Get(OJ.BattlePointType.SummonPoint) >= card.Cost;
        }

        private void OnBuy(int index)
        {
            battle.Game.TryBuyShopCard(index);
        }

        private void OnLock(int index)
        {
            battle.Game.Shop.ToggleLock(index);
        }

        private void OnReroll()
        {
            battle.Game.TryRerollShop();
        }

        private void OnRecipe()
        {
            GameContainer.UI?.Show<UIRecipeBookDialog>();
        }

        private void OnCollapse()
        {
            collapsed = !collapsed;
            Refresh();
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────
        //
        // 조립 헬퍼는 UIBountyUIFactory 를 쓴다(같은 이유 — UIWaveRewindButton 주석).

        /// <summary>패널 크기. 카드 셋(296x232 + 틈 20)과 위의 버튼 줄이 들어간다.</summary>
        private static readonly Vector2 PanelSize = new Vector2(1040f, 520f);

        /// <summary>
        /// 화면 세로 중심에서의 위치. 보드(중심 -551, 칸이 -368 아래)의 바로 위다 —
        /// <b>다이스 칸을 가리지 않는 것이 이 값의 조건이다.</b>
        /// </summary>
        internal const float CenterY = -20f;

        private static readonly Color TrayColor = new Color(0x63 / 255f, 0x64 / 255f, 0x81 / 255f, 0.94f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UICardShopPanel Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UICardShopPanel", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            UIBountyUIFactory.SetRect(rootRect, PanelSize, new Vector2(0f, CenterY));

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var panel = root.AddComponent<UICardShopPanel>();
            panel.dialogView = view;

            // 전투 중 백키는 일시정지 확인이라 이 창이 가로채지 않는다.
            panel.UseBackBtn = false;

            // 열림 연출은 두지 않는다. 웨이브마다 다시 뜨는 HUD 라 매번 팝이 되면 거슬린다.
            panel.BakeOpenStyle(DialogOpenStyle.None);

            // 받침 — 현상금 선택 창의 카드 받침과 같은 상자(636481).
            Image tray = UIBountyUIFactory.CreateSprite("Tray", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 4f,
                new Vector2(PanelSize.x, 424f), new Vector2(0f, -48f), true);
            tray.color = TrayColor;
            tray.raycastTarget = true;

            // 버튼 줄. 킷 버튼(32²)은 여백이 사방 4px 이라 x3 로 12px 씩 키운다.
            panel.recipeButton = CreateKitButton("RecipeButton", view.transform, "Ingame/Big_Btn_Yellow",
                new Vector2(190f, 56f), new Vector2(-400f, 204f), "조합식", font, out _);
            panel.rerollButton = CreateKitButton("RerollButton", view.transform, "Ingame/Big_Btn_Green",
                new Vector2(210f, 56f), new Vector2(400f, 204f), "리롤", font, out panel.rerollLabel);
            panel.collapseButton = CreateKitButton("CollapseButton", view.transform, "Ingame/Btn_Gray",
                new Vector2(150f, 56f), new Vector2(0f, 204f), "접기", font, out panel.collapseLabel);

            GameObject row = UIBountyUIFactory.CreateRect("CardRow", view.transform);
            UIBountyUIFactory.Stretch(row.GetComponent<RectTransform>());
            panel.cardRow = row;

            panel.slots = new UIShopCardSlot[DiceRecipes.CardCount];
            for (int i = 0; i < panel.slots.Length; i++)
            {
                float x = (i - 1) * (UIShopCardSlot.VisibleSize.x + 20f);
                panel.slots[i] = UIShopCardSlot.Create(row.transform, i, new Vector2(x, -48f), font, plainMaterial);
            }

            return panel;
        }

        /// <summary>킷 버튼 + 글자. 돌려주는 것은 Button, 글자는 <paramref name="label"/> 로 나온다.</summary>
        internal static Button CreateKitButton(
            string name, Transform parent, string spritePath, Vector2 visibleSize, Vector2 position,
            string text, TMP_FontAsset font, out TMP_Text label)
        {
            Image image = UIBountyUIFactory.CreateSprite(name, parent,
                UIBountyUIFactory.LoadSprite(spritePath), 3f,
                visibleSize + new Vector2(24f, 24f), position, true);
            image.raycastTarget = true;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            label = UIBountyUIFactory.CreateText("Label", image.transform, text, 28f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(label.rectTransform, visibleSize, new Vector2(0f, 3f));
            return button;
        }
    }
}
