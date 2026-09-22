using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.Relic;
using OJ.UI;

namespace OJ.Stage
{
    /// <summary>
    /// 소탕 횟수를 고르는 창.
    ///
    /// <code>
    ///            소탕
    ///        N 스테이지
    ///   [MIN] [-]  12  [+] [MAX]
    ///      고기 60 / 보유 340
    ///     [취소]        [소탕]
    /// </code>
    ///
    /// <b>왜 버튼에서 바로 안 주고 창을 한 번 끼우나.</b> 소탕은 고기를 먹는다
    /// (<see cref="SweepRules.StaminaCostPerSweep"/>). 되돌릴 수 없는 소모를 한 번의
    /// 오터치로 끝내면 안 되고, 몇 회를 돌지도 유저가 정해야 한다.
    ///
    /// <b>지불과 지급이 여기 한곳에 있다.</b> 창이 횟수만 돌려주고 버튼이 지불하는 구조로
    /// 두면 "창이 보여 준 비용"과 "버튼이 뺀 비용"이 두 자리에 생긴다. 표시와 결제가
    /// 어긋나는 사고는 그 틈에서만 난다.
    /// </summary>
    public class UISweepCountDialog : DialogBase
    {
        private static readonly Color OverlayColor = new Color(0.015f, 0.025f, 0.08f, 0.86f);
        private static readonly Color PanelColor = new Color(0.075f, 0.10f, 0.22f, 1f);
        private static readonly Color PanelInnerColor = new Color(0.10f, 0.15f, 0.29f, 1f);
        private static readonly Color CountBoxColor = new Color(0.05f, 0.08f, 0.18f, 1f);
        private static readonly Color CyanColor = new Color(0.15f, 0.80f, 0.95f, 1f);
        private static readonly Color OrangeColor = new Color(0.95f, 0.38f, 0.16f, 1f);
        private static readonly Color MutedTextColor = new Color(0.70f, 0.78f, 0.90f, 1f);
        private static readonly Color ShortageColor = new Color(1f, 0.42f, 0.42f, 1f);

        /// <summary>
        /// 이 창과 <see cref="UISweepLobbyButton"/> 이 화면에 낼 수 있는 <b>모든</b> 글자.
        /// <c>SweepCountDialogPrefabBaker</c> 가 굽기 전에 폰트 아틀라스와 대조한다.
        ///
        /// <b>왜 이런 목록이 필요한가.</b> 프로젝트 UI 폰트(<c>BMHANNAProOTF SDF</c>)는
        /// <b>Static</b> 이고 폴백이 비어 있다 — 아틀라스에 없는 글자는 예외도 로그도 없이
        /// <b>빈칸</b>으로 나온다. 실제로 이 창의 "소탕" 에서 '탕'(U+D0D5) 이 그렇게 사라졌다.
        /// 문자 집합은 <c>BMHANNAProOTF_CharacterSet.txt</c> 가 정본이고, 여기 없는 글자를
        /// 쓰려면 그 파일에 더한 뒤 아틀라스를 다시 구워야 한다.
        ///
        /// 서식 문자열로 조립되는 말(<c>"{0} 스테이지"</c> 따위)은 구워진 계층을 훑어서는
        /// 안 보이므로 여기에 손으로 모은다. <b>새 문구를 넣으면 여기도 같이 늘릴 것.</b>
        /// </summary>
        public const string RequiredGlyphs =
            "소탕취스테이지클리어한가없다고기보유필요회" +
            "MINAX0123456789,/ －＋";

        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button minButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button maxButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmText;

        private int targetStageIndex;
        private int maxCount;
        private int selectedCount;

        public static UISweepCountDialog Create(Transform parent, TMP_FontAsset fontAsset)
        {
            GameObject root = CreateRect("UISweepCountDialog", parent);
            Stretch(root.GetComponent<RectTransform>());

            GameObject view = CreateRect("DialogView", root.transform);
            Stretch(view.GetComponent<RectTransform>());
            Image overlay = view.AddComponent<Image>();
            overlay.color = OverlayColor;
            overlay.raycastTarget = true;
            Button overlayButton = view.AddComponent<Button>();
            overlayButton.transition = Selectable.Transition.None;

            UISweepCountDialog dialog = root.AddComponent<UISweepCountDialog>();
            dialog.font = fontAsset;
            dialog.dialogView = view;
            dialog.UseBackBtn = true;
            dialog.Build(view.transform);
            dialog.AddExitButton(overlayButton);
            return dialog;
        }

