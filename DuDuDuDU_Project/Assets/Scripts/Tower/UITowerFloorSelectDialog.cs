using System.Collections.Generic;
using System.Text;
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

            if (currentFloorText != null)
                currentFloorText.SetText(highestUnlocked + "층 · " + currentPlan.DisplayName);

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
                rewardOverlayText.SetText(BuildRewardListText());

            rewardOverlay.SetActive(true);
        }

        private void CloseRewardOverlay()
        {
            if (rewardOverlay != null)
                rewardOverlay.SetActive(false);
        }

        /// <summary>
        /// 앞으로 받을 것들. <b>다 보여 주지 않는다</b> — 300층치를 늘어놓으면 목록이
        /// 아니라 표가 되고, 기획서 5.2 가 지키려는 "다음 한 층" 의 초점이 흐려진다.
        /// 다음 구간 보상 다섯 개와 남은 다이스 해금까지만 적는다.
        /// </summary>
        private static string BuildRewardListText()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
                return string.Empty;

            var sb = new StringBuilder();
            int floor = progress.HighestUnlockedFloor;

            sb.AppendLine("<b>구간 보상</b>");
            int band = TowerFormula.BandOf(floor);
            for (int i = 0; i < 5 && band + i <= TowerFormula.BandCount; i++)
            {
                int lastFloor = TowerFormula.ClampFloor(
                    TowerFormula.BandStartFloor(band + i) + TowerFormula.FloorsPerBand - 1);

                sb.Append("  ").Append(lastFloor).Append("층 — 다이아 ")
                    .Append(TowerFormula.BandRewardDia(lastFloor))
                    .Append(" · 신화 스크롤 ")
                    .Append(TowerFormula.BandRewardMaterial(lastFloor))
                    .AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("<b>다이스 해금</b>");

            // <b>기준이 층에서 보유로 바뀌었다.</b> 예전에는 "아직 안 올라간 층" 을 남은
            // 해금으로 셌지만, 이제 같은 다이스를 별·스테이지로 먼저 얻을 수 있다 —
            // 그러면 층은 안 올라갔어도 이미 갖고 있고, 목록에 남겨 두면 거짓말이 된다.
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            IReadOnlyList<DiceUnlockDefinition> unlocks = DiceUnlockDatabaseProvider.Database.Definitions;
            bool anyLocked = false;
            for (int i = 0; i < unlocks.Count; i++)
            {
                DiceUnlockDefinition unlock = unlocks[i];
                if (unlock == null || unlock.towerFloor <= 0)
                    continue;

                if (ownership != null && ownership.IsOwned(unlock.diceType))
                    continue;

                anyLocked = true;
                sb.Append("  ").Append(unlock.towerFloor).Append("층 — ").Append(unlock.diceType).AppendLine();
            }

            if (!anyLocked)
                sb.AppendLine("  모두 사용할 수 있어요");

            sb.AppendLine();
            sb.Append("층 최초 클리어 시 골드 ")
                .Append(TowerFormula.FirstClearGold(floor))
                .Append(" (").Append(floor).Append("층 기준)");

            return sb.ToString();
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // 값을 아는 것은 코드이므로 인스펙터에 좌표를 옮겨 적지 않는다.
        // ──────────────────────────────────────────────────────────────

        private const float PanelWidth = 980f;
        private const float PanelHeight = 1560f;
        private const float GaugeWidth = 880f;

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

            Image blocker = UITowerUIFactory.CreateImage("Backdrop", view.transform, UITowerUIFactory.Backdrop);
            UITowerUIFactory.Stretch(blocker.rectTransform);
            var blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.targetGraphic = blocker;
            blockerButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(blockerButton);

            Image edge = UITowerUIFactory.CreateImage("Edge", view.transform, UITowerUIFactory.PanelEdge);
            UITowerUIFactory.SetRect(edge.rectTransform, new Vector2(PanelWidth + 8f, PanelHeight + 8f), Vector2.zero);
            edge.raycastTarget = false;

            Image panel = UITowerUIFactory.CreateImage("Panel", view.transform, UITowerUIFactory.PanelColor);
            UITowerUIFactory.SetRect(panel.rectTransform, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            Transform p = panel.transform;

            float top = PanelHeight * 0.5f;

            // 헤더
            dialog.titleText = UITowerUIFactory.CreateText("Title", p, "무한의 탑", 44f,
                TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(dialog.titleText.rectTransform,
                new Vector2(500f, 60f), new Vector2(-220f, top - 60f));

            dialog.bestFloorText = UITowerUIFactory.CreateText("Best", p, "최고 25층", 32f,
                TextAlignmentOptions.Right, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(dialog.bestFloorText.rectTransform,
                new Vector2(380f, 60f), new Vector2(260f, top - 60f));

            var closeButton = UITowerUIFactory.CreateButton("Close", p, "X",
                new Vector2(64f, 64f), new Vector2(PanelWidth * 0.5f - 50f, top - 58f),
                UITowerUIFactory.DangerColor, Color.white, 34f, font);
            dialog.AddExitButton(closeButton);

            // (1) 현재 진행 요약
            Image summary = UITowerUIFactory.CreateImage("Summary", p, UITowerUIFactory.CardColor);
            UITowerUIFactory.SetRect(summary.rectTransform, new Vector2(920f, 190f), new Vector2(0f, top - 210f));

            TMP_Text summaryCaption = UITowerUIFactory.CreateText("Caption", summary.transform, "현재 진행", 24f,
                TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(summaryCaption.rectTransform, new Vector2(880f, 30f), new Vector2(0f, 62f));

            dialog.currentFloorText = UITowerUIFactory.CreateText("Current", summary.transform,
                "26층 · 고방어 부대", 38f, TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(dialog.currentFloorText.rectTransform,
                new Vector2(880f, 48f), new Vector2(0f, 20f));

            dialog.bandGaugeFill = UITowerUIFactory.CreateGauge("BandGauge", summary.transform,
                new Vector2(GaugeWidth, 18f), new Vector2(0f, -22f), UITowerUIFactory.Accent);

            dialog.bandGaugeText = UITowerUIFactory.CreateText("BandGaugeText", summary.transform,
                "구간 보상까지 4층", 24f, TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(dialog.bandGaugeText.rectTransform,
                new Vector2(880f, 30f), new Vector2(0f, -58f));

            // (2) 목표 배너
            Image banner = UITowerUIFactory.CreateImage("GoalBanner", p, UITowerUIFactory.AccentSoft);
            UITowerUIFactory.SetRect(banner.rectTransform, new Vector2(920f, 76f), new Vector2(0f, top - 344f));

            dialog.unlockGoalText = UITowerUIFactory.CreateText("UnlockGoal", banner.transform,
                "30층 · KingFire 해금", 26f, TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(dialog.unlockGoalText.rectTransform,
                new Vector2(440f, 60f), new Vector2(-230f, 0f));

            dialog.bandRewardText = UITowerUIFactory.CreateText("BandReward", banner.transform,
                "30층 · 다이아 22 · 신화 스크롤 27", 24f, TextAlignmentOptions.Right, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(dialog.bandRewardText.rectTransform,
                new Vector2(440f, 60f), new Vector2(230f, 0f));

            // (3) 층 카드 목록
            const float listHeight = 940f;
            dialog.listContent = UITowerUIFactory.CreateScrollList("FloorList", p,
                new Vector2(920f, listHeight), new Vector2(0f, top - 344f - 38f - listHeight * 0.5f - 16f), 12f);

            dialog.cardTemplate = UITowerFloorCard.Create(dialog.listContent, font);

            // 하단 버튼
            float bottomY = -PanelHeight * 0.5f + 70f;

            dialog.rewardListButton = UITowerUIFactory.CreateButton("RewardList", p, "보상 목록",
                new Vector2(420f, 96f), new Vector2(-240f, bottomY),
                UITowerUIFactory.CardColor, Color.white, 32f, font);

            dialog.challengeButton = UITowerUIFactory.CreateButton("Challenge", p, "26층 도전",
                new Vector2(420f, 96f), new Vector2(240f, bottomY),
                UITowerUIFactory.Accent, Color.white, 34f, font);
            dialog.challengeLabel = dialog.challengeButton.GetComponentInChildren<TMP_Text>();

            // 보상 목록 오버레이. 판 위에 통째로 덮는다.
            GameObject overlay = UITowerUIFactory.CreateRect("RewardOverlay", p);
            UITowerUIFactory.Stretch(overlay.GetComponent<RectTransform>());
            dialog.rewardOverlay = overlay;

            Image overlayBg = UITowerUIFactory.CreateImage("Dim", overlay.transform, new Color(0.05f, 0.06f, 0.12f, 0.97f));
            UITowerUIFactory.Stretch(overlayBg.rectTransform);
            var overlayBlock = overlayBg.gameObject.AddComponent<Button>();
            overlayBlock.targetGraphic = overlayBg;
            overlayBlock.transition = Selectable.Transition.None;

            TMP_Text overlayTitle = UITowerUIFactory.CreateText("Title", overlay.transform, "보상 목록", 40f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(overlayTitle.rectTransform, new Vector2(880f, 60f), new Vector2(0f, top - 80f));

            dialog.rewardOverlayText = UITowerUIFactory.CreateText("Body", overlay.transform, string.Empty, 28f,
                TextAlignmentOptions.TopLeft, Color.white, font);
            UITowerUIFactory.SetRect(dialog.rewardOverlayText.rectTransform,
                new Vector2(860f, 1100f), new Vector2(0f, 60f));
            dialog.rewardOverlayText.textWrappingMode = TextWrappingModes.Normal;

            dialog.rewardOverlayCloseButton = UITowerUIFactory.CreateButton("OverlayClose", overlay.transform, "닫기",
                new Vector2(420f, 96f), new Vector2(0f, bottomY),
                UITowerUIFactory.Accent, Color.white, 32f, font);

            overlay.SetActive(false);

            return dialog;
        }
    }
}
