using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Bounty;
using OJ.DI;
using OJ.UI;

namespace OJ.Dice
{
    /// <summary>
    /// 신화 소환 목록 — 신화 카드를 누르면 뜬다. 신화 다섯 종을 <b>소환할 수 있는 것 → 재료가 모자란 것 →
    /// 보유하지 않은 것</b> 순으로 늘어놓는다(<see cref="MythicList"/>).
    ///
    /// <list type="bullet">
    /// <item>소환할 수 있음: 밝게, 재료 둘에 "보유". 누르면 SP 를 내고 목록이 닫히며 소환 연출이 돈다.</item>
    /// <item>보유했지만 재료 부족: 딤드. 모자란 재료의 아이콘과 "필요" 를 적는다. 둘 다 모자라면 두 줄.</item>
    /// <item>보유하지 않음: 딤드 + "구매필요".</item>
    /// </list>
    /// 바깥이나 닫기를 누르면 그냥 닫힌다 — 아무것도 치르지 않는다.
    /// </summary>
    public sealed class UIMythicListDialog : DialogBase
    {
        [Serializable]
        private sealed class RowView
        {
            public Button button;
            public Image background;
            public CanvasGroup group;
            public Image kingIcon;
            public TMP_Text nameText;
            public Image line1Icon;
            public TMP_Text line1Text;
            public Image line2Icon;
            public TMP_Text line2Text;
            public TMP_Text stateText;
        }

        [Inject] private IBattleRefs battle;
        [Inject] private DiceOwnershipManager ownership;

        [SerializeField] private RowView[] rows;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button outsideButton;
        [SerializeField] private TMP_Text costText;

        private int cardIndex;
        private readonly List<MythicOption> options = new List<MythicOption>(5);

        private static readonly Color DimAlphaColor = Color.white;
        private static readonly Color StateGoodColor = new Color(1f, 0.86f, 0.25f, 1f);
        private static readonly Color NeedColor = new Color(1f, 0.45f, 0.4f, 1f);