        private void Build(Transform viewRoot)
        {
            panel = CreateImage("Panel", viewRoot, PanelColor).gameObject;
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(900f, 640f), Vector2.zero);

            TMP_Text title = CreateText("Title", panel.transform, "소탕", 48f, TextAlignmentOptions.Center, Color.white);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(800f, 80f));

            stageText = CreateText("StageText", panel.transform, string.Empty, 32f, TextAlignmentOptions.Center, MutedTextColor);
            SetAnchored(stageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(800f, 56f));

            // 횟수 줄. 스샷의 [MIN] [-] 수량 [+] [MAX] 배치다.
            minButton = CreateButton("MinButton", panel.transform, "MIN", PanelInnerColor, out _);
            SetAnchored(minButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-330f, 20f), new Vector2(150f, 100f));

            decreaseButton = CreateButton("DecreaseButton", panel.transform, "－", PanelInnerColor, out TMP_Text decreaseLabel);
            decreaseLabel.fontSize = 46f;
            SetAnchored(decreaseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-185f, 20f), new Vector2(110f, 100f));

            Image countBox = CreateImage("CountBox", panel.transform, CountBoxColor);
            SetAnchored(countBox.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(230f, 110f));

            countText = CreateText("CountText", countBox.transform, "1", 52f, TextAlignmentOptions.Center, Color.white);
            Stretch(countText.rectTransform);

            increaseButton = CreateButton("IncreaseButton", panel.transform, "＋", PanelInnerColor, out TMP_Text increaseLabel);
            increaseLabel.fontSize = 46f;
            SetAnchored(increaseButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(185f, 20f), new Vector2(110f, 100f));

            maxButton = CreateButton("MaxButton", panel.transform, "MAX", PanelInnerColor, out _);
            SetAnchored(maxButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(330f, 20f), new Vector2(150f, 100f));

            costText = CreateText("CostText", panel.transform, string.Empty, 30f, TextAlignmentOptions.Center, MutedTextColor);
            SetAnchored(costText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(800f, 56f));

            cancelButton = CreateButton("CancelButton", panel.transform, "취소", CyanColor, out _);
            SetAnchored(cancelButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-220f, 95f), new Vector2(380f, 115f));

            confirmButton = CreateButton("ConfirmButton", panel.transform, "소탕", OrangeColor, out confirmText);
            SetAnchored(confirmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(220f, 95f), new Vector2(380f, 115f));
        }

        /// <summary>
        /// 버튼 배선. 프리팹에는 델리게이트가 저장되지 않으므로 여기서 다시 붙인다.
        /// <see cref="DialogBase.Load"/> 가 최초 1회만 부른다.
        /// </summary>
        protected override void OnLoad()
        {
            base.OnLoad();

            if (minButton != null) minButton.onClick.AddListener(() => SetCount(1));
            if (decreaseButton != null) decreaseButton.onClick.AddListener(() => SetCount(selectedCount - 1));
            if (increaseButton != null) increaseButton.onClick.AddListener(() => SetCount(selectedCount + 1));
            if (maxButton != null) maxButton.onClick.AddListener(() => SetCount(maxCount));
            if (cancelButton != null) cancelButton.onClick.AddListener(Exit);
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        }

        protected override void OnEnter()
        {
            base.OnEnter();

            StageProgressManager progress = StageProgressManager.Instance;
            int lastCleared = progress != null ? progress.GetLastClearedStageIndex() : 0;
            targetStageIndex = SweepRules.ResolveTargetStageIndex(lastCleared);

            PointManager points = PointManager.Instance;
            int ownedStamina = points != null ? points.Get(PointType.Stamina) : 0;
            maxCount = SweepRules.ResolveMaxCount(ownedStamina);

            // 열 때는 최대치로 시작한다. 소탕은 "쌓인 것을 한 번에 턴다"가 기본 동작이라
            // 매번 MAX 를 한 번 더 누르게 하면 그 탭이 순수한 군더더기가 된다.
            SetCount(maxCount);
        }

        private void SetCount(int value)
        {
            selectedCount = SweepRules.ClampToMax(value, maxCount);
            Refresh();
        }

        private void Refresh()
        {
            PointManager points = PointManager.Instance;
            int ownedStamina = points != null ? points.Get(PointType.Stamina) : 0;
            bool canSweep = targetStageIndex >= 1 && selectedCount >= 1;

            if (stageText != null)
            {
                stageText.SetText(targetStageIndex >= 1
                    ? $"{targetStageIndex} 스테이지"
                    : "클리어한 스테이지가 없다");
            }

            if (countText != null)
                countText.SetText(selectedCount.ToString("#,##0"));

            if (costText != null)
            {
                // 0 회일 때도 1회분 비용을 적는다. "고기 5 / 보유 3" 이라고 보여야
                // 왜 못 누르는지가 한 줄로 읽힌다.
                int shownCount = canSweep ? selectedCount : 1;
                int cost = SweepRules.TotalStaminaCost(shownCount);
                costText.color = canSweep ? MutedTextColor : ShortageColor;
                costText.SetText($"고기 {cost:#,##0} / 보유 {ownedStamina:#,##0}");
            }

            if (confirmButton != null)
                confirmButton.interactable = canSweep;

            if (minButton != null) minButton.interactable = canSweep && selectedCount > 1;
            if (decreaseButton != null) decreaseButton.interactable = canSweep && selectedCount > 1;
            if (increaseButton != null) increaseButton.interactable = canSweep && selectedCount < maxCount;
            if (maxButton != null) maxButton.interactable = canSweep && selectedCount < maxCount;
        }

        private void Confirm()
        {
            if (targetStageIndex < 1 || selectedCount < 1)
                return;

            PointManager points = PointManager.Instance;
            if (points == null)
            {
                Debug.LogWarning("[소탕] PointManager 가 없다. 소탕을 돌리지 않는다.");
                return;
            }

            int cost = SweepRules.TotalStaminaCost(selectedCount);

            // 화면을 믿지 않고 여기서 한 번 더 뺀다. 창을 열어 둔 사이에 다른 경로가
            // 고기를 쓰면 maxCount 는 낡은 값이 된다 — TrySpend 만이 최종 판정이다.
            if (!points.TrySpend(PointType.Stamina, cost))
            {
                Debug.Log($"[소탕] 고기가 모자라 취소했다. 필요 {cost} / 보유 {points.Get(PointType.Stamina)}");
                Refresh();
                return;
            }

            int count = selectedCount;
            List<PointRewardEntry> rewards = SweepRules.BuildRewards(targetStageIndex, count);

            // 클리어와 같은 보상이므로 유물 보너스도 같이 탄다
            // (GameManager.ClearStage 와 같은 순서 — 지급 전에 곱한다).
            if (RelicManager.Instance != null)
                rewards = RelicManager.Instance.ApplyStageClearRewardBonus(rewards);

            PointRewardUtility.GrantRewards(rewards);

            Debug.Log($"Sweep Stage {targetStageIndex} x{count} | 고기 -{cost} | " +
                      $"{PointRewardUtility.BuildRewardSummary(rewards)}");

            // 결과창을 이 창 위에 겹치지 않는다. 먼저 닫고 띄운다.
            Exit();

            UIRewardResultDialog resultDialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (resultDialog != null)
                resultDialog.Open(rewards, $"스테이지 {targetStageIndex} 소탕 {count}회");
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = CreateRect(name, parent);
            Image image = gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private TMP_Text CreateText(
            string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Color color)
        {
            GameObject gameObject = CreateRect(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            if (font != null)
                text.font = font;
            return text;
        }

        private Button CreateButton(string name, Transform parent, string label, Color color, out TMP_Text text)
        {
            Image image = CreateImage(name, parent, color);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.90f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.42f, 0.45f, 0.52f, 0.75f);
            button.colors = colors;
            text = CreateText("Label", button.transform, label, 34f, TextAlignmentOptions.Center, Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 size, Vector2 anchoredPosition)
        {
            SetAnchored(rectTransform, new Vector2(0.5f, 0.5f), anchoredPosition, size);
        }

        private static void SetAnchored(RectTransform rectTransform, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }
    }
}
