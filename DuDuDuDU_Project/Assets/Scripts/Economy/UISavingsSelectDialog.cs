using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Battle;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.UI;

namespace OJ.Economy
{
    /// <summary>
    /// 통장 선택 창. 통장 버튼을 누르면 열리고, <b>얼마를 맡겨 얼마를 받을지</b> 셋 중 하나를 고른다
    /// (<see cref="SavingsRules"/>). 카드 한 장에 <c>맡기는 SP → 돌려받는 SP</c>, 걸리는 시간, 순이익이 큼직하게 적힌다.
    /// "SP" 라는 글자 대신 <b>SP 아이콘</b>을 붙여 한눈에 읽히게 했다.
    ///
    /// SP 가 모자란 카드는 흐려지고 눌리지 않는다(왜 못 고르는지가 보이도록 금액은 그대로 적는다).
    /// 고르면 SP 를 맡기고 창이 닫힌다. 열려 있는 동안에는 SP 가 늘어도 어긋나지 않게 매 프레임 맞춘다.
    ///
    /// <b>열려 있는지는 정적 값으로 알린다</b>(<see cref="IsOpen"/>) — 통장 버튼이 Open 상태를 그린다(현상금 선택 창과 같은 방식).
    /// </summary>
    public sealed class UISavingsSelectDialog : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [Serializable]
        private sealed class Card
        {
            public Button button;
            public Image background;
            public Image depositIcon;
            public TMP_Text depositText;
            public Image payoutIcon;
            public TMP_Text payoutText;
            public TMP_Text timeText;
            public TMP_Text gainText;
        }

        [SerializeField] private Button outsideButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private List<Card> cards = new List<Card>();
        [SerializeField] private Image spIcon;
        [SerializeField] private TMP_Text spText;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite blockedSprite;

        private static readonly Color DimColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        /// <summary>통장 선택 창이 열려 있는가. 통장 버튼의 Open 상태가 읽는다.</summary>
        public static bool IsOpen { get; private set; }

