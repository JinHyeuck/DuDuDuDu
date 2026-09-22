using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// 서버가 내려준 슬롯 -> 시드백에서 시드 -> 시뮬 재생.
    /// 구슬 여러 개가 동시에 굴러갈 수 있고, 각자 독립된 궤적과 시계를 갖는다.
    ///
    /// 주의: 시뮬은 구슬 하나를 기준으로 계산된 것이라 구슬끼리는 충돌하지 않는다.
    /// 겹쳐 보이는 건 연출상 문제일 뿐 결과에는 영향이 없다.
    /// 겹침이 거슬리면 launchInterval 을 0.3초 이상 주면 거의 눈에 띄지 않는다.
    ///
    /// boardView 가 있으면 UI 캔버스 위에서, 없으면 월드 Transform 으로 움직인다.
    /// </summary>
    public sealed class PinballPlayback : MonoBehaviour
    {
        [Header("필수")]
        [SerializeField] private SeedTable seedTable;

        [Header("UI 모드 — 구워진 프리팹의 루트를 넣으면 된다")]
        [SerializeField] private PinballBoardView boardView;

        [Header("월드 모드 — boardView 가 비어있을 때 쓴다")]
        [Tooltip("구슬 '원본'. 동시에 굴러가는 구슬만큼 복제된다.")]
        [SerializeField] private Transform ballView;
        [SerializeField] private Transform boardOrigin;

        [Header("연출")]
        [Tooltip("연출 세기를 정규화할 기준 충돌 속도. 이 값 이상이면 최대 세기로 친다.")]
        [SerializeField] private float impactReference = 12f;

        [Tooltip("PlayForSlots 로 여러 발을 쏠 때 기본 발사 간격(초).")]
        [SerializeField] private float launchInterval = 0.35f;

        /// <summary>인자: (구슬 id, 핀 인덱스 또는 -1=벽, 그 구슬의 누적 충돌 수, 세기 0~1)</summary>
        public event Action<int, int, int, float> OnHit;

        /// <summary>인자: (구슬 id, 착지한 슬롯 인덱스). 구슬 하나가 끝날 때마다 호출된다.</summary>
        public event Action<int, int> OnLanded;

        /// <summary>
        /// OnLanded 와 같은 시점에 호출되지만 정보가 더 많다.
        /// 발사 순번(launchIndex)이 필요하거나 로그를 남길 때 쓴다.
        /// </summary>
        public event Action<BallResult> OnBallLanded;

        /// <summary>
        /// 특수 핀에 맞을 때마다. 인자: (구슬 id, 핀 인덱스, specialTag, 이 구슬의 특수핀 누적 적중 수)
        /// </summary>
        public event Action<int, int, int, int> OnSpecialHit;

        /// <summary>대기 중인 발사와 굴러가는 구슬이 모두 소진되면 한 번 호출된다.</summary>
        public event Action OnAllLanded;

        /// <summary>이번 판에서 특수 핀에 맞은 총 횟수. OnAllLanded 시점에 읽으면 된다.</summary>
        public int SessionSpecialHits { get; private set; }

        /// <summary>착지한 구슬 한 개의 결과.</summary>
        public readonly struct BallResult
        {
            /// <summary>이 컴포넌트가 켜진 뒤로 계속 증가하는 고유 번호.</summary>
            public readonly int ballId;

            /// <summary>이번 판에서 몇 번째로 발사됐는지. 0부터 시작하고 판마다 리셋된다.</summary>
            public readonly int launchIndex;

            /// <summary>착지한 보상 칸.</summary>
            public readonly int slot;

            /// <summary>재생에 쓰인 시드. 버그 재현에 필요하다.</summary>
            public readonly uint seed;

            public readonly int pegHits;

            /// <summary>이 구슬이 특수 핀에 맞은 횟수.</summary>
            public readonly int specialHits;

            public readonly float flightSeconds;

            public BallResult(int ballId, int launchIndex, int slot, uint seed,
                              int pegHits, int specialHits, float flightSeconds)
            {
                this.ballId = ballId;
                this.launchIndex = launchIndex;
                this.slot = slot;
                this.seed = seed;
                this.pegHits = pegHits;
                this.specialHits = specialHits;
                this.flightSeconds = flightSeconds;
            }

            public override string ToString() =>
                $"ball#{launchIndex} (id {ballId}) -> slot {slot}, seed {seed}, " +
                $"peg {pegHits}, special {specialHits}, {flightSeconds:0.00}s";
        }

        // ── 내부 상태

        private sealed class Ball
        {
            public int id;
            public int launchIndex;
            public int slot;
            public uint seed;
            public int pegHits;
            public int specialHitsTotal;
            public int specialHitsFired;
            public readonly List<Vector2> path = new List<Vector2>(1024);
            public readonly List<HitEvent> hits = new List<HitEvent>(64);
            public float clock;
            public int hitCursor;
            public int hitCount;
            public RectTransform ui;
            public Transform world;

            public void Reset()
            {
                path.Clear();
                hits.Clear();
                clock = 0f;
                hitCursor = 0;
                hitCount = 0;
                specialHitsTotal = 0;
                specialHitsFired = 0;
                ui = null;
                world = null;
            }
        }

        private struct PendingLaunch
        {
            public int slot;
            public float dueTime;

            /// <summary>지정된 시드. <see cref="hasSeed"/> 가 false 면 무시된다.</summary>
            public uint seed;

            /// <summary>호출부가 시드를 직접 골랐는가.</summary>
            public bool hasSeed;
        }

        private PinballSimulator _sim;
        private SeedBag _bag;
        private PinballBoard _board;
        private SpecialHitSolver _solver;
        private readonly System.Random _maskRand = new System.Random();
        private float _dt;
        private int _nextId;
        private int _sessionLaunchIndex;

        private readonly List<Ball> _active = new List<Ball>();
        private readonly Stack<Ball> _ballPool = new Stack<Ball>();
        private readonly List<PendingLaunch> _pending = new List<PendingLaunch>();
        private readonly Stack<Transform> _worldPool = new Stack<Transform>();

        /// <summary>OnAllLanded 를 한 번만 쏘기 위한 래치. 발사가 시작되면 켜진다.</summary>
        private bool _sessionOpen;

        public int ActiveBallCount => _active.Count;
        public int PendingLaunchCount => _pending.Count;
        public bool IsBusy => _active.Count > 0 || _pending.Count > 0;

        private void Awake()
        {
            var board = seedTable != null ? seedTable.board : null;
            if (board == null)
            {
                Debug.LogError("[Pinball] SeedTable 또는 Board 가 지정되지 않았습니다.", this);
                enabled = false;
                return;
            }

            _board = board;
            var snapshot = board.CreateSnapshot();
            _sim = new PinballSimulator(snapshot);
            _bag = new SeedBag(seedTable);
            _solver = new SpecialHitSolver(seedTable, board);
            _dt = snapshot.dt;

            if (ballView != null) ballView.gameObject.SetActive(false);   // 월드 모드 원본 숨기기

            if (seedTable.IsStale)
            {
                Debug.LogError(
                    "[Pinball] SeedTable 이 현재 보드와 불일치합니다. Sim Lab 에서 다시 베이크하세요.\n" +
                    "이 상태로는 표기 확률과 실제 궤적이 어긋납니다.", seedTable);
            }
        }

        // ──────────────────────────────────────────────── 발사

        /// <summary>
        /// 버튼 OnClick 등 UnityEvent 에 직접 연결할 수 있는 발사.
        ///
        /// UnityEvent 는 "인자 1개 + 반환값 있는 메서드"를 호출하지 못한다.
        /// (에디터 드롭다운에는 보이지만 실행 시 ArgumentException: method return type is incompatible)
        /// 그래서 반환값이 필요 없는 경우를 위해 void 버전을 따로 둔다.
        /// </summary>
        public void Shoot(int slot) => PlayForSlot(slot);

        /// <summary>
        /// 구슬 하나를 즉시 발사한다. 이미 굴러가는 구슬은 그대로 둔다.
        ///
        /// 주의: 반환값이 있으므로 버튼 OnClick 에 직접 꽂으면 런타임에 터진다. Shoot(int) 를 쓸 것.
        /// </summary>
        /// <returns>구슬 id. 실패하면 -1.</returns>
        public int PlayForSlot(int slot)
        {
            if (_sim == null) return -1;

            int mask = DrawSpecialMask(slot);

            if (!_bag.TryTake(slot, mask, out uint seed))
            {
                if (mask >= 0 && _bag.TryTake(slot, -1, out seed))
                {
                    // 그 (슬롯, 적중 패턴) 조합의 궤적이 없다. 슬롯은 지켜야 하므로 패턴을 포기한다.
                    Debug.LogWarning(
                        $"[Pinball] slot {slot} + 적중 패턴 {mask} 의 궤적이 없어 패턴 지정을 폴백했습니다. " +
                        "Sim Lab 의 Bake 탭에서 태그별 표본을 확인하세요.", this);
                }
                else
                {
                    Debug.LogError($"[Pinball] slot {slot} 시드 풀이 비었습니다. 폴백이 필요합니다.", this);
                    return -1;
                }
            }

            return Launch(slot, seed);
        }

        /// <summary>
        /// 시드를 <b>지정해</b> 한 발 쏜다. 시드 풀에서 고르지 않는다.
        ///
        /// <b>호출부가 이미 결과를 확정한 경우에 쓴다.</b> 보상 라운드가 그렇다 —
        /// 샷 버튼을 누르는 순간에 시드를 다 뽑아 <c>SeedTable.hitMask</c> 로 적중을 합산하고
        /// 지급·저장까지 끝낸 뒤, 그 시드를 그대로 재생한다. 그래야 굴러가는 도중에
        /// 앱이 죽거나 화면을 나가도 <b>보상과 진행도가 갈라지지 않는다.</b>
        ///
        /// 시드가 그 칸으로 가지 않으면 <b>발사하지 않고</b> -1 을 돌려준다 —
        /// 표와 판이 어긋난 상태를 연출로 덮지 않기 위해서다.
        /// </summary>
        /// <returns>구슬 id. 실패하면 -1.</returns>
        public int PlayForSeed(int slot, uint seed)
        {
            return _sim == null ? -1 : Launch(slot, seed);
        }

        private int Launch(int slot, uint seed)
        {
            var ball = _ballPool.Count > 0 ? _ballPool.Pop() : new Ball();
            ball.Reset();
            ball.id = _nextId++;
            ball.launchIndex = _sessionLaunchIndex++;
            ball.seed = seed;

            var r = _sim.Run(seed, ball.path, ball.hits);

            if (r.slot != slot)
            {
                // 여기 걸리면 테이블이 판과 어긋난 것 = 확률 표기 위반 직전 상황
                Debug.LogError($"[Pinball] 재현 불일치! seed={seed} expected={slot} actual={r.slot}", this);
                _sessionLaunchIndex--;          // 발사되지 않았으므로 순번을 돌려놓는다
                _ballPool.Push(ball);
                return -1;
            }

            ball.slot = r.slot;
            ball.pegHits = r.pegHits;
            ball.specialHitsTotal = r.specialHits;
            AcquireVisual(ball);
            SetBall(ball, ball.path[0]);

            _active.Add(ball);
            _sessionOpen = true;
            return ball.id;
        }

        /// <summary>
        /// 태그별 목표 확률에 맞는 적중 패턴을 뽑는다. Natural 정책이면 -1(가리지 않음).
        /// </summary>
        private int DrawSpecialMask(int slot)
        {
            if (_board == null || _solver == null) return -1;
            if (_board.specialHitPolicy == SpecialHitPolicy.Natural) return -1;
            if (_solver.BitCount == 0) return -1;

            return _solver.PickMask(slot, _maskRand);
        }

        /// <summary>
        /// 여러 발을 간격을 두고 순차 발사한다.
        /// 마지막 구슬까지 전부 착지하면 OnAllLanded 가 한 번 호출된다.
        /// </summary>
        /// <param name="intervalSeconds">음수면 인스펙터의 launchInterval 을 쓴다.</param>
        public void PlayForSlots(IReadOnlyList<int> slots, float intervalSeconds = -1f)
        {
            if (slots == null || slots.Count == 0) return;

            float gap = intervalSeconds < 0f ? launchInterval : intervalSeconds;
            float now = Time.time;

            for (int i = 0; i < slots.Count; i++)
                _pending.Add(new PendingLaunch { slot = slots[i], dueTime = now + gap * i });

            _sessionOpen = true;
        }

        /// <summary>
        /// 시드를 지정해 여러 발을 간격을 두고 순차 발사한다.
        /// <paramref name="slots"/> 와 <paramref name="seeds"/> 는 인덱스가 대응해야 한다.
        ///
        /// 둘의 길이가 다르면 <b>짧은 쪽에 맞춘다</b> — 짝이 안 맞는 시드를 짐작으로
        /// 쓰면 엉뚱한 칸으로 가는 구슬이 섞인다.
        /// </summary>
        public void PlayForSeeds(
            IReadOnlyList<int> slots, IReadOnlyList<uint> seeds, float intervalSeconds = -1f)
        {
            if (slots == null || seeds == null) return;

            int count = Mathf.Min(slots.Count, seeds.Count);
            if (count == 0) return;

            if (count != slots.Count || count != seeds.Count)
            {
                Debug.LogWarning(
                    $"[Pinball] 칸({slots.Count})과 시드({seeds.Count}) 개수가 달라 {count}발만 쏴니다.", this);
            }

            float gap = intervalSeconds < 0f ? launchInterval : intervalSeconds;
            float now = Time.time;

            for (int i = 0; i < count; i++)
            {
                _pending.Add(new PendingLaunch
                {
                    slot = slots[i],
                    seed = seeds[i],
                    hasSeed = true,
                    dueTime = now + gap * i,
                });
            }

            _sessionOpen = true;
        }

        /// <summary>대기 중인 발사를 취소하고 굴러가는 구슬을 전부 회수한다.</summary>
        public void StopAll(bool fireAllLandedCallback = false)
        {
            _pending.Clear();

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ReleaseVisual(_active[i]);
                _ballPool.Push(_active[i]);
            }
            _active.Clear();

            if (fireAllLandedCallback && _sessionOpen) OnAllLanded?.Invoke();
            _sessionOpen = false;
            _sessionLaunchIndex = 0;
            SessionSpecialHits = 0;
        }

        // ──────────────────────────────────────────────── 재생

        private void Update()
        {
            PumpPending();
            TickBalls();

            if (_sessionOpen && _active.Count == 0 && _pending.Count == 0)
            {
                _sessionOpen = false;
                _sessionLaunchIndex = 0;        // 다음 판은 다시 0번부터
                OnAllLanded?.Invoke();
                SessionSpecialHits = 0;         // 콜백이 읽은 뒤에 리셋
            }
        }

        private void PumpPending()
        {
            if (_pending.Count == 0) return;

            // _pending 은 시간순으로 쌓이므로 앞에서부터 처리해야 발사 순서가 보존된다.
            float now = Time.time;
            int due = 0;
            while (due < _pending.Count && _pending[due].dueTime <= now) due++;
            if (due == 0) return;

            var ready = new PendingLaunch[due];
            for (int i = 0; i < due; i++) ready[i] = _pending[i];
            _pending.RemoveRange(0, due);

            for (int i = 0; i < due; i++)
            {
                if (ready[i].hasSeed)
                    PlayForSeed(ready[i].slot, ready[i].seed);
                else
                    PlayForSlot(ready[i].slot);
            }
        }

        private void TickBalls()
        {
            float delta = Time.deltaTime;

            // 착지한 구슬을 제거하므로 뒤에서부터 순회한다
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var b = _active[i];
                if (b.path.Count < 2) { Finish(b, i); continue; }

                b.clock += delta;
                float f = b.clock / _dt;
                int step = (int)f;

                // 이번 프레임에 지나친 충돌 이벤트를 전부 발사한다.
                // 60fps 로 120Hz 시뮬을 재생하므로 한 프레임에 2스텝씩 지나간다.
                while (b.hitCursor < b.hits.Count && b.hits[b.hitCursor].step <= step)
                {
                    var h = b.hits[b.hitCursor++];
                    b.hitCount++;

                    float strength = Mathf.Clamp01(h.impact / Mathf.Max(0.01f, impactReference));
                    if (h.pegIndex >= 0 && boardView != null) boardView.PunchPeg(h.pegIndex, strength);
                    OnHit?.Invoke(b.id, h.pegIndex, b.hitCount, strength);

                    if (h.pegIndex >= 0 && _board != null && h.pegIndex < _board.pegs.Length)
                    {
                        int tag = _board.pegs[h.pegIndex].specialTag;
                        if (tag != 0)
                        {
                            b.specialHitsFired++;
                            SessionSpecialHits++;
                            OnSpecialHit?.Invoke(b.id, h.pegIndex, tag, b.specialHitsFired);
                        }
                    }
                }

                if (step >= b.path.Count - 1)
                {
                    SetBall(b, b.path[b.path.Count - 1]);
                    Finish(b, i);
                    continue;
                }

                SetBall(b, Vector2.Lerp(b.path[step], b.path[step + 1], f - step));
            }
        }

        private void Finish(Ball b, int index)
        {
            // 풀에 반납하기 전에 필요한 값을 복사해둔다.
            // 콜백 안에서 PlayForSlot 을 호출하면 이 객체가 즉시 재사용될 수 있다.
            var result = new BallResult(b.id, b.launchIndex, b.slot, b.seed, b.pegHits,
                                        b.specialHitsTotal, b.path.Count * _dt);

            _active.RemoveAt(index);
            ReleaseVisual(b);
            _ballPool.Push(b);

            OnLanded?.Invoke(result.ballId, result.slot);
            OnBallLanded?.Invoke(result);
        }

        // ──────────────────────────────────────────────── 비주얼

        private void AcquireVisual(Ball b)
        {
            if (boardView != null)
            {
                b.ui = boardView.AcquireBall();
                return;
            }

            if (ballView == null) return;

            if (_worldPool.Count > 0)
            {
                b.world = _worldPool.Pop();
            }
            else
            {
                b.world = Instantiate(ballView, ballView.parent);
                b.world.name = ballView.name + "_Instance";
            }
            b.world.gameObject.SetActive(true);
        }

        private void ReleaseVisual(Ball b)
        {
            if (b.ui != null && boardView != null)
            {
                boardView.ReleaseBall(b.ui);
                b.ui = null;
            }

            if (b.world != null)
            {
                b.world.gameObject.SetActive(false);
                _worldPool.Push(b.world);
                b.world = null;
            }
        }

        private void SetBall(Ball b, Vector2 local)
        {
            if (b.ui != null && boardView != null)
            {
                boardView.SetBallPosition(b.ui, local);
                return;
            }

            if (b.world == null) return;
            var origin = boardOrigin ? boardOrigin.position : Vector3.zero;
            b.world.position = origin + new Vector3(local.x, local.y, 0f);
        }
    }
}
