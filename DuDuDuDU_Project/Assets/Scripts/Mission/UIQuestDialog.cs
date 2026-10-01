using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.UI;

namespace OJ.Mission
{
    /// <summary>퀘스트 창의 탭.</summary>
    public enum QuestTab
    {
        Daily = 0,
        Achievement,
    }

    /// <summary>
    /// 일일 미션과 업적을 한 창에서 보여 준다.
    ///
    /// <b>탭 둘이 같은 목록 영역을 쓴다.</b> 창을 둘로 나누면 열고 닫는 경로가 둘이 되고,
    /// 로비 버튼이 어느 쪽을 열지 매번 정해야 한다. 지금 기획의 두 컨텐츠는 "받을 것이
    /// 있나" 라는 질문이 같아서, 한 창에서 탭으로 가르는 쪽이 맞다.
    ///
    /// <b>추가 보상 게이지는 일일 탭에만 있다.</b> 업적에는 "n개 깨면" 이라는 축이 없다.
    /// 숨기지 않고 비워 두면 업적 탭에서 빈 게이지가 남아 고장으로 읽힌다.
    ///
    /// <b>목록은 열 때마다 다시 그린다.</b> 11~17줄짜리라 재활용 목록을 쓸 이유가 없고,
    /// 매번 지었다 짓는 편이 "전투 중에 쌓인 카운트가 반영 안 된다" 는 사고를 원천적으로 없앤다.
    /// </summary>
    public sealed class UIQuestDialog : DialogBase
    {
        [SerializeField] private TMP_Text titleText;

        /// <summary>남은 시간 글자. <see cref="resetTimerRoot"/> 의 자식이다.</summary>
        [SerializeField] private TMP_Text resetTimerText;

        /// <summary>
        /// 남은 시간 상자(배경). <b>글자가 아니라 이것을 끈다.</b> 글자만 끄면 업적 탭에
        /// 빈 검은 상자가 남는데, 그건 "값을 못 불러왔다" 로 읽힌다.
        /// </summary>
        [SerializeField] private GameObject resetTimerRoot;

        [SerializeField] private GameObject tierRoot;
        [SerializeField] private Image tierGaugeFill;
        [SerializeField] private RectTransform tierItemRoot;
        [SerializeField] private UIMissionTierItem tierTemplate;

        [SerializeField] private RectTransform listContent;
        [SerializeField] private UIMissionRow rowTemplate;

        /// <summary>
        /// 목록이 차지하는 사각형. <b>탭마다 크기가 다르다</b> — 업적 탭은 추가 보상 줄이
        /// 없으므로 그 자리까지 목록이 올라온다. 고정해 두면 업적 탭 위쪽에 창 높이의
        /// 1/4 짜리 빈칸이 남는다.
        /// </summary>
        [SerializeField] private RectTransform listViewport;

        [SerializeField] private Button dailyTabButton;
        [SerializeField] private Button achievementTabButton;
        [SerializeField] private Image dailyTabImage;
        [SerializeField] private Image achievementTabImage;
        [SerializeField] private GameObject achievementTabDot;

        [SerializeField] private TMP_Text emptyText;

        /// <summary>
        /// 추가 보상 게이지 트랙의 너비. <b>칸 배치에서 역산한 값이라 임의로 못 바꾼다.</b>
        ///
        /// 칸 4개가 120 폭 + 24 간격으로 놓이므로 중심이 -216·-72·+72·+216 이고,
        /// 게이지는 눈금 4개(1/4·2/4·3/4·4/4)가 그 중심에 정확히 떨어져야 한다.
        /// 눈금 간격 = 너비/4 = 144 → 너비 576, 왼쪽 끝 -360, 오른쪽 끝 +216
        /// (그래서 트랙 중심이 0 이 아니라 -72 다 — <see cref="TierGaugeCenterX"/>).
        ///
        /// 이 둘이 어긋나면 "3개 깼는데 게이지가 첫 상자에 안 닿는다" 가 된다.
        /// </summary>
        private const float TierGaugeWidth = 576f;

        /// <summary>게이지 트랙의 가로 중심. 위 주석의 역산 결과다.</summary>
        private const float TierGaugeCenterX = -72f;

