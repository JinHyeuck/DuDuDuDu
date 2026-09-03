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
    /// 현상금 선택 창. 가로 3칸 세로 2줄, <b>첫 칸이 "소환 X"</b> 이고 나머지 다섯이 등급이다.
    ///
    /// <b>왜 첫 칸이 소환 X 인가.</b> 이 시스템의 실제 결정은 "몇 등급을 켤까" 가 아니라
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
        [SerializeField] private Button closeButton;

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
        // 값을 아는 것은 코드이므로 인스펙터에 좌표를 옮겨 적지 않는다
        // (UIBattleDiceDetailPanel 과 같은 이유).
        // ──────────────────────────────────────────────────────────────

        private static readonly Color PanelColor = new Color(0.086f, 0.11f, 0.23f, 0.98f);
        private static readonly Color PanelEdgeColor = new Color(0.42f, 0.56f, 0.85f, 1f);
        private static readonly Color CloseColor = new Color(0.62f, 0.24f, 0.28f, 1f);

        // 칸 여섯 개가 들어가는 판의 크기. 3x2 라서 한 칸이 (Width - 좌우여백 - 가로틈 2)/3 이다.
        // 이 세 수를 고치면 SlotWidth/SlotHeight 가 따라 바뀌므로 아래 식을 그대로 둘 것.
        private const float PanelWidth = 1000f;
        private const float PanelHeight = 700f;
        private const float PanelPadding = 24f;
        private const float SlotGap = 16f;
        private const float HeaderHeight = 60f;

        private const float SlotWidth = (PanelWidth - PanelPadding * 2f - SlotGap * 2f) / 3f;
        private const float SlotHeight = (PanelHeight - PanelPadding * 2f - SlotGap - HeaderHeight) / 2f;

        /// <summary>
        /// 판이 놓이는 높이. 화면 중상위의 빈 영역 —
        /// 위로는 웨이브 게이지, 아래로는 벽과 다이스 보드 사이다.
        /// 1080x1920 기준이며 <c>UIService</c> 의 팝업 캔버스가 같은 해상도로 스케일한다.
        /// </summary>
        private const float PanelCenterY = 300f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UIBountySelectDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIBountySelectDialog", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIBountySelectDialog>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

            // 화면 전체를 덮는 투명 버튼. 바깥을 누르면 닫힌다.
            // alpha 0 인 Image 는 그려지지 않지만 레이캐스트는 받는다 — 빼면 클릭이 뒤로 샌다.
            Image blocker = UIBountyUIFactory.CreateImage("OutsideCatcher", view.transform, new Color(0f, 0f, 0f, 0.55f));
            UIBountyUIFactory.Stretch(blocker.rectTransform);
            dialog.outsideButton = blocker.gameObject.AddComponent<Button>();
            dialog.outsideButton.targetGraphic = blocker;
            dialog.outsideButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(dialog.outsideButton);

            // 테두리는 판의 <b>형제</b>여야 한다. 자식으로 넣으면 uGUI 가 부모 위에 그려서
            // 테두리 색이 칸 여섯 개를 통째로 덮는다.
            Image edge = UIBountyUIFactory.CreateImage("Edge", view.transform, PanelEdgeColor);
            UIBountyUIFactory.SetRect(edge.rectTransform,
                new Vector2(PanelWidth + 8f, PanelHeight + 8f), new Vector2(0f, PanelCenterY));
            edge.raycastTarget = false;

            Image panelImage = UIBountyUIFactory.CreateImage("Panel", view.transform, PanelColor);
            UIBountyUIFactory.SetRect(panelImage.rectTransform,
                new Vector2(PanelWidth, PanelHeight), new Vector2(0f, PanelCenterY));
            dialog.panel = panelImage.rectTransform;

            float headerY = PanelHeight * 0.5f - PanelPadding - HeaderHeight * 0.5f;

            TMP_Text title = UIBountyUIFactory.CreateText("Title", dialog.panel, "현상금 몬스터", 40f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(title.rectTransform,
                new Vector2(PanelWidth - PanelPadding * 2f - 80f, HeaderHeight),
                new Vector2(-40f, headerY));

            Image close = UIBountyUIFactory.CreateImage("CloseButton", dialog.panel, CloseColor);
            UIBountyUIFactory.SetRect(close.rectTransform, new Vector2(64f, 64f),
                new Vector2(PanelWidth * 0.5f - PanelPadding - 32f, headerY));
            dialog.closeButton = close.gameObject.AddComponent<Button>();
            dialog.closeButton.targetGraphic = close;
            dialog.AddExitButton(dialog.closeButton);

            TMP_Text closeLabel = UIBountyUIFactory.CreateText("Label", close.transform, "X", 36f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(closeLabel.rectTransform, new Vector2(64f, 64f), Vector2.zero);

            // 칸 여섯 개. 인덱스 0 이 "소환 X"(등급 0)이고 1..5 가 등급이다.
            // 왼쪽 위에서 오른쪽으로, 그다음 아랫줄 — 스크린샷의 배치 그대로다.
            float gridTop = headerY - HeaderHeight * 0.5f - SlotGap;

            for (int index = 0; index <= BountyFormula.GradeCount; index++)
            {
                int column = index % 3;
                int row = index / 3;

                float x = -(SlotWidth + SlotGap) + column * (SlotWidth + SlotGap);
                float y = gridTop - SlotHeight * 0.5f - row * (SlotHeight + SlotGap);

                UIBountySlot slot = UIBountySlot.Create(
                    dialog.panel, index, new Vector2(SlotWidth, SlotHeight), new Vector2(x, y), font);

                dialog.slots.Add(slot);
            }

            return dialog;
        }
    }
}
