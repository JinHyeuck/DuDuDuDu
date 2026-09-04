using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.UI;
using OJ.Utils;

namespace OJ.IdleReward
{
    /// <summary>
    /// 방치 보상 창. (MIGRATION_BASELINE 10.5)
    ///
    /// <b>이 창만 UI 를 코드로 지었다.</b> 그래서 세 가지가 나머지 16개와 달랐다 —
    /// 프리팹이 없어 카탈로그에 못 올리고, 레이아웃을 인스펙터에서 볼 수 없고,
    /// 폰트를 씬에서 아무 TMP_Text 나 주워 와야 했다. 마지막 것은 못 찾으면 null 을
    /// 돌려줘서 <b>기본 폰트(라틴 전용)로 떨어지고 한글이 네모로 뜬다.</b>
    ///
    /// 이제 <see cref="Create"/> 는 <b>에디터에서 프리팹을 굽는 용도로만</b> 쓴다.
    /// 게임에서는 다른 창과 똑같이 카탈로그에서 나온다.
    ///
    /// <b>버튼 배선은 <see cref="OnLoad"/> 에서 한다.</b> onClick.AddListener 로 붙인
    /// 델리게이트는 프리팹에 직렬화되지 않는다 — 구울 때 붙여 봐야 저장되지 않으므로
    /// 런타임에 다시 붙여야 한다. 구조를 만드는 일(굽기)과 동작을 붙이는 일(실행)을
    /// 나눈 것이 이 클래스의 핵심이다.
    /// </summary>
    public class UIIdleRewardDialog : DialogBase
    {
        private static readonly Color OverlayColor = new Color(0.015f, 0.025f, 0.08f, 0.86f);
        private static readonly Color PanelColor = new Color(0.075f, 0.10f, 0.22f, 1f);
        private static readonly Color PanelInnerColor = new Color(0.10f, 0.15f, 0.29f, 1f);
        private static readonly Color CyanColor = new Color(0.15f, 0.80f, 0.95f, 1f);
        private static readonly Color YellowColor = new Color(1f, 0.72f, 0.10f, 1f);
        private static readonly Color MutedTextColor = new Color(0.70f, 0.78f, 0.90f, 1f);

        // 전부 [SerializeField] 여야 한다. 프리팹으로 구울 때 이 참조들이 함께 저장되고
        // 런타임에는 다시 찾지 않는다. 하나라도 빠뜨리면 그 부분만 조용히 죽는다.
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject autoView;
        [SerializeField] private GameObject meatView;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button autoTabButton;
        [SerializeField] private Button meatTabButton;
        [SerializeField] private TMP_Text autoTabText;
        [SerializeField] private TMP_Text meatTabText;

        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text autoTimerText;
        [SerializeField] private TMP_Text autoGuideText;
        [SerializeField] private Image autoProgressFill;
        [SerializeField] private RectTransform autoRewardRoot;
        [SerializeField] private Button autoClaimButton;
        [SerializeField] private TMP_Text autoClaimText;

        [SerializeField] private TMP_Text meatStoredText;
        [SerializeField] private TMP_Text meatTimerText;
        [SerializeField] private List<Image> meatSetSlots = new List<Image>();
        [SerializeField] private Button meatClaimButton;
        [SerializeField] private TMP_Text meatClaimText;

        private bool showingAuto = true;
        private float nextRefreshTime;
        private string rewardSignature = string.Empty;