        /// <summary>
        /// 목록 사각형. 탭마다 다르다 — 일일 탭은 추가 보상 줄(아래끝 315) 밑에서 시작하고,
        /// 업적 탭은 그 줄이 없으므로 제목 바로 아래(570)까지 올라온다.
        /// 둘 다 아래끝은 탭 버튼 위(-545)다.
        /// </summary>
        private static readonly Vector2 DailyListSize = new Vector2(880f, 840f);
        private static readonly Vector2 DailyListPosition = new Vector2(0f, -122f);
        private static readonly Vector2 AchievementListSize = new Vector2(880f, 1110f);
        private static readonly Vector2 AchievementListPosition = new Vector2(0f, 10f);

        private readonly List<UIMissionRow> rows = new List<UIMissionRow>();
        private readonly List<UIMissionTierItem> tierItems = new List<UIMissionTierItem>();

        private QuestTab tab = QuestTab.Daily;

        /// <summary>
        /// 남은 시간 글자를 다시 쓰는 간격(초). 분 단위로만 적으므로 1초마다 고칠 이유가 없다.
        /// </summary>
        private const float TimerRefreshSeconds = 20f;

        private float nextTimerRefresh;

        protected override void OnLoad()
        {
            UseBackBtn = true;

            if (rowTemplate != null)
                rowTemplate.gameObject.SetActive(false);

            if (tierTemplate != null)
                tierTemplate.gameObject.SetActive(false);

            if (dailyTabButton != null)
                dailyTabButton.onClick.AddListener(ShowDaily);

            if (achievementTabButton != null)
                achievementTabButton.onClick.AddListener(ShowAchievements);
        }

        protected override void OnUnload()
        {
            if (dailyTabButton != null)
                dailyTabButton.onClick.RemoveListener(ShowDaily);

            if (achievementTabButton != null)
                achievementTabButton.onClick.RemoveListener(ShowAchievements);
        }

        protected override void OnEnter()
        {
            // 열 때마다 일일 탭에서 시작한다. 닫을 때의 탭을 기억하면 "업적을 보고 닫았더니
            // 다음에 일일 미션이 안 보인다" 가 되는데, 매일 받는 쪽이 일일이다.
            tab = QuestTab.Daily;
            nextTimerRefresh = 0f;

            MissionManager missions = MissionManager.Instance;
            if (missions != null)
                missions.OnChanged += Refresh;

            Refresh();
        }

        protected override void OnExit()
        {
            MissionManager missions = MissionManager.Instance;
            if (missions != null)
                missions.OnChanged -= Refresh;
        }

        private void Update()
        {
            if (!isEnter || resetTimerText == null)
                return;

            // 배속·일시정지를 타지 않는다. 로비에서는 둘 다 1 이지만, 이 창을 전투에서
            // 열게 되는 날 글자가 배속만큼 빨리 바뀌는 것은 말이 안 된다.
            if (Time.unscaledTime < nextTimerRefresh)
                return;

            nextTimerRefresh = Time.unscaledTime + TimerRefreshSeconds;
            RefreshTimer();
        }

        private void ShowDaily()
        {
            tab = QuestTab.Daily;
            Refresh();
        }

        private void ShowAchievements()
        {
            tab = QuestTab.Achievement;
            Refresh();
        }

        // ── 그리기 ─────────────────────────────────────────────────────

        private void Refresh()
        {
            MissionManager missions = MissionManager.Instance;
            if (missions == null)
            {
                // 컨테이너가 서기 전에 열렸다. 빈 창을 보여 주되 조용히 넘어가지는 않는다 —
                // 이 상태는 배선 사고이지 "할 미션이 없음" 이 아니다.
                Debug.LogError("[퀘스트] MissionManager 가 없다. 목록을 그리지 못한다.");
                SetEmpty("미션을 불러오지 못했다.");
                return;
            }

            RefreshTabs();
            RefreshTimer();
            RefreshListRect();

            if (tab == QuestTab.Daily)
                RefreshDaily(missions);
            else
                RefreshAchievements(missions);
        }

        private void RefreshTabs()
        {
            if (dailyTabImage != null)
            {
                dailyTabImage.color = tab == QuestTab.Daily
                    ? UIMissionUIFactory.TabOnColor
                    : UIMissionUIFactory.TabOffColor;
            }

            if (achievementTabImage != null)
            {
                achievementTabImage.color = tab == QuestTab.Achievement
                    ? UIMissionUIFactory.TabOnColor
                    : UIMissionUIFactory.TabOffColor;
            }

            if (achievementTabDot != null)
                achievementTabDot.SetActive(HasClaimableAchievement());
        }

