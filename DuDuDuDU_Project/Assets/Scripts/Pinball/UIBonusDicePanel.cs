using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Pinball;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.Rewind;
using OJ.Save;
using OJ.UI;

namespace OJ.Pinball
{
    /// <summary>
    /// 보상 라운드 화면. 주사위를 굴려 족보를 만들고, 그 족보만큼 공을 보상판에 쏟는다.
    ///
    /// <b><see cref="UIPinballPage"/> 안에 사는 패널이지 다이얼로그가 아니다.</b> 전환이
    /// "판이 휘릭 돌아가는" 연출이라 같은 화면에 머물러야 하고, 그 페이지는 이미 900줄이
    /// 넘어 여기까지 얹으면 읽을 수 없게 된다.
    ///
    /// <b>게이지는 공이 착지할 때마다 드러난다.</b> 카운트 자체는 샷 버튼을 누르는 순간
    /// 이미 확정돼 저장까지 끝나 있지만(<see cref="BonusDiceManager.TryShoot"/>), 그것을
    /// 그 자리에서 다 보여 주면 공이 구르기도 전에 게이지가 차 버린다. 그래서 화면은
    /// <see cref="BonusShot.HitMask"/> 를 들고 있다가 착지마다 그 몫만 연다 —
    /// 보이는 것과 셈한 것이 끝에서 정확히 만난다.
    /// </summary>
    public sealed class UIBonusDicePanel : MonoBehaviour
    {
        /// <summary>주사위 한 칸. 버튼을 누르면 잠기고, 잠긴 것은 리롤에서 빠진다.</summary>
        [Serializable]
        public sealed class DiceSlot
        {
            public Button button;
            public TMP_Text label;

            [Tooltip("잠겼을 때만 켜지는 표식. 없어도 동작한다.")]
            public GameObject lockMark;
        }

        [Header("보상판 — 구워진 프리팹을 자식으로 넣고 연결한다")]
        [SerializeField] private PinballPlayback playback;
        [SerializeField] private PinballBoardView boardView;

        [Tooltip("보상판 전용 SeedTable. 핀볼 것과 섞이면 안 된다.")]
        [SerializeField] private SeedTable seedTable;

        [Tooltip("보상판 전용 PinballBoard. 특수 핀 태그의 정본이다.")]
        [SerializeField] private PinballBoard board;

        [Header("주사위")]
        [SerializeField] private List<DiceSlot> diceSlots = new List<DiceSlot>();

