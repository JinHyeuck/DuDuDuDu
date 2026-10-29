using System.Collections.Generic;
using System.Text;
using OJ.Core;
using OJ.Point;
using OJ.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Synergy
{
    /// <summary>
    /// 시너지 리롤 화면. 사용자 스케치(2026-10-28): 계열 4패널, 패널마다 프리셋 1~4 탭, 조건 줄·효과 3줄에 자물쇠,
    /// 안 열린 효과 줄은 "스테이지 20/60", 아래에 리롤 버튼과 비용, 오른쪽 위에 확률표 버튼.
    ///
    /// <b>Best 보정을 잘 보이게.</b> 사용자 요청이다 — 패널마다 "BEST 확률 x.xx% / 30%" 와 게이지를 두고,
    /// Best 수치인 줄은 금색 + (BEST) 로 표시한다.
    ///
    /// <b>프리팹은 뼈대만 굽는다.</b> 뿌리·dialogView·닫기 버튼만 프리팹에 있고 안의 내용은 <see cref="OnLoad"/> 에서
    /// 코드로 만든다. 확정 콘텐츠가 아니라서(사용자: "완전 픽스된 컨텐츠가 아니니") 시안이 나오면 갈아엎을 화면이고,
    /// 그때까지 배치를 코드 한 곳에서 고치려는 것이다. 굽기는 <c>SynergyInstaller</c> 가 한다.
    /// </summary>
    public sealed class UISynergyDialog : DialogBase
    {
        private const float PanelWidth = 1000f;
        private const float CardWidth = 960f;
        private const float CardHeight = 470f;
        private const float CardGap = 22f;
        private const float LineHeight = 58f;

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.78f);
        private static readonly Color PanelColor = new Color(0.12f, 0.1f, 0.17f, 1f);
        private static readonly Color CardColor = new Color(0.19f, 0.16f, 0.26f, 1f);
        private static readonly Color LineColor = new Color(0.09f, 0.08f, 0.13f, 1f);
        private static readonly Color LockedLineColor = new Color(0.07f, 0.07f, 0.09f, 1f);
        private static readonly Color TabColor = new Color(0.28f, 0.25f, 0.36f, 1f);
        private static readonly Color BestColor = new Color(1f, 0.83f, 0.25f, 1f);
        private static readonly Color MutedText = new Color(0.62f, 0.6f, 0.7f, 1f);
        private static readonly Color RerollColor = new Color(0.3f, 0.72f, 0.38f, 1f);
        private static readonly Color DisabledColor = new Color(0.32f, 0.32f, 0.36f, 1f);

        private sealed class LineView
        {
            public Image Back;
            public TMP_Text Text;
            public Button LockButton;
            public Image LockImage;
            public TMP_Text LockText;
        }

        private sealed class CardView
        {
            public SynergyCategory Category;
            public RectTransform Root;
            public readonly List<Image> Tabs = new List<Image>();
            public LineView Condition;
            public readonly List<LineView> Effects = new List<LineView>();
            public TMP_Text BestText;
            public Image BestFill;
            public Button RerollButton;
            public Image RerollImage;
            public TMP_Text RerollText;
            public GameObject LockedCover;
            public TMP_Text LockedText;
            public float FlashLeft;
            public int FlashLine = -2;
        }

        private readonly CardView[] cards = new CardView[SynergyRules.CategoryCount];
        private TMP_Text ticketText;
        private GameObject oddsPanel;
        private TMP_Text oddsText;
        private TMP_Text toastText;
        private float toastLeft;

        // ── 가이드가 짚는 자리 ─────────────────────────────────

        /// <summary>그 계열 카드의 리롤 버튼. 로비 가이드(<c>LobbyUnlockDirector</c>)가 가리킨다.</summary>
        public Button RerollButtonOf(SynergyCategory category) => cards[(int)category]?.RerollButton;

        /// <summary>그 계열 카드 전체.</summary>
        public RectTransform CardRectOf(SynergyCategory category) => cards[(int)category]?.Root;

        // ── 수명 ───────────────────────────────────────────────

        protected override void OnLoad()
        {
            Build((RectTransform)dialogView.transform);
        }

        protected override void OnEnter()
        {
            if (SynergyManager.Instance != null)
                SynergyManager.Instance.OnChanged += Refresh;
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            oddsPanel.SetActive(false);
            Refresh();
        }

        protected override void OnExit()
        {
            if (SynergyManager.Instance != null)
                SynergyManager.Instance.OnChanged -= Refresh;
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        private void OnPointChanged(PointType type, int value)
        {
            if (type == PointType.SynergyTicket)
                Refresh();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (toastLeft > 0f)
            {
                toastLeft -= dt;
                toastText.gameObject.SetActive(toastLeft > 0f);
            }

            foreach (CardView card in cards)
            {
                if (card == null || card.FlashLeft <= 0f)
                    continue;

                card.FlashLeft -= dt;
                if (card.FlashLeft <= 0f)
                {
                    card.FlashLine = -2;
                    Refresh();
                }
            }
        }

        // ── 만들기 ─────────────────────────────────────────────

        private void Build(RectTransform view)
        {
            Image dim = NewImage("Dim", view, null, DimColor);
            Stretch(dim.rectTransform);

            Image panel = NewImage("Panel", view, null, PanelColor);
            panel.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            panel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            panel.rectTransform.sizeDelta = new Vector2(PanelWidth, -120f);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            RectTransform p = panel.rectTransform;

            // 머리줄 — 제목, 보유 티켓, 확률표, 닫기.
            TMP_Text title = NewText("Title", p, "시너지", 54f, Color.white, TextAlignmentOptions.Left);
            TopRect(title.rectTransform, new Vector2(300f, 80f), new Vector2(40f + 150f, -60f));

            Sprite ticketIcon = PointRewardUtility.GetPointIcon(PointType.SynergyTicket);
            if (ticketIcon != null)
            {
                Image icon = NewImage("TicketIcon", p, ticketIcon, Color.white);
                icon.preserveAspect = true;
                TopRect(icon.rectTransform, new Vector2(56f, 56f), new Vector2(330f, -60f));
            }

            ticketText = NewText("Tickets", p, "0", 34f, Color.white, TextAlignmentOptions.Left);
            TopRect(ticketText.rectTransform, new Vector2(360f, 60f), new Vector2(370f + 180f, -60f));

            // 확률표는 오른쪽 위(스케치). 개발 빌드의 DEV 배지가 오른쪽 끝을 덮으므로 한 칸 안쪽에 둔다.
            Button odds = NewButton("Odds", p, TabColor, "확률", 30f, out _);
            TopRect((RectTransform)odds.transform, new Vector2(130f, 72f), new Vector2(PanelWidth - 230f, -60f));
            odds.onClick.AddListener(() => oddsPanel.SetActive(!oddsPanel.activeSelf));

            // 닫기는 아래 가운데. 오른쪽 위는 DEV 배지가 가린다.
            Button close = NewButton("Close", p, new Color(0.42f, 0.38f, 0.52f, 1f), "닫기", 34f, out _);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.sizeDelta = new Vector2(320f, 84f);
            closeRect.anchoredPosition = new Vector2(0f, 62f);
            AddExitButton(close);
            close.onClick.AddListener(Exit);

            // 카드 목록 — 화면이 짧은 기기에서도 네 장이 다 보이게 스크롤로 감싼다.
            GameObject viewportGo = NewRect("Viewport", p);
            var viewport = (RectTransform)viewportGo.transform;
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(0f, 120f);
            viewport.offsetMax = new Vector2(0f, -120f);
            viewportGo.AddComponent<RectMask2D>();
            Image hit = viewportGo.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            GameObject contentGo = NewRect("Content", viewport);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, SynergyRules.CategoryCount * (CardHeight + CardGap) + CardGap);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            for (int c = 0; c < SynergyRules.CategoryCount; c++)
                cards[c] = BuildCard((SynergyCategory)c, content, -CardGap - c * (CardHeight + CardGap));

            BuildOdds(p);

            toastText = NewText("Toast", view, string.Empty, 44f, BestColor, TextAlignmentOptions.Center);
            SetRect(toastText.rectTransform, new Vector2(900f, 120f), new Vector2(0f, 0f));
            toastText.gameObject.SetActive(false);
        }

        private CardView BuildCard(SynergyCategory category, RectTransform content, float top)
        {
            var card = new CardView { Category = category };
            Color color = UISynergyHud.CategoryColor(category);

            Image back = NewImage("Card_" + category, content, null, CardColor);
            card.Root = back.rectTransform;
            card.Root.anchorMin = card.Root.anchorMax = new Vector2(0.5f, 1f);
            card.Root.pivot = new Vector2(0.5f, 1f);
            card.Root.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.Root.anchoredPosition = new Vector2(0f, top);

            // 왼쪽 원 — 계열 이름(스케치의 "다이스" 동그라미).
            Image badge = NewImage("Badge", card.Root, UISynergyHud.Circle, color);
            TopLeftRect(badge.rectTransform, new Vector2(150f, 150f), new Vector2(95f, -120f));
            TMP_Text badgeText = NewText("Name", badge.rectTransform, SynergyText.CategoryName(category), 38f, Color.white, TextAlignmentOptions.Center);
            SetRect(badgeText.rectTransform, new Vector2(140f, 60f), Vector2.zero);

            // 프리셋 탭 1~4. 누르면 그 프리셋을 보고 <b>장착한다</b> — 전투는 장착 프리셋을 쓴다.
            for (int i = 0; i < SynergyRules.PresetCount; i++)
            {
                int index = i;
                Button tab = NewButton("Preset" + (i + 1), card.Root, TabColor, (i + 1).ToString(), 30f, out Image tabImage);
                TopLeftRect((RectTransform)tab.transform, new Vector2(84f, 56f), new Vector2(235f + i * 96f, -42f));
                tab.onClick.AddListener(() => SynergyManager.Instance?.SetEquipped(category, index));
                card.Tabs.Add(tabImage);
            }

            TMP_Text equipHint = NewText("EquipHint", card.Root, "장착 프리셋", 22f, MutedText, TextAlignmentOptions.Left);
            TopLeftRect(equipHint.rectTransform, new Vector2(200f, 40f), new Vector2(235f + 4 * 96f + 100f, -42f));

            // 조건 줄.
            float x = 190f + (CardWidth - 190f - 20f) * 0.5f;
            float lineWidth = CardWidth - 190f - 20f;
            card.Condition = BuildLine(card.Root, category, -1, new Vector2(x, -110f), lineWidth, true);

            // 효과 3줄.
            for (int i = 0; i < SynergyRules.EffectLineCount; i++)
                card.Effects.Add(BuildLine(card.Root, category, i, new Vector2(x, -186f - i * (LineHeight + 6f)), lineWidth, false));

            // 아래 줄 — Best 확률 게이지 + 리롤 버튼.
            card.BestText = NewText("Best", card.Root, string.Empty, 26f, BestColor, TextAlignmentOptions.Left);
            TopLeftRect(card.BestText.rectTransform, new Vector2(520f, 40f), new Vector2(30f + 260f, -CardHeight + 78f));
            Image bestTrack = NewImage("BestTrack", card.Root, null, LineColor);
            TopLeftRect(bestTrack.rectTransform, new Vector2(520f, 18f), new Vector2(30f + 260f, -CardHeight + 44f));
            // Filled 이미지는 스프라이트가 없으면 채움 비율을 무시하고 꽉 찬다 — 오른쪽 앵커로 길이를 낸다.
            card.BestFill = NewImage("BestFill", bestTrack.rectTransform, null, BestColor);
            Stretch(card.BestFill.rectTransform);

            card.RerollButton = NewButton("Reroll", card.Root, RerollColor, "리롤", 32f, out card.RerollImage);
            TopLeftRect((RectTransform)card.RerollButton.transform, new Vector2(330f, 92f), new Vector2(CardWidth - 185f, -CardHeight + 60f));
            card.RerollText = card.RerollButton.GetComponentInChildren<TMP_Text>();
            card.RerollButton.onClick.AddListener(() => OnReroll(category));

            // 안 열린 계열 덮개.
            Image cover = NewImage("LockedCover", card.Root, null, new Color(0.05f, 0.05f, 0.07f, 0.88f));
            Stretch(cover.rectTransform);
            cover.raycastTarget = true;
            card.LockedText = NewText("LockedText", cover.rectTransform, string.Empty, 34f, Color.white, TextAlignmentOptions.Center);
            SetRect(card.LockedText.rectTransform, new Vector2(CardWidth - 80f, 200f), Vector2.zero);
            card.LockedCover = cover.gameObject;

            return card;
        }

        private LineView BuildLine(RectTransform card, SynergyCategory category, int line, Vector2 center, float width, bool condition)
        {
            var view = new LineView();
            view.Back = NewImage(condition ? "Condition" : "Effect" + line, card, null, LineColor);
            TopLeftRect(view.Back.rectTransform, new Vector2(width, condition ? 62f : LineHeight), center);

            view.Text = NewText("Text", view.Back.rectTransform, string.Empty, condition ? 28f : 26f, Color.white, TextAlignmentOptions.Left);
            view.Text.rectTransform.anchorMin = Vector2.zero;
            view.Text.rectTransform.anchorMax = Vector2.one;
            view.Text.rectTransform.offsetMin = new Vector2(18f, 0f);
            view.Text.rectTransform.offsetMax = new Vector2(-84f, 0f);
            view.Text.enableAutoSizing = true;
            view.Text.fontSizeMin = 16f;
            view.Text.fontSizeMax = condition ? 28f : 26f;

            view.LockButton = NewButton("Lock", view.Back.rectTransform, TabColor, string.Empty, 22f, out view.LockImage);
            var lockRect = (RectTransform)view.LockButton.transform;
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(1f, 0.5f);
            lockRect.pivot = new Vector2(1f, 0.5f);
            lockRect.sizeDelta = new Vector2(72f, 46f);
            lockRect.anchoredPosition = new Vector2(-6f, 0f);
            view.LockText = view.LockButton.GetComponentInChildren<TMP_Text>();
            view.LockButton.onClick.AddListener(() => OnToggleLock(category, line));
            return view;
        }

        private void BuildOdds(RectTransform panel)
        {
            Image back = NewImage("Odds", panel, null, new Color(0.06f, 0.05f, 0.1f, 0.98f));
            back.rectTransform.anchorMin = new Vector2(0f, 0f);
            back.rectTransform.anchorMax = new Vector2(1f, 1f);
            back.rectTransform.offsetMin = new Vector2(20f, 120f);
            back.rectTransform.offsetMax = new Vector2(-20f, -120f);
            back.raycastTarget = true;
            oddsPanel = back.gameObject;

            GameObject viewportGo = NewRect("Viewport", back.rectTransform);
            var viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport);
            viewport.offsetMin = new Vector2(24f, 24f);
            viewport.offsetMax = new Vector2(-24f, -24f);
            viewportGo.AddComponent<RectMask2D>();
            Image hit = viewportGo.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            oddsText = NewText("Text", viewport, string.Empty, 24f, Color.white, TextAlignmentOptions.TopLeft);
            var textRect = oddsText.rectTransform;
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = Vector2.zero;
            oddsText.textWrappingMode = TextWrappingModes.Normal;
            oddsText.text = BuildOddsText();
            textRect.sizeDelta = new Vector2(0f, oddsText.GetPreferredValues(oddsText.text, PanelWidth - 90f, 0f).y + 40f);

            var scroll = viewportGo.AddComponent<ScrollRect>();
            scroll.content = textRect;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            oddsPanel.SetActive(false);
        }

        /// <summary>확률표. 굴림과 같은 표(<see cref="SynergyCatalog"/>)를 읽는다 — 표기와 실제가 어긋날 수 없다.</summary>
        private static string BuildOddsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<size=34><color=#FFD440>BEST 보정</color></size>");
            sb.Append("리롤에 쓴 티켓 1장마다 BEST 확률이 ")
                .Append(SynergyRules.FormatBp(SynergyRules.BestChancePerTicketBp)).Append(" 오르고, 최대 ")
                .Append(SynergyRules.FormatBp(SynergyRules.BestChanceCapBp)).Append("(티켓 ")
                .Append(SynergyRules.TicketsToCap).AppendLine("장)에서 멈춥니다.");
            sb.AppendLine("BEST 가 나오면 이번에 굴린 줄 중 1줄이 최고 수치가 되고, 확률은 0% 로 돌아갑니다.");
            sb.AppendLine("계열마다 따로 쌓이고, 같은 계열의 프리셋끼리는 공유합니다. 잠근 줄은 굴리지 않습니다.");
            sb.AppendLine();
            sb.AppendLine("<size=34><color=#FFD440>리롤 비용</color></size>");
            sb.Append("기본 ").Append(SynergyRules.BaseRerollCost).Append("장 + 잠근 효과 1줄마다 ")
                .Append(SynergyRules.LockedEffectCostStep).Append("장, 조건을 잠그면 ×")
                .Append(SynergyRules.LockedConditionCostMultiplier).AppendLine(".");
            sb.AppendLine();

            for (int c = 0; c < SynergyRules.CategoryCount; c++)
            {
                var category = (SynergyCategory)c;
                List<SynergyOption> conditions = SynergyCatalog.Pool(category, true);
                List<SynergyOption> effects = SynergyCatalog.Pool(category, false);

                sb.Append("<size=34><color=#").Append(ColorUtility.ToHtmlStringRGB(UISynergyHud.CategoryColor(category)))
                    .Append('>').Append(SynergyText.CategoryName(category)).AppendLine(" 시너지</color></size>");
                sb.Append("<color=#A09CB3>조건 (각 ").Append(Percent(conditions.Count)).AppendLine(")</color>");
                foreach (SynergyOption option in conditions)
                    sb.Append("  ").AppendLine(SynergyText.DescribeRange(option));

                sb.Append("<color=#A09CB3>효과 (줄마다 각 ").Append(Percent(effects.Count)).AppendLine(")</color>");
                foreach (SynergyOption option in effects)
                    sb.Append("  ").AppendLine(SynergyText.DescribeRange(option));

                sb.AppendLine();
            }

            sb.Append("<color=#A09CB3>수치는 범위 안에서 고르게 나옵니다. 왼쪽이 가장 낮은 값, 오른쪽이 BEST 입니다.</color>");
            return sb.ToString();
        }

        private static string Percent(int count)
        {
            if (count <= 0)
                return "0%";

            return SynergyRules.FormatBp(10000 / count);
        }

        // ── 갱신 ───────────────────────────────────────────────

        private void Refresh()
        {
            SynergyManager manager = SynergyManager.Instance;
            if (manager == null || cards[0] == null)
                return;

            int tickets = PointManager.Instance != null ? PointManager.Instance.Get(PointType.SynergyTicket) : 0;
            ticketText.text = "<size=26><color=#A09CB3>시너지 티켓</color></size> " + tickets.ToString("N0");
            int openLines = manager.OpenEffectLines;

            foreach (CardView card in cards)
                RefreshCard(card, manager, openLines, tickets);
        }

        private void RefreshCard(CardView card, SynergyManager manager, int openLines, int tickets)
        {
            bool open = manager.IsOpen(card.Category);
            card.LockedCover.SetActive(!open);
            if (!open)
            {
                card.LockedText.text = "잠김\n<size=26><color=#A09CB3>" + manager.UnlockHint(card.Category) + "</color></size>";
                return;
            }

            int equipped = manager.GetEquippedIndex(card.Category);
            Color accent = UISynergyHud.CategoryColor(card.Category);
            for (int i = 0; i < card.Tabs.Count; i++)
                card.Tabs[i].color = i == equipped ? accent : TabColor;

            SynergyPreset preset = manager.GetPreset(card.Category, equipped);
            FillLine(card.Condition, preset.Condition, true, true, card.FlashLine == -1);
            for (int i = 0; i < card.Effects.Count; i++)
            {
                bool lineOpen = i < openLines;
                FillLine(card.Effects[i], preset.Effects[i], false, lineOpen, card.FlashLine == i);
                if (!lineOpen)
                    card.Effects[i].Text.text = "<color=#7A7690>잠김 - 스테이지 " + SynergyRules.EffectLineUnlockStage(i) + " 클리어 시 개방</color>";
            }

            int spent = manager.GetBestSpent(card.Category);
            int bp = SynergyRules.BestChanceBp(spent);
            card.BestText.text = "BEST 확률 " + SynergyRules.FormatBp(bp) + " / " + SynergyRules.FormatBp(SynergyRules.BestChanceCapBp)
                                 + "  <size=20><color=#A09CB3>누적 " + Mathf.Min(spent, SynergyRules.TicketsToCap) + "/" + SynergyRules.TicketsToCap + "</color></size>";
            card.BestFill.rectTransform.anchorMax = new Vector2(bp / (float)SynergyRules.BestChanceCapBp, 1f);

            bool canRoll = SynergyRules.CanReroll(preset, openLines);
            int cost = SynergyRules.RerollCost(preset, openLines);
            bool affordable = tickets >= cost;
            card.RerollImage.color = canRoll && affordable ? RerollColor : DisabledColor;
            card.RerollText.text = canRoll ? "리롤  <size=26>티켓 " + cost + "</size>" : "<size=26>모두 잠김</size>";
        }

        private static void FillLine(LineView view, SynergyLine line, bool condition, bool open, bool flash)
        {
            view.Back.color = flash ? new Color(0.45f, 0.36f, 0.08f, 1f) : open ? LineColor : LockedLineColor;
            view.LockButton.gameObject.SetActive(open && !line.IsEmpty);
            if (!open)
                return;

            string text = SynergyText.Describe(line, condition);
            if (string.IsNullOrEmpty(text))
                text = "<color=#7A7690>리롤하면 옵션이 붙습니다</color>";
            else if (SynergyRules.IsBest(line, condition))
                text = "<color=#FFD440>" + text + " (BEST)</color>";

            view.Text.text = text;
            view.LockImage.color = line.Locked ? BestColor : TabColor;
            view.LockText.text = line.Locked ? "<color=#2A2000>잠금</color>" : "잠금";
        }

        // ── 조작 ───────────────────────────────────────────────

        private void OnToggleLock(SynergyCategory category, int line)
        {
            SynergyManager manager = SynergyManager.Instance;
            if (manager == null)
                return;

            manager.ToggleLock(category, manager.GetEquippedIndex(category), line);
        }

        private void OnReroll(SynergyCategory category)
        {
            SynergyManager manager = SynergyManager.Instance;
            if (manager == null)
                return;

            int index = manager.GetEquippedIndex(category);
            if (!manager.CanReroll(category, index))
            {
                ShowToast("굴릴 줄이 없습니다. 잠금을 풀어 주세요.");
                return;
            }

            int cost = manager.GetRerollCost(category, index);
            int tickets = PointManager.Instance != null ? PointManager.Instance.Get(PointType.SynergyTicket) : 0;
            if (tickets < cost)
            {
                ShowToast("시너지 티켓이 부족합니다.\n<size=30>핀볼에서 얻을 수 있어요.</size>");
                return;
            }

            SynergyRerollResult result = manager.Reroll(category, index);
            if (result.BestHit)
            {
                CardView card = cards[(int)category];
                card.FlashLine = result.BestLine;
                card.FlashLeft = 1.2f;
                ShowToast("BEST!");
            }

            Refresh();
        }

        private void ShowToast(string message)
        {
            toastText.text = message;
            toastText.gameObject.SetActive(true);
            toastText.transform.SetAsLastSibling();
            toastLeft = 1.4f;
        }

        // ── 모양 ───────────────────────────────────────────────

        private static GameObject NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            Image image = NewRect(name, parent).AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>글꼴은 TMP 기본값 — 이 프로젝트에서는 BM HANNA 다(<c>DevCheatUI</c> 주석).</summary>
        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI label = NewRect(name, parent).AddComponent<TextMeshProUGUI>();

            // 글꼴을 직접 넣는다. 꺼진 dialogView 아래에서 만들면 TMP 가 OnEnable 전이라 글꼴이 비어 있고,
            // 그 상태로 GetPreferredValues 를 부르면 NRE 가 난다.
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static Button NewButton(string name, Transform parent, Color color, string label, float size, out Image image)
        {
            image = NewImage(name, parent, null, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TMP_Text text = NewText("Label", image.rectTransform, label, size, Color.white, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        /// <summary>부모 위쪽 가운데 기준. <paramref name="center"/> 의 x 는 부모 왼쪽에서의 거리다.</summary>
        private static void TopRect(RectTransform rect, Vector2 size, Vector2 center)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = center;
        }

        private static void TopLeftRect(RectTransform rect, Vector2 size, Vector2 center) => TopRect(rect, size, center);

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
