using System.Collections.Generic;
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
    /// <b>목록의 방향이 탑의 방향과 같다.</b> 위로 갈수록 미해금(? ? ?), 아래로 갈수록
    /// 이미 깬 층이다. 그래서 스크롤을 올리는 동작이 곧 "위층을 올려다보는" 동작이 되고,
    /// 현재 도전 층은 언제나 목록 위쪽에 고정되어 시선이 거기서 시작한다.
    ///
    /// <b>카드를 다시 쓰지 않고 매번 만든다.</b> 목록이 <see cref="VisibleFloorCount"/>
    /// 장뿐이고 화면이 열릴 때 한 번만 그려지므로, 풀링으로 얻을 것이 없고
    /// 대신 "지난 층의 값이 남아 있는" 종류의 사고를 원천적으로 없앤다.
    /// </summary>
    public class UITowerFloorSelectDialog : DialogBase
    {
        /// <summary>
        /// 한 번에 보여 주는 층 수. 현재 도전 층 위로 한 칸(? ? ?), 아래로 나머지다.
        ///
        /// 300개를 다 그리지 않는 이유는 성능이 아니라 <b>읽기</b>다 —
        /// 기획서 5.2 가 "다음 한 층에 시선이 고정되도록" 이라고 적고 있는데,
        /// 300줄짜리 목록에서는 그 한 층이 묻힌다.
        /// </summary>
        private const int VisibleFloorCount = 24;

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bestFloorText;

        [Header("현재 진행 요약")]
        [SerializeField] private TMP_Text currentFloorText;
        [SerializeField] private TMP_Text currentConceptText;
        [SerializeField] private Image bandGaugeFill;
        [SerializeField] private TMP_Text bandGaugeText;

        [Header("목표 배너")]
        [SerializeField] private TMP_Text unlockGoalText;
        [SerializeField] private TMP_Text bandRewardText;

        [Header("목록")]
        [SerializeField] private RectTransform listContent;
        [SerializeField] private UITowerFloorCard cardTemplate;

        [Header("하단")]
        [SerializeField] private Button rewardListButton;
        [SerializeField] private Button challengeButton;
        [SerializeField] private TMP_Text challengeLabel;

        [Header("보상 목록 오버레이")]
        [SerializeField] private GameObject rewardOverlay;
        [SerializeField] private TMP_Text rewardOverlayText;
        [SerializeField] private Button rewardOverlayCloseButton;

        private readonly List<UITowerFloorCard> cards = new List<UITowerFloorCard>();

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
            EnsureCards();

            // 맨 위 한 칸은 <b>아직 못 여는 층</b>이다. 기획서 5.2 목업이 그렇고,
            // 그 한 칸이 "위가 더 있다" 를 말없이 알려 준다.
            int topFloor = TowerFormula.ClampFloor(highestUnlocked + 1);

            for (int i = 0; i < cards.Count; i++)
            {
                UITowerFloorCard card = cards[i];
                int floor = topFloor - i;

                if (floor < 1)
                {
                    card.gameObject.SetActive(false);
                    continue;
                }

                bool revealed = progress.IsFloorRevealed(floor);

                card.gameObject.SetActive(true);
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
        }

        private void EnsureCards()
        {
            if (cardTemplate == null || listContent == null)
                return;

            while (cards.Count < VisibleFloorCount)
            {
                UITowerFloorCard card = Instantiate(cardTemplate, listContent);
                card.gameObject.SetActive(false);
                cards.Add(card);
            }
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

        /// <summary>
        /// 목록 영역. 시안의 첫 카드가 진행 패널 바로 밑(y 480)에서 시작하고 하단 버튼 위(y 1490)에서 잘린다.
        /// </summary>
        private const float ListTop = 480f;
        private const float ListBottom = 1490f;

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

            // 게이지 — 트랙과 채움. 채움은 트랙의 자식으로 왼쪽에 붙어 폭만 바뀐다.
            Image track = UITowerUIFactory.CreateSprite("BandGauge", p,
                UITowerUIFactory.LoadSprite("Upgrade/Upgrade_Gauge_Bg"), 1f,
                new Vector2(GaugeWidth, GaugeHeight), UITowerUIFactory.Pos(193f + GaugeWidth * 0.5f, 400f), true);

            Image fill = UITowerUIFactory.CreateSprite("Fill", track.transform,
                UITowerUIFactory.LoadSprite("Upgrade/Upgrade_Gauge_Full"), 1f,
                new Vector2(GaugeWidth, GaugeHeight), Vector2.zero, true);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = new Vector2(GaugeWidth * 0.5f, 0f);
            dialog.bandGaugeFill = fill;

            dialog.bandGaugeText = UITowerUIFactory.CreateText("BandGaugeText", p, "구간 보상까지 3층", 30f,
                TextAlignmentOptions.Left, UITowerUIFactory.TextMuted, font);
            UITowerUIFactory.SetRect(dialog.bandGaugeText.rectTransform,
                new Vector2(500f, 40f), UITowerUIFactory.Pos(443f, 432f));

            // (2) 층 카드 목록
            float listHeight = ListBottom - ListTop;
            dialog.listContent = UITowerUIFactory.CreateScrollList("FloorList", p,
                new Vector2(UITowerUIFactory.PanelWidth + 24f, listHeight),
                UITowerUIFactory.Pos(cx, ListTop + listHeight * 0.5f), CardSpacing);

            // 카드 그림(여백 포함)이 카드 칸보다 사방 12px 크다. 좌우 여유는 그것을 받는다.
            // 위쪽은 시안처럼 패널에 바짝 붙인다.
            var layout = dialog.listContent.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 0, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;

            dialog.cardTemplate = UITowerFloorCard.Create(dialog.listContent, font);

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
