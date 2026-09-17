using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Pinball;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.UI;

namespace OJ.Pinball
{
    /// <summary>
    /// 핀볼 화면. 티켓을 내고 공을 한 개씩 굴린다.
    ///
    /// <b>세션.</b> 첫 발사로 세션이 열리고, 굴러가는 공이 하나라도 있으면 계속 이어진다.
    /// 세션 중에는 <see cref="PinballManager.MaxShotsPerSession"/> 발까지 더 쏠 수 있고,
    /// 마지막 공이 착지하면 획득 팝업이 뜬다. 그 팝업을 닫아야 세션이 완전히 끝난다.
    ///
    /// <b>배율은 세션이 잠근다.</b> 세션이 열리는 순간의 배율이 끝까지 간다. 중간에 올리게
    /// 두면 낮은 배율로 간 보고 좋아 보일 때만 올리는 것이 되어, 배율에 건 조건이 무의미해진다.
    ///
    /// <b>지급은 발사 순간이다.</b> 이 판은 뽑은 칸으로 가는 궤적을 재생할 뿐이라 결과가
    /// 발사할 때 이미 정해져 있다. 재생은 순수한 연출이고, 도중에 앱이 죽거나 화면을 나가도
    /// 티켓과 경품이 갈라지지 않는다. 팝업은 그 세션에 받은 것을 모아 보여 주는 요약이다.
    /// </summary>
    public class UIPinballPage : DialogBase
    {
        [Header("핀볼 판 — 구워진 프리팹을 자식으로 넣고 연결한다")]
        [SerializeField] private PinballPlayback playback;

        [Tooltip("착지 확률표(declaredProbability)와 칸 좌표를 여기서 꺼낸다.")]
        [SerializeField] private PinballBoardView boardView;

        [Header("UI")]
        [SerializeField] private Button launchButton;

        [Tooltip("배율 버튼들. 잠긴 것은 조건이, 열린 것은 배수가 찍힌다.")]
        [SerializeField] private List<MultiplierOption> multiplierOptions = new List<MultiplierOption>();

        [Tooltip("DialogBase 의 exitBtn 이 아니라 여기에 꽂는다 — 세션 중에는 막아야 한다.")]
        [SerializeField] private Button closeButton;

        [SerializeField] private TMP_Text ticketText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text sessionText;
        [SerializeField] private TMP_Text gaugeText;

        [Header("연출")]
        [Tooltip("드랍 연출이 떠오르는 높이(px).")]
        [SerializeField] private float dropRise = 90f;

        [Tooltip("드랍 연출이 사라지기까지의 시간(초).")]
        [SerializeField] private float dropDuration = 1.1f;

        /// <summary>핀 위 게이지 글자색. 흰 보상 수량과 구별되어야 한 눈에 읽힌다.</summary>
        private static readonly Color GaugeColor = new Color(1f, 0.84f, 0.38f, 1f);

        /// <summary>배율 버튼 하나.</summary>
        [System.Serializable]
        public sealed class MultiplierOption
        {
            [Min(1)] public int multiplier = 1;
            public Button button;

            [Tooltip("배수(x10)가 찍힌다. 비워도 된다.")]
            public TMP_Text label;

            [Tooltip("잠겼을 때 필요한 보유 티켓이 찍힌다. 비워도 된다.")]
            public TMP_Text requirementLabel;

            [Tooltip("선택 중임을 보여줄 테두리·체크 등. 비워도 된다.")]
            public GameObject selectedMark;
        }

        /// <summary>다음 세션에 쓸 배율. 세션이 열리면 <see cref="sessionMultiplier"/> 로 굳는다.</summary>
        private int selectedMultiplier = 1;

        /// <summary>진행 중인 세션의 배율. 세션이 없으면 0.</summary>
        private int sessionMultiplier;

        /// <summary>이번 세션에 쏜 공 수.</summary>
        private int sessionShots;

        /// <summary>마지막 공이 착지해 팝업을 띄웠고, 아직 닫히지 않았다.</summary>
        private bool awaitingResult;