        /// <summary>
        /// 계층을 짓는다. <b>에디터에서 프리팹을 굽는 용도다</b> — 게임에서는 부르지 않는다.
        ///
        /// 폰트를 인자로 받는다. 예전에는 씬에서 아무 TMP_Text 나 주워 왔는데, 그 방식은
        /// 무엇을 집을지 씬 구성에 달려 있고 못 찾으면 조용히 null 이 된다.
        /// 굽는 쪽이 어떤 폰트인지 알고 있으므로 넘겨주는 것이 맞다.
        ///
        /// 루트에는 아무것도 그리지 않고 DialogView 자식이 딤과 패널을 갖는다.
        /// <see cref="DialogBase"/> 가 그 자식만 켜고 끄기 때문이다 — 루트에 딤을 두면
        /// 창이 닫혀 있어도 화면이 어두워진다.
        /// </summary>
        public static UIIdleRewardDialog Create(Transform parent, TMP_FontAsset fontAsset)
        {
            GameObject root = CreateRect("UIIdleRewardDialog", parent);
            Stretch(root.GetComponent<RectTransform>());

            GameObject view = CreateRect("DialogView", root.transform);
            Stretch(view.GetComponent<RectTransform>());
            Image overlay = view.AddComponent<Image>();
            overlay.color = OverlayColor;
            overlay.raycastTarget = true;
            Button overlayButton = view.AddComponent<Button>();
            overlayButton.transition = Selectable.Transition.None;

            UIIdleRewardDialog dialog = root.AddComponent<UIIdleRewardDialog>();
            dialog.font = fontAsset;
            dialog.dialogView = view;
            dialog.UseBackBtn = true;
            dialog.Build(view.transform);
            dialog.AddExitButton(overlayButton);
            return dialog;
        }

        /// <summary>
        /// 버튼 배선. 프리팹에는 델리게이트가 저장되지 않으므로 여기서 다시 붙인다.
        /// <see cref="DialogBase.Load"/> 가 최초 1회만 부른다.
        /// </summary>
        protected override void OnLoad()
        {
            base.OnLoad();

            if (closeButton != null) closeButton.onClick.AddListener(Exit);
            if (autoTabButton != null) autoTabButton.onClick.AddListener(() => ShowTab(true));
            if (meatTabButton != null) meatTabButton.onClick.AddListener(() => ShowTab(false));
            if (autoClaimButton != null) autoClaimButton.onClick.AddListener(ClaimAutoBattle);
            if (meatClaimButton != null) meatClaimButton.onClick.AddListener(ClaimMeat);
        }

        protected override void OnEnter()
        {
            base.OnEnter();
            ShowTab(showingAuto);
            Refresh(true);
        }

        private void OnEnable()
        {
            if (IdleRewardManager.Instance != null)
                IdleRewardManager.Instance.OnChanged += OnRewardChanged;
        }

        private void OnDisable()
        {
            if (IdleRewardManager.Instance != null)
                IdleRewardManager.Instance.OnChanged -= OnRewardChanged;
        }

        private void Update()
        {
            // Escape 처리를 지웠다. DialogBase + AOSBackBtnManager 스택이 담당한다 —
            // 여기서 따로 보면 팝업이 여러 개 떠 있을 때 이 창만 닫히거나, 스택 맨 위가
            // 아닌데도 반응하는 일이 생긴다.
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + 1f;
            Refresh(false);
        }

        private void OnRewardChanged()
        {
            Refresh(true);
        }

        private void Build(Transform viewRoot)
        {
            panel = CreateImage("Panel", viewRoot, PanelColor).gameObject;
            SetRect(panel.GetComponent<RectTransform>(), new Vector2(900f, 1320f), Vector2.zero);

            Image header = CreateImage("Header", panel.transform, new Color(0.09f, 0.45f, 0.70f, 1f));
            SetAnchored(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(900f, 190f));

            Image pigIcon = CreateImage("PigIcon", header.transform, Color.white);
            pigIcon.sprite = Resources.Load<Sprite>("Art/Main/Icon_Reward_Pig");
            pigIcon.preserveAspect = true;
            SetAnchored(pigIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(82f, 0f), new Vector2(125f, 125f));

            TMP_Text title = CreateText("Title", header.transform, "자동전투 보상 & 고기 축제", 45f, TextAlignmentOptions.Center, Color.white);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(55f, 0f), new Vector2(690f, 100f));

            closeButton = CreateButton("CloseButton", panel.transform, "×", new Color(0.83f, 0.25f, 0.25f, 1f), out TMP_Text closeText);
            closeText.fontSize = 55f;
            SetAnchored(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-55f, -55f), new Vector2(90f, 90f));