        protected override void OnLoad()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                int option = i;
                cards[i].button.onClick.AddListener(() => OnCardClicked(option));
            }
        }

        protected override void OnEnter()
        {
            IsOpen = true;

            // 통장 버튼 위에 오도록 맨 마지막 형제로 올린다(같은 캔버스 안의 순서는 만들어진 순서다).
            transform.SetAsLastSibling();

            // 아이콘은 런타임에 채운다 — 굽는 시점에는 재화 메타데이터가 없을 수 있다.
            Sprite sp = BattlePointUtility.GetIcon(BattlePointType.SummonPoint);
            SetIcon(spIcon, sp);
            for (int i = 0; i < cards.Count; i++)
            {
                SetIcon(cards[i].depositIcon, sp);
                SetIcon(cards[i].payoutIcon, sp);
            }

            Refresh();
        }

        protected override void OnExit()
        {
            IsOpen = false;
        }

        protected override void OnUnload()
        {
            IsOpen = false;
        }

        private void Update()
        {
            if (_isEnter)
                Refresh();
        }

        /// <summary>카드의 영역. 튜토리얼이 "이것을 골라요" 를 가리키는 데 쓴다. 없으면 null.</summary>
        public RectTransform GetCardRect(int option)
        {
            return option >= 0 && option < cards.Count ? cards[option].button.transform as RectTransform : null;
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void Refresh()
        {
            SavingsManager savings = battle != null && battle.Game != null ? battle.Game.Savings : null;
            if (savings == null)
                return;

            // 맡긴 것이 생기거나(고른 직후) 판이 끝나 못 맡게 되면 닫는다.
            if (!savings.CanOpen)
            {
                Exit();
                return;
            }

            int sp = battle.BattlePoints != null ? battle.BattlePoints.Get(OJ.BattlePointType.SummonPoint) : 0;
            if (spText != null)
                spText.SetText(ShortNumberFormat.Format(sp));

            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                bool can = savings.CanDeposit(i);

                card.depositText.SetText(SavingsRules.Deposit(i).ToString());
                card.payoutText.SetText(SavingsRules.Payout(i).ToString());
                card.timeText.SetText(Mathf.RoundToInt(SavingsRules.Duration(i)) + "초");
                card.gainText.SetText("+" + SavingsRules.Gain(i));

                card.button.interactable = can;
                card.background.sprite = can ? readySprite : blockedSprite;
                card.background.color = can ? Color.white : DimColor;
            }
        }

        private void OnCardClicked(int option)
        {
            SavingsManager savings = battle != null && battle.Game != null ? battle.Game.Savings : null;
            if (savings == null || !savings.TryDeposit(option))
                return;

            // 맡겼다. 창을 열어 둘 이유가 없다 — 카운트다운은 통장 버튼이 보여 준다.
            Exit();
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        private static readonly Vector2 PanelVisible = new Vector2(940f, 850f);
        private static readonly Vector2 CardSize = new Vector2(840f, 200f);
        private const float CardGap = 22f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UISavingsSelectDialog Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UISavingsSelectDialog", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISavingsSelectDialog>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

            dialog.readySprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green");
            dialog.blockedSprite = UIBountyUIFactory.LoadSprite("Ingame/Btn_Gray");

            // 화면 전체를 덮는 어두운 막. 바깥을 누르면 닫힌다.
            Image blocker = UIBountyUIFactory.CreateImage("OutsideCatcher", view.transform, new Color(0f, 0f, 0f, 0.6f));
            UIBountyUIFactory.Stretch(blocker.rectTransform);
            dialog.outsideButton = blocker.gameObject.AddComponent<Button>();
            dialog.outsideButton.targetGraphic = blocker;
            dialog.outsideButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(dialog.outsideButton);

            // 판. 클릭을 먹어야 한다(카드 사이 틈의 탭이 막으로 새어 창이 닫히면 안 된다).
            Image panelImage = UIBountyUIFactory.CreateSprite("Panel", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_Bg"), 4f,
                PanelVisible + new Vector2(24f, 24f), Vector2.zero, true);
            panelImage.raycastTarget = true;
            RectTransform panel = panelImage.rectTransform;
            float top = PanelVisible.y / 2f;

            TMP_Text title = UIBountyUIFactory.CreateText("Title", panel, "통장", 56f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(title.rectTransform, new Vector2(500f, 70f), new Vector2(0f, top - 60f));

            // 가진 SP — 오른쪽 위.
            dialog.spIcon = UIBountyUIFactory.CreateImage("SpIcon", panel, Color.white);
            UIBountyUIFactory.SetRect(dialog.spIcon.rectTransform, new Vector2(52f, 52f), new Vector2(220f, top - 60f));
            dialog.spIcon.preserveAspect = true;
            dialog.spIcon.raycastTarget = false;
            dialog.spText = UIBountyUIFactory.CreateText("SpText", panel, "0", 40f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.spText.rectTransform, new Vector2(200f, 56f), new Vector2(360f, top - 60f));

            TMP_Text hint = UIBountyUIFactory.CreateText("Hint", panel,
                "맡길 만큼 고르면 시간이 지난 뒤 더 많이 돌려받아요!", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(hint.rectTransform, new Vector2(860f, 44f), new Vector2(0f, top - 122f));

            // 카드 셋. 위에서부터 작은 것 → 큰 것.
            float firstY = top - 170f - CardSize.y * 0.5f;
            for (int i = 0; i < SavingsRules.OptionCount; i++)
            {
                float y = firstY - i * (CardSize.y + CardGap);
                dialog.cards.Add(CreateCard(panel, i, new Vector2(0f, y), dialog.readySprite, font));
            }

            return dialog;
        }

        private static Card CreateCard(Transform parent, int index, Vector2 position, Sprite sprite, TMP_FontAsset font)
        {
            var card = new Card();

            // 킷 버튼(32², 여백 사방 4px)을 x3 로 키워 보이는 크기에 맞춘다.
            card.background = UIBountyUIFactory.CreateSprite("Card" + index, parent, sprite, 3f,
                CardSize + new Vector2(24f, 24f), position, true);
            card.background.raycastTarget = true;
            card.button = card.background.gameObject.AddComponent<Button>();
            card.button.targetGraphic = card.background;

            Transform t = card.background.transform;
            Color yellow = new Color(1f, 0.93f, 0.45f, 1f);

            // 맡기는 SP.
            card.depositIcon = CreateIcon("DepositIcon", t, new Vector2(-330f, 6f), 84f);
            card.depositText = CreateText("DepositText", t, TextAlignmentOptions.Left, 78f, Color.white,
                new Vector2(160f, 100f), new Vector2(-218f, 6f), font);

            // 화살표 — 킷의 화살표는 왼쪽을 향해서 뒤집는다.
            Image arrow = UIBountyUIFactory.CreateImage("Arrow", t, Color.white);
            arrow.sprite = UIBountyUIFactory.LoadSprite("Main/Icon_NewxArrow");
            arrow.preserveAspect = true;
            arrow.raycastTarget = false;
            UIBountyUIFactory.SetRect(arrow.rectTransform, new Vector2(52f, 52f), new Vector2(-90f, 6f));
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);

            // 돌려받는 SP — 더 크고 노랗게.
            card.payoutIcon = CreateIcon("PayoutIcon", t, new Vector2(-10f, 6f), 96f);
            card.payoutText = CreateText("PayoutText", t, TextAlignmentOptions.Left, 92f, yellow,
                new Vector2(190f, 110f), new Vector2(122f, 6f), font);

            // 시간과 순이익.
            card.timeText = CreateText("TimeText", t, TextAlignmentOptions.Center, 44f, Color.white,
                new Vector2(170f, 56f), new Vector2(300f, 38f), font);
            card.gainText = CreateText("GainText", t, TextAlignmentOptions.Center, 40f, new Color(0.7f, 1f, 0.55f, 1f),
                new Vector2(170f, 52f), new Vector2(300f, -24f), font);

            return card;
        }

        private static Image CreateIcon(string name, Transform parent, Vector2 position, float size)
        {
            Image icon = UIBountyUIFactory.CreateImage(name, parent, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIBountyUIFactory.SetRect(icon.rectTransform, new Vector2(size, size), position);
            return icon;
        }

        private static TMP_Text CreateText(
            string name, Transform parent, TextAlignmentOptions align, float size, Color color,
            Vector2 rectSize, Vector2 position, TMP_FontAsset font)
        {
            TMP_Text text = UIBountyUIFactory.CreateText(name, parent, "0", size, align, color, font);
            UIBountyUIFactory.SetRect(text.rectTransform, rectSize, position);
            return text;
        }
    }
}