        /// <summary>이번 세션에 받은 것 전부. 팝업에 요약으로 넘긴다.</summary>
        private readonly List<PointRewardEntry> sessionRewards = new List<PointRewardEntry>();

        private readonly List<DropFx> activeFx = new List<DropFx>();
        private readonly Stack<DropFx> fxPool = new Stack<DropFx>();

        private RectTransform slotLabelRoot;
        private RectTransform fxRoot;
        private bool labelsBuilt;
        private bool closing;

        /// <summary>
        /// 특수 핀 위에 붙인 게이지 글자. 태그 하나에 핀이 여럿일 수 있어 목록이다
        /// (<c>Assets/Pinball/README.md</c>: "같은 값을 여러 핀에 주면 한 그룹").
        /// </summary>
        private readonly Dictionary<int, List<TMP_Text>> specialGaugeLabels =
            new Dictionary<int, List<TMP_Text>>();

        private static PinballManager Manager => PinballManager.Instance;

        /// <summary>세션이 열려 있는가 — 공이 굴러가거나 결과 팝업을 기다리는 중.</summary>
        public bool SessionOpen => sessionMultiplier > 0;

        private bool BallsInPlay => playback != null && playback.IsBusy;

        // ──────────────────────────────────────────────── 수명

        protected override void OnLoad()
        {
            if (launchButton != null)
                launchButton.onClick.AddListener(OnLaunchClicked);

            for (int i = 0; i < multiplierOptions.Count; i++)
            {
                MultiplierOption option = multiplierOptions[i];
                if (option == null || option.button == null)
                    continue;

                // 람다가 option 을 잡는다. 루프 변수를 직접 잡으면 전부 마지막 것이 된다.
                MultiplierOption captured = option;
                captured.button.onClick.AddListener(() => OnMultiplierClicked(captured.multiplier));
            }

            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);

            if (playback != null)
            {
                playback.OnSpecialHit += HandleSpecialHit;
                playback.OnLanded += HandleLanded;
                playback.OnAllLanded += HandleAllLanded;
            }
            else
            {
                Debug.LogError("[핀볼] PinballPlayback 이 연결되지 않았다. 화면이 열려도 공이 안 나간다.");
            }

            // 백키도 닫기 버튼과 같은 판정을 받아야 한다. 이게 없으면 세션 도중에 백키로
            // 빠져나가 연출만 사라진다.
            BackKeyOverride = OnCloseClicked;
        }

        protected override void OnUnload()
        {
            if (launchButton != null)
                launchButton.onClick.RemoveListener(OnLaunchClicked);

            // 배율 버튼은 람다로 달았으므로 개별 제거가 안 된다. 이 페이지 전용 버튼이라
            // 통째로 지워도 남의 리스너를 지울 일이 없다.
            for (int i = 0; i < multiplierOptions.Count; i++)
            {
                if (multiplierOptions[i] != null && multiplierOptions[i].button != null)
                    multiplierOptions[i].button.onClick.RemoveAllListeners();
            }

            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseClicked);

