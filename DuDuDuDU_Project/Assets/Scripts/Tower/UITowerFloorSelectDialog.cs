using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.UI;

namespace OJ.Tower
{
    /// <summary>
    /// 층 선택 화면. 기획서 5.2.
    ///
    /// <b>목록의 방향이 탑의 방향과 같다.</b> 맨 위가 최고층(300), 맨 아래가 1층이다.
    /// 스크롤을 올리는 동작이 곧 "위층을 올려다보는" 동작이 된다.
    ///
    /// <b>300층을 다 보여 주되 카드는 보이는 만큼만 만든다</b>(<see cref="UIRecycleVerticalList"/>).
    /// 창이 열릴 때마다 <b>도전 가능한 층이 위에서 두 번째 칸</b>에 오도록 스크롤한다 —
    /// 시안처럼 바로 위의 잠긴 한 층이 "위가 더 있다" 를 말없이 알려 주고, 시선이 도전할
    /// 층에서 시작한다(사용자 지시 2026-10-01: 최고층까지 보이되 첫 진입엔 도전 층이 보일 것).
    /// </summary>
    public class UITowerFloorSelectDialog : DialogBase
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bestFloorText;

        [Header("현재 진행 요약")]
        [SerializeField] private TMP_Text currentFloorText;
        [SerializeField] private TMP_Text currentConceptText;
        [SerializeField] private Image bandGaugeFill;
        [SerializeField] private TMP_Text bandGaugeText;

        /// <summary>
        /// 게이지 기준을 숫자로 — "이번 구간에서 몇 번째 층 / 구간 층 수"(예: 5/5).
        /// 게이지만 있으면 무엇을 재는지 알 수 없다(사용자 피드백 2026-10-01).
        /// </summary>
        [SerializeField] private TMP_Text bandProgressText;

        [Header("목표 배너")]
        [SerializeField] private TMP_Text unlockGoalText;
        [SerializeField] private TMP_Text bandRewardText;

        [Header("목록")]
        [SerializeField] private RectTransform listContent;
        [SerializeField] private UITowerFloorCard cardTemplate;
        [SerializeField] private UIRecycleVerticalList floorList;

        [Header("하단")]
        [SerializeField] private Button rewardListButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private TMP_Text challengeLabel;

        [Header("보상 목록 오버레이")]
        [SerializeField] private GameObject rewardOverlay;
        [SerializeField] private TMP_Text rewardOverlayText;
        [SerializeField] private Button rewardOverlayCloseButton;

        /// <summary>
        /// <b><c>Awake</c> 를 쓰지 않는다.</b> <c>DialogBase.Awake</c> 가 private 이라
        /// 파생 클래스가 같은 이름을 선언하면 Unity 가 그쪽만 부르고 <c>Load()</c> 가
        /// 통째로 건너뛰어진다 — 창이 열리긴 하는데 버튼이 하나도 안 먹는 형태로 드러난다.
        /// 이 프로젝트의 다이얼로그 여덟 개가 전부 <c>OnLoad</c> 를 쓰는 이유다.
        /// </summary>
        protected override void OnLoad()
        {
            // 목록 템플릿은 <b>프리팹 안에 꺼진 채로</b> 들어 있다. 런타임에 이것을 복제해
            // 카드를 만든다 — 카탈로그에 카드용 프리팹을 따로 등재하면 그것이 "열 수 있는
            // 창" 으로 보이고, DialogCatalogBuilder 의 검사에도 걸린다.
            if (cardTemplate != null)
                cardTemplate.gameObject.SetActive(false);

            if (rewardListButton != null)
                rewardListButton.onClick.AddListener(OpenRewardOverlay);

            if (rewardOverlayCloseButton != null)
                rewardOverlayCloseButton.onClick.AddListener(CloseRewardOverlay);

            if (challengeButton != null)
                challengeButton.onClick.AddListener(OnClickChallenge);
        }

        protected override void OnDestroy()
        {
            if (rewardListButton != null)
                rewardListButton.onClick.RemoveListener(OpenRewardOverlay);

            if (rewardOverlayCloseButton != null)
                rewardOverlayCloseButton.onClick.RemoveListener(CloseRewardOverlay);

            if (challengeButton != null)
                challengeButton.onClick.RemoveListener(OnClickChallenge);

            base.OnDestroy();
        }