        [Header("버튼")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private Button shootButton;

        [Tooltip("광고로 기회 더 받기. IsAvailable 이 false 면 아예 안 보인다.")]
        [SerializeField] private Button adButton;

        [Tooltip("광고제거권 보유자만 보이는 「바로 전액 받기」.")]
        [SerializeField] private Button claimAllButton;

        [Header("글자")]
        [SerializeField] private TMP_Text handText;
        [SerializeField] private TMP_Text cycleText;
        [SerializeField] private TMP_Text rerollText;

        [Tooltip("핀 위에 붙는 글자 크기.")]
        [SerializeField] private float pinLabelSize = 24f;

        /// <summary>라운드가 끝났을 때. 페이지가 이걸 받아 판을 되돌린다.</summary>
        public event Action OnRoundFinished;

        private BonusShotSource source;

        /// <summary>착지할 때마다 열리는 표시용 카운트. 매니저의 값과 끝에서 만난다.</summary>
        private readonly Dictionary<int, int> revealed = new Dictionary<int, int>();

        private readonly List<BonusShot> shots = new List<BonusShot>();
        private readonly List<int> slotBuffer = new List<int>();
        private readonly List<uint> seedBuffer = new List<uint>();
        private readonly List<PointRewardEntry> roundRewards = new List<PointRewardEntry>();
        /// <summary>
        /// 특수 핀 태그 → 그 핀 위에 붙은 진행도 글자들.
        ///
        /// <b>같은 태그의 핀이 여럿일 수 있다.</b> 그래서 값이 목록이다 — 판에 같은 태그를
        /// 두 곳에 박아 두면 둘 다 같은 숫자를 보여 줘야 한다.
        /// </summary>
        private readonly Dictionary<int, List<TMP_Text>> pinGaugeLabels =
            new Dictionary<int, List<TMP_Text>>();

        /// <summary>핀 위 라벨을 모아 둔 부모. 다시 지을 때 통째로 지운다.</summary>
        private RectTransform pegLabelRoot;

        private bool bound;
        private bool firing;
        private bool finishing;

        /// <summary>다 채운 핀의 글자색. 흰 진행도와 구별되어야 한 눈에 읽힌다.</summary>
        private static readonly Color DoneColor = new Color(1f, 0.84f, 0.38f, 1f);

        private static BonusDiceManager Manager => BonusDiceManager.Instance;

        // ──────────────────────────────────────────────── 수명

        private void Awake()
        {
            EnsureSource();
        }

        /// <summary>
        /// 궤적 공급부를 한 번만 만든다.
        ///
        /// <b><c>Awake</c> 에만 두면 안 된다.</b> 이 패널은 프리팹에서 꺼진 채로 시작하고,
        /// 꺼진 오브젝트의 <c>Awake</c> 는 <b>처음 켜질 때까지 돌지 않는다</b> —
        /// 그런데 켜는 쪽(<c>UIPinballPage</c>)은 켜자마자 <see cref="BeginRound"/> 를 부른다.
        ///
        /// <b>보이는 것을 켜고 끄는 주도권은 페이지에 있다.</b> 여기서 스스로 끄면
        /// 페이지가 켜는 순간 다시 꺼져 판이 돌아가도 아무것도 안 보인다.
        /// </summary>
        private void EnsureSource()
        {
            if (source != null)
                return;

            source = new BonusShotSource(seedTable, board);

            if (seedTable == null || board == null)
            {
                Debug.LogError(
                    "[보상라운드] 보상판(PinballBoard)이나 SeedTable 이 연결되지 않았다. " +
                    "패널이 열려도 공이 안 나간다.");
            }
            else if (seedTable.IsStale)
            {
                Debug.LogError(
                    "[보상라운드] SeedTable 이 판과 어긋났다(IsStale). 핀을 옮기고 재베이크를 " +
                    "잊은 상태다. Sim Lab 의 Bake 탭에서 다시 구울 것.");
            }
        }

        private void Bind()
        {
            if (bound)
                return;

            bound = true;

            if (rerollButton != null)
                rerollButton.onClick.AddListener(OnRerollClicked);

            if (shootButton != null)
                shootButton.onClick.AddListener(OnShootClicked);

            if (adButton != null)
                adButton.onClick.AddListener(OnAdClicked);

            if (claimAllButton != null)
                claimAllButton.onClick.AddListener(OnClaimAllClicked);

            for (int i = 0; i < diceSlots.Count; i++)
            {
                DiceSlot slot = diceSlots[i];
                if (slot == null || slot.button == null)
                    continue;

                int captured = i;   // 루프 변수를 그대로 잡으면 전부 마지막 것이 된다
                slot.button.onClick.AddListener(() => OnDiceClicked(captured));
            }

            if (playback != null)
            {
                // 공이 핀에 <b>맞는 순간</b>마다 온다. 카운트가 눈으로 올라가는 곳이 여기다.
                playback.OnSpecialHit += HandleSpecialHit;
                playback.OnAllLanded += HandleAllLanded;
            }

            if (Manager != null)
            {
                Manager.OnChanged += Refresh;
                Manager.OnRoundEnded += HandleRoundEnded;
            }

            if (EntitlementManager.Instance != null)
                EntitlementManager.Instance.OnChanged += Refresh;
        }

        private void Unbind()
        {
            if (!bound)
                return;

            bound = false;

            if (rerollButton != null)
                rerollButton.onClick.RemoveListener(OnRerollClicked);

            if (shootButton != null)
                shootButton.onClick.RemoveListener(OnShootClicked);

            if (adButton != null)
                adButton.onClick.RemoveListener(OnAdClicked);

            if (claimAllButton != null)
                claimAllButton.onClick.RemoveListener(OnClaimAllClicked);

            for (int i = 0; i < diceSlots.Count; i++)
            {
                if (diceSlots[i] != null && diceSlots[i].button != null)
                    diceSlots[i].button.onClick.RemoveAllListeners();
            }

            if (playback != null)
            {
                playback.OnSpecialHit -= HandleSpecialHit;
                playback.OnAllLanded -= HandleAllLanded;
            }

            if (Manager != null)
            {
                Manager.OnChanged -= Refresh;
                Manager.OnRoundEnded -= HandleRoundEnded;
            }

            if (EntitlementManager.Instance != null)
                EntitlementManager.Instance.OnChanged -= Refresh;
        }

        // ──────────────────────────────────────────────── 라운드

        /// <summary>
        /// 패널을 열고 라운드를 시작한다. 이미 진행 중이면 <b>이어서</b> 연다 —
        /// 앱이 죽었다 돌아온 경우가 그렇고, 그때 라운드를 새로 열면 진행도가 날아간다.
        /// </summary>
        public void BeginRound()
        {
            BonusDiceManager manager = Manager;
            if (manager == null)
            {
                Debug.LogError("[보상라운드] BonusDiceManager 가 없어 라운드를 열지 못했다.");
                return;
            }

            EnsureSource();
            gameObject.SetActive(true);
            Bind();

            finishing = false;
            firing = false;
            roundRewards.Clear();

            manager.StartRound();       // 진행 중이면 스스로 아무것도 안 한다

            SyncRevealedToManager();
            BuildPinLabels();
            Refresh();
        }

        /// <summary>패널을 닫는다. 라운드 상태는 건드리지 않는다 — 저장된 것이 정본이다.</summary>
        public void ClosePanel()
        {
            if (playback != null && playback.IsBusy)
                playback.StopAll();

            Unbind();
        }

        // ──────────────────────────────────────────────── 입력

        private void OnDiceClicked(int index)
        {
            BonusDiceManager manager = Manager;
            if (manager == null || firing || !manager.CanShoot)
                return;

            manager.SetLock(index, !manager.IsLocked(index));
        }

        private void OnRerollClicked()
        {
            if (firing)
                return;

            Manager?.Roll();
        }

        private void OnShootClicked()
        {
            BonusDiceManager manager = Manager;
            if (manager == null || firing || !manager.CanShoot)
                return;

            if (source == null || !source.IsUsable)
            {
                Debug.LogError("[보상라운드] 보상판 배선이 성치 않아 발사하지 않는다.");
                return;
            }

            // 지금까지 보이던 값에서 이어서 열려야 한다. 샷 전에 잠가 둔다.
            SyncRevealedToManager();

            // <b>TryShoot 보다 먼저 세운다.</b> 이 샷으로 마지막 핀이 채워지면 매니저가
            // 그 안에서 라운드를 닫고 OnRoundEnded 를 쏘는데, 그때 firing 이 false 면
            // <b>공이 구르기도 전에 결과 팝업이 뜬다.</b>
            firing = true;

            IReadOnlyList<PointRewardEntry> rewards =
                manager.TryShoot(source, shots, out DiceHand hand, out int ballCount);

            if (rewards == null)
            {
                // 매니저가 이미 크게 울었다. 사이클도 그대로이므로 버튼을 돌려준다.
                firing = false;
                Refresh();
                return;
            }

            if (rewards.Count > 0)
                roundRewards.AddRange(rewards);

            slotBuffer.Clear();
            seedBuffer.Clear();
            for (int i = 0; i < shots.Count; i++)
            {
                slotBuffer.Add(shots[i].Slot);
                seedBuffer.Add(shots[i].Seed);
            }

            if (playback != null)
            {
                playback.PlayForSeeds(slotBuffer, seedBuffer);
            }
            else
            {
                // 재생이 없으면 연출만 없는 것이다. 결과는 이미 확정·저장됐으므로 바로 마감한다.
                RevealEverything();
                HandleAllLanded();
            }

            Refresh();
        }

        private void OnAdClicked()
        {
            BonusDiceManager manager = Manager;
            if (manager == null || firing || !manager.CanWatchAd)
                return;

            IRewardedAdService ads = GameContainer.RewardedAds;
            if (ads == null || !ads.IsAvailable)
                return;

            // 끝까지 본 경우에만 onRewarded 가 온다. 중간에 닫으면 아무 일도 없다.
            ads.Show(
                () => manager.GrantAdRetry(),
                () => Debug.Log("[보상라운드] 광고를 끝까지 보지 않아 기회를 주지 않았다."));
        }

        private void OnClaimAllClicked()
        {
            BonusDiceManager manager = Manager;
            if (manager == null || firing || !manager.CanClaimAll)
                return;

            IReadOnlyList<PointRewardEntry> rewards = manager.ClaimAllRemaining();
            if (rewards != null && rewards.Count > 0)
                roundRewards.AddRange(rewards);

            // ClaimAllRemaining 이 라운드를 닫으므로 OnRoundEnded 가 이어서 온다.
        }

        // ──────────────────────────────────────────────── 재생 콜백

        /// <summary>
        /// 공이 특수 핀에 맞았다. 그 핀의 카운트를 <b>한 칸 올려 보여 준다.</b>
        ///
        /// <b>지급은 여기서 하지 않는다.</b> 이번 샷의 결과는 샷 버튼을 누르는 순간 이미
        /// 확정돼 저장까지 끝나 있다(<see cref="BonusDiceManager.TryShoot"/>).
        /// 여기가 하는 일은 그 확정된 값까지 숫자를 천천히 드러내는 것뿐이라,
        /// 도중에 화면을 나가도 보상은 이미 들어가 있다.
        ///
        /// <b>매니저가 센 값을 넘지 않는다.</b> 넘으면 화면이 거짓말을 하게 되는데,
        /// <c>BonusShotSource</c> 가 재생과 같은 궤적을 돌려 세므로 정상적으로는 딱 맞는다.
        /// </summary>
        private void HandleSpecialHit(int ballId, int pegIndex, int tag, int hitsForThisBall)
        {
            BonusDiceManager manager = Manager;
            if (manager == null)
                return;

            revealed.TryGetValue(tag, out int shown);
            int final = manager.GetPinCount(tag);
            if (shown >= final)
                return;

            revealed[tag] = shown + 1;
            RefreshPinLabels();
        }

        private void HandleAllLanded()
        {
            firing = false;
            RevealEverything();

            BonusDiceManager manager = Manager;
            if (manager != null && !manager.InProgress)
            {
                FinishRound();
                return;
            }

            Refresh();
        }

        private void HandleRoundEnded()
        {
            // 재생 중이면 마지막 공이 떨어진 뒤에 마감한다 — 공이 구르는데 판이 돌아가면
            // 무엇 때문에 끝났는지 안 보인다.
            if (firing)
                return;

            FinishRound();
        }

        private void FinishRound()
        {
            if (finishing)
                return;

            finishing = true;
            RevealEverything();
            Refresh();

            if (roundRewards.Count == 0)
            {
                OnRoundFinished?.Invoke();
                return;
            }

            List<PointRewardEntry> merged = PointRewardUtility.MergeRewards(roundRewards);
            roundRewards.Clear();

            UIRewardResultDialog dialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (dialog == null)
            {
                Debug.LogError("[보상라운드] UIRewardResultDialog 를 못 열었다. 지급은 이미 끝났다 — " +
                               PointRewardUtility.BuildRewardSummary(merged));
                OnRoundFinished?.Invoke();
                return;
            }

            dialog.Open(merged, "보상 게임을 완료했습니다.", () => OnRoundFinished?.Invoke());
        }

        // ──────────────────────────────────────────────── 표시용 카운트

        private void SyncRevealedToManager()
        {
            revealed.Clear();

            BonusDiceManager manager = Manager;
            if (manager?.Pins == null)
                return;

            for (int i = 0; i < manager.Pins.Count; i++)
            {
                BonusPinReward pin = manager.Pins[i];
                if (pin != null)
                    revealed[pin.tag] = manager.GetPinCount(pin.tag);
            }
        }

        /// <summary>남은 몫을 한꺼번에 연다. 재생이 끝났거나 건너뛴 경우.</summary>
        private void RevealEverything()
        {
            SyncRevealedToManager();
            RefreshPinLabels();
        }

        private int Shown(int tag)
        {
            return revealed.TryGetValue(tag, out int value) ? value : 0;
        }

        // ──────────────────────────────────────────────── 갱신

        private void Refresh()
        {
            BonusDiceManager manager = Manager;
            if (manager == null)
                return;

            RefreshDice(manager);
            RefreshTexts(manager);
            RefreshButtons(manager);
            RefreshPinLabels();
        }

        private void RefreshDice(BonusDiceManager manager)
        {
            IReadOnlyList<int> dice = manager.Dice;

            for (int i = 0; i < diceSlots.Count; i++)
            {
                DiceSlot slot = diceSlots[i];
                if (slot == null)
                    continue;

                bool exists = i < dice.Count;

                if (slot.button != null)
                {
                    slot.button.gameObject.SetActive(exists);
                    slot.button.interactable = exists && !firing && manager.CanShoot;
                }

                if (!exists)
                    continue;

                if (slot.label != null)
                    slot.label.text = dice[i].ToString();

                if (slot.lockMark != null)
                    slot.lockMark.SetActive(manager.IsLocked(i));
            }
        }

        private void RefreshTexts(BonusDiceManager manager)
        {
            if (handText != null)
                handText.text = HandName(manager.CurrentHand) + "  →  공 " + manager.CurrentBallCount + "개";

            if (cycleText != null)
            {
                string text = "남은 기회 " + manager.RemainingCycles;

                // 배율은 핀볼에서 센터핀을 여러 번 채워 얻은 것이다. 판 안에서도 보여야
                // 안내에 뜬 x3 이 실제로 적용됐다는 것이 확인된다.
                if (manager.RewardMultiplier > 1)
                    text += "    보상 x" + manager.RewardMultiplier;

                cycleText.text = text;
            }

            if (rerollText != null)
                rerollText.text = "다시 굴리기 " + manager.RerollsLeft;
        }

        private void RefreshButtons(BonusDiceManager manager)
        {
            if (rerollButton != null)
                rerollButton.interactable = !firing && manager.CanRoll;

            if (shootButton != null)
                shootButton.interactable = !firing && manager.CanShoot;

            // 규약: 광고가 없으면 버튼을 아예 안 그린다. 눌리지 않는 버튼은 고장으로 읽힌다.
            if (adButton != null)
                adButton.gameObject.SetActive(!firing && manager.CanWatchAd);

            if (claimAllButton != null)
                claimAllButton.gameObject.SetActive(!firing && manager.CanClaimAll);
        }

        /// <summary>
        /// 특수 핀 <b>위에</b> 보상 아이콘과 진행도를 붙인다.
        ///
        /// <b>판의 자식으로 만든다.</b> <c>PinballBoardView.pegs</c> 가 판 루트 기준 anchored
        /// 좌표를 주므로, 같은 부모 아래에서는 그 값을 그대로 쓸 수 있다.
        /// <c>UIPinballPage.BuildSpecialLabels</c> 와 같은 방식이다.
        ///
        /// <b>보상표에 없는 태그는 건너뛴다.</b> 카운트가 올라가지 않는 핀에 숫자를 붙이면
        /// 받을 수 없는 보상을 광고하는 셈이 된다.
        /// </summary>
        private void BuildPinLabels()
        {
            pinGaugeLabels.Clear();

            if (pegLabelRoot != null)
                Destroy(pegLabelRoot.gameObject);

            BonusDiceManager manager = Manager;
            if (manager == null || boardView == null ||
                boardView.board == null || boardView.board.pegs == null || boardView.pegs == null)
            {
                return;
            }

            pegLabelRoot = NewChild("PinGauges", boardView.transform);

            Peg[] pegs = boardView.board.pegs;
            int limit = Mathf.Min(pegs.Length, boardView.pegs.Length);

            for (int i = 0; i < limit; i++)
            {
                int tag = pegs[i].specialTag;
                if (tag == 0)
                    continue;

                BonusPinReward pin = FindPin(manager, tag);
                if (pin == null)
                    continue;

                BuildOnePinLabel(i, tag, pin);
            }
        }

        private void BuildOnePinLabel(int pegIndex, int tag, BonusPinReward pin)
        {
            BonusDiceManager manager = Manager;

            RectTransform holder = NewChild("Peg_" + pegIndex + "_Tag" + tag, pegLabelRoot);
            holder.anchoredPosition = PegAnchored(pegIndex);

            // 핀 반지름이 20px 안팎이라 그 위로 올려야 핀을 가리지 않는다.
            bool hasReward = pin.rewards != null && pin.rewards.Count > 0;
            if (hasReward)
            {
                Sprite icon = PointRewardUtility.GetPointIcon(pin.rewards[0].pointType);
                if (icon != null)
                {
                    RectTransform iconRect = NewChild("Icon", holder);
                    var image = iconRect.gameObject.AddComponent<Image>();
                    image.sprite = icon;
                    image.raycastTarget = false;
                    image.preserveAspect = true;
                    iconRect.sizeDelta = new Vector2(40f, 40f);
                    iconRect.anchoredPosition = new Vector2(0f, 74f);
                }

                TMP_Text amount = NewLabel(
                    "Amount", holder,
                    BuildRewardText(pin, manager != null ? manager.RewardMultiplier : 1),
                    pinLabelSize - 2f);
                amount.rectTransform.sizeDelta = new Vector2(140f, 44f);
                amount.rectTransform.anchoredPosition = new Vector2(0f, 44f);
            }

            // 진행도. 공이 맞을 때마다 올라야 하므로 참조를 들고 있는다.
            TMP_Text gauge = NewLabel("Gauge", holder, string.Empty, pinLabelSize);
            gauge.rectTransform.sizeDelta = new Vector2(140f, 38f);
            gauge.rectTransform.anchoredPosition = new Vector2(0f, hasReward ? 18f : 44f);

            if (!pinGaugeLabels.TryGetValue(tag, out List<TMP_Text> labels))
            {
                labels = new List<TMP_Text>();
                pinGaugeLabels[tag] = labels;
            }

            labels.Add(gauge);
        }

        private void RefreshPinLabels()
        {
            BonusDiceManager manager = Manager;
            if (manager == null || pinGaugeLabels.Count == 0)
                return;

            foreach (KeyValuePair<int, List<TMP_Text>> pair in pinGaugeLabels)
            {
                BonusPinReward pin = FindPin(manager, pair.Key);
                if (pin == null)
                    continue;

                int shown = Mathf.Min(Shown(pair.Key), pin.requiredHits);
                bool done = pin.requiredHits > 0 && shown >= pin.requiredHits;

                string text = done ? "획득!" : shown + " / " + pin.requiredHits;
                Color color = done ? DoneColor : Color.white;

                List<TMP_Text> labels = pair.Value;
                for (int i = 0; i < labels.Count; i++)
                {
                    if (labels[i] == null)
                        continue;

                    labels[i].text = text;
                    labels[i].color = color;
                }
            }
        }

        private static BonusPinReward FindPin(BonusDiceManager manager, int tag)
        {
            IReadOnlyList<BonusPinReward> pins = manager.Pins;
            if (pins == null)
                return null;

            for (int i = 0; i < pins.Count; i++)
            {
                if (pins[i] != null && pins[i].tag == tag)
                    return pins[i];
            }
            return null;
        }

        /// <summary>
        /// 핀 위에 적을 보상 수량. <b>배율을 곱한 실제 지급액을 적는다</b> —
        /// 기본값을 적어 두면 x3 라운드에서 받은 것과 적힌 것이 달라 보인다.
        ///
        /// 배율은 판이 뒤집히기 전에 이미 확정되므로(핀볼 세션에서만 오른다)
        /// 라벨을 한 번 만들어 두면 라운드 내내 맞다.
        /// </summary>
        private static string BuildRewardText(BonusPinReward pin, int multiplier)
        {
            int scale = Mathf.Max(1, multiplier);

            var sb = new StringBuilder();
            for (int i = 0; i < pin.rewards.Count; i++)
            {
                if (i > 0)
                    sb.Append('\n');

                sb.Append(PinballRules.ScaleAmount(pin.rewards[i].amount, scale).ToString("N0"));
            }
            return sb.ToString();
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

        private static RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            // 판의 자식들과 같은 규약이어야 peg 좌표가 그대로 맞는다.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private TMP_Text NewLabel(string name, Transform parent, string text, float size)
        {
            RectTransform rect = NewChild(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();

            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.color = Color.white;

            // 폰트는 형제에게서 물려받는다. 없으면 TMP 기본이 나오는데, 한글이 깨진다.
            if (handText != null && handText.font != null)
                label.font = handText.font;

            return label;
        }

        private static string HandName(DiceHand hand)
        {
            switch (hand)
            {
                case DiceHand.OnePair: return "원페어";
                case DiceHand.TwoPair: return "투페어";
                case DiceHand.Triple: return "트리플";
                case DiceHand.SmallStraight: return "스트레이트";
                case DiceHand.FullHouse: return "풀하우스";
                case DiceHand.LargeStraight: return "라지 스트레이트";
                case DiceHand.FourOfAKind: return "포카드";
                case DiceHand.FiveOfAKind: return "파이브카드";
                default: return "노페어";
            }
        }
    }
}