            if (playback != null)
            {
                playback.OnSpecialHit -= HandleSpecialHit;
                playback.OnLanded -= HandleLanded;
                playback.OnAllLanded -= HandleAllLanded;
            }
        }

        protected override void OnEnter()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += HandlePointChanged;

            if (Manager != null)
                Manager.OnGaugeChanged += RefreshGauge;

            BuildSlotLabels();
            ClampSelectedMultiplier();
            Refresh();
        }

        protected override void OnExit()
        {
            // 여기까지 오면 세션은 이미 닫혀 있다(OnCloseClicked 가 막는다). 그래도 안전망을
            // 둔다 — 다른 경로로 창이 닫힐 수 있고, 그때 남은 게이지 변화가 사라지면 안 된다.
            if (BallsInPlay)
            {
                closing = true;
                playback.StopAll(true);
                closing = false;
            }

            EndSession();
            ClearAllFx();

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= HandlePointChanged;

            if (Manager != null)
                Manager.OnGaugeChanged -= RefreshGauge;
        }

        // ──────────────────────────────────────────────── 배율

        private void OnMultiplierClicked(int multiplier)
        {
            if (SessionOpen)
            {
                Debug.Log("[핀볼] 세션이 끝나야 배율을 바꿀 수 있습니다.");
                return;
            }

            PinballManager manager = Manager;
            if (manager == null || !manager.IsMultiplierUnlocked(multiplier))
            {
                int required = manager != null ? manager.RequiredTicketsFor(multiplier) : -1;
                Debug.Log("[핀볼] x" + multiplier + " 은(는) 티켓 " + required + "장부터 쓸 수 있습니다.");
                return;
            }

            selectedMultiplier = multiplier;
            Refresh();
        }

        /// <summary>
        /// 티켓이 줄어 고른 배율이 잠기면 쓸 수 있는 가장 높은 것으로 내린다.
        /// 잠긴 배율을 고른 채로 두면 발사 버튼이 이유 없이 잠긴 것처럼 보인다.
        /// </summary>
        private void ClampSelectedMultiplier()
        {
            PinballManager manager = Manager;
            if (manager == null || SessionOpen)
                return;

            if (manager.IsMultiplierUnlocked(selectedMultiplier))
                return;

            IReadOnlyList<PinballMultiplierTier> tiers = manager.MultiplierTiers;
            int best = 1;

            if (tiers != null)
            {
                for (int i = 0; i < tiers.Count; i++)
                {
                    PinballMultiplierTier tier = tiers[i];
                    if (tier == null || tier.multiplier <= best)
                        continue;

                    if (manager.IsMultiplierUnlocked(tier.multiplier))
                        best = tier.multiplier;
                }
            }

            selectedMultiplier = best;
        }

        // ──────────────────────────────────────────────── 발사

        private void OnLaunchClicked()
        {
            PinballManager manager = Manager;
            if (manager == null || playback == null)
            {
                Debug.LogError("[핀볼] 매니저나 재생기가 없어 발사하지 못한다.");
                return;
            }

            if (awaitingResult)
            {
                Debug.Log("[핀볼] 결과를 확인한 뒤에 다시 쏠 수 있습니다.");
                return;
            }

            int multiplier = SessionOpen ? sessionMultiplier : selectedMultiplier;

            if (SessionOpen && sessionShots >= manager.MaxShotsPerSession)
            {
                Debug.Log("[핀볼] 이번 세션에 쏠 수 있는 " + manager.MaxShotsPerSession +
                          "발을 다 썼습니다. 굴러가는 공이 멈추면 다시 쏠 수 있습니다.");
                return;
            }

            if (!manager.CanShoot(multiplier))
            {
                // 공용 토스트가 프로젝트에 없다(UIShopPage.Report 와 같은 사정).
                // 버튼이 이미 잠겨 있어 여기 닿는 것은 드물다.
                Debug.Log("[핀볼] 티켓이 모자랍니다.");
                Refresh();
                return;
            }

            // 여기서 티켓이 빠지고 칸 경품이 들어오고 저장까지 끝난다.
            IReadOnlyList<PointRewardEntry> granted =
                manager.TryShoot(DeclaredProbability(), multiplier, out int slot);

            if (granted == null)
                return;

            // 시드 풀이 비면 -1 이고 아무것도 발사되지 않는다. 지급은 이미 끝났으므로
            // 유저가 손해 보지는 않지만, 공이 안 보이는 것은 사고라 로그를 남긴다.
            if (playback.PlayForSlot(slot) < 0)
                Debug.LogError("[핀볼] " + slot + "번 칸의 궤적을 재생하지 못했다. 지급은 끝났다.");

            if (!SessionOpen)
            {
                sessionMultiplier = multiplier;
                sessionShots = 0;
                sessionRewards.Clear();
            }

            sessionShots++;
            sessionRewards.AddRange(granted);
            Refresh();
        }

        private IReadOnlyList<float> DeclaredProbability()
        {
            if (boardView == null || boardView.board == null)
            {
                Debug.LogError("[핀볼] 보드가 연결되지 않아 확률표를 읽지 못한다. " +
                               "UIPinballPage 의 Board View 를 확인할 것.");
                return null;
            }

            return boardView.board.declaredProbability;
        }

        private void OnCloseClicked()
        {
            if (SessionOpen)
            {
                Debug.Log("[핀볼] 세션이 끝나야 나갈 수 있습니다.");
                return;
            }

            Exit();
        }

        // ──────────────────────────────────────────────── 콜백

        /// <summary>
        /// 공 하나가 칸에 닿았다. <b>지급은 발사 때 이미 끝났으므로 여기서는 보여주기만 한다.</b>
        /// </summary>
        private void HandleLanded(int ballId, int slot)
        {
            PinballManager manager = Manager;
            if (manager == null)
                return;

            int multiplier = sessionMultiplier > 0 ? sessionMultiplier : 1;
            SpawnDropFx(SlotAnchored(slot), manager.GetLandingRewards(slot, multiplier));
        }

        private void HandleSpecialHit(int ballId, int pegIndex, int tag, int hitsForThisBall)
        {
            PinballManager manager = Manager;
            if (manager == null)
                return;

            int multiplier = sessionMultiplier > 0 ? sessionMultiplier : 1;
            IReadOnlyList<PointRewardEntry> rewards = manager.ResolveSpecialHit(tag, multiplier);
            if (rewards.Count == 0)
                return;     // 게이지만 올랐다. OnGaugeChanged 가 표시를 갱신한다.

            sessionRewards.AddRange(rewards);
            SpawnDropFx(PegAnchored(pegIndex), rewards);
        }

        private void HandleAllLanded()
        {
            PinballManager manager = Manager;
            if (manager != null)
                manager.FlushSave();

            if (!SessionOpen)
            {
                // 세션 없이 온 콜백(StopAll 등). 정리만 하고 끝낸다.
                Refresh();
                return;
            }

            if (sessionRewards.Count == 0)
            {
                EndSession();
                Refresh();
                return;
            }

            List<PointRewardEntry> merged = PointRewardUtility.MergeRewards(sessionRewards);
            awaitingResult = true;
            Refresh();

            ShowResult(merged);
        }

        private void HandlePointChanged(PointType pointType, int amount)
        {
            if (pointType != PointType.PinballTicket)
                return;

            ClampSelectedMultiplier();
            Refresh();
        }

        private void ShowResult(IReadOnlyList<PointRewardEntry> rewards)
        {
            if (closing)
            {
                Debug.Log("[핀볼] 화면을 닫으며 마감했다 — " +
                          PointRewardUtility.BuildRewardSummary(rewards));
                EndSession();
                return;
            }

            // 결과창·스테이지 보상·상점이 쓰는 UIRewardResultDialog 를 그대로 쓴다.
            // 핀볼만 자기 팝업을 만들면 같은 재화가 화면마다 다른 모양으로 뜬다.
            UIRewardResultDialog dialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (dialog == null)
            {
                Debug.LogError("[핀볼] UIRewardResultDialog 를 못 열었다. 지급은 이미 끝났다 — " +
                               PointRewardUtility.BuildRewardSummary(rewards));
                EndSession();
                Refresh();
                return;
            }

            // 팝업을 닫아야 세션이 끝난다. 그래야 "결과를 보고 배율을 다시 고른다"는
            // 순서가 지켜진다.
            dialog.Open(rewards, "핀볼 보상을 획득했습니다.", HandleResultClosed);
        }

        private void HandleResultClosed()
        {
            EndSession();
            Refresh();
        }

        private void EndSession()
        {
            sessionMultiplier = 0;
            sessionShots = 0;
            awaitingResult = false;
            sessionRewards.Clear();
            ClampSelectedMultiplier();
        }

        // ──────────────────────────────────────────────── 칸 경품 표시

        /// <summary>
        /// 칸마다 경품 아이콘과 수량을 붙인다.
        ///
        /// <b>판의 자식으로 만든다.</b> <c>PinballBoardView.SlotCenter</c> 가 판 루트 기준
        /// anchored 좌표를 주므로, 같은 부모 아래에서는 그 값을 그대로 쓸 수 있다.
        ///
        /// <b>배수는 여기 곱하지 않는다.</b> 칸에 적힌 것은 기본 경품이고, 배율은 화면 위쪽에
        /// 따로 보인다. 칸 숫자까지 같이 흔들리면 무엇이 기준인지 읽기 어려워진다.
        /// </summary>
        private void BuildSlotLabels()
        {
            if (labelsBuilt)
                return;

            PinballManager manager = Manager;
            if (manager == null || boardView == null)
                return;

            slotLabelRoot = NewChild("SlotRewards", boardView.transform);

            int count = manager.SlotCount;
            for (int slot = 0; slot < count; slot++)
                BuildOneSlotLabel(manager, slot);

            BuildSpecialLabels(manager);

            // 연출은 라벨 위에 떠야 한다. 마지막에 만들어 형제 순서를 뒤로 보낸다.
            fxRoot = NewChild("DropFx", boardView.transform);

            labelsBuilt = true;
        }

        /// <summary>
        /// 특수 핀 위에 그 핀의 보상과 누적 카운트를 붙인다.
        ///
        /// <b>핀마다 하나씩 붙인다.</b> 같은 태그를 여러 핀에 줄 수 있고, 그때 한 곳에만
        /// 적으면 나머지 핀은 평범한 핀처럼 보인다 — 유저가 어디를 노려야 하는지 모른다.
        /// </summary>
        private void BuildSpecialLabels(PinballManager manager)
        {
            specialGaugeLabels.Clear();

            if (boardView.board == null || boardView.board.pegs == null || boardView.pegs == null)
                return;

            RectTransform root = NewChild("SpecialRewards", boardView.transform);

            Peg[] pegs = boardView.board.pegs;
            int limit = Mathf.Min(pegs.Length, boardView.pegs.Length);

            for (int i = 0; i < limit; i++)
            {
                int tag = pegs[i].specialTag;
                if (tag == 0)
                    continue;

                PinballSpecialReward rule = manager.GetSpecialReward(tag);
                if (rule == null)
                {
                    // 판에는 특수 핀인데 경품표에 없다. 게이지도 안 오르는 태그라
                    // 라벨을 붙이면 받을 수 없는 보상을 광고하는 셈이 된다.
                    continue;
                }

                BuildOneSpecialLabel(root, i, tag, rule);
            }
        }

        private void BuildOneSpecialLabel(
            RectTransform root, int pegIndex, int tag, PinballSpecialReward rule)
        {
            RectTransform holder = NewChild("Peg_" + pegIndex + "_Tag" + tag, root);
            holder.anchoredPosition = PegAnchored(pegIndex);

            // 핀 반지름이 30px 안팎이라 그 위로 올려야 핀을 가리지 않는다.
            bool hasReward = rule.rewards != null && rule.rewards.Count > 0;
            if (hasReward)
            {
                Sprite icon = PointRewardUtility.GetPointIcon(rule.rewards[0].pointType);
                if (icon != null)
                {
                    RectTransform iconRect = NewChild("Icon", holder);
                    var image = iconRect.gameObject.AddComponent<Image>();
                    image.sprite = icon;
                    image.raycastTarget = false;
                    image.preserveAspect = true;
                    iconRect.sizeDelta = new Vector2(44f, 44f);
                    iconRect.anchoredPosition = new Vector2(0f, 82f);
                }

                TMP_Text amount = NewLabel("Amount", holder, BuildSpecialRewardText(rule), 24f);
                amount.rectTransform.sizeDelta = new Vector2(140f, 50f);
                amount.rectTransform.anchoredPosition = new Vector2(0f, 48f);
            }

            // 누적 카운트. 실시간으로 차야 하므로 참조를 들고 있는다.
            TMP_Text gauge = NewLabel("Gauge", holder, string.Empty, 24f);
            gauge.color = GaugeColor;
            gauge.rectTransform.sizeDelta = new Vector2(140f, 40f);
            gauge.rectTransform.anchoredPosition = new Vector2(0f, hasReward ? 20f : 48f);

            if (!specialGaugeLabels.TryGetValue(tag, out List<TMP_Text> labels))
            {
                labels = new List<TMP_Text>();
                specialGaugeLabels[tag] = labels;
            }

            labels.Add(gauge);
        }

        private static string BuildSpecialRewardText(PinballSpecialReward rule)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < rule.rewards.Count; i++)
            {
                if (i > 0)
                    builder.Append('\n');

                builder.Append(rule.rewards[i].amount.ToString("N0"));
            }

            return builder.ToString();
        }

        private void BuildOneSlotLabel(PinballManager manager, int slot)
        {
            IReadOnlyList<PinballReward> rewards = manager.GetSlotRewards(slot);
            if (rewards == null || rewards.Count == 0)
                return;

            RectTransform holder = NewChild("Slot_" + slot, slotLabelRoot);
            holder.anchoredPosition = SlotAnchored(slot);

            // 첫 경품을 아이콘으로 세운다. 한 칸에 여러 개면 수량 줄이 여러 줄이 된다.
            Sprite icon = PointRewardUtility.GetPointIcon(rewards[0].pointType);
            if (icon != null)
            {
                RectTransform iconRect = NewChild("Icon", holder);
                var image = iconRect.gameObject.AddComponent<Image>();
                image.sprite = icon;
                image.raycastTarget = false;
                image.preserveAspect = true;
                iconRect.sizeDelta = new Vector2(52f, 52f);
                iconRect.anchoredPosition = new Vector2(0f, 26f);
            }

            TMP_Text amount = NewLabel("Amount", holder, BuildSlotText(rewards), 26f);
            amount.rectTransform.sizeDelta = new Vector2(120f, 60f);
            amount.rectTransform.anchoredPosition = new Vector2(0f, icon != null ? -26f : 0f);
        }

        private static string BuildSlotText(IReadOnlyList<PinballReward> rewards)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                if (i > 0)
                    builder.Append('\n');

                builder.Append(rewards[i].amount.ToString("N0"));
            }

            return builder.ToString();
        }

        private Vector2 SlotAnchored(int slot)
        {
            return boardView != null ? boardView.SlotCenter(slot) : Vector2.zero;
        }

        private Vector2 PegAnchored(int pegIndex)
        {
            if (boardView == null || boardView.pegs == null ||
                pegIndex < 0 || pegIndex >= boardView.pegs.Length || boardView.pegs[pegIndex] == null)
            {
                return Vector2.zero;
            }

            return boardView.pegs[pegIndex].anchoredPosition;
        }

        // ──────────────────────────────────────────────── 드랍 연출

        /// <summary>경품 하나가 떠오르며 사라지는 연출. 아이콘 + 수량.</summary>
        private sealed class DropFx
        {
            public RectTransform root;
            public Image icon;
            public TMP_Text label;
            public CanvasGroup group;
            public Vector2 origin;
            public float clock;
        }

        private void SpawnDropFx(Vector2 anchored, IReadOnlyList<PointRewardEntry> rewards)
        {
            if (fxRoot == null || rewards == null)
                return;

            for (int i = 0; i < rewards.Count; i++)
            {
                DropFx fx = fxPool.Count > 0 ? fxPool.Pop() : CreateFx();

                Sprite sprite = PointRewardUtility.GetPointIcon(rewards[i].PointType);
                fx.icon.sprite = sprite;
                // 아이콘이 없으면 흰 사각형이 뜬다. 꺼서 글자만 남긴다.
                fx.icon.enabled = sprite != null;

                fx.label.text = "+" + rewards[i].Amount.ToString("N0");

                // 같은 자리에서 여러 개가 겹치지 않게 조금씩 벌린다.
                fx.origin = anchored + new Vector2(0f, i * 34f);
                fx.clock = 0f;
                fx.root.anchoredPosition = fx.origin;
                fx.group.alpha = 1f;
                fx.root.gameObject.SetActive(true);

                activeFx.Add(fx);
            }
        }

        private DropFx CreateFx()
        {
            RectTransform root = NewChild("Fx", fxRoot);

            var fx = new DropFx
            {
                root = root,
                group = root.gameObject.AddComponent<CanvasGroup>(),
            };

            RectTransform iconRect = NewChild("Icon", root);
            fx.icon = iconRect.gameObject.AddComponent<Image>();
            fx.icon.raycastTarget = false;
            fx.icon.preserveAspect = true;
            iconRect.sizeDelta = new Vector2(44f, 44f);
            iconRect.anchoredPosition = new Vector2(-34f, 0f);

            fx.label = NewLabel("Amount", root, string.Empty, 30f);
            fx.label.rectTransform.sizeDelta = new Vector2(180f, 44f);
            fx.label.rectTransform.anchoredPosition = new Vector2(34f, 0f);

            fx.group.blocksRaycasts = false;
            fx.group.interactable = false;
            return fx;
        }

        private void Update()
        {
            if (activeFx.Count == 0)
                return;

            float duration = Mathf.Max(0.05f, dropDuration);

            // 뒤에서부터 도는 이유는 다 끝난 것을 목록에서 빼기 때문이다.
            for (int i = activeFx.Count - 1; i >= 0; i--)
            {
                DropFx fx = activeFx[i];
                fx.clock += Time.deltaTime;

                float t = Mathf.Clamp01(fx.clock / duration);
                fx.root.anchoredPosition = fx.origin + new Vector2(0f, dropRise * t);

                // 처음 20% 는 또렷하게 두고 그 뒤로 사라진다. 바로 흐려지면 뭘 받았는지 못 읽는다.
                fx.group.alpha = t < 0.2f ? 1f : 1f - Mathf.InverseLerp(0.2f, 1f, t);

                if (t < 1f)
                    continue;

                activeFx.RemoveAt(i);
                Release(fx);
            }
        }

        private void ClearAllFx()
        {
            for (int i = 0; i < activeFx.Count; i++)
                Release(activeFx[i]);

            activeFx.Clear();
        }

        private void Release(DropFx fx)
        {
            if (fx.root != null)
                fx.root.gameObject.SetActive(false);

            fxPool.Push(fx);
        }

        // ──────────────────────────────────────────────── 갱신

        private void Refresh()
        {
            PinballManager manager = Manager;
            int multiplier = SessionOpen ? sessionMultiplier : selectedMultiplier;

            if (ticketText != null)
                ticketText.text = manager != null ? manager.TicketCount.ToString("N0") : "0";

            if (costText != null)
                costText.text = manager != null ? manager.ShotCost(multiplier).ToString("N0") : "1";

            if (sessionText != null)
            {
                int max = manager != null ? manager.MaxShotsPerSession : 0;
                sessionText.text = SessionOpen ? sessionShots + " / " + max : string.Empty;
            }

            RefreshLaunchButton(manager, multiplier);
            RefreshMultiplierButtons(manager, multiplier);

            // 세션 중에는 나갈 수 없다. 지급은 이미 끝났지만 연출은 끝까지 보여준다.
            if (closeButton != null)
                closeButton.interactable = !SessionOpen;

            RefreshGauge();
        }

        private void RefreshLaunchButton(PinballManager manager, int multiplier)
        {
            if (launchButton == null)
                return;

            bool sessionFull = SessionOpen && manager != null && sessionShots >= manager.MaxShotsPerSession;

            // 굴러가는 공이 있어도 잠그지 않는다 — 세션을 이어 가는 것이 이 컨텐츠의 재미다.
            // 잠기는 것은 셋뿐: 결과 대기 중, 세션 발사 수를 다 씀, 티켓 부족.
            launchButton.interactable =
                manager != null && !awaitingResult && !sessionFull && manager.CanShoot(multiplier);
        }

        private void RefreshMultiplierButtons(PinballManager manager, int activeMultiplier)
        {
            for (int i = 0; i < multiplierOptions.Count; i++)
            {
                MultiplierOption option = multiplierOptions[i];
                if (option == null)
                    continue;

                bool unlocked = manager != null && manager.IsMultiplierUnlocked(option.multiplier);

                // 세션 중에는 전부 잠긴다. 배율은 세션이 끝나야 바꿀 수 있다.
                if (option.button != null)
                    option.button.interactable = unlocked && !SessionOpen;

                if (option.label != null)
                    option.label.text = "x" + option.multiplier;

                if (option.requirementLabel != null)
                {
                    int required = manager != null ? manager.RequiredTicketsFor(option.multiplier) : -1;
                    // 열린 배율에 조건을 계속 띄우면 무엇이 잠긴 것인지 구별이 안 된다.
                    option.requirementLabel.text = unlocked || required <= 0
                        ? string.Empty
                        : required.ToString("N0") + "장";
                }

                if (option.selectedMark != null)
                    option.selectedMark.SetActive(option.multiplier == activeMultiplier);
            }
        }

        private void RefreshGauge()
        {
            PinballManager manager = Manager;

            RefreshGaugeSummary(manager);
            RefreshGaugeOnPegs(manager);
        }

        /// <summary>화면 위쪽의 한 줄 요약.</summary>
        private void RefreshGaugeSummary(PinballManager manager)
        {
            if (gaugeText == null)
                return;

            if (manager == null)
            {
                gaugeText.text = string.Empty;
                return;
            }

            IReadOnlyList<PinballSpecialReward> rules = manager.SpecialRewards;
            if (rules == null || rules.Count == 0)
            {
                gaugeText.text = string.Empty;
                return;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < rules.Count; i++)
            {
                PinballSpecialReward rule = rules[i];
                if (rule == null)
                    continue;

                if (builder.Length > 0)
                    builder.Append("   ");

                string label = string.IsNullOrWhiteSpace(rule.label)
                    ? "특수 핀 " + rule.tag
                    : rule.label;

                builder.Append(label).Append(' ')
                       .Append(manager.GetGauge(rule.tag)).Append(" / ").Append(rule.requiredHits);
            }

            gaugeText.text = builder.ToString();
        }

        /// <summary>핀 위에 붙은 누적 카운트.</summary>
        private void RefreshGaugeOnPegs(PinballManager manager)
        {
            if (manager == null || specialGaugeLabels.Count == 0)
                return;

            foreach (KeyValuePair<int, List<TMP_Text>> pair in specialGaugeLabels)
            {
                PinballSpecialReward rule = manager.GetSpecialReward(pair.Key);
                if (rule == null)
                    continue;

                string text = manager.GetGauge(pair.Key) + " / " + rule.requiredHits;

                List<TMP_Text> labels = pair.Value;
                for (int i = 0; i < labels.Count; i++)
                {
                    if (labels[i] != null)
                        labels[i].text = text;
                }
            }
        }

        // ──────────────────────────────────────────────── 조립 헬퍼

        private static RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            // 판의 자식들과 같은 규약이어야 SlotCenter 좌표가 그대로 맞는다.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        /// <summary>
        /// 글자를 만든다. 폰트는 <see cref="gaugeText"/> 에서 빌려 온다 —
        /// 화면이 이미 쓰고 있는 폰트라 한글이 반드시 나오고, 새 참조를 늘리지 않아도 된다.
        /// </summary>
        private TMP_Text NewLabel(string name, Transform parent, string text, float size)
        {
            RectTransform rect = NewChild(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.color = Color.white;

            if (gaugeText != null && gaugeText.font != null)
                label.font = gaugeText.font;

            return label;
        }
    }
}
