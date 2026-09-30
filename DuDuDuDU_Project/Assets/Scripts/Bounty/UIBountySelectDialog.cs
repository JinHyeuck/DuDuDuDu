using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Core;
using OJ.DI;
using OJ.UI;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금 선택 창. 다이스 보드 자리에 가로 한 줄 여섯 장, <b>첫 장이 "사냥 안하기"</b> 이고
    /// 나머지 다섯이 등급이다(시안 <c>Art/Layout/Wanted_Layout.png</c>).
    ///
    /// <b>왜 첫 장이 사냥 안하기 인가.</b> 이 시스템의 실제 결정은 "몇 등급을 켤까" 가 아니라
    /// <b>"이번 판은 여기서 멈출까"</b> 다. 머지가 꼬였을 때 끄는 것이 유일한 탈출구인데,
    /// 그것을 목록 끝에 두거나 별도 버튼으로 빼면 급할 때 못 찾는다.
    ///
    /// <b>잠긴 칸도 내용을 다 보여준다.</b> 체력과 보상을 가리면 "다음에 뭘 노릴까" 를
    /// 계획할 수 없어서, 순서대로 잡는 것 말고 할 일이 없어진다. 가리는 것은 <b>누를 수
    /// 있음</b> 뿐이다.
    ///
    /// <b>이 프리팹은 코드로 굽는다.</b> <see cref="Create"/> 가 정본이고
    /// <c>OJ/개발/현상금/UI 프리팹 굽기</c> 가 같은 경로에 저장한다.
    /// </summary>
    public class UIBountySelectDialog : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [Header("Layout")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button outsideButton;

        [SerializeField] private List<UIBountySlot> slots = new List<UIBountySlot>();

        protected override void OnEnter()
        {
            // 배너 위에 오도록 맨 마지막 형제로 올린다. 둘 다 UIService 의 같은 캔버스에
            // 붙는데, 그 캔버스 안의 순서는 <b>만들어진 순서</b>라 어느 쪽이 먼저 열렸는지에
            // 따라 배너가 팝업을 덮을 수 있다.
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Refresh()
        {
            BountyManager bounty = battle.Bounty;
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
                    selected: bounty.SelectedGrade == grade,
                    unlocked: bounty.IsSelectable(grade),
                    onClick: OnSlotClicked);
            }
        }

        private void OnSlotClicked(int grade)
        {
            BountyManager bounty = battle.Bounty;
            if (bounty == null || !bounty.Select(grade))
                return;

            // 고르면 닫는다. 창을 열어 둔 채 갱신만 하면 "골랐다"가 화면에 남지 않아
            // 한 번 더 누르게 된다 — 그러면 같은 칸을 두 번 눌러 아무 일도 안 일어난다.
            Refresh();
            Exit();
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // 시안: Art/Layout/Wanted_Layout(2).png — 수치는 Docs/WantedUIArtPort.md
        // ──────────────────────────────────────────────────────────────

        /// <summary>판(Ui_Popup_Bg x4)의 보이는 사각형 — 시안 x27~1057 · y1301~1722. 다이스 보드를 덮는 자리다.</summary>
        private static readonly Vector2 PanelCenter = new Vector2(542f, 1511.5f);
        private static readonly Vector2 PanelVisible = new Vector2(1031f, 422f);

        /// <summary>카드 첫 장의 보이는 중심과 간격. 여섯 장이 160px 간격으로 선다.</summary>
        private const float FirstCardX = 140.5f;
        private const float CardSpacing = 160f;
        private const float CardY = 1549.5f;

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

            // 화면 전체를 덮는 투명 버튼. 바깥을 누르면 닫힌다. 시안에 어둡게 깔린 막이
            // 없어서 투명이다 — alpha 0 인 Image 는 안 그려지지만 레이캐스트는 받는다.
            Image blocker = UIBountyUIFactory.CreateImage("OutsideCatcher", view.transform, new Color(0f, 0f, 0f, 0f));
            UIBountyUIFactory.Stretch(blocker.rectTransform);
            dialog.outsideButton = blocker.gameObject.AddComponent<Button>();
            dialog.outsideButton.targetGraphic = blocker;
            dialog.outsideButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(dialog.outsideButton);

            // 판. Ui_Popup_Bg 는 사방 3px 투명 여백이 있어 x4 로 12px 씩 키운다.
            Image panelImage = UIBountyUIFactory.CreateSprite("Panel", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_Bg"), 4f,
                PanelVisible + new Vector2(24f, 24f), UIBountyUIFactory.Pos(PanelCenter.x, PanelCenter.y), true);

            // 판은 클릭을 먹어야 한다. 안 그러면 카드 사이 틈을 누른 탭이 뒤의 투명 막에 닿아 창이 닫힌다.
            panelImage.raycastTarget = true;
            dialog.panel = panelImage.rectTransform;

            TMP_Text title = UIBountyUIFactory.CreateText("Title", dialog.panel, "현상금", 40f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(title.rectTransform, new Vector2(400f, 60f),
                UIBountyUIFactory.Local(PanelCenter.x, 1360f, PanelCenter));

            // 카드 받침(Ui_Popup_SmallBox x4, 636481). 보이는 978x298 · x52~1029 · y1399~1696.
            // 여백이 왼·위 6 · 오른·아래 7px 이라 Image 중심이 보이는 중심보다 (2, 2) 치우친다.
            Image tray = UIBountyUIFactory.CreateSprite("Tray", dialog.panel,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 4f,
                new Vector2(978f + 52f, 298f + 52f),
                UIBountyUIFactory.Local(540.5f + 2f, 1547.5f + 2f, PanelCenter), true);
            tray.color = UIBountyUIFactory.Hex(0x636481);

            // 칸 여섯 개. 인덱스 0 이 "사냥 안하기"(등급 0)이고 1..5 가 등급이다.
            for (int index = 0; index <= BountyFormula.GradeCount; index++)
            {
                var center = new Vector2(FirstCardX + CardSpacing * index, CardY);
                UIBountySlot slot = UIBountySlot.Create(dialog.panel, index, center, PanelCenter, font, plainMaterial);
                dialog.slots.Add(slot);
            }

            return dialog;
        }
    }
}
