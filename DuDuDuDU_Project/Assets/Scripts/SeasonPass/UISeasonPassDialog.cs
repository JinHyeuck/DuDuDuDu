using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.DI;
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

        [SerializeField] private Button premiumButton;
        [SerializeField] private TMP_Text premiumButtonLabel;
        [SerializeField] private GameObject premiumButtonCrown;
        [SerializeField] private Button claimAllButton;

        [SerializeField] private RectTransform listContent;
        [SerializeField] private UISeasonPassLevelRow rowTemplate;

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

                // 산 뒤에는 글자만 노랗게 바꾼다(PSD 주석). 버튼 그림은 그대로 두고
                // 꺼진 틴트도 쓰지 않는다 — 금색 바탕이 어두워지면 글자까지 같이 묻힌다.
                premiumButtonLabel.color = unlocked
                    ? UISeasonPassUIFactory.PremiumActiveText
                    : Color.white;
            }

            // 이미 샀으면 누를 것이 없다. 버튼 그림(티켓)과 왕관을 끄고 글자만 남긴다 —
            // 그림이 남아 있으면 눌러 볼 자리로 읽히는데 눌러도 아무 일이 없다.
            if (premiumButton != null)
            {
                premiumButton.interactable = !unlocked;

                if (premiumButton.targetGraphic != null)
                    premiumButton.targetGraphic.enabled = !unlocked;
            }

            if (premiumButtonCrown != null)
                premiumButtonCrown.SetActive(!unlocked);
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
            bool lineDrawn = false;

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
                // 진행 선은 못 닿은 첫 줄 하나에만 긋는다.
                bool firstLocked = !lineDrawn && level > reached;
                lineDrawn |= firstLocked;

                int captured = level;
                rows[i].Bind(
                    level, level <= reached, firstLocked, free, premium,
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

        /// <summary>구매 창을 띄운다. 실제 활성화는 그 창의 구매 버튼이 한다.</summary>
        private void OnClickPremium()
        {
            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass == null || pass.PremiumUnlocked)
                return;

            if (GameContainer.UI?.Show<UISeasonPassPremiumDialog>() == null)
                Debug.LogError("[시즌패스] 구매 창을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
        }

        // ── 굽기 ───────────────────────────────────────────────────────
        //
        // 좌표는 PSD(Art/0PSD/패스.psd) 레이어 bbox 그대로다. 측정표: Docs/SeasonPassArtPort.md

        /// <summary>목록 영역(PSD y). 트랙 머리 아래 검은 선부터 하단 띠 위 검은 선까지.</summary>
        private const float ListTop = 676f;
        private const float ListBottom = 1714f;

        /// <summary>첫 줄 칸 중심(PSD y 828)과 목록 위끝 사이의 여백.</summary>
        private const int ListPadding = 828 - 676 - (int)(UISeasonPassLevelRow.RowHeight / 2f);

        /// <summary>창 하나를 조립한다. 에디터 굽기 경로에서만 부른다.</summary>
        public static UISeasonPassDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISeasonPassDialog", parent);
            UISeasonPassUIFactory.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISeasonPassDialog>();

            // 화면 전체를 덮는 창이다. 가운데서 커지는 팝업 연출은 맞지 않는다.
            dialog.BakeOpenStyle(DialogOpenStyle.Page);

            // dialogView 는 루트가 아니라 자식이어야 한다. DialogBase 가 이것을 끄고 켜는데,
            // 루트를 끄면 컴포넌트까지 같이 잠들어 다시 열 수 없다.
            GameObject view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;

            Transform p = view.transform;

            // 바탕 — 화면 전체가 무료 트랙 색이고 오른쪽 열만 유료 색이다.
            // 바탕이 클릭을 먹어야 뒤의 로비가 눌리지 않는다.
            Image background = UISeasonPassUIFactory.CreateImage("Background", p, UISeasonPassUIFactory.FreeTrackColor);
            UISeasonPassUIFactory.Stretch(background.rectTransform);

            UISeasonPassUIFactory.Solid(p, "PremiumTrack", UISeasonPassUIFactory.PremiumTrackColor, 536f, 657f, 1086f, 2100f);
            BakePatterns(p);

            BakeHeader(dialog, p, font);
            BakeList(dialog, p, font);
            BakeFooter(dialog, p, font);

            return dialog;
        }

        /// <summary>Pass_Pattern x2 넉 장. 목록과 같이 움직이지 않는 바탕 무늬다.</summary>
        private static void BakePatterns(Transform p)
        {
            PatternAt(p, "FreePattern1", UISeasonPassUIFactory.FreePatternTint, 253f, 942f);
            PatternAt(p, "FreePattern2", UISeasonPassUIFactory.FreePatternTint, 261.5f, 1470f);
            PatternAt(p, "PremiumPattern1", UISeasonPassUIFactory.PremiumPatternTint, 845.5f, 942f);
            PatternAt(p, "PremiumPattern2", UISeasonPassUIFactory.PremiumPatternTint, 853.5f, 1470f);
        }

        private static void PatternAt(Transform p, string name, Color tint, float x, float y)
        {
            Image image = UISeasonPassUIFactory.Picture(
                p, name, "Pass/Pass_Pattern", new Vector2(1024f, 1024f), UISeasonPassUIFactory.Pos(x, y));
            image.color = tint;
        }

        private static void BakeHeader(UISeasonPassDialog dialog, Transform p, TMP_FontAsset font)
        {
            // 윗 그림 — PSD 에서 잘라 온 한 장(FromPsd, 정식 파일 대기). 가장자리가 바탕색으로 번진다.
            UISeasonPassUIFactory.Picture(p, "Hero", "Pass/FromPsd/FromPsd_PassHero",
                new Vector2(1080f, 481f), UISeasonPassUIFactory.Pos(534f, 222.5f));

            TMP_Text title = UISeasonPassUIFactory.CreateText(
                "Title", p, "시즌 패스", 95f, TextAlignmentOptions.Left, UISeasonPassUIFactory.TitleColor, font);
            UISeasonPassUIFactory.SetRect(title.rectTransform, new Vector2(700f, 110f), UISeasonPassUIFactory.Pos(388f, 87f));

            dialog.seasonNameText = UISeasonPassUIFactory.CreateText(
                "SeasonName", p, "시즌", 45f, TextAlignmentOptions.Left, Color.white, font);
            UISeasonPassUIFactory.SetRect(
                dialog.seasonNameText.rectTransform, new Vector2(700f, 60f), UISeasonPassUIFactory.Pos(389f, 164.5f));

            // 남은 기간 칸 — SmallBox x4 검정 80%, 보이는 361x62 (30,327). 여백 6/7px x4
            UISeasonPassUIFactory.Sliced(p, "RemainingBox", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(413f, 114f), UISeasonPassUIFactory.Pos(212.5f, 360f), UISeasonPassUIFactory.SmallBoxColor);
            UISeasonPassUIFactory.Picture(p, "Clock", "Pass/Pass_Clork",
                new Vector2(96f, 96f), UISeasonPassUIFactory.Pos(64f, 356f));

            dialog.remainingText = UISeasonPassUIFactory.CreateFittedText(
                "Remaining", p, "7일 3시간", 40f, 28f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(
                dialog.remainingText.rectTransform, new Vector2(260f, 56f), UISeasonPassUIFactory.Pos(228f, 356.5f));

            // 레벨 띠 — 검은 선 5 + 밝은 선 5 + 바탕, 트랙 머리의 검은 선(559)까지.
            UISeasonPassUIFactory.Solid(p, "BandEdge", Color.black, -12f, 421f, 1094f, 426f);
            UISeasonPassUIFactory.Solid(p, "BandLine", UISeasonPassUIFactory.BandLineColor, -12f, 426f, 1094f, 431f);
            UISeasonPassUIFactory.Solid(p, "Band", UISeasonPassUIFactory.BandColor, -12f, 431f, 1094f, 559f);

            UISeasonPassUIFactory.Sliced(p, "PointBox", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(413f, 114f), UISeasonPassUIFactory.Pos(212.5f, 491f), UISeasonPassUIFactory.SmallBoxColor);

            // 레벨 뱃지 — Pass_LevelNumber x3, 보이는 90x93 (14,441)
            UISeasonPassUIFactory.Picture(p, "LevelBadge", "Pass/Pass_LevelNumber",
                new Vector2(96f, 96f), UISeasonPassUIFactory.Pos(59f, 489f));

            dialog.levelText = UISeasonPassUIFactory.CreateFittedText(
                "Level", p, "1", 40f, 24f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(
                dialog.levelText.rectTransform, new Vector2(70f, 56f), UISeasonPassUIFactory.Pos(58.5f, 486.5f));

            // 번개 — Gem_Thunder, 보이는 39x45 → x1.6
            UISeasonPassUIFactory.Picture(p, "PointIcon", "Gem/Gem_Thunder",
                new Vector2(102.4f, 102.4f), UISeasonPassUIFactory.Pos(132.5f, 490.5f));

            dialog.pointText = UISeasonPassUIFactory.CreateFittedText(
                "Point", p, "0 / 1000", 40f, 28f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(
                dialog.pointText.rectTransform, new Vector2(220f, 56f), UISeasonPassUIFactory.Pos(260.5f, 490.5f));

            // 프리미엄 버튼 — Pass_premium_Btn x3, 가로만 9슬라이스. 보이는 386x102 (642,440).
            // 그림이 128 안에 위아래 47px 씩 비어 있어 Image 가 384 높이다 — 누르는 자리는 보이는 만큼으로 줄인다.
            Image premiumImage = UISeasonPassUIFactory.Sliced(p, "PremiumButton", "Pass/Pass_premium_Btn", 3f,
                new Vector2(458f, 384f), UISeasonPassUIFactory.Pos(835f, 491f), Color.white);
            premiumImage.raycastPadding = new Vector4(36f, 141f, 36f, 141f);
            dialog.premiumButton = UISeasonPassUIFactory.SpriteButton(premiumImage);

            // 왕관 x2, 보이는 48x36 (681,472)
            dialog.premiumButtonCrown = UISeasonPassUIFactory.Picture(premiumImage.transform, "Crown", "Pass/Pass_Crown",
                new Vector2(64f, 64f), new Vector2(705f - 835f, 1f)).gameObject;

            dialog.premiumButtonLabel = UISeasonPassUIFactory.CreateFittedText(
                "Label", premiumImage.transform, "프리미엄 활성화", 40f, 26f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(
                dialog.premiumButtonLabel.rectTransform, new Vector2(270f, 56f), new Vector2(864f - 835f, 1.5f));

            // 트랙 머리 — Pass_Free / Pass_premium 9슬라이스 x1(테두리 6px), y 559~676.
            // 바깥쪽 테두리는 화면 밖으로 민다. 두 머리 사이의 검은 선은 x 532~538 이다.
            UISeasonPassUIFactory.Sliced(p, "FreeHeader", "Pass/Pass_Free", 1f,
                new Vector2(544f, 117f), UISeasonPassUIFactory.Pos(266f, 617.5f), Color.white);
            UISeasonPassUIFactory.Sliced(p, "PremiumHeader", "Pass/Pass_premium", 1f,
                new Vector2(554f, 117f), UISeasonPassUIFactory.Pos(809f, 617.5f), Color.white);

            TMP_Text freeLabel = UISeasonPassUIFactory.CreateText(
                "FreeLabel", p, "무료", 45f, TextAlignmentOptions.Center, UISeasonPassUIFactory.FreeLabelColor, font);
            UISeasonPassUIFactory.SetRect(freeLabel.rectTransform, new Vector2(200f, 60f), UISeasonPassUIFactory.Pos(255f, 620f));

            // 유료 머리는 왕관만 둔다(PSD 의 '프리미엄' 글자 레이어는 꺼져 있다). Pass_Crown x3, 보이는 72x54
            UISeasonPassUIFactory.Picture(p, "PremiumCrown", "Pass/Pass_Crown",
                new Vector2(96f, 96f), UISeasonPassUIFactory.Pos(699f, 614f));
        }

        private static void BakeList(UISeasonPassDialog dialog, Transform p, TMP_FontAsset font)
        {
            dialog.listContent = UISeasonPassUIFactory.CreateScrollList(
                "List", p, new Vector2(1080f, ListBottom - ListTop),
                UISeasonPassUIFactory.Pos(540f, (ListTop + ListBottom) * 0.5f), 0f);

            // 마지막 줄 아래에는 여백을 두지 않는다 — 못 닿은 줄의 딤이 하단 띠까지 이어져야 한다.
            var layout = dialog.listContent.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, ListPadding, 0);

            // 스크롤이 칸 사이 빈 곳에서도 잡히게 뷰포트에 투명 판을 깐다. 없으면 드래그가
            // 칸(버튼) 위에서 시작할 때만 ScrollRect 에 닿는다.
            Image hitArea = dialog.listContent.parent.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;

            dialog.rowTemplate = UISeasonPassLevelRow.Create(dialog.listContent, font);
        }

        private static void BakeFooter(UISeasonPassDialog dialog, Transform p, TMP_FontAsset font)
        {
            UISeasonPassUIFactory.Solid(p, "FooterEdge", Color.black, -12f, 1714f, 1094f, 1719f);
            UISeasonPassUIFactory.Solid(p, "FooterLine", UISeasonPassUIFactory.BandLineColor, -12f, 1719f, 1094f, 1724f);
            UISeasonPassUIFactory.Solid(p, "Footer", UISeasonPassUIFactory.BandColor, -12f, 1724f, 1094f, 2100f);

            // 뒤로 — 무한의 탑과 같은 Btn_Gray x5 + Icon_Back x4. 보이는 164x169 (5,1739).
            // Btn_Gray 여백 좌우 4 · 위 6 · 아래 2 px x5
            Image backImage = UISeasonPassUIFactory.Sliced(p, "BackButton", "Ingame/Btn_Gray", 5f,
                new Vector2(204f, 209f), UISeasonPassUIFactory.Pos(87f, 1813.5f), Color.white);
            dialog.AddExitButton(UISeasonPassUIFactory.SpriteButton(backImage));

            // 화살표 — 보이는 92x92 (41,1767), 여백 4/5px x4
            UISeasonPassUIFactory.Picture(backImage.transform, "Icon", "Main/Icon_Back",
                new Vector2(128f, 128f), new Vector2(89f - 87f, 1813.5f - 1815f));

            // 일괄 수령 — Big_Btn_Green x4, 보이는 331x145 (716,1751). 여백 좌우 4 · 위 6 · 아래 2 px x4
            Image claimImage = UISeasonPassUIFactory.Sliced(p, "ClaimAllButton", "Ingame/Big_Btn_Green", 4f,
                new Vector2(363f, 177f), UISeasonPassUIFactory.Pos(881.5f, 1815.5f), Color.white);
            dialog.claimAllButton = UISeasonPassUIFactory.SpriteButton(claimImage);

            // 글자 크기는 PSD 글자 레이어(45pt)를 따른다. 시안 주석에는 37pt 라고 적혀 있다.
            TMP_Text claimLabel = UISeasonPassUIFactory.CreateFittedText(
                "Label", claimImage.transform, "일괄 수령", 45f, 30f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(
                claimLabel.rectTransform, new Vector2(300f, 60f), new Vector2(887f - 881.5f, 1815.5f - 1812f));
        }
    }
}
