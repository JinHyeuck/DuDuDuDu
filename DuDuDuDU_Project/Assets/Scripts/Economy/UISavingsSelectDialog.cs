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
    /// 저금통 선택 창. 저금통 버튼을 누르면 열린다. <b>방식이 둘이고 한 프리팹에 배치를 둘 다 굽는다</b>(<see cref="SavingsMode"/>) —
    /// 판이 시작될 때 정해진 방식의 배치만 켠다.
    ///
    /// <list type="bullet">
    /// <item><b>선택(기본)</b>: 팝업을 꽉 채우는 큰 버튼 둘. 왼쪽 "지금 바로 N SP", 오른쪽 "45초 뒤 M SP(×배율)". 어느 쪽을
    ///   눌러도 45초 뒤에 다시 고른다. 글자를 아주 크게 하고 "SP" 대신 <b>SP 아이콘</b>을 붙였다.</item>
    /// <item><b>3단(이전 방식)</b>: 카드 셋 — <c>맡기는 SP → 돌려받는 SP</c>, 걸리는 시간, 순이익. SP 가 모자란 카드는 흐려져 눌리지 않는다.</item>
    /// </list>
    ///
    /// 고르면 창이 닫힌다. 열려 있는 동안에는 SP 가 늘어도 어긋나지 않게 매 프레임 맞춘다.
    /// <b>열려 있는지는 정적 값으로 알린다</b>(<see cref="IsOpen"/>) — 저금통 버튼이 Open 상태를 그린다(현상금 선택 창과 같은 방식).
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

        [Serializable]
        private sealed class Choice
        {
            public Button button;
            public Image icon;
            public TMP_Text valueText;
            public TMP_Text subText;
        }

        [SerializeField] private Button outsideButton;
        [SerializeField] private Image spIcon;
        [SerializeField] private TMP_Text spText;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite blockedSprite;

        [Header("3단(이전 방식)")]
        [SerializeField] private GameObject tieredRoot;
        [SerializeField] private List<Card> cards = new List<Card>();

        [Header("선택(기본)")]
        [SerializeField] private GameObject choiceRoot;
        [SerializeField] private Choice instantChoice = new Choice();
        [SerializeField] private Choice delayedChoice = new Choice();

        private static readonly Color DimColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private SavingsMode mode;

        /// <summary>저금통 선택 창이 열려 있는가. 저금통 버튼의 Open 상태가 읽는다.</summary>
        public static bool IsOpen { get; private set; }

        protected override void OnLoad()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                int option = i;
                cards[i].button.onClick.AddListener(() => OnCardClicked(option));
            }

            instantChoice.button.onClick.AddListener(() => OnChoiceClicked(true));
            delayedChoice.button.onClick.AddListener(() => OnChoiceClicked(false));
        }

        protected override void OnEnter()
        {
            IsOpen = true;

            // 저금통 버튼 위에 오도록 맨 마지막 형제로 올린다(같은 캔버스 안의 순서는 만들어진 순서다).
            transform.SetAsLastSibling();

            // 아이콘은 런타임에 채운다 — 굽는 시점에는 재화 메타데이터가 없을 수 있다.
            Sprite sp = BattlePointUtility.GetIcon(BattlePointType.SummonPoint);
            SetIcon(spIcon, sp);
            for (int i = 0; i < cards.Count; i++)
            {
                SetIcon(cards[i].depositIcon, sp);
                SetIcon(cards[i].payoutIcon, sp);
            }

            SetIcon(instantChoice.icon, sp);
            SetIcon(delayedChoice.icon, sp);

            // 방식에 맞는 배치만 켠다. 선택 방식은 오퍼를 여기서 굴린다(이미 있으면 그대로 — 닫았다 열어도 값이 같다).
            SavingsManager savings = Savings;
            mode = savings != null ? savings.Mode : SavingsMode.Choice;
            tieredRoot.SetActive(mode == SavingsMode.Tiered);
            choiceRoot.SetActive(mode == SavingsMode.Choice);
            savings?.EnsureOffer();

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

        private SavingsManager Savings => battle != null && battle.Game != null ? battle.Game.Savings : null;

        private void Update()
        {
            if (_isEnter)
                Refresh();
        }

        /// <summary>3단 방식 카드의 영역. 튜토리얼이 "이것을 골라요" 를 가리키는 데 쓴다. 없으면 null.</summary>
        public RectTransform GetCardRect(int option)
        {
            return option >= 0 && option < cards.Count ? cards[option].button.transform as RectTransform : null;
        }

        /// <summary>선택 방식 버튼의 영역(true = 지금 바로). 튜토리얼이 쓴다.</summary>
        public RectTransform GetChoiceRect(bool instant)
        {
            Choice choice = instant ? instantChoice : delayedChoice;
            return choice.button != null ? choice.button.transform as RectTransform : null;
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
            SavingsManager savings = Savings;
            if (savings == null)
                return;

            // 고른 직후(대기 중이 됨)나 판이 끝나 못 고르게 되면 닫는다.
            if (!savings.CanOpen)
            {
                Exit();
                return;
            }

            int sp = battle.BattlePoints != null ? battle.BattlePoints.Get(OJ.BattlePointType.SummonPoint) : 0;
            if (spText != null)
                spText.SetText(ShortNumberFormat.Format(sp));

            if (mode == SavingsMode.Choice)
                RefreshChoice(savings);
            else
                RefreshTiered(savings);
        }

        private void RefreshChoice(SavingsManager savings)
        {
            int instant = savings.OfferInstant;
            int delayed = savings.OfferDelayed;
            bool ready = instant > 0;

            instantChoice.valueText.SetText(ready ? instant.ToString() : "-");
            delayedChoice.valueText.SetText(ready ? delayed.ToString() : "-");

            // 배율은 소수 한 자리. 나중 값은 언제나 즉시 값의 1.1 배 이상이다.
            float ratio = ready ? delayed / (float)instant : 0f;
            delayedChoice.subText.SetText(ready ? ratio.ToString("0.0") + "배" : string.Empty);

            instantChoice.button.interactable = ready;
            delayedChoice.button.interactable = ready;
        }

        private void RefreshTiered(SavingsManager savings)
        {
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
            SavingsManager savings = Savings;
            if (savings == null || !savings.TryDeposit(option))
                return;

            // 맡겼다. 창을 열어 둘 이유가 없다 — 카운트다운은 저금통 버튼이 보여 준다.
            Exit();
        }

        private void OnChoiceClicked(bool instant)
        {
            SavingsManager savings = Savings;
            if (savings == null || !savings.TryChoose(instant))
                return;

            Exit();
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        private static readonly Vector2 PanelVisible = new Vector2(940f, 850f);
        private static readonly Vector2 CardSize = new Vector2(840f, 200f);
        private const float CardGap = 22f;
        private static readonly Vector2 ChoiceSize = new Vector2(430f, 570f);

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

            // 판. 클릭을 먹어야 한다(버튼 사이 틈의 탭이 막으로 새어 창이 닫히면 안 된다).
            Image panelImage = UIBountyUIFactory.CreateSprite("Panel", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_Bg"), 4f,
                PanelVisible + new Vector2(24f, 24f), Vector2.zero, true);
            panelImage.raycastTarget = true;
            RectTransform panel = panelImage.rectTransform;
            float top = PanelVisible.y / 2f;

            TMP_Text title = UIBountyUIFactory.CreateText("Title", panel, "저금통", 56f,
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

            // ── 3단(이전 방식) 배치. 자식들의 좌표는 판 중심 기준이라 뿌리를 판 전체로 늘려 둔다.
            GameObject tiered = UIBountyUIFactory.CreateRect("TieredRoot", panel);
            UIBountyUIFactory.Stretch(tiered.GetComponent<RectTransform>());
            dialog.tieredRoot = tiered;

            TMP_Text tieredHint = UIBountyUIFactory.CreateText("Hint", tiered.transform,
                "맡길 만큼 고르면 시간이 지난 뒤 더 많이 돌려받아요!", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(tieredHint.rectTransform, new Vector2(860f, 44f), new Vector2(0f, top - 122f));

            float firstY = top - 170f - CardSize.y * 0.5f;
            for (int i = 0; i < SavingsRules.OptionCount; i++)
            {
                float y = firstY - i * (CardSize.y + CardGap);
                dialog.cards.Add(CreateCard(tiered.transform, i, new Vector2(0f, y), dialog.readySprite, font));
            }

            // ── 선택(기본) 배치. 큰 버튼 둘이 판을 꽉 채운다.
            GameObject choice = UIBountyUIFactory.CreateRect("ChoiceRoot", panel);
            UIBountyUIFactory.Stretch(choice.GetComponent<RectTransform>());
            dialog.choiceRoot = choice;

            float choiceY = top - 130f - ChoiceSize.y * 0.5f;
            float choiceX = ChoiceSize.x * 0.5f + 12f;
            dialog.instantChoice = CreateChoice(choice.transform, "Instant", new Vector2(-choiceX, choiceY),
                UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green"), "지금 바로", font);
            dialog.delayedChoice = CreateChoice(choice.transform, "Delayed",new Vector2(choiceX, choiceY),
                UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Yellow"), SavingsRules.ChoiceCooldownSeconds + "초 뒤", font);
            dialog.delayedChoice.subText.SetText("2.0배");

            TMP_Text choiceHint = UIBountyUIFactory.CreateText("Hint", choice.transform,
                "어느 쪽을 골라도 " + (int)SavingsRules.ChoiceCooldownSeconds + "초 뒤에 다시 고를 수 있어요!", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(choiceHint.rectTransform, new Vector2(860f, 44f), new Vector2(0f, -top + 48f));

            return dialog;
        }

        private static Choice CreateChoice(Transform parent, string name, Vector2 position, Sprite sprite, string label, TMP_FontAsset font)
        {
            var choice = new Choice();

            // 킷 버튼(32², 여백 사방 4px)을 x3 로 키워 보이는 크기에 맞춘다.
            Image background = UIBountyUIFactory.CreateSprite(name, parent, sprite, 3f,
                ChoiceSize + new Vector2(24f, 24f), position, true);
            background.raycastTarget = true;
            choice.button = background.gameObject.AddComponent<Button>();
            choice.button.targetGraphic = background;

            Transform t = background.transform;

            TMP_Text title = UIBountyUIFactory.CreateText("Label", t, label, 68f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(title.rectTransform, new Vector2(400f, 90f), new Vector2(0f, 205f));

            choice.icon = CreateIcon("Icon", t, new Vector2(0f, 75f), 140f);

            choice.valueText = CreateText("Value", t, TextAlignmentOptions.Center, 150f, new Color(1f, 0.97f, 0.7f, 1f),
                new Vector2(410f, 180f), new Vector2(0f, -85f), font);

            choice.subText = CreateText("Sub", t, TextAlignmentOptions.Center, 44f, Color.white,
                new Vector2(400f, 60f), new Vector2(0f, -215f), font);
            choice.subText.SetText(string.Empty);

            return choice;
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
