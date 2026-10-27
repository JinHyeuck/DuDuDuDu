using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.UI;

namespace OJ.Dice
{
    /// <summary>
    /// 확률업 버튼. 하단 오른쪽 — 삭제 버튼이 있던 자리다. SP 를 내고 소환되는 다이스의 성급 확률을 올린다
    /// (<see cref="SummonStarRules"/>).
    ///
    /// 시안 <c>Art/Layout/Ingame_NewLayour.png</c> 의 "확률 UP!" — 주사위 + 초록 화살표 아이콘(x2) 아래 이름.
    /// 시안에는 비용이 없지만 SP 를 내는 버튼이라 비용 칩을 아이콘 아래 끝에 겹쳐 붙인다(가격표처럼). 레벨은 아이콘 왼쪽 위.
    /// 보드 아래 끝(1738)과 화면 아래 끝 사이 180 에 아이콘 · 칩 · 이름을 다 넣어야 해서 칩을 따로 한 줄로 두지 못한다.
    /// SP 가 모자라면 칩의 숫자가 붉다(소환 버튼과 같은 색).
    ///
    /// <b>여기에 규칙을 두지 않는다.</b> 올리는 일은 <c>UIDiceSummonSystem.TryUpgradeProbability</c> 가 하고,
    /// 이 창은 그 상태를 그리고 눌림을 넘길 뿐이다.
    ///
    /// <b>왜 다이얼로그인가.</b> 씬 <c>.unity</c> 를 손으로 편집하지 않는 규칙(AGENTS 절대 규칙 3)이라
    /// <c>UIWaveRewindButton</c> 과 같은 방식으로 프리팹을 구워 카탈로그에 올린다.
    /// 되돌리기가 <c>RunState</c> 를 직접 고쳐도 어긋나지 않도록 <b>매 프레임 다시 그린다</b>(글자가 같으면 쓰지 않는다).
    /// </summary>
    public sealed class UIProbabilityUpButton : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image chipBackground;
        [SerializeField] private TMP_Text chipText;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite maxSprite;

        private static readonly Color DimColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private int shownLevel = -1;
        private int shownCost = int.MinValue;
        private int shownState = -1;

        protected override void OnLoad()
        {
            button.onClick.AddListener(OnClick);
        }

        protected override void OnEnter()
        {
            shownLevel = -1;
            shownCost = int.MinValue;
            shownState = -1;
            Refresh();
        }

        private void Update()
        {
            if (_isEnter)
                Refresh();
        }

        private void Refresh()
        {
            UIDiceSummonSystem summon = battle != null ? battle.Summon : null;
            if (summon == null || battle.BattlePoints == null)
                return;

            int level = summon.ProbLevel;
            int cost = summon.NextProbUpgradeCost;

            if (level != shownLevel)
            {
                levelText.SetText("Lv.{0}", level);
                shownLevel = level;
            }

            if (cost != shownCost)
            {
                if (cost < 0)
                    chipText.SetText("MAX");
                else
                    chipText.SetText("{0} SP", cost);
                shownCost = cost;
            }

            // 0 = 올릴 수 있다, 1 = SP 모자람, 2 = 최대.
            int state = cost < 0 ? 2 : battle.BattlePoints.Get(OJ.BattlePointType.SummonPoint) >= cost ? 0 : 1;
            if (state == shownState)
                return;

            shownState = state;
            button.interactable = state == 0;
            icon.color = state == 0 ? Color.white : DimColor;
            chipBackground.sprite = state == 2 ? maxSprite : readySprite;
            chipText.color = state == 1 ? UIDiceSummonSystem.LackingSpColor : Color.white;
        }

        private void OnClick()
        {
            battle.Summon.TryUpgradeProbability();
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────

        // 시안 아이콘 (885,1748)~(1001,1870) — 가운데 x 943, 오른쪽에서 137. 누르는 칸은 화면 y 1745~1915 —
        // 위로 더 키우면 보드 아래 줄의 칸 끝을 이 버튼이 가로챈다(팝업 캔버스가 보드 위에 있다).
        // 아래 좌표는 모두 화면 y 를 버튼 가운데(1830) 기준으로 옮긴 것이다.
        private static readonly Vector2 Size = new Vector2(160f, 170f);
        private static readonly Vector2 Position = new Vector2(-137f, 90f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UIProbabilityUpButton Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIProbabilityUpButton", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Size;
            rootRect.anchoredPosition = Position;

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIProbabilityUpButton>();
            dialog.dialogView = view;
            dialog.UseBackBtn = false;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            Image hit = UIBountyUIFactory.CreateImage("Hit", view.transform, new Color(1f, 1f, 1f, 0f));
            UIBountyUIFactory.Stretch(hit.rectTransform);
            dialog.button = hit.gameObject.AddComponent<Button>();
            dialog.button.targetGraphic = hit;
            dialog.button.transition = Selectable.Transition.None;

            // 아이콘 — 64 그림 x2(시안 실측: 불투명 58x61 → 116x122).
            dialog.icon = UIBountyUIFactory.CreateImage("Icon", view.transform, Color.white);
            dialog.icon.sprite = UIBountyUIFactory.LoadSprite("Ingame/Icon_ProbabilityUp");
            dialog.icon.preserveAspect = true;
            dialog.icon.raycastTarget = false;
            UIBountyUIFactory.SetRect(dialog.icon.rectTransform, new Vector2(128f, 128f), new Vector2(0f, 35f));   // 화면 y 1795

            dialog.levelText = UIBountyUIFactory.CreateText("Level", view.transform, "Lv.1", 24f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.levelText.rectTransform, new Vector2(90f, 28f), new Vector2(-38f, 66f));   // 1764

            TMP_Text nameText = UIBountyUIFactory.CreateText("Name", view.transform, "확률 UP!", 24f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(nameText.rectTransform, new Vector2(Size.x + 20f, 34f), new Vector2(0f, -62f));   // 1892

            dialog.readySprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green");
            dialog.maxSprite = UIBountyUIFactory.LoadSprite("Ingame/Btn_Gray");

            dialog.chipBackground = UIBountyUIFactory.CreateSprite("Chip", view.transform,
                dialog.readySprite, 3f, new Vector2(106f + 24f, 36f + 24f), new Vector2(0f, -20f), true);   // 1850
            dialog.chipBackground.raycastTarget = false;

            dialog.chipText = UIBountyUIFactory.CreateText("ChipText", dialog.chipBackground.transform,
                "30 SP", 26f, TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.chipText.rectTransform, new Vector2(106f, 36f), new Vector2(0f, 2f));

            return dialog;
        }
    }
}
