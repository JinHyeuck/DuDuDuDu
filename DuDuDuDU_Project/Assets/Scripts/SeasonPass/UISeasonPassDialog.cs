using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.UI;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 시즌 패스 창.
    ///
    /// <b>목록은 열 때마다 다시 그린다.</b> 30레벨 × 2트랙이라 재활용 목록을 쓸 만하지만,
    /// 매번 짓는 편이 "전투에서 쓴 고기가 반영 안 된다" 는 사고를 원천적으로 없앤다.
    /// 줄 수가 늘어 느려지면 그때 <c>UIRecycleVerticalList</c> 로 옮긴다.
    ///
    /// <b>남은 시간을 크게 띄운다.</b> 시즌이 바뀌면 미수령 보상이 사라지는데
    /// (<c>SeasonPassManager.EnsureSeason</c>), 그 사실을 화면이 말해 주지 않으면
    /// "받아 둔 게 없어졌다" 가 된다.
    /// </summary>
    public sealed class UISeasonPassDialog : DialogBase
    {
        [SerializeField] private TMP_Text seasonNameText;
        [SerializeField] private TMP_Text remainingText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text pointText;
        [SerializeField] private Image pointGaugeFill;

        [SerializeField] private Button premiumButton;
        [SerializeField] private TMP_Text premiumButtonLabel;
        [SerializeField] private Button claimAllButton;

        [SerializeField] private RectTransform listContent;
        [SerializeField] private UISeasonPassLevelRow rowTemplate;

        /// <summary>포인트 게이지 트랙의 너비. 굽기와 같은 값이어야 한다.</summary>
        private const float PointGaugeWidth = 520f;

        /// <summary>남은 시간 글자를 다시 쓰는 간격(초). 분 단위로만 적어 1초마다 고칠 이유가 없다.</summary>
        private const float TimerRefreshSeconds = 20f;

        private readonly List<UISeasonPassLevelRow> rows = new List<UISeasonPassLevelRow>();
        private float nextTimerRefresh;

        protected override void OnLoad()
        {
            UseBackBtn = true;

            if (rowTemplate != null)
                rowTemplate.gameObject.SetActive(false);

            if (premiumButton != null)
                premiumButton.onClick.AddListener(OnClickPremium);

            if (claimAllButton != null)
                claimAllButton.onClick.AddListener(OnClickClaimAll);
        }

        protected override void OnUnload()
        {
            if (premiumButton != null)
                premiumButton.onClick.RemoveListener(OnClickPremium);

            if (claimAllButton != null)
                claimAllButton.onClick.RemoveListener(OnClickClaimAll);
        }

        protected override void OnEnter()
        {
            nextTimerRefresh = 0f;

            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass != null)
                pass.OnChanged += Refresh;

            Refresh();
        }

        protected override void OnExit()
        {
            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass != null)
                pass.OnChanged -= Refresh;
        }

        private void Update()
        {
            if (!isEnter || remainingText == null)
                return;

            // 배속·일시정지를 타지 않는다. 로비에서는 둘 다 1 이지만 이 창을 전투에서
            // 열게 되는 날 글자가 배속만큼 빨리 바뀌는 것은 말이 안 된다.
            if (Time.unscaledTime < nextTimerRefresh)
                return;

            nextTimerRefresh = Time.unscaledTime + TimerRefreshSeconds;
            RefreshRemaining();
        }

        // ── 그리기 ─────────────────────────────────────────────────────

        private void Refresh()
        {
            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass == null)
            {
                // 컨테이너가 서기 전에 열렸다. 조용히 넘어가지 않는다 —
                // 이 상태는 배선 사고이지 "패스가 없음" 이 아니다.
                Debug.LogError("[시즌패스] SeasonPassManager 가 없다. 창을 그리지 못한다.");
                return;
            }

            SeasonPassSeason season = pass.Season;

            if (seasonNameText != null)
                seasonNameText.SetText(season.displayName);

            if (levelText != null)
                levelText.SetText(pass.Level.ToString());

            if (pointText != null)
            {
                pointText.SetText(SeasonPassText.PointGauge(
                    pass.PointsIntoLevel, pass.PointsPerLevel, pass.Level, pass.MaxLevel));
            }

            UISeasonPassUIFactory.SetGauge(pointGaugeFill, PointGaugeWidth, pass.LevelProgress);

            RefreshRemaining();
            RefreshPremiumButton(pass);
            RefreshClaimAll(pass);
            RefreshRows(pass, season);
        }

        private void RefreshRemaining()
        {
            SeasonPassManager pass = SeasonPassManager.Instance;
            if (remainingText == null || pass == null)
                return;

            remainingText.SetText(SeasonPassText.Remaining(pass.TimeUntilSeasonEnd));
        }

        private void RefreshPremiumButton(SeasonPassManager pass)
        {
            bool unlocked = pass.PremiumUnlocked;

            if (premiumButtonLabel != null)
            {
                premiumButtonLabel.SetText(unlocked ? "프리미엄 적용 중" : "프리미엄 활성화");

                // <b>색을 직접 준다.</b> interactable 을 끄면 uGUI 가 바탕을 어둡게 덮는데,
                // 바탕이 금색이고 글자가 어두운 색이라 둘 다 어두워져 글자가 안 읽힌다.
                premiumButtonLabel.color = unlocked
                    ? UISeasonPassUIFactory.LightText
                    : UISeasonPassUIFactory.DarkText;
            }

            // 이미 샀으면 누를 것이 없다. 버튼을 남겨 두면 눌러 보고 아무 일도 안 일어나는
            // 자리가 되는데, 그건 고장으로 읽힌다.
            if (premiumButton != null)
            {
                premiumButton.interactable = !unlocked;

                if (premiumButton.targetGraphic is Image image)
                {
                    image.color = unlocked
                        ? UISeasonPassUIFactory.SlotDoneColor
                        : UISeasonPassUIFactory.PremiumButtonColor;
                }
            }
        }

        private void RefreshClaimAll(SeasonPassManager pass)
        {
            if (claimAllButton != null)
                claimAllButton.interactable = pass.HasClaimable();
        }

        private void RefreshRows(SeasonPassManager pass, SeasonPassSeason season)
        {
            List<SeasonPassSlotView> slots = pass.GetSlots();

            // 칸은 레벨마다 무료·유료 둘씩 들어온다. 줄 수는 그 절반이다.
            int levelCount = slots.Count / 2;
            EnsureRows(levelCount);

            int reached = pass.Level;

            for (int i = 0; i < rows.Count; i++)
            {
                if (i >= levelCount)
                {
                    rows[i].gameObject.SetActive(false);
                    continue;
                }

                SeasonPassSlotView free = slots[i * 2];
                SeasonPassSlotView premium = slots[i * 2 + 1];
                int level = free.Level;

                rows[i].gameObject.SetActive(true);

                // 레벨을 지역 변수로 떼어 낸다. 대리자가 view 를 통째로 잡으면 다음 갱신의
                // 값을 보게 되고, 그러면 엉뚱한 레벨의 보상을 받는다.
                int captured = level;
                rows[i].Bind(
                    level, level <= reached, free, premium,
                    () => Claim(captured, false),
                    () => Claim(captured, true));
            }
        }

        private void EnsureRows(int count)
        {
            if (rowTemplate == null || listContent == null)
                return;

            while (rows.Count < count)
            {
                UISeasonPassLevelRow row = Instantiate(rowTemplate, listContent);
                row.gameObject.SetActive(true);
                rows.Add(row);
            }
        }

        // ── 수령 ───────────────────────────────────────────────────────
        //
        // 수령 뒤에 Refresh 를 부르지 않는다. 매니저가 OnChanged 를 쏘고 이 창이 그것을
        // 구독하고 있어서, 여기서 또 부르면 같은 프레임에 목록을 두 번 짓는다.

        private void Claim(int level, bool premium)
        {
            SeasonPassManager.Instance?.TryClaim(level, premium);
        }

        private void OnClickClaimAll()
        {
            SeasonPassManager.Instance?.ClaimAll();
        }

        /// <summary>
        /// 유료 트랙을 연다.
        ///
        /// <b>지금은 결제 없이 열린다.</b> 이 프로젝트에 IAP 가 0 줄이라
        /// <c>ShopPurchaseManager.TryBuyCashProduct</c> 도 "결제된 셈 치고" 지급하는 상태다.
        /// 상품이 생기면 그 관문을 지난 뒤에 여기를 부르도록 바꾼다 —
        /// <b>고칠 곳은 이 메서드 하나다.</b>
        /// </summary>
        private void OnClickPremium()
        {
            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass == null || pass.PremiumUnlocked)
                return;

            Debug.LogWarning("[시즌패스] 결제 없이 유료 트랙을 연다(IAP 미연동). 출시 전 실제 결제로 교체할 것.");
            pass.TryUnlockPremium();
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        /// <summary>창 하나를 조립한다. 에디터 굽기 경로에서만 부른다.</summary>
        public static UISeasonPassDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISeasonPassDialog", parent);
            UISeasonPassUIFactory.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISeasonPassDialog>();
            dialog.BakeOpenStyle(DialogOpenStyle.Popup);

            // dialogView 는 루트가 아니라 자식이어야 한다. DialogBase 가 이것을 끄고 켜는데,
            // 루트를 끄면 컴포넌트까지 같이 잠들어 다시 열 수 없다.
            GameObject view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;

            Image backdrop = UISeasonPassUIFactory.CreateImage(
                "Backdrop", view.transform, UISeasonPassUIFactory.Backdrop);
            UISeasonPassUIFactory.Stretch(backdrop.rectTransform);

            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            dialog.AddExitButton(backdropButton);

            Image panel = UISeasonPassUIFactory.CreateImage(
                "Panel", view.transform, UISeasonPassUIFactory.PanelColor);
            UISeasonPassUIFactory.SetRect(panel.rectTransform, new Vector2(1000f, 1700f), Vector2.zero);

            BakeHeader(dialog, panel.transform, font);

            dialog.listContent = UISeasonPassUIFactory.CreateScrollList(
                "List", panel.transform, new Vector2(900f, 980f), new Vector2(0f, -170f), 8f);
            dialog.rowTemplate = UISeasonPassLevelRow.Create(dialog.listContent, font);

            BakeFooter(dialog, panel.transform, font);

            return dialog;
        }

        private static void BakeHeader(UISeasonPassDialog dialog, Transform parent, TMP_FontAsset font)
        {
            Image header = UISeasonPassUIFactory.CreateImage(
                "Header", parent, UISeasonPassUIFactory.HeaderColor);
            UISeasonPassUIFactory.SetRect(header.rectTransform, new Vector2(1000f, 300f), new Vector2(0f, 700f));
            header.raycastTarget = false;

            TMP_Text title = UISeasonPassUIFactory.CreateText(
                "Title", header.transform, "시즌 패스", 64f,
                TextAlignmentOptions.Left, UISeasonPassUIFactory.LightText, font);
            UISeasonPassUIFactory.SetRect(title.rectTransform, new Vector2(600f, 80f), new Vector2(-180f, 90f));

            dialog.seasonNameText = UISeasonPassUIFactory.CreateText(
                "SeasonName", header.transform, "시즌", 36f,
                TextAlignmentOptions.Left, UISeasonPassUIFactory.MutedText, font);
            UISeasonPassUIFactory.SetRect(
                dialog.seasonNameText.rectTransform, new Vector2(600f, 48f), new Vector2(-180f, 28f));

            Image remainingBg = UISeasonPassUIFactory.CreateImage(
                "RemainingBg", header.transform, UISeasonPassUIFactory.GaugeTrackColor);
            UISeasonPassUIFactory.SetRect(remainingBg.rectTransform, new Vector2(320f, 62f), new Vector2(-320f, -42f));
            remainingBg.raycastTarget = false;

            dialog.remainingText = UISeasonPassUIFactory.CreateText(
                "Remaining", remainingBg.transform, "0일", 34f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.LightText, font);
            UISeasonPassUIFactory.SetRect(
                dialog.remainingText.rectTransform, new Vector2(320f, 62f), Vector2.zero);

            Button close = UISeasonPassUIFactory.CreateButton(
                "CloseButton", header.transform, "X", new Vector2(88f, 88f), new Vector2(440f, 96f),
                UISeasonPassUIFactory.PremiumButtonColor, UISeasonPassUIFactory.DarkText, 44f, font);
            dialog.AddExitButton(close);

            // 레벨 뱃지 + 포인트 게이지. 헤더 아래 띠다.
            Image levelBadge = UISeasonPassUIFactory.CreateImage(
                "LevelBadge", parent, UISeasonPassUIFactory.LevelBadgeColor);
            UISeasonPassUIFactory.SetRect(levelBadge.rectTransform, new Vector2(88f, 88f), new Vector2(-420f, 500f));
            levelBadge.raycastTarget = false;

            dialog.levelText = UISeasonPassUIFactory.CreateText(
                "Level", levelBadge.transform, "1", 40f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.DarkText, font);
            UISeasonPassUIFactory.SetRect(dialog.levelText.rectTransform, new Vector2(88f, 88f), Vector2.zero);

            dialog.pointGaugeFill = UISeasonPassUIFactory.CreateGauge(
                "PointGauge", parent, new Vector2(PointGaugeWidth, 56f), new Vector2(-90f, 500f));

            dialog.pointText = UISeasonPassUIFactory.CreateText(
                "Point", parent, "0 / 0", 32f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.LightText, font);
            UISeasonPassUIFactory.SetRect(
                dialog.pointText.rectTransform, new Vector2(PointGaugeWidth, 56f), new Vector2(-90f, 500f));

            dialog.premiumButton = UISeasonPassUIFactory.CreateButton(
                "PremiumButton", parent, "프리미엄 활성화", new Vector2(300f, 88f), new Vector2(330f, 500f),
                UISeasonPassUIFactory.PremiumButtonColor, UISeasonPassUIFactory.DarkText, 32f, font);
            dialog.premiumButtonLabel = dialog.premiumButton.GetComponentInChildren<TMP_Text>();
        }

        private static void BakeFooter(UISeasonPassDialog dialog, Transform parent, TMP_FontAsset font)
        {
            dialog.claimAllButton = UISeasonPassUIFactory.CreateButton(
                "ClaimAllButton", parent, "일괄 수령", new Vector2(420f, 110f), new Vector2(140f, -770f),
                UISeasonPassUIFactory.ClaimAllColor, UISeasonPassUIFactory.LightText, 42f, font);

            Button back = UISeasonPassUIFactory.CreateButton(
                "BackButton", parent, "뒤로", new Vector2(200f, 110f), new Vector2(-330f, -770f),
                UISeasonPassUIFactory.SlotDimColor, UISeasonPassUIFactory.LightText, 38f, font);
            dialog.AddExitButton(back);
        }
    }
}
