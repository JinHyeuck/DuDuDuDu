using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Battle;
using OJ.Core;
using OJ.DI;
using OJ.UI;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금 선택 창 — 등급 카드 3x2. <b>카드를 누르면 그 자리에서 소환하고 닫힌다</b>
    /// (<see cref="BountyManager.TrySummon"/>). 예전처럼 "이번 웨이브에 켜 두기" 가 아니다.
    ///
    /// 바깥이나 X 를 누르면 아무것도 소환하지 않고 닫힌다 — 쿨타임도 시작되지 않는다.
    /// 위 헤더에 지금 가진 SP · 마석을 띄운다: 보상으로 무엇이 들어오는지 읽는 기준점이다.
    ///
    /// <b>열려 있는지는 정적 값으로 알린다</b>(<see cref="IsOpen"/>). 현상금 버튼이 Open 상태를 그릴 때
    /// <c>UIService.Get</c> 으로 창을 물으면 닫혀 있을 때 인스턴스가 새로 만들어지기 때문이다.
    /// </summary>
    public class UIBountySelectDialog : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button outsideButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private List<UIBountySlot> slots = new List<UIBountySlot>();
        [SerializeField] private Image spIcon;
        [SerializeField] private TMP_Text spText;
        [SerializeField] private Image stoneIcon;
        [SerializeField] private TMP_Text stoneText;

        /// <summary>선택 창이 열려 있는가. 현상금 버튼의 Open 상태가 읽는다.</summary>
        public static bool IsOpen { get; private set; }

        protected override void OnEnter()
        {
            IsOpen = true;

            // 현상금 버튼 위에 오도록 맨 마지막 형제로 올린다. 같은 캔버스 안의 순서는 <b>만들어진 순서</b>라
            // 어느 쪽이 먼저 만들어졌느냐에 따라 버튼이 팝업을 덮을 수 있다.
            transform.SetAsLastSibling();
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
            // SP·마석이 들어오거나 등급이 열려도 어긋나지 않게 맞춘다(열려 있는 동안만이라 비용이 작다).
            if (_isEnter)
                Refresh();
        }

        /// <summary>등급 칸의 영역. 튜토리얼이 "이 칸을 골라요" 를 가리키는 데 쓴다. 없으면 null.</summary>
        public RectTransform GetSlotRect(int grade)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Grade == grade)
                    return slots[i].transform as RectTransform;
            }

            return null;
        }

        public void Refresh()
        {
            BountyManager bounty = battle != null ? battle.Bounty : null;
            if (bounty == null)
                return;

            for (int i = 0; i < slots.Count; i++)
            {
                UIBountySlot slot = slots[i];
                if (slot == null)
                    continue;

                int grade = slot.Grade;
                slot.Bind(
                    definition: bounty.GetDefinition(grade),
                    hp: bounty.GetHp(grade),
                    unlocked: bounty.IsSelectable(grade),
                    onClick: OnSlotClicked);
            }

            // 아이콘은 런타임에 채운다 — 굽는 시점에는 재화 메타데이터가 없을 수 있다.
            SetHeaderIcon(spIcon, BattlePointType.SummonPoint);
            SetHeaderIcon(stoneIcon, BattlePointType.EnhanceStone);

            if (battle.BattlePoints != null)
            {
                spText.SetText(ShortNumberFormat.Format(battle.BattlePoints.Get(OJ.BattlePointType.SummonPoint)));
                stoneText.SetText(ShortNumberFormat.Format(battle.BattlePoints.Get(OJ.BattlePointType.EnhanceStone)));
            }
        }

        private static void SetHeaderIcon(Image icon, BattlePointType kind)
        {
            if (icon == null || icon.sprite != null)
                return;

            icon.sprite = BattlePointUtility.GetIcon(kind);
            icon.enabled = icon.sprite != null;
        }

        private void OnSlotClicked(int grade)
        {
            BountyManager bounty = battle.Bounty;
            if (bounty == null || !bounty.TrySummon(grade))
                return;

            // 소환됐다. 창을 열어 둘 이유가 없다 — 쿨타임이 시작됐고 몬스터는 이미 필드에 있다.
            Exit();
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        private static readonly Vector2 PanelVisible = new Vector2(980f, 1010f);
        private const float CardGapX = 18f;
        private const float CardGapY = 18f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UIBountySelectDialog Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIBountySelectDialog", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIBountySelectDialog>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

            // 화면 전체를 덮는 어두운 막. 바깥을 누르면 닫힌다.
            Image blocker = UIBountyUIFactory.CreateImage("OutsideCatcher", view.transform, new Color(0f, 0f, 0f, 0.6f));
            UIBountyUIFactory.Stretch(blocker.rectTransform);
            dialog.outsideButton = blocker.gameObject.AddComponent<Button>();
            dialog.outsideButton.targetGraphic = blocker;
            dialog.outsideButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(dialog.outsideButton);

            // 판. Ui_Popup_Bg 는 사방 3px 투명 여백이 있어 x4 로 12px 씩 키운다.
            Image panelImage = UIBountyUIFactory.CreateSprite("Panel", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_Bg"), 4f,
                PanelVisible + new Vector2(24f, 24f), Vector2.zero, true);

            // 판은 클릭을 먹어야 한다. 안 그러면 카드 사이 틈을 누른 탭이 뒤의 막에 닿아 창이 닫힌다.
            panelImage.raycastTarget = true;
            RectTransform panel = panelImage.rectTransform;
            float top = PanelVisible.y / 2f;

            // 헤더 — 가진 SP · 마석. 글자는 아이콘 오른쪽에 둔다.
            dialog.spIcon = CreateHeaderIcon("SpIcon", panel, BattlePointType.SummonPoint, new Vector2(-300f, top - 52f));
            dialog.spText = UIBountyUIFactory.CreateText("SpText", panel, "0", 40f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.spText.rectTransform, new Vector2(200f, 56f), new Vector2(-190f, top - 52f));

            dialog.stoneIcon = CreateHeaderIcon("StoneIcon", panel, BattlePointType.EnhanceStone, new Vector2(40f, top - 52f));
            dialog.stoneText = UIBountyUIFactory.CreateText("StoneText", panel, "0", 40f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.stoneText.rectTransform, new Vector2(200f, 56f), new Vector2(150f, top - 52f));

            // 안내 한 줄 — 쿨타임이 있다는 것을 고르기 전에 알린다.
            TMP_Text hint = UIBountyUIFactory.CreateText("Hint", panel,
                "고르면 바로 나와요. 다음 현상금은 " + (int)BountyFormula.CooldownSeconds + "초 뒤에 부를 수 있어요.", 26f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(hint.rectTransform, new Vector2(900f, 38f), new Vector2(0f, top - 108f));

            // 카드 3x2. 등급 1~5 를 왼쪽 위부터 채우고 마지막 칸은 비운다.
            float cardW = UIBountySlot.VisibleSize.x;
            float cardH = UIBountySlot.VisibleSize.y;
            float firstX = -(cardW + CardGapX);
            float firstY = top - 150f - cardH * 0.5f;

            for (int grade = 1; grade <= BountyFormula.GradeCount; grade++)
            {
                int index = grade - 1;
                int col = index % 3;
                int row = index / 3;
                var position = new Vector2(
                    firstX + col * (cardW + CardGapX),
                    firstY - row * (cardH + CardGapY));

                dialog.slots.Add(UIBountySlot.Create(panel, grade, position, font, plainMaterial));
            }

            // 닫기 — 오른쪽 위 모서리.
            dialog.closeButton = CreateCloseButton(panel, new Vector2(PanelVisible.x * 0.5f - 52f, top - 52f), font);
            dialog.AddExitButton(dialog.closeButton);

            return dialog;
        }

        private static Image CreateHeaderIcon(string name, Transform parent, BattlePointType kind, Vector2 position)
        {
            Image icon = UIBountyUIFactory.CreateImage(name, parent, Color.white);
            UIBountyUIFactory.SetRect(icon.rectTransform, new Vector2(56f, 56f), position);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            // 스프라이트는 비워 둔다 — 런타임 Refresh 가 채운다. 그 전에는 흰 사각형이 비치지 않게 끈다.
            icon.enabled = false;
            return icon;
        }

        private static Button CreateCloseButton(Transform parent, Vector2 position, TMP_FontAsset font)
        {
            Image image = UIBountyUIFactory.CreateSprite("CloseButton", parent,
                UIBountyUIFactory.LoadSprite("Ingame/Btn_Gray"), 3f, new Vector2(64f + 24f, 64f + 24f), position, true);
            image.raycastTarget = true;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text label = UIBountyUIFactory.CreateText("Label", image.transform, "X", 40f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.Stretch(label.rectTransform);
            return button;
        }
    }
}
