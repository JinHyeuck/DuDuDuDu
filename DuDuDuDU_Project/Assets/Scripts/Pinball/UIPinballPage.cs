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
    /// 핀볼 화면. 티켓을 내고 공을 굴린다. <b>여러 발을 연달아 쏠 수 있다.</b>
    ///
    /// <b>이 화면이 하는 일은 배선과 연출뿐이다.</b> 확률·경품·게이지 판정은 전부
    /// <see cref="PinballManager"/> 와 <see cref="PinballRules"/> 에 있다.
    ///
    /// <b>지급은 공이 칸에 닿는 순간이다.</b> 발사 시점에 이미 결과(칸)는 정해져 있지만,
    /// 그때 재화를 올리면 아직 굴러가는 중에 숫자가 먼저 움직여 연출과 어긋난다.
    /// 발사 순간에 빠지는 것은 티켓뿐이다.
    ///
    /// <b>저장은 판 끝에 한 번.</b> 연사하면 착지가 공 수만큼 일어나는데 그때마다 파일을
    /// 쓰면 착지할 때마다 프레임이 끊긴다. 티켓 차감도 같은 저장에 묶여 있어, 중간에 앱이
    /// 죽으면 티켓도 경품도 없던 일이 된다.
    /// </summary>
    public class UIPinballPage : DialogBase
    {
        [Header("핀볼 판 — 구워진 프리팹을 자식으로 넣고 연결한다")]
        [SerializeField] private PinballPlayback playback;

        [Tooltip("착지 확률표(declaredProbability)와 칸 좌표를 여기서 꺼낸다.")]
        [SerializeField] private PinballBoardView boardView;

        [Header("UI")]
        [Tooltip("1발·10발·30발 처럼 발수가 다른 버튼을 원하는 만큼 둔다.")]
        [SerializeField] private List<LaunchOption> launchOptions = new List<LaunchOption>();

        [Tooltip("DialogBase 의 exitBtn 이 아니라 여기에 꽂는다 — 공이 굴러가는 중에는 막아야 한다.")]
        [SerializeField] private Button closeButton;

        [SerializeField] private TMP_Text ticketText;
        [SerializeField] private TMP_Text gaugeText;

        [Header("연출")]
        [Tooltip("연사할 때 공과 공 사이 간격(초). 30발이면 이 값 × 30 만큼 걸린다.")]
        [SerializeField] private float burstInterval = 0.12f;

        [Tooltip("드랍 연출이 떠오르는 높이(px).")]
        [SerializeField] private float dropRise = 90f;

        [Tooltip("드랍 연출이 사라지기까지의 시간(초).")]
        [SerializeField] private float dropDuration = 1.1f;

        /// <summary>발사 버튼 하나. 발수와 비용 표시가 짝이다.</summary>
        [System.Serializable]
        public sealed class LaunchOption
        {
            [Min(1)] public int ballCount = 1;
            public Button button;

            [Tooltip("드는 티켓 수가 여기 찍힌다. 비워도 된다.")]
            public TMP_Text costLabel;
        }

        /// <summary>이번 연사에 뽑힌 칸. 발사 순서대로.</summary>
        private readonly List<int> pendingSlots = new List<int>();

        /// <summary>이번 연사에서 지급한 것 전부. 마지막 공이 착지하면 결과창에 넘긴다.</summary>
        private readonly List<PointRewardEntry> sessionRewards = new List<PointRewardEntry>();

        private readonly List<DropFx> activeFx = new List<DropFx>();
        private readonly Stack<DropFx> fxPool = new Stack<DropFx>();

        private RectTransform slotLabelRoot;
        private RectTransform fxRoot;
        private bool labelsBuilt;
        private bool closing;

        private static PinballManager Manager => PinballManager.Instance;

        /// <summary>공이 굴러가거나 발사 대기 중인가. 그동안은 화면을 닫을 수 없다.</summary>
        public bool IsBusy => playback != null && playback.IsBusy;

        // ──────────────────────────────────────────────── 수명

        protected override void OnLoad()
        {
            for (int i = 0; i < launchOptions.Count; i++)
            {
                LaunchOption option = launchOptions[i];
                if (option == null || option.button == null)
                    continue;

                // 람다가 option 을 잡는다. 루프 변수를 직접 잡으면 전부 마지막 것이 된다.
                LaunchOption captured = option;
                captured.button.onClick.AddListener(() => OnLaunchClicked(captured.ballCount));
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

            // 백키도 닫기 버튼과 같은 판정을 받아야 한다. 이게 없으면 공이 굴러가는 중에
            // 백키로 빠져나가 연출만 사라지고 지급은 OnExit 의 강제 마감에 맡겨진다.
            BackKeyOverride = OnCloseClicked;
        }

        protected override void OnUnload()
        {
            // 람다로 달았으므로 개별 제거가 안 된다. 이 버튼들은 이 페이지 전용이라
            // 통째로 지워도 남의 리스너를 지울 일이 없다.
            for (int i = 0; i < launchOptions.Count; i++)
            {
                if (launchOptions[i] != null && launchOptions[i].button != null)
                    launchOptions[i].button.onClick.RemoveAllListeners();
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
            Refresh();
        }

        protected override void OnExit()
        {
            // 여기까지 오면 공은 이미 없다(OnCloseClicked 가 막는다). 그래도 안전망을 둔다 —
            // 다른 경로로 창이 닫힐 수 있고, 그때 티켓만 쓰고 경품이 사라지면 안 된다.
            if (playback != null && playback.IsBusy)
            {
                closing = true;
                playback.StopAll(true);
                closing = false;
            }

            ClearAllFx();

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= HandlePointChanged;

            if (Manager != null)
                Manager.OnGaugeChanged -= RefreshGauge;
        }

        // ──────────────────────────────────────────────── 발사

        private void OnLaunchClicked(int ballCount)
        {
            PinballManager manager = Manager;
            if (manager == null || playback == null)
            {
                Debug.LogError("[핀볼] 매니저나 재생기가 없어 발사하지 못한다.");
                return;
            }

            if (!manager.CanLaunch(ballCount))
            {
                // 공용 토스트가 프로젝트에 없다(UIShopPage.Report 와 같은 사정).
                // 버튼이 이미 잠겨 있어 여기 닿는 것은 드물다.
                Debug.Log("[핀볼] 티켓이 모자랍니다.");
                Refresh();
                return;
            }

            // 여기서 티켓이 빠지고 칸 경품이 전부 들어오고 저장까지 끝난다.
            // 아래 재생은 순수한 연출이라, 도중에 앱이 죽어도 손해가 없다.
            IReadOnlyList<PointRewardEntry> granted =
                manager.TryLaunch(DeclaredProbability(), ballCount, pendingSlots);

            if (granted == null)
                return;

            sessionRewards.AddRange(granted);
            playback.PlayForSlots(pendingSlots, burstInterval);
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
            if (IsBusy)
            {
                Debug.Log("[핀볼] 공이 굴러가는 중에는 나갈 수 없습니다.");
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

            SpawnDropFx(SlotAnchored(slot), manager.GetLandingRewards(slot));
        }

        private void HandleSpecialHit(int ballId, int pegIndex, int tag, int hitsForThisBall)
        {
            PinballManager manager = Manager;
            if (manager == null)
                return;

            IReadOnlyList<PointRewardEntry> rewards = manager.ResolveSpecialHit(tag);
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

            if (sessionRewards.Count > 0)
            {
                List<PointRewardEntry> merged = PointRewardUtility.MergeRewards(sessionRewards);
                sessionRewards.Clear();
                ShowResult(merged);
            }

            Refresh();
        }

        private void HandlePointChanged(PointType pointType, int amount)
        {
            if (pointType == PointType.PinballTicket)
                Refresh();
        }

        private void ShowResult(IReadOnlyList<PointRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return;

            if (closing)
            {
                Debug.Log("[핀볼] 화면을 닫으며 마감했다 — " +
                          PointRewardUtility.BuildRewardSummary(rewards));
                return;
            }

            // 결과창·스테이지 보상·상점이 쓰는 UIRewardResultDialog 를 그대로 쓴다.
            // 핀볼만 자기 팝업을 만들면 같은 재화가 화면마다 다른 모양으로 뜬다.
            UIRewardResultDialog dialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (dialog == null)
            {
                Debug.LogError("[핀볼] UIRewardResultDialog 를 못 열었다. 지급은 이미 끝났다 — " +
                               PointRewardUtility.BuildRewardSummary(rewards));
                return;
            }

            dialog.Open(rewards, "핀볼 보상을 획득했습니다.");
        }

        // ──────────────────────────────────────────────── 칸 경품 표시

        /// <summary>
        /// 칸마다 경품 아이콘과 수량을 붙인다.
        ///
        /// <b>판의 자식으로 만든다.</b> <c>PinballBoardView.SlotCenter</c> 가 판 루트 기준
        /// anchored 좌표를 주므로, 같은 부모 아래에서는 그 값을 그대로 쓸 수 있다.
        /// 판을 다시 구워 칸 수가 바뀌어도 여기가 따라간다.
        /// </summary>
        private void BuildSlotLabels()
        {
            if (labelsBuilt)
                return;

            PinballManager manager = Manager;
            if (manager == null || boardView == null)
                return;

            slotLabelRoot = NewChild("SlotRewards", boardView.transform);
            fxRoot = NewChild("DropFx", boardView.transform);

            int count = manager.SlotCount;
            for (int slot = 0; slot < count; slot++)
                BuildOneSlotLabel(manager, slot);

            labelsBuilt = true;
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
            fx.label.rectTransform.sizeDelta = new Vector2(140f, 44f);
            fx.label.rectTransform.anchoredPosition = new Vector2(24f, 0f);

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

            if (ticketText != null)
                ticketText.text = manager != null ? manager.TicketCount.ToString("N0") : "0";

            // 발사 버튼은 IsBusy 로 잠그지 않는다 — 연사가 이 컨텐츠의 재미이고,
            // 시뮬레이션이 공 한 개 단위라 몇 발이 동시에 굴러가도 서로 간섭하지 않는다.
            // 티켓이 모자란 버튼만 잠긴다(10발 버튼은 9장일 때 잠긴다).
            for (int i = 0; i < launchOptions.Count; i++)
            {
                LaunchOption option = launchOptions[i];
                if (option == null)
                    continue;

                if (option.button != null)
                    option.button.interactable = manager != null && manager.CanLaunch(option.ballCount);

                if (option.costLabel != null)
                {
                    int cost = (manager != null ? manager.TicketCost : 1) * Mathf.Max(1, option.ballCount);
                    option.costLabel.text = cost.ToString("N0");
                }
            }

            // 나가는 것만 막는다. 지급은 이미 끝났지만 연출은 끝까지 보여준다.
            if (closeButton != null)
                closeButton.interactable = !IsBusy;

            RefreshGauge();
        }

        private void RefreshGauge()
        {
            if (gaugeText == null)
                return;

            PinballManager manager = Manager;
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