        private bool HasClaimableAchievement()
        {
            MissionManager missions = MissionManager.Instance;
            if (missions == null)
                return false;

            List<AchievementView> views = missions.GetAchievements();
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i].Claimable)
                    return true;
            }

            return false;
        }

        private void RefreshTimer()
        {
            MissionManager missions = MissionManager.Instance;

            // 업적은 리셋되지 않는다. 그 탭에서 남은 시간을 띄우면 업적이 사라지는 것처럼 읽힌다.
            bool show = tab == QuestTab.Daily && missions != null;

            // 글자가 아니라 상자를 끈다. 글자만 끄면 빈 상자가 남아 "값을 못 불러왔다" 로 보인다.
            if (resetTimerRoot != null)
                resetTimerRoot.SetActive(show);

            if (show && resetTimerText != null)
                resetTimerText.SetText(MissionText.ResetCountdown(missions.TimeUntilReset));
        }

        /// <summary>
        /// 탭에 맞춰 목록 사각형을 맞춘다. 일일 탭은 추가 보상 줄 아래, 업적 탭은 그 자리까지.
        /// </summary>
        private void RefreshListRect()
        {
            if (listViewport == null)
                return;

            bool daily = tab == QuestTab.Daily;
            listViewport.sizeDelta = daily ? DailyListSize : AchievementListSize;
            listViewport.anchoredPosition = daily ? DailyListPosition : AchievementListPosition;
        }

        private void RefreshDaily(MissionManager missions)
        {
            if (tierRoot != null)
                tierRoot.SetActive(true);

            List<DailyMissionView> views = missions.GetDailyMissions();

            EnsureRows(views.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                if (i >= views.Count)
                {
                    rows[i].gameObject.SetActive(false);
                    continue;
                }

                DailyMissionView view = views[i];
                rows[i].gameObject.SetActive(true);

                // id 를 지역 변수로 떼어 낸다. 대리자가 view 를 통째로 잡으면 다음 갱신의
                // 값을 보게 되고, 그러면 <b>엉뚱한 미션의 보상을 받는다.</b>
                string missionId = view.Definition.id;
                rows[i].Bind(view, () => Claim(missionId));
            }

            SetEmpty(views.Count == 0 ? "오늘의 미션이 없다." : null);
            RefreshTiers(missions);
        }

        private void RefreshTiers(MissionManager missions)
        {
            IReadOnlyList<DailyMissionTier> tiers = DailyMissionDatabaseProvider.Database.Tiers;
            int cleared = missions.DailyClearedCount;

            List<int> thresholds = DailyMissionDatabaseProvider.Database.GetTierThresholds();
            UIMissionUIFactory.SetGauge(
                tierGaugeFill, TierGaugeWidth, MissionRules.TierGaugeProgress(cleared, thresholds));

            EnsureTierItems(tiers.Count);
            for (int i = 0; i < tierItems.Count; i++)
            {
                if (i >= tiers.Count || tiers[i] == null)
                {
                    tierItems[i].gameObject.SetActive(false);
                    continue;
                }

                DailyMissionTier tier = tiers[i];
                tierItems[i].gameObject.SetActive(true);

                int threshold = tier.requiredClearCount;
                tierItems[i].Bind(
                    tier,
                    cleared >= threshold,
                    missions.IsTierClaimed(threshold),
                    () => ClaimTier(threshold));
            }
        }

        private void RefreshAchievements(MissionManager missions)
        {
            if (tierRoot != null)
                tierRoot.SetActive(false);

            List<AchievementView> views = missions.GetAchievements();

            EnsureRows(views.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                if (i >= views.Count)
                {
                    rows[i].gameObject.SetActive(false);
                    continue;
                }

                AchievementView view = views[i];
                rows[i].gameObject.SetActive(true);

                string achievementId = view.Definition.id;
                rows[i].Bind(view, () => ClaimAchievement(achievementId));
            }

            SetEmpty(views.Count == 0 ? "업적 데이터가 없다." : null);
        }

        private void SetEmpty(string message)
        {
            if (emptyText == null)
                return;

            bool show = !string.IsNullOrEmpty(message);
            emptyText.gameObject.SetActive(show);

            if (show)
                emptyText.SetText(message);
        }

        // ── 수령 ───────────────────────────────────────────────────────

        // 수령 뒤에 Refresh 를 부르지 않는다. 매니저가 OnChanged 를 쏘고 이 창이 그것을
        // 구독하고 있어서, 여기서 또 부르면 같은 프레임에 목록을 두 번 짓는다.

        private void Claim(string missionId)
        {
            MissionManager missions = MissionManager.Instance;
            missions?.TryClaimDaily(missionId);
        }

        private void ClaimTier(int requiredClearCount)
        {
            MissionManager missions = MissionManager.Instance;
            missions?.TryClaimDailyTier(requiredClearCount);
        }

        private void ClaimAchievement(string achievementId)
        {
            MissionManager missions = MissionManager.Instance;
            missions?.TryClaimAchievement(achievementId);
        }

        // ── 줄 풀 ──────────────────────────────────────────────────────

        private void EnsureRows(int count)
        {
            if (rowTemplate == null || listContent == null)
                return;

            while (rows.Count < count)
            {
                UIMissionRow row = Instantiate(rowTemplate, listContent);
                row.gameObject.SetActive(true);
                rows.Add(row);
            }
        }

        private void EnsureTierItems(int count)
        {
            if (tierTemplate == null || tierItemRoot == null)
                return;

            while (tierItems.Count < count)
            {
                UIMissionTierItem item = Instantiate(tierTemplate, tierItemRoot);
                item.gameObject.SetActive(true);
                tierItems.Add(item);
            }
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        /// <summary>창 하나를 조립한다. 에디터 굽기 경로에서만 부른다.</summary>
        public static UIQuestDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIMissionUIFactory.CreateRect("UIQuestDialog", parent);
            UIMissionUIFactory.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIQuestDialog>();
            dialog.BakeOpenStyle(DialogOpenStyle.Popup);

            // dialogView 는 루트가 아니라 자식이어야 한다. DialogBase 가 이것을 끄고 켜는데,
            // 루트를 끄면 컴포넌트까지 같이 잠들어 다시 열 수 없다.
            GameObject view = UIMissionUIFactory.CreateRect("View", root.transform);
            UIMissionUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;

            Image backdrop = UIMissionUIFactory.CreateImage("Backdrop", view.transform, UIMissionUIFactory.Backdrop);
            UIMissionUIFactory.Stretch(backdrop.rectTransform);

            // 바깥을 눌러도 닫힌다. 전체 화면 창이라 닫기 버튼 하나뿐이면
            // 누를 곳을 못 찾는 사람이 생긴다.
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            dialog.AddExitButton(backdropButton);

            Image panel = UIMissionUIFactory.CreateImage("Panel", view.transform, UIMissionUIFactory.PanelColor);
            UIMissionUIFactory.SetRect(panel.rectTransform, new Vector2(960f, 1420f), Vector2.zero);

            dialog.titleText = UIMissionUIFactory.CreateText(
                "Title", panel.transform, "퀘스트", 64f,
                TextAlignmentOptions.Center, UIMissionUIFactory.DarkText, font);
            UIMissionUIFactory.SetRect(
                dialog.titleText.rectTransform, new Vector2(600f, 80f), new Vector2(0f, 630f));

            Button close = UIMissionUIFactory.CreateButton(
                "CloseButton", panel.transform, "X", new Vector2(96f, 96f), new Vector2(430f, 640f),
                UIMissionUIFactory.ClaimColor, UIMissionUIFactory.DarkText, 48f, font);
            dialog.AddExitButton(close);

            dialog.resetTimerText = UIMissionUIFactory.CreateText(
                "ResetTimer", panel.transform, "0분", 40f,
                TextAlignmentOptions.Center, UIMissionUIFactory.LightText, font);
            Image timerBg = UIMissionUIFactory.CreateImage(
                "ResetTimerBg", panel.transform, UIMissionUIFactory.TrackColor);
            UIMissionUIFactory.SetRect(timerBg.rectTransform, new Vector2(460f, 72f), new Vector2(0f, 530f));
            timerBg.raycastTarget = false;
            dialog.resetTimerText.transform.SetParent(timerBg.transform, false);
            UIMissionUIFactory.SetRect(
                dialog.resetTimerText.rectTransform, new Vector2(460f, 72f), Vector2.zero);
            dialog.resetTimerRoot = timerBg.gameObject;

            BakeTierRow(dialog, panel.transform, font);

            // 일일 탭 크기로 굽는다. 업적 탭으로 넘어가면 RefreshListRect 가 늘린다 —
            // 추가 보상 줄과 겹치면 첫 줄이 선물 상자 밑으로 들어가는데, 스크롤은 되기 때문에
            // 고장으로 안 보인다.
            dialog.listContent = UIMissionUIFactory.CreateScrollList(
                "List", panel.transform, DailyListSize, DailyListPosition, 14f);
            dialog.listViewport = dialog.listContent.parent as RectTransform;
            dialog.rowTemplate = UIMissionRow.Create(dialog.listContent, font);

            BakeTabs(dialog, panel.transform, font);

            dialog.emptyText = UIMissionUIFactory.CreateText(
                "Empty", panel.transform, string.Empty, 40f,
                TextAlignmentOptions.Center, UIMissionUIFactory.MutedText, font);
            UIMissionUIFactory.SetRect(
                dialog.emptyText.rectTransform, new Vector2(700f, 80f), new Vector2(0f, -105f));
            dialog.emptyText.gameObject.SetActive(false);

            return dialog;
        }

        private static void BakeTierRow(UIQuestDialog dialog, Transform parent, TMP_FontAsset font)
        {
            GameObject tierRoot = UIMissionUIFactory.CreateRect("TierRoot", parent);
            UIMissionUIFactory.SetRect(
                tierRoot.GetComponent<RectTransform>(), new Vector2(880f, 210f), new Vector2(0f, 420f));
            dialog.tierRoot = tierRoot;

            // 게이지는 상자(위)와 숫자(아래) <b>사이</b>를 가로지른다. 셋의 세로 범위가
            // 겹치면 숫자가 바 위에 찍혀 둘 다 안 읽힌다 — 첫 굽기에서 그랬다.
            // 상자 아래끝 -14 · 게이지 -41..-19 · 숫자 위끝 -46 으로 5px 씩 띄워 뒀다.
            dialog.tierGaugeFill = UIMissionUIFactory.CreateGauge(
                "TierGauge", tierRoot.transform, new Vector2(TierGaugeWidth, 22f),
                new Vector2(TierGaugeCenterX, -30f), UIMissionUIFactory.FillColor);

            GameObject itemRoot = UIMissionUIFactory.CreateRect("TierItems", tierRoot.transform);
            RectTransform itemRect = itemRoot.GetComponent<RectTransform>();
            UIMissionUIFactory.SetRect(itemRect, new Vector2(880f, 210f), Vector2.zero);

            // 가로로 고르게 편다. 문턱 간격이 3·5·7·9 처럼 고르지 않아도 칸은 등간격이고,
            // 게이지의 눈금도 그 칸 중심에 떨어진다(TierGaugeWidth 주석의 역산).
            // <b>칸 폭·간격을 바꾸면 TierGaugeWidth 도 같이 바꿔야 한다.</b>
            var layout = itemRoot.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            dialog.tierItemRoot = itemRect;
            dialog.tierTemplate = UIMissionTierItem.Create(itemRect, font);
        }

        private static void BakeTabs(UIQuestDialog dialog, Transform parent, TMP_FontAsset font)
        {
            dialog.dailyTabButton = UIMissionUIFactory.CreateButton(
                "DailyTab", parent, "일일 퀘스트", new Vector2(430f, 110f), new Vector2(-225f, -620f),
                UIMissionUIFactory.TabOnColor, UIMissionUIFactory.LightText, 42f, font);
            dialog.dailyTabImage = dialog.dailyTabButton.GetComponent<Image>();

            dialog.achievementTabButton = UIMissionUIFactory.CreateButton(
                "AchievementTab", parent, "업적", new Vector2(430f, 110f), new Vector2(225f, -620f),
                UIMissionUIFactory.TabOffColor, UIMissionUIFactory.LightText, 42f, font);
            dialog.achievementTabImage = dialog.achievementTabButton.GetComponent<Image>();

            Image dot = UIMissionUIFactory.CreateImage(
                "Dot", dialog.achievementTabButton.transform, UIMissionUIFactory.RedDotColor);
            UIMissionUIFactory.SetRect(dot.rectTransform, new Vector2(30f, 30f), new Vector2(190f, 42f));
            dot.raycastTarget = false;
            dialog.achievementTabDot = dot.gameObject;
        }
    }
}
