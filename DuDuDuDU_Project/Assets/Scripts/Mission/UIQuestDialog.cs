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

        /// <summary>메달 위 숫자 — 오늘 깬 미션 수. 게이지는 이 메달에서 오른쪽으로 찬다.</summary>
        [SerializeField] private TMP_Text tierClearedText;

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
        [SerializeField] private TMP_Text dailyTabText;
        [SerializeField] private TMP_Text achievementTabText;

        /// <summary>선택된 탭 그림(Popup_Select_Btn_Prs)과 선택 안 된 탭 그림(_Normal).</summary>
        [SerializeField] private Sprite tabOnSprite;
        [SerializeField] private Sprite tabOffSprite;
        [SerializeField] private GameObject achievementTabDot;

        [SerializeField] private TMP_Text emptyText;

        /// <summary>
        /// 추가 보상 게이지 트랙의 너비. <b>칸 배치에서 역산한 값이라 임의로 못 바꾼다.</b>
        ///
        /// 칸 4개의 슬롯 중심이 PSD 에서 338·509·681·852 (간격 171.3)이다.
        /// 게이지는 눈금 4개(1/4·2/4·3/4·4/4)가 그 중심에 정확히 떨어져야 하므로
        /// 눈금 간격 = 너비/4 = 171.3 → 너비 685.2, 왼쪽 끝 338 - 171.3 = 166.7(메달 뒤에 가려진다),
        /// 오른쪽 끝 852(마지막 슬롯 뒤에 가려진다). 가운데 x = 509.35.
        ///
        /// 이 둘이 어긋나면 "3개 깼는데 게이지가 첫 상자에 안 닿는다" 가 된다.
        /// 칸 폭(<see cref="UIMissionTierItem.ItemSize"/>)을 바꾸면 이 값도 같이 바꾼다.
        /// </summary>
        private const float TierGaugeWidth = 685.2f;

        /// <summary>
        /// 목록 사각형(PSD 좌표). 일일 탭은 안쪽 판 위에서 추가 보상 줄(아래끝 784) 밑,
        /// 업적 탭은 그 줄이 없으므로 탭 바로 아래(440)까지 올라온다. 둘 다 아래끝은 안쪽 판 안(1797)이다.
        /// 폭은 안쪽 판(857)과 같다.
        /// </summary>
        private static readonly Vector2 DailyListSize = new Vector2(857f, 996f);
        private static readonly Vector2 DailyListPosition = new Vector2(537.5f - 540f, 960f - 1299f);
        private static readonly Vector2 AchievementListSize = new Vector2(857f, 1357f);
        private static readonly Vector2 AchievementListPosition = new Vector2(537.5f - 540f, 960f - 1118.5f);

        /// <summary>줄 사이 간격 — 판 높이 184 + 15 = 시안 줄 간격 약 199.</summary>
        private const float RowSpacing = 15f;

        /// <summary>목록 맨 위 여백 — 시안에서 첫 판이 안쪽 판 위끝에서 8 떨어져 시작한다.</summary>
        private const int ListTopPadding = 8;

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
            bool daily = tab == QuestTab.Daily;
            ApplyTab(dailyTabImage, dailyTabText, daily);
            ApplyTab(achievementTabImage, achievementTabText, !daily);

            if (achievementTabDot != null)
                achievementTabDot.SetActive(HasClaimableAchievement());
        }

        /// <summary>선택은 눌린 그림(어두움)+흰 글자, 안 선택은 평평한 그림+c3c4e2 글자(시안 주석).</summary>
        private void ApplyTab(Image image, TMP_Text label, bool selected)
        {
            if (image != null)
            {
                Sprite sprite = selected ? tabOnSprite : tabOffSprite;
                if (sprite != null)
                    image.sprite = sprite;
            }

            if (label != null)
                label.color = selected ? Color.white : UIMissionUIFactory.TabOffTextColor;
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
                resetTimerText.SetText("갱신까지 " + MissionText.ResetCountdown(missions.TimeUntilReset));
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
                rows[i].Bind(view, () => Claim(missionId), Exit);
            }

            SetEmpty(views.Count == 0 ? "오늘의 미션이 없다." : null);
            RefreshTiers(missions);
        }

        private void RefreshTiers(MissionManager missions)
        {
            IReadOnlyList<DailyMissionTier> tiers = DailyMissionDatabaseProvider.Database.Tiers;
            int cleared = missions.DailyClearedCount;

            if (tierClearedText != null)
                tierClearedText.SetText(cleared.ToString());

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
                rows[i].Bind(view, () => ClaimAchievement(achievementId), Exit);
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
        //
        // 좌표는 PSD(Art/0PSD/퀘스트.psd, 1080x1920) bbox 그대로 적고 Pos() 로 화면 중심 기준으로 옮긴다.
        // Image 크기 = 보이는 크기 + 스프라이트 투명 여백 x 배율(Tools/ui/PORTING.md).

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
            Transform p = view.transform;

            Image backdrop = UIMissionUIFactory.CreateImage("Backdrop", p, UIMissionUIFactory.Backdrop);
            UIMissionUIFactory.Stretch(backdrop.rectTransform);

            // 바깥을 눌러도 닫힌다. 전체 화면 창이라 닫기 버튼 하나뿐이면
            // 누를 곳을 못 찾는 사람이 생긴다.
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(backdropButton);

            // 팝업 판 — Ui_Popup_Bg x4, 보이는 924x1678 (75,164) + 여백 3px x4.
            // 판 위의 빈 곳을 눌러도 바깥으로 안 새게 레이캐스트를 받는다(딤 버튼이 닫아 버리면 안 된다).
            Image panel = UIMissionUIFactory.Sliced(p, "Panel", "Upgrade/Ui_Popup_Bg", 4f,
                new Vector2(948f, 1702f), UIMissionUIFactory.Pos(537f, 1003f), Color.white);
            panel.raycastTarget = true;

            dialog.titleText = UIMissionUIFactory.CreateText(
                "Title", p, "퀘스트", 45f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(
                dialog.titleText.rectTransform, new Vector2(600f, 60f), UIMissionUIFactory.Pos(538f, 229f));

            // 닫기 — PSD 에서 잘라 온 버튼 한 장(오프라인 보상 창과 같은 그림).
            Image close = UIMissionUIFactory.Picture(p, "CloseButton", "OffLineReward/FromPsd/FromPsd_CloseButton",
                new Vector2(112f, 111f), UIMissionUIFactory.Pos(911f, 180.5f));
            Button closeButton = UIMissionUIFactory.SpriteButton(close);
            dialog.AddExitButton(closeButton);

            // 안쪽 큰 판 — SmallBox x4 #636481, 보이는 857x1372 (107,428). SmallBox 여백 6/6/7/7 px x4.
            UIMissionUIFactory.Sliced(p, "InnerBox", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(909f, 1424f), UIMissionUIFactory.Pos(537.5f, 1116f), UIMissionUIFactory.InnerBoxColor);

            BakeTimer(dialog, p, font);
            BakeTierRow(dialog, p, font);

            // 일일 탭 크기로 굽는다. 업적 탭으로 넘어가면 RefreshListRect 가 늘린다 —
            // 추가 보상 줄과 겹치면 첫 줄이 보상 상자 밑으로 들어가는데, 스크롤은 되기 때문에
            // 고장으로 안 보인다.
            dialog.listContent = UIMissionUIFactory.CreateScrollList(
                "List", p, DailyListSize, DailyListPosition, RowSpacing, ListTopPadding);
            dialog.listViewport = dialog.listContent.parent as RectTransform;
            dialog.rowTemplate = UIMissionRow.Create(dialog.listContent, font);

            // 탭은 목록 뒤(위)에 그린다 — 목록이 탭 밑으로 스크롤돼 들어가는 일은 없지만,
            // 겹침이 생겨도 탭이 눌리게 하려는 것이다.
            BakeTabs(dialog, p, font);

            dialog.emptyText = UIMissionUIFactory.CreateText(
                "Empty", p, string.Empty, 40f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(
                dialog.emptyText.rectTransform, new Vector2(700f, 80f), UIMissionUIFactory.Pos(537.5f, 1000f));
            dialog.emptyText.gameObject.SetActive(false);

            return dialog;
        }

        /// <summary>
        /// 갱신 타이머 — SmallBox x4 #45465f, 보이는 397x79 (339,447). 글자 35pt.
        /// <b>상자를 통째로 끄고 켜기 위해</b> 글자를 그 자식으로 둔다(<see cref="resetTimerRoot"/> 주석).
        /// </summary>
        private static void BakeTimer(UIQuestDialog dialog, Transform p, TMP_FontAsset font)
        {
            Image timerBg = UIMissionUIFactory.Sliced(p, "ResetTimerBg", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(449f, 131f), UIMissionUIFactory.Pos(539.5f, 488.5f), UIMissionUIFactory.DarkBoxColor);
            dialog.resetTimerRoot = timerBg.gameObject;

            dialog.resetTimerText = UIMissionUIFactory.CreateText(
                "ResetTimer", timerBg.transform, "갱신까지 0분", 35f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(
                dialog.resetTimerText.rectTransform, new Vector2(390f, 50f), new Vector2(-2.5f, 0f));
        }

        private static void BakeTierRow(UIQuestDialog dialog, Transform parent, TMP_FontAsset font)
        {
            // 추가 보상 줄 전체를 한 묶음으로 둔다 — 업적 탭에서 통째로 끈다.
            // 묶음은 화면 전체 크기이고 안은 PSD 좌표(Pos)라서 어디에 놓든 시안과 같다.
            GameObject tierRoot = UIMissionUIFactory.CreateRect("TierRoot", parent);
            UIMissionUIFactory.Stretch(tierRoot.GetComponent<RectTransform>());
            dialog.tierRoot = tierRoot;
            Transform t = tierRoot.transform;

            // 보라 상자 — Mission_Reward x2, 보이는 828x244(시안 823x238) (126,546). 512 그림 한가운데에 보인다.
            UIMissionUIFactory.Picture(t, "RewardBox", "Mission/Mission_Reward",
                new Vector2(1024f, 1024f), UIMissionUIFactory.Pos(537.5f, 665f));

            // 게이지는 상자(위)와 칸(위) <b>사이 뒤</b>를 가로지른다 — 칸·메달이 위에 그려져 양 끝이 가려진다.
            dialog.tierGaugeFill = UIMissionUIFactory.CreateGauge(
                "TierGauge", t, new Vector2(TierGaugeWidth, 48f), UIMissionUIFactory.Pos(509.35f, 646f),
                UIMissionUIFactory.LimeColor, UIMissionUIFactory.DarkBoxColor, 0f);

            // 메달 — Icon_Medal x4, 보이는 96x128 (148,581). 안의 숫자 35pt.
            UIMissionUIFactory.Picture(t, "Medal", "Mission/Icon_Medal",
                new Vector2(128f, 128f), UIMissionUIFactory.Pos(196f, 645f));
            dialog.tierClearedText = UIMissionUIFactory.CreateText(
                "MedalCount", t, "0", 35f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(
                dialog.tierClearedText.rectTransform, new Vector2(90f, 44f), UIMissionUIFactory.Pos(195.5f, 659f));

            GameObject itemRoot = UIMissionUIFactory.CreateRect("TierItems", t);
            RectTransform itemRect = itemRoot.GetComponent<RectTransform>();

            // 칸 넷이 가운데 595(슬롯 중심 338~852의 한가운데), 슬롯 중심 높이 644 에 놓인다.
            UIMissionUIFactory.SetRect(
                itemRect, new Vector2(UIMissionTierItem.ItemSize.x * 4f, UIMissionTierItem.ItemSize.y),
                UIMissionUIFactory.Pos(595f, 644f));

            // 가로로 고르게 편다. 문턱 간격이 3·5·7·9 처럼 고르지 않아도 칸은 등간격이고,
            // 게이지의 눈금도 그 칸 중심에 떨어진다(TierGaugeWidth 주석의 역산).
            // <b>칸 폭을 바꾸면 TierGaugeWidth 도 같이 바꿔야 한다.</b>
            var layout = itemRoot.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 0f;
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
            dialog.tabOnSprite = UIMissionUIFactory.LoadSprite("OffLineReward/Popup_Select_Btn_Prs");
            dialog.tabOffSprite = UIMissionUIFactory.LoadSprite("OffLineReward/Popup_Select_Btn_Normal");

            // Popup_Select_Btn x4, 보이는 360x110. 그림 여백 좌우 3·위 7·아래 3 x4 → 384x150,
            // 그림이 아래로 치우쳐 있어 중심이 보이는 중심보다 8 위다.
            dialog.dailyTabButton = BakeTab(parent, "DailyTab", "일일 퀘스트", dialog.tabOnSprite,
                UIMissionUIFactory.Pos(324f, 346f), font, out dialog.dailyTabImage, out dialog.dailyTabText);

            dialog.achievementTabButton = BakeTab(parent, "AchievementTab", "업적", dialog.tabOffSprite,
                UIMissionUIFactory.Pos(749.5f, 345f), font, out dialog.achievementTabImage, out dialog.achievementTabText);

            Image dot = UIMissionUIFactory.CreateImage(
                "Dot", dialog.achievementTabButton.transform, UIMissionUIFactory.RedDotColor);
            UIMissionUIFactory.SetRect(dot.rectTransform, new Vector2(30f, 30f), new Vector2(165f, 40f));
            dot.raycastTarget = false;
            dialog.achievementTabDot = dot.gameObject;
        }

        private static Button BakeTab(
            Transform parent, string name, string label, Sprite sprite, Vector2 position,
            TMP_FontAsset font, out Image image, out TMP_Text text)
        {
            image = UIMissionUIFactory.Sliced(parent, name, "OffLineReward/Popup_Select_Btn_Normal", 4f,
                new Vector2(384f, 150f), position, Color.white);
            image.sprite = sprite;
            Button button = UIMissionUIFactory.SpriteButton(image);

            text = UIMissionUIFactory.CreateText(
                "Label", image.transform, label, 45f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(text.rectTransform, new Vector2(340f, 60f), new Vector2(1.5f, -5f));
            return button;
        }
    }
}