            autoTabButton = CreateButton("AutoTab", panel.transform, "자동전투 보상", CyanColor, out autoTabText);
            SetAnchored(autoTabButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-220f, -230f), new Vector2(420f, 100f));

            meatTabButton = CreateButton("MeatTab", panel.transform, "고기 축제", PanelInnerColor, out meatTabText);
            SetAnchored(meatTabButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(220f, -230f), new Vector2(420f, 100f));

            autoView = CreateRect("AutoBattleView", panel.transform);
            SetAnchored(autoView.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -115f), new Vector2(840f, 960f));
            BuildAutoView();

            meatView = CreateRect("MeatFestivalView", panel.transform);
            SetAnchored(meatView.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -115f), new Vector2(840f, 960f));
            BuildMeatView();
        }

        private void BuildAutoView()
        {
            stageText = CreateText("StageText", autoView.transform, string.Empty, 34f, TextAlignmentOptions.Center, Color.white);
            SetAnchored(stageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(800f, 60f));

            autoGuideText = CreateText("GuideText", autoView.transform, "마지막 클리어 스테이지 기준 · 시간당 3회", 27f, TextAlignmentOptions.Center, MutedTextColor);
            SetAnchored(autoGuideText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(800f, 52f));

            Image progressBack = CreateImage("ProgressBack", autoView.transform, new Color(0.03f, 0.06f, 0.13f, 1f));
            SetAnchored(progressBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -175f), new Vector2(760f, 45f));
            autoProgressFill = CreateImage("Fill", progressBack.transform, CyanColor);
            Stretch(autoProgressFill.rectTransform);
            autoProgressFill.type = Image.Type.Filled;
            autoProgressFill.fillMethod = Image.FillMethod.Horizontal;
            autoProgressFill.fillOrigin = 0;

            autoTimerText = CreateText("TimerText", autoView.transform, "00:00:00 / 08:00:00", 30f, TextAlignmentOptions.Center, Color.white);
            SetAnchored(autoTimerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(760f, 50f));

            GameObject viewport = CreateRect("RewardViewport", autoView.transform);
            SetAnchored(viewport.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -505f), new Vector2(780f, 480f));
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.04f, 0.07f, 0.15f, 0.85f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            GameObject content = CreateRect("RewardGrid", viewport.transform);
            autoRewardRoot = content.GetComponent<RectTransform>();
            autoRewardRoot.anchorMin = new Vector2(0f, 1f);
            autoRewardRoot.anchorMax = new Vector2(1f, 1f);
            autoRewardRoot.pivot = new Vector2(0.5f, 1f);
            autoRewardRoot.anchoredPosition = Vector2.zero;
            autoRewardRoot.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(18, 18, 18, 18);
            grid.spacing = new Vector2(14f, 14f);
            grid.cellSize = new Vector2(170f, 135f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = autoRewardRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            autoClaimButton = CreateButton("ClaimButton", autoView.transform, "보상 수령", YellowColor, out autoClaimText);
            SetAnchored(autoClaimButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(560f, 120f));
            autoClaimButton.onClick.AddListener(ClaimAutoBattle);
        }

        private void BuildMeatView()
        {
            TMP_Text guide = CreateText("Guide", meatView.transform, "6시간마다 고기 30개 · 최대 30세트 저장", 31f, TextAlignmentOptions.Center, MutedTextColor);
            SetAnchored(guide.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(810f, 70f));

            meatStoredText = CreateText("StoredText", meatView.transform, string.Empty, 42f, TextAlignmentOptions.Center, Color.white);
            SetAnchored(meatStoredText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -145f), new Vector2(800f, 80f));

            meatTimerText = CreateText("TimerText", meatView.transform, string.Empty, 29f, TextAlignmentOptions.Center, MutedTextColor);
            SetAnchored(meatTimerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(800f, 60f));

            GameObject gridObject = CreateRect("MeatSetGrid", meatView.transform);
            SetAnchored(gridObject.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0f, -515f), new Vector2(730f, 500f));
            GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(15f, 15f);
            grid.cellSize = new Vector2(108f, 68f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.MiddleCenter;

            Sprite staminaIcon = PointRewardUtility.GetPointIcon(PointType.Stamina);
            for (int i = 0; i < IdleRewardManager.MaxMeatSetCount; i++)
            {
                Image slot = CreateImage("MeatSet_" + (i + 1), gridObject.transform, PanelInnerColor);
                Image icon = CreateImage("Icon", slot.transform, Color.white);
                icon.sprite = staminaIcon;
                icon.preserveAspect = true;
                SetAnchored(icon.rectTransform, new Vector2(0.30f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
                TMP_Text amount = CreateText("Amount", slot.transform, "×30", 23f, TextAlignmentOptions.Center, Color.white);
                SetAnchored(amount.rectTransform, new Vector2(0.70f, 0.5f), Vector2.zero, new Vector2(58f, 50f));
                meatSetSlots.Add(slot);
            }

            meatClaimButton = CreateButton("ClaimButton", meatView.transform, "일괄 수령", YellowColor, out meatClaimText);
            SetAnchored(meatClaimButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 75f), new Vector2(560f, 120f));
            meatClaimButton.onClick.AddListener(ClaimMeat);
        }

        private void ShowTab(bool showAuto)
        {
            showingAuto = showAuto;
            autoView.SetActive(showAuto);
            meatView.SetActive(!showAuto);
            SetButtonColor(autoTabButton, showAuto ? CyanColor : PanelInnerColor);
            SetButtonColor(meatTabButton, showAuto ? PanelInnerColor : YellowColor);
            autoTabText.color = showAuto ? Color.white : MutedTextColor;
            meatTabText.color = showAuto ? MutedTextColor : Color.white;
            Refresh(true);
        }

        private void Refresh(bool forceCards)
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            if (manager == null)
                return;

            int stageIndex = manager.GetAutoBattleStageIndex();
            TimeSpan elapsed = manager.GetAutoBattleElapsed();
            List<PointRewardEntry> rewards = manager.GetAutoBattleRewards();

            stageText.SetText(stageIndex > 0 ? "기준 스테이지 {0}" : "클리어한 스테이지가 없습니다", stageIndex);
            autoGuideText.SetText(stageIndex > 0
                ? "마지막 클리어 스테이지 기준 · 시간당 3회"
                : "스테이지를 1회 클리어하면 보상이 쌓입니다");
            autoTimerText.SetText("누적 " + FormatTime(elapsed) + " / 08:00:00");
            autoProgressFill.fillAmount = manager.GetAutoBattleProgress01();
            autoClaimButton.interactable = rewards.Count > 0;
            autoClaimText.SetText(rewards.Count > 0 ? "보상 수령" : "누적 중");
            RefreshRewardCards(rewards, forceCards);

            int storedSets = manager.GetStoredMeatSetCount();
            int meatAmount = storedSets * IdleRewardManager.MeatPerSet;
            meatStoredText.SetText("저장 {0}/{1}세트  ·  고기 {2:#,##0}개", storedSets, IdleRewardManager.MaxMeatSetCount, meatAmount);
            meatTimerText.SetText(storedSets >= IdleRewardManager.MaxMeatSetCount
                ? "최대 저장량에 도달했습니다"
                : "다음 세트까지 " + FormatTime(manager.GetTimeUntilNextMeatSet()));
            meatClaimButton.interactable = storedSets > 0;
            meatClaimText.SetText(storedSets > 0 ? "일괄 수령  ×{0:#,##0}" : "고기 준비 중", meatAmount);

            for (int i = 0; i < meatSetSlots.Count; i++)
            {
                bool stored = i < storedSets;
                meatSetSlots[i].color = stored ? new Color(0.18f, 0.55f, 0.78f, 1f) : new Color(0.10f, 0.15f, 0.29f, 0.55f);
            }
        }

        private void RefreshRewardCards(IReadOnlyList<PointRewardEntry> rewards, bool force)
        {
            string signature = BuildRewardSignature(rewards);
            if (!force && signature == rewardSignature)
                return;

            rewardSignature = signature;
            for (int i = autoRewardRoot.childCount - 1; i >= 0; i--)
                Destroy(autoRewardRoot.GetChild(i).gameObject);

            if (rewards.Count == 0)
            {
                GameObject emptyCard = CreateImage("Empty", autoRewardRoot, new Color(0.10f, 0.15f, 0.29f, 0.65f)).gameObject;
                TMP_Text emptyText = CreateText("Text", emptyCard.transform, "보상 누적 중", 25f, TextAlignmentOptions.Center, MutedTextColor);
                Stretch(emptyText.rectTransform);
                return;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                PointRewardEntry reward = rewards[i];
                Image card = CreateImage(reward.PointType.ToString(), autoRewardRoot, new Color(0.13f, 0.30f, 0.47f, 1f));
                Image icon = CreateImage("Icon", card.transform, Color.white);
                icon.sprite = PointRewardUtility.GetPointIcon(reward.PointType);
                icon.preserveAspect = true;
                SetAnchored(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(75f, 75f));
                TMP_Text amount = CreateText("Amount", card.transform, "×" + reward.Amount.ToString("#,##0"), 25f, TextAlignmentOptions.Center, Color.white);
                SetAnchored(amount.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(160f, 42f));
            }
        }

        private void ClaimAutoBattle()
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            if (manager == null || !manager.TryClaimAutoBattle(out List<PointRewardEntry> rewards, out int stageIndex))
                return;

            ShowRewardResult(rewards, "스테이지 " + stageIndex + " 자동전투 보상을 획득했습니다.");
            Refresh(true);
        }

        private void ClaimMeat()
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            if (manager == null || !manager.TryClaimMeat(out int meatAmount, out int setCount))
                return;

            var rewards = new List<PointRewardEntry>
            {
                new PointRewardEntry(PointType.Stamina, meatAmount),
            };
            ShowRewardResult(rewards, "고기 " + setCount + "세트를 일괄 수령했습니다.");
            Refresh(true);
        }

        /// <summary>
        /// 보상 결과창을 띄운다. (10.4)
        ///
        /// 예전에는 씬을 <c>FindFirstObjectByType</c> 으로 뒤져 찾았다. 그 방식은
        /// <b>씬에 인스턴스가 상주해야만</b> 성립하는데, 팝업은 이제 필요할 때 만들어진다.
        /// 게다가 못 찾으면 조용히 <c>return</c> 이라, 보상을 줬는데 화면에 아무것도
        /// 안 뜨는 상태가 로그 없이 지나갔다.
        /// </summary>
        private void ShowRewardResult(IReadOnlyList<PointRewardEntry> rewards, string message)
        {
            UIRewardResultDialog resultDialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (resultDialog == null)
                return;

            // 팝업 루트 안에서 맨 위로 올린다. 다른 팝업이 이미 떠 있을 때 그 뒤에
            // 가리지 않게 하려는 것으로, 원래 코드가 하던 것과 같다.
            resultDialog.transform.SetAsLastSibling();
            resultDialog.Open(rewards, message);
        }

        private static string BuildRewardSignature(IReadOnlyList<PointRewardEntry> rewards)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                builder.Append((int)rewards[i].PointType);
                builder.Append(':');
                builder.Append(rewards[i].Amount);
                builder.Append('|');
            }
            return builder.ToString();
        }

        private static string FormatTime(TimeSpan time)
        {
            int totalHours = Mathf.FloorToInt((float)time.TotalHours);
            return string.Format("{0:00}:{1:00}:{2:00}", totalHours, time.Minutes, time.Seconds);
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

        private TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Color color)
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

        private static void SetButtonColor(Button button, Color color)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = color;
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