        protected override void OnLoad()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int captured = i;
                rows[i].button.onClick.AddListener(() => OnPick(captured));
            }
        }

        /// <summary>카드 <paramref name="index"/> 의 신화 목록을 연다.</summary>
        public void Open(int index)
        {
            cardIndex = index;
            Enter();
        }

        protected override void OnEnter()
        {
            Rebuild();
        }

        private void Update()
        {
            // SP 가 들어오거나 보드가 바뀌어도 목록이 어긋나지 않게 매 프레임 맞춘다(테스트 모드 전용 창).
            if (_isEnter)
                Rebuild();
        }

        private void Rebuild()
        {
            if (battle == null || battle.Board == null)
                return;

            options.Clear();
            options.AddRange(MythicList.Build(new BoardShopContext(battle.Board, ownership)));

            int sp = battle.BattlePoints != null ? battle.BattlePoints.Get(OJ.BattlePointType.SummonPoint) : 0;
            bool affordable = sp >= DiceRecipes.MythicCostSp;
            costText.text = "소환 비용 " + DiceRecipes.MythicCostSp + " SP";
            costText.color = affordable ? Color.white : NeedColor;

            for (int i = 0; i < rows.Length; i++)
            {
                bool has = i < options.Count;
                rows[i].background.gameObject.SetActive(has);
                if (has)
                    Bind(rows[i], options[i], affordable);
            }
        }

        private static void Bind(RowView row, MythicOption option, bool affordable)
        {
            DiceRecipe r = option.Recipe;

            row.kingIcon.sprite = DiceMetaDataProvider.GetIcon(r.Result, 1);
            row.kingIcon.enabled = row.kingIcon.sprite != null;
            row.nameText.text = UIShopCardSlot.NameOf(r.Result);

            row.line1Icon.gameObject.SetActive(false);
            row.line1Text.gameObject.SetActive(false);
            row.line2Icon.gameObject.SetActive(false);
            row.line2Text.gameObject.SetActive(false);

            bool pickable = false;

            switch (option.State)
            {
                case MythicOptionState.Craftable:
                    // 재료 둘이 다 있다. 두 줄로 "보유" 를 보여 준다.
                    ShowLine(row.line1Icon, row.line1Text, r.MainType, r.MainStar, "보유", Color.white, 0f, 30f);
                    ShowLine(row.line2Icon, row.line2Text, r.SubType, r.SubStar, "보유", Color.white, 0f, -30f);

                    pickable = affordable;
                    row.stateText.text = affordable ? "소환 가능" : "SP 부족";
                    row.stateText.color = affordable ? StateGoodColor : NeedColor;
                    break;

                case MythicOptionState.MissingMaterials:
                    // 모자란 것만 적는다. 하나면 가운데 한 줄, 둘 다면 두 줄.
                    if (option.NeedMain && option.NeedSub)
                    {
                        ShowLine(row.line1Icon, row.line1Text, r.MainType, r.MainStar, "필요", NeedColor, 0f, 30f);
                        ShowLine(row.line2Icon, row.line2Text, r.SubType, r.SubStar, "필요", NeedColor, 0f, -30f);
                    }
                    else if (option.NeedMain)
                    {
                        ShowLine(row.line1Icon, row.line1Text, r.MainType, r.MainStar, "필요", NeedColor, 0f, 0f);
                    }
                    else
                    {
                        ShowLine(row.line1Icon, row.line1Text, r.SubType, r.SubStar, "필요", NeedColor, 0f, 0f);
                    }

                    row.stateText.text = string.Empty;
                    break;

                default:
                    row.stateText.text = "구매필요";
                    row.stateText.color = NeedColor;
                    break;
            }

            row.button.interactable = pickable;
            row.group.alpha = option.State == MythicOptionState.Craftable && pickable ? 1f : 0.5f;
            row.background.color = pickable ? new Color(0.45f, 0.42f, 0.2f, 1f) : DimAlphaColor;
        }

        private static void ShowLine(
            Image icon, TMP_Text text, DiceType type, int star, string label, Color color, float dx, float y)
        {
            icon.sprite = DiceMetaDataProvider.GetIcon(type, star);
            icon.enabled = icon.sprite != null;
            icon.gameObject.SetActive(true);
            icon.rectTransform.anchoredPosition = new Vector2(icon.rectTransform.anchoredPosition.x + dx, y);

            text.text = label;
            text.color = color;
            text.gameObject.SetActive(true);
            text.rectTransform.anchoredPosition = new Vector2(text.rectTransform.anchoredPosition.x + dx, y);
        }

        private void OnPick(int row)
        {
            if (row < 0 || row >= options.Count || options[row].State != MythicOptionState.Craftable)
                return;

            DiceType king = options[row].Recipe.Result;
            UIDice dice = battle.Game.TrySummonMythic(cardIndex, king);
            if (dice == null)
                return;

            // 목록은 닫히고 카드 칸은 비었다. 소환 연출이 이어진다.
            Exit();
            GameContainer.UI?.Get<UIMythicSummonCinematic>()?.Play(king, dice);
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────

        private static readonly Vector2 PanelVisible = new Vector2(940f, 1130f);
        private const float RowHeight = 150f;
        private const float RowGap = 10f;

        private static readonly Color RowColor = new Color(0x63 / 255f, 0x64 / 255f, 0x81 / 255f, 0.95f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UIMythicListDialog Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIMythicListDialog", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIMythicListDialog>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

            // 바깥을 누르면 닫힌다.
            Image blocker = UIBountyUIFactory.CreateImage("OutsideCatcher", view.transform, new Color(0f, 0f, 0f, 0.6f));
            UIBountyUIFactory.Stretch(blocker.rectTransform);
            dialog.outsideButton = blocker.gameObject.AddComponent<Button>();
            dialog.outsideButton.targetGraphic = blocker;
            dialog.outsideButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(dialog.outsideButton);

            Image panelImage = UIBountyUIFactory.CreateSprite("Panel", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_Bg"), 4f,
                PanelVisible + new Vector2(24f, 24f), Vector2.zero, true);
            panelImage.raycastTarget = true;
            RectTransform panel = panelImage.rectTransform;

            float top = PanelVisible.y / 2f;

            TMP_Text title = UIBountyUIFactory.CreateText("Title", panel, "신화 다이스 소환", 44f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(title.rectTransform, new Vector2(700f, 64f), new Vector2(0f, top - 56f));

            dialog.costText = UIBountyUIFactory.CreateText("Cost", panel, "소환 비용 50 SP", 28f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.costText.rectTransform, new Vector2(700f, 40f), new Vector2(0f, top - 104f));

            dialog.rows = new RowView[DiceRecipes.Mythic.Count];
            float y = top - 150f;
            for (int i = 0; i < dialog.rows.Length; i++)
            {
                dialog.rows[i] = BuildRow(panel, new Vector2(0f, y - RowHeight / 2f), font, plainMaterial);
                y -= RowHeight + RowGap;
            }

            dialog.closeButton = UICardShopPanel.CreateKitButton("CloseButton", panel, "Ingame/Btn_Gray",
                new Vector2(200f, 60f), new Vector2(0f, -top + 52f), "닫기", font, out _);
            dialog.AddExitButton(dialog.closeButton);

            return dialog;
        }

        private static RowView BuildRow(RectTransform panel, Vector2 position, TMP_FontAsset font, Material plainMaterial)
        {
            var row = new RowView();

            row.background = UIBountyUIFactory.CreateSprite("Row", panel,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 3f,
                new Vector2(860f + 26f, RowHeight + 26f), position, true);
            row.background.color = RowColor;
            row.background.raycastTarget = true;

            row.button = row.background.gameObject.AddComponent<Button>();
            row.button.targetGraphic = row.background;
            row.group = row.background.gameObject.AddComponent<CanvasGroup>();

            Transform r = row.background.transform;

            row.kingIcon = Icon("King", r, new Vector2(-350f, 0f), 112f);

            row.nameText = UIBountyUIFactory.CreateText("Name", r, "이름", 32f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(row.nameText.rectTransform, new Vector2(230f, 44f), new Vector2(-160f, 0f));

            row.line1Icon = Icon("Line1Icon", r, new Vector2(10f, 30f), 56f);
            row.line1Text = Line("Line1Text", r, new Vector2(110f, 30f), font);
            row.line2Icon = Icon("Line2Icon", r, new Vector2(10f, -30f), 56f);
            row.line2Text = Line("Line2Text", r, new Vector2(110f, -30f), font);

            row.stateText = UIBountyUIFactory.CreateText("State", r, "소환 가능", 34f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(row.stateText.rectTransform, new Vector2(230f, 50f), new Vector2(320f, 0f));

            return row;
        }

        private static Image Icon(string name, Transform parent, Vector2 position, float size)
        {
            Image icon = UIBountyUIFactory.CreateImage(name, parent, Color.white);
            UIBountyUIFactory.SetRect(icon.rectTransform, new Vector2(size, size), position);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        private static TMP_Text Line(string name, Transform parent, Vector2 position, TMP_FontAsset font)
        {
            TMP_Text text = UIBountyUIFactory.CreateText(name, parent, "필요", 30f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(text.rectTransform, new Vector2(150f, 40f), position);
            return text;
        }
    }
}
