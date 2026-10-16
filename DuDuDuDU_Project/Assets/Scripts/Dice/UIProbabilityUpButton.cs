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
    /// (<see cref="SummonStarRules"/>). 라벨은 두 줄: 지금 레벨 / 다음 비용(최대면 MAX).
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
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;

        private static readonly Color ReadyColor = Color.white;
        private static readonly Color DimColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private string lastText;

        protected override void OnLoad()
        {
            button.onClick.AddListener(OnClick);
        }

        protected override void OnEnter()
        {
            lastText = null;
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

            string text = cost < 0
                ? "확률UP Lv." + level + "\nMAX"
                : "확률UP Lv." + level + "\n" + cost + " SP";

            if (text != lastText)
            {
                label.SetText(text);
                lastText = text;
            }

            bool can = cost >= 0 && battle.BattlePoints.Get(OJ.BattlePointType.SummonPoint) >= cost;
            button.interactable = can;
            background.color = can ? ReadyColor : DimColor;
        }

        private void OnClick()
        {
            battle.Summon.TryUpgradeProbability();
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────

        // 삭제 버튼과 같은 자리·같은 크기다(BattleScene 의 DiceRemove: 오른쪽 아래 기준).
        private static readonly Vector2 Size = new Vector2(274f, 106f);
        private static readonly Vector2 Position = new Vector2(-204.6f, 90.7f);

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

            // 킷 버튼(32², 여백 사방 4px)을 x3 로 키워 보이는 크기에 맞춘다.
            Image image = UIBountyUIFactory.CreateSprite("Background", view.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green"), 3f, Size, Vector2.zero, true);
            image.raycastTarget = true;
            dialog.background = image;

            dialog.button = image.gameObject.AddComponent<Button>();
            dialog.button.targetGraphic = image;

            dialog.label = UIBountyUIFactory.CreateText("Label", image.transform, "확률UP Lv.1\n30 SP", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.label.rectTransform, new Vector2(Size.x - 24f, Size.y - 20f), new Vector2(0f, 3f));
            dialog.label.lineSpacing = -8f;

            return dialog;
        }
    }
}
