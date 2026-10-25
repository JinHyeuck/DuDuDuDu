using System;
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
    /// 영혼 보너스 보상 선택 창. 목표를 채운 저금통을 <b>유저가 눌러</b> 연다. SP 와 마석 중 하나를 고르면 닫힌다.
    /// <b>게임은 멈추지 않는다</b> — 바깥을 누르거나 백키로 닫고 나중에 다시 받아도 된다(값은 고정이다).
    ///
    /// 큰 버튼 둘이 판을 꽉 채운다. 왼쪽은 <b>SP</b>, 오른쪽은 <b>마석</b>이고 글자를 아주 크게 해 한눈에 읽힌다.
    /// 값은 팝업이 뜰 때 한 번 굴려 <c>RunState</c> 에 고정되어 있다(<see cref="SoulBonusManager.OfferSp"/>).
    /// <b>열려 있는지는 정적 값으로 알린다</b>(<see cref="IsOpen"/>) — 튜토리얼이 팝업을 기다릴 때 읽는다.
    /// </summary>
    public sealed class UISoulBonusDialog : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [Serializable]
        private sealed class Choice
        {
            public Button button;
            public Image icon;
            public TMP_Text valueText;
            public TMP_Text subText;
        }

        [SerializeField] private Choice spChoice = new Choice();
        [SerializeField] private Choice stoneChoice = new Choice();

        /// <summary>영혼 보너스 팝업이 열려 있는가.</summary>
        public static bool IsOpen { get; private set; }

        protected override void OnLoad()
        {
            spChoice.button.onClick.AddListener(() => OnChoiceClicked(true));
            stoneChoice.button.onClick.AddListener(() => OnChoiceClicked(false));
        }

        protected override void OnEnter()
        {
            IsOpen = true;

            // 다른 창 위에 오도록 맨 마지막 형제로 올린다(같은 캔버스 안의 순서는 만들어진 순서다).
            transform.SetAsLastSibling();

            // 아이콘은 런타임에 채운다 — 굽는 시점에는 재화 메타데이터가 없을 수 있다.
            SetIcon(spChoice.icon, BattlePointUtility.GetIcon(BattlePointType.SummonPoint));
            SetIcon(stoneChoice.icon, BattlePointUtility.GetIcon(BattlePointType.EnhanceStone));

            Refresh();
        }

        protected override void OnExit()
        {
            IsOpen = false;
            Manager?.NotifyDialogClosed();
        }

        protected override void OnUnload()
        {
            IsOpen = false;
        }

        private SoulBonusManager Manager => battle != null && battle.Game != null ? battle.Game.SoulBonus : null;

        /// <summary>버튼의 영역(true = SP). 튜토리얼이 쓴다.</summary>
        public RectTransform GetChoiceRect(bool sp)
        {
            Choice choice = sp ? spChoice : stoneChoice;
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
            SoulBonusManager manager = Manager;
            if (manager == null)
                return;

            spChoice.valueText.SetText(manager.OfferSp.ToString());
            stoneChoice.valueText.SetText(manager.OfferStones.ToString());
        }

        private void OnChoiceClicked(bool sp)
        {
            // 지급·재개·닫기는 매니저가 한다. 여기서 Exit 를 부르지 않는다 — 매니저가 Hide 한다.
            Choice choice = sp ? spChoice : stoneChoice;
            Manager?.Choose(sp, choice.icon != null ? choice.icon.rectTransform : null);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        private static readonly Vector2 PanelVisible = new Vector2(940f, 850f);
        private static readonly Vector2 ChoiceSize = new Vector2(430f, 570f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UISoulBonusDialog Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UISoulBonusDialog", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISoulBonusDialog>();
            dialog.dialogView = view;

            // 게임이 멈추지 않으니 닫을 수 있다 — 백키·바깥 탭으로 닫고 나중에 다시 받아도 된다.
            dialog.UseBackBtn = true;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            // 화면 전체를 덮는 어두운 막. 바깥을 누르면 닫힌다.
            Image blocker = UIBountyUIFactory.CreateImage("Blocker", view.transform, new Color(0f, 0f, 0f, 0.6f));
            UIBountyUIFactory.Stretch(blocker.rectTransform);
            blocker.raycastTarget = true;
            Button outside = blocker.gameObject.AddComponent<Button>();
            outside.targetGraphic = blocker;
            outside.transition = Selectable.Transition.None;
            dialog.AddExitButton(outside);

            // 판. 클릭을 먹어야 한다.
            Image panelImage = UIBountyUIFactory.CreateSprite("Panel", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_Bg"), 4f,
                PanelVisible + new Vector2(24f, 24f), Vector2.zero, true);
            panelImage.raycastTarget = true;
            RectTransform panel = panelImage.rectTransform;
            float top = PanelVisible.y / 2f;

            TMP_Text title = UIBountyUIFactory.CreateText("Title", panel, "영혼 보너스!", 64f,
                TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.4f, 1f), font);
            UIBountyUIFactory.SetRect(title.rectTransform, new Vector2(800f, 80f), new Vector2(0f, top - 62f));

            TMP_Text sub = UIBountyUIFactory.CreateText("Sub", panel, "보상 하나를 골라요", 36f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(sub.rectTransform, new Vector2(800f, 50f), new Vector2(0f, top - 128f));

            float choiceY = top - 170f - ChoiceSize.y * 0.5f;
            float choiceX = ChoiceSize.x * 0.5f + 12f;
            dialog.spChoice = CreateChoice(panel, "SpChoice", new Vector2(-choiceX, choiceY),
                UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green"), "SP", "받기", font);
            dialog.stoneChoice = CreateChoice(panel, "StoneChoice", new Vector2(choiceX, choiceY),
                UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Yellow"), "마석", "개", font);

            TMP_Text hint = UIBountyUIFactory.CreateText("Hint", panel, "하나를 골라요! (바깥을 누르면 나중에 받을 수 있어요)", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(hint.rectTransform, new Vector2(860f, 44f), new Vector2(0f, -top + 50f));

            return dialog;
        }

        private static Choice CreateChoice(Transform parent, string name, Vector2 position, Sprite sprite, string label, string unit, TMP_FontAsset font)
        {
            var choice = new Choice();

            // 킷 버튼(32², 여백 사방 4px)을 x3 로 키워 보이는 크기에 맞춘다.
            Image background = UIBountyUIFactory.CreateSprite(name, parent, sprite, 3f,
                ChoiceSize + new Vector2(24f, 24f), position, true);
            background.raycastTarget = true;
            choice.button = background.gameObject.AddComponent<Button>();
            choice.button.targetGraphic = background;

            Transform t = background.transform;

            TMP_Text title = UIBountyUIFactory.CreateText("Label", t, label, 76f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(title.rectTransform, new Vector2(400f, 100f), new Vector2(0f, 205f));

            choice.icon = UIBountyUIFactory.CreateImage("Icon", t, Color.white);
            choice.icon.preserveAspect = true;
            choice.icon.raycastTarget = false;
            UIBountyUIFactory.SetRect(choice.icon.rectTransform, new Vector2(140f, 140f), new Vector2(0f, 75f));

            choice.valueText = UIBountyUIFactory.CreateText("Value", t, "0", 150f,
                TextAlignmentOptions.Center, new Color(1f, 0.97f, 0.7f, 1f), font);
            UIBountyUIFactory.SetRect(choice.valueText.rectTransform, new Vector2(410f, 180f), new Vector2(0f, -85f));
            choice.valueText.raycastTarget = false;

            choice.subText = UIBountyUIFactory.CreateText("Sub", t, unit, 48f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(choice.subText.rectTransform, new Vector2(400f, 60f), new Vector2(0f, -215f));

            return choice;
        }
    }
}