        protected override void OnEnter()
        {
            transform.SetAsLastSibling();
            CloseRewardOverlay();
            Refresh();
            ScrollToChallengeFloor();
        }

        /// <summary>도전 가능한 층을 위에서 두 번째 칸에 둔다. 클래스 주석 참조.</summary>
        private void ScrollToChallengeFloor()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null || floorList == null)
                return;

            floorList.ScrollTo(IndexOfFloor(progress.HighestUnlockedFloor), 1);
        }

        /// <summary>목록 번호 0 이 최고층이다.</summary>
        private static int IndexOfFloor(int floor)
        {
            return TowerFormula.TotalFloors - TowerFormula.ClampFloor(floor);
        }

        private static int FloorOfIndex(int index)
        {
            return TowerFormula.TotalFloors - index;
        }

        /// <summary>
        /// 화면을 다시 그린다. 전투에서 돌아왔을 때도 이것 하나면 최신이 된다.
        /// </summary>
        public void Refresh()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
                return;

            int highestUnlocked = progress.HighestUnlockedFloor;
            TowerFloorPlan currentPlan = TowerDatabaseProvider.GetPlan(highestUnlocked);

            if (titleText != null)
                titleText.SetText("무한의 탑");

            if (bestFloorText != null)
            {
                bestFloorText.SetText(progress.HighestClearedFloor > 0
                    ? "최고 " + progress.HighestClearedFloor + "층"
                    : "기록 없음");
            }

            // 시안은 층과 이름을 두 칸으로 나눈다(층은 금색 왼쪽, 이름은 흰색 가운데).
            // 옛 프리팹처럼 이름 칸이 없으면 한 줄로 합쳐 적는다.
            if (currentFloorText != null)
            {
                currentFloorText.SetText(currentConceptText != null
                    ? highestUnlocked + "층"
                    : highestUnlocked + "층 · " + currentPlan.DisplayName);
            }

            if (currentConceptText != null)
                currentConceptText.SetText(currentPlan.DisplayName);

            RefreshBandGauge(highestUnlocked);
            RefreshGoalBanner(progress, highestUnlocked);
            RefreshList(progress, highestUnlocked);

            if (challengeLabel != null)
                challengeLabel.SetText(highestUnlocked + "층 도전");
        }

        private void RefreshBandGauge(int currentFloor)
        {
            int remaining = TowerFormula.FloorsUntilBandReward(currentFloor);
            int done = TowerFormula.FloorInBand(currentFloor);

            UITowerUIFactory.SetGauge(bandGaugeFill, GaugeWidth,
                (float)done / TowerFormula.FloorsPerBand);

            if (bandProgressText != null)
                bandProgressText.SetText("{0}/{1}", done, TowerFormula.FloorsPerBand);

            if (bandGaugeText == null)
                return;

            bandGaugeText.SetText(remaining <= 0
                ? "이번 층이 구간 보상 층"
                : "구간 보상까지 " + remaining + "층");
        }

        private void RefreshGoalBanner(TowerProgressManager progress, int currentFloor)
        {
            if (unlockGoalText != null)
            {
                DiceUnlockDefinition next = progress.GetNextUnlock();
                unlockGoalText.SetText(next != null
                    ? next.towerFloor + "층 · " + next.diceType + " 해금"
                    : "모든 다이스 사용 가능");
            }

            if (bandRewardText != null)
            {
                int bandLastFloor = TowerFormula.BandStartFloor(TowerFormula.BandOf(currentFloor))
                                    + TowerFormula.FloorsPerBand - 1;
                bandLastFloor = TowerFormula.ClampFloor(bandLastFloor);
                bandRewardText.SetText(bandLastFloor + "층 · 다이아 " +
                                       TowerFormula.BandRewardDia(bandLastFloor) + " · 신화 스크롤 " +
                                       TowerFormula.BandRewardMaterial(bandLastFloor));
            }
        }

        private void RefreshList(TowerProgressManager progress, int highestUnlocked)
        {
            if (floorList != null)
                floorList.SetCount(TowerFormula.TotalFloors, BindCard);
        }

        /// <summary>
        /// 목록이 보이는 칸을 채울 때마다 부른다. 카드는 돌려 쓰므로 <b>모든 칸을 매번
        /// 전부 덮어써야 한다</b> — 한 칸이라도 조건부로 건너뛰면 지난 층의 값이 남는다.
        /// </summary>
        private void BindCard(int index, RectTransform item)
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            UITowerFloorCard card = item.GetComponent<UITowerFloorCard>();
            if (progress == null || card == null)
                return;

            int floor = FloorOfIndex(index);
            bool revealed = progress.IsFloorRevealed(floor);

            card.Bind(
                floor: floor,
                plan: revealed ? TowerDatabaseProvider.GetPlan(floor) : null,
                revealed: revealed,
                challengeable: progress.IsFloorChallengeable(floor),
                cleared: progress.IsFloorCleared(floor),
                bestClearMilliseconds: progress.GetBestClearMilliseconds(floor),
                bestRemainingPercent: progress.GetBestRemainingHpPercent(floor),
                onClick: OnFloorClicked);
        }

        private void OnFloorClicked(int floor)
        {
            OpenLoadout(floor);
        }

        private void OnClickChallenge()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
                return;

            OpenLoadout(progress.HighestUnlockedFloor);
        }

        /// <summary>
        /// 편성 화면으로 넘긴다. 기획서 5.1 의 "층 선택 → 다이스 편성".
        ///
        /// <b>이 창을 닫는다.</b> 뒤에 남겨 두면 편성 화면을 백키로 닫았을 때 두 창이
        /// 겹쳐 있는 상태가 되고, 어느 쪽이 위인지가 만들어진 순서에 달리게 된다.
        /// </summary>
        private void OpenLoadout(int floor)
        {
            UITowerLoadoutDialog loadout = GameContainer.UI?.Get<UITowerLoadoutDialog>();
            if (loadout == null)
            {
                Debug.LogError("[탑] 편성 창을 열지 못했다. 카탈로그에 UITowerLoadoutDialog 가 있는지 볼 것.");
                return;
            }

            Exit();
            loadout.Open(floor);
        }

        // ── 보상 목록 ───────────────────────────────────────────────────
        //
        // 기획서 5.2 하단의 "보상 목록" 버튼이다. <b>별도 창을 만들지 않고</b> 이 창 안의
        // 오버레이로 둔다 — 카탈로그 항목이 하나 늘면 그만큼 등재 누락으로 안 열리는
        // 자리가 늘고, 내용이 목록 한 장이라 창을 옮겨 다닐 값어치가 없다.

        private void OpenRewardOverlay()
        {
            if (rewardOverlay == null)
                return;

            if (rewardOverlayText != null)
                rewardOverlayText.SetText(TowerRewardListText.Build());

            rewardOverlay.SetActive(true);
        }

        private void CloseRewardOverlay()
        {
            if (rewardOverlay != null)
                rewardOverlay.SetActive(false);
        }

        /// <summary>
        /// 백키(Esc). 보상 목록이 열려 있으면 그것만 닫고, 아니면 이 창을 닫아 로비로 간다.
        /// </summary>
        public override void BackKeyCall()
        {
            if (rewardOverlay != null && rewardOverlay.activeSelf)
            {
                CloseRewardOverlay();

                // 백키 관리자는 부르기 전에 이 창을 스택에서 꺼냈다. 안쪽만 닫았으니 되돌려 둔다.
                KeepOnBackStack();
                return;
            }

            base.BackKeyCall();
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // 값을 아는 것은 코드이므로 인스펙터에 좌표를 옮겨 적지 않는다.
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 구간 게이지의 폭. 시안의 "강화 업그레이드 때 쓰던 게이지 688x19" 다.
        /// 런타임 <see cref="RefreshBandGauge"/> 도 이 값으로 채움 폭을 계산한다.
        /// </summary>
        private const float GaugeWidth = 688f;
        private const float GaugeHeight = 19f;

        // 채움 그림(Upgrade_Gauge_Full)은 32px 중 가로 5~27, 세로 6~25 에만 색이 있다.
        // Image 를 그 여백만큼 트랙 밖으로 키워 보이는 초록이 트랙에 딱 겹치게 한다.
        // <b>fillAmount 는 진행률 그대로다</b> — uGUI 가 Filled 를 그릴 때 스프라이트의 투명
        // 여백을 빼고 보이는 영역 안에서 비율을 먹인다(실측: 0.5 → 트랙 정확히 절반).
        private const float GaugeArtSize = 32f;
        private const float GaugeArtLeft = 5f;
        private const float GaugeArtRight = 27f;
        private const float GaugeArtTop = 6f;
        private const float GaugeArtBottom = 25f;



        /// <summary>
        /// 목록 영역. 시안의 첫 카드가 진행 패널 바로 밑(y 480)에서 시작한다.
        /// 높이 1000 · PosY -20 (사용자 조정, 2026-10-01).
        /// </summary>
        private const float ListTop = 480f;
        private const float ListBottom = 1480f;

        /// <summary>
        /// 카드 사이 간격. 시안의 카드 간격이 172~184 로 조금씩 흔들려서 평균(≈179)을 쓴다.
        /// </summary>
        private const float CardSpacing = 7f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UITowerFloorSelectDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UITowerUIFactory.CreateRect("UITowerFloorSelectDialog", parent);
            UITowerUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UITowerUIFactory.CreateRect("DialogView", root.transform);
            UITowerUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UITowerFloorSelectDialog>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

            // 성 화면 틀 — 전체 화면이라 바깥을 눌러 닫는 길이 없다. 뒤로 버튼이 그 길이다.
            UITowerUIFactory.CreateCastleScreen(view.transform, font, out dialog.titleText, out Button back);
            dialog.AddExitButton(back);

            Transform p = view.transform;
            float cx = UITowerUIFactory.PanelCenterX;

            // (1) 현재 진행 — Infinity_Popup_Bg_Dark x4, 764x216 (y 267~483)
            UITowerUIFactory.CreateSprite("Summary", p,
                UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Popup_Bg_Dark"), 4f,
                UITowerUIFactory.PanelRect(UITowerUIFactory.PanelWidth, 216f), UITowerUIFactory.Pos(cx, 375f), true);

            TMP_Text caption = UITowerUIFactory.CreateText("Caption", p, "현재 진행", 30f,
                TextAlignmentOptions.Left, UITowerUIFactory.TextMuted, font);
            UITowerUIFactory.SetRect(caption.rectTransform, new Vector2(300f, 40f), UITowerUIFactory.Pos(343f, 305f));

            dialog.currentFloorText = UITowerUIFactory.CreateText("CurrentFloor", p, "102층", 40f,
                TextAlignmentOptions.Left, UITowerUIFactory.TextGold, font);
            UITowerUIFactory.SetRect(dialog.currentFloorText.rectTransform,
                new Vector2(170f, 52f), UITowerUIFactory.Pos(280f, 357f));

            dialog.currentConceptText = UITowerUIFactory.CreateText("CurrentConcept", p, "혼합 부대 + 보호막 적", 40f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(dialog.currentConceptText.rectTransform,
                new Vector2(500f, 52f), UITowerUIFactory.Pos(545f, 357f));

            // 게이지 — 트랙과 채움. 채움은 트랙을 꽉 채우는 Filled(가로, 왼쪽 기준) 이미지이고
            // 진행은 fillAmount 로 먹인다. 폭을 줄이는 방식은 앵커·피벗이 하나만 어긋나도
            // 가운데서 자라는 게이지가 된다.
            Image track = UITowerUIFactory.CreateSprite("BandGauge", p,
                UITowerUIFactory.LoadSprite("Upgrade/Upgrade_Gauge_Bg"), 1f,
                new Vector2(GaugeWidth, GaugeHeight), UITowerUIFactory.Pos(193f + GaugeWidth * 0.5f, 400f), true);

            Image fill = UITowerUIFactory.CreateSprite("Fill", track.transform,
                UITowerUIFactory.LoadSprite("Upgrade/Upgrade_Gauge_Full"), 1f,
                new Vector2(GaugeWidth, GaugeHeight), Vector2.zero, false);
            UITowerUIFactory.Stretch(fill.rectTransform);

            // 투명 여백만큼 트랙 밖으로 넓힌다(위 GaugeArt* 주석).
            float padX = GaugeWidth * GaugeArtLeft / (GaugeArtRight - GaugeArtLeft);
            float unitY = GaugeHeight / (GaugeArtBottom - GaugeArtTop);
            fill.rectTransform.offsetMin = new Vector2(-padX, -(GaugeArtSize - GaugeArtBottom) * unitY);
            fill.rectTransform.offsetMax = new Vector2(padX, GaugeArtTop * unitY);

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.5f;
            dialog.bandGaugeFill = fill;

            // 구간 진행 숫자 — 게이지 오른쪽 끝 바로 위(이름 줄 오른쪽). 이름과 게이지 사이가
            // 좁아 줄을 따로 내지 않는다.
            dialog.bandProgressText = UITowerUIFactory.CreateText("BandProgress", p, "5/5", 30f,
                TextAlignmentOptions.Right, Color.white, font);
            UITowerUIFactory.SetRect(dialog.bandProgressText.rectTransform,
                new Vector2(120f, 40f), UITowerUIFactory.Pos(828f, 366f));

            dialog.bandGaugeText = UITowerUIFactory.CreateText("BandGaugeText", p, "구간 보상까지 3층", 30f,
                TextAlignmentOptions.Left, UITowerUIFactory.TextMuted, font);
            UITowerUIFactory.SetRect(dialog.bandGaugeText.rectTransform,
                new Vector2(500f, 40f), UITowerUIFactory.Pos(443f, 432f));

            // (2) 층 카드 목록
            float listHeight = ListBottom - ListTop;
            dialog.listContent = UITowerUIFactory.CreateScrollList("FloorList", p,
                new Vector2(UITowerUIFactory.PanelWidth + 24f, listHeight),
                UITowerUIFactory.Pos(cx, ListTop + listHeight * 0.5f), CardSpacing);

            // 300층을 보이는 만큼만 만든다. 위치는 UIRecycleVerticalList 가 정하므로 Content 의
            // 레이아웃·크기 맞춤은 걷어 낸다(둘이 같은 값을 번갈아 덮는다).
            Object.DestroyImmediate(dialog.listContent.GetComponent<ContentSizeFitter>());
            Object.DestroyImmediate(dialog.listContent.GetComponent<VerticalLayoutGroup>());

            dialog.cardTemplate = UITowerFloorCard.Create(dialog.listContent, font);

            // 위쪽은 시안처럼 패널에 바짝 붙이고, 아래는 마지막 카드 그림 여백(12)만큼 남긴다.
            RectTransform viewport = (RectTransform)dialog.listContent.parent;
            dialog.floorList = viewport.gameObject.AddComponent<UIRecycleVerticalList>();
            dialog.floorList.BakeSetup(viewport.GetComponent<ScrollRect>(), dialog.listContent,
                (RectTransform)dialog.cardTemplate.transform,
                UITowerFloorCard.CardHeight, CardSpacing, 0f, 12f);

            // (3) 하단 버튼 — 보이는 360x150, 시안 y 1499~1649
            UITowerUIFactory.ButtonRect(362.5f, 1574f, 360f, 150f, 3f, out Vector2 rewardSize, out Vector2 rewardPos);
            dialog.rewardListButton = UITowerUIFactory.CreateSpriteButton("RewardList", p,
                UITowerUIFactory.LoadSprite("Ingame/Btn_Gray"), 3f, rewardSize, rewardPos,
                "보상 목록", 45f, Color.white, font);

            UITowerUIFactory.ButtonRect(749.5f, 1574f, 360f, 150f, 3f, out Vector2 challengeSize, out Vector2 challengePos);
            dialog.challengeButton = UITowerUIFactory.CreateSpriteButton("Challenge", p,
                UITowerUIFactory.LoadSprite("Ingame/Big_Btn_Yellow"), 3f, challengeSize, challengePos,
                "102층 도전", 45f, Color.white, font);
            dialog.challengeLabel = dialog.challengeButton.GetComponentInChildren<TMP_Text>();

            // 보상 목록 오버레이 — 화면 전체를 덮는다.
            dialog.rewardOverlay = UITowerUIFactory.CreateRewardOverlay(view.transform, font,
                out dialog.rewardOverlayText, out dialog.rewardOverlayCloseButton);

            return dialog;
        }
    }
}
