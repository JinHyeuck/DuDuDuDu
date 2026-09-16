using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pinball.EditorTools
{
    /// <summary>
    /// 핀볼 판 편집 + 시드 베이크 + 궤적 확인을 한 창에서 한다.
    /// 좌측 패널에서 조작하고 우측 캔버스는 항상 떠 있어서, 편집 결과가 바로 눈에 보인다.
    ///
    /// 파일 분할:
    ///   PinballSimWindow.cs         - 상태, 레이아웃, 베이크/프리뷰 패널
    ///   PinballSimWindow.Canvas.cs  - 좌표 변환과 그리기
    ///   PinballSimWindow.Edit.cs    - 레이아웃 편집 도구와 마우스 처리
    /// </summary>
    public sealed partial class PinballSimWindow : EditorWindow
    {
        public enum Tab { Edit, Bake, Preview, Prefab }
        public enum EditTool { Move, AddPeg, AddWall, Erase }

        [MenuItem("Tools/Pinball/Sim Lab")]
        public static void Open()
        {
            var w = GetWindow<PinballSimWindow>("Pinball Sim Lab");
            w.minSize = new Vector2(980f, 640f);
        }

        // ── 에셋
        private PinballBoard _board;
        private SeedTable _table;

        // ── 탭
        private Tab _tab = Tab.Edit;
        private Vector2 _panelScroll;

        // ── 편집
        private EditTool _tool = EditTool.Move;
        private bool _snap = true;
        private float _snapSize = 0.05f;
        private float _newPegRadius = 0.11f;
        private int _dragPeg = -1;
        private int _dragWall = -1;
        private bool _dragWallIsB;
        private int _dragDivider = -1;
        private bool _dragLaunch;
        private Vector2? _wallStart;
        private int _hoverPeg = -1;
        private int _selectedPeg = -1;
        private int _newSpecialTag = 1;

        // 생성기 파라미터
        private int _gRows = 7, _gCols = 6;
        private float _gTopY = 7.05f, _gRowGap = 0.72f, _gColGap = 0.79f;
        private float _gLeftX = 0.53f, _gRadius = 0.11f, _gMaxX = 4.90f;
        private int _rowCount = 5;
        private float _rowY = 1.95f, _rowFrom = 0.62f, _rowTo = 4.70f;
        private float _mirrorAxis = 2.65f;

        // ── 베이크
        private int _sampleCount = 200000;
        private int _keepPerSlot = 2000;
        private string _status = "";

        // ── 퀵테스트
        private int _probeCount = 2000;
        private ProbeReport? _probe;
        private List<string> _diagnosis = new List<string>();

        // ── 프리팹
        private GameObject _lastPrefab;

        // ── 프리뷰
        private PinballSimulator _sim;
        private readonly List<Vector2> _path = new List<Vector2>(4096);
        private SimResult _last;
        private int _selectedSlot;
        private int _seedIndex;
        private uint _manualSeed = 1;
        private int _scrub = -1;
        private bool _animate;
        private double _animStart;
        private bool _showAllPaths;
        private readonly List<List<Vector2>> _ghostPaths = new List<List<Vector2>>();

        private void OnEnable() { EditorApplication.update += Tick; }
        private void OnDisable() { EditorApplication.update -= Tick; }

        /// <summary>판이 바뀌면 캐시된 시뮬과 궤적을 전부 버린다.</summary>
        private void InvalidateBoard()
        {
            _sim = null;
            _path.Clear();
            _ghostPaths.Clear();
            _probe = null;
            _diagnosis.Clear();
            _scrub = -1;
            _animate = false;
            _selectedPeg = -1;
        }

        private PinballSimulator Sim
        {
            get
            {
                if (_sim == null && _board != null) _sim = new PinballSimulator(_board.CreateSnapshot());
                return _sim;
            }
        }

        private void Tick()
        {
            if (!_animate || _path.Count == 0) return;
            float dt = _board != null ? 1f / Mathf.Max(1, _board.stepsPerSecond) : 1f / 120f;
            int i = (int)((EditorApplication.timeSinceStartup - _animStart) / dt);
            if (i >= _path.Count) { i = _path.Count - 1; _animate = false; }
            _scrub = i;
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftPanel();
                DrawCanvasColumn();
            }
        }

        // ──────────────────────────────────────────────── 좌측 패널

        private void DrawLeftPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(360f)))
            {
                DrawAssets();

                if (_board == null)
                {
                    EditorGUILayout.HelpBox(
                        "Tools > Pinball > Create Default Board 로 판을 하나 만드세요.", MessageType.Info);
                    GUILayout.FlexibleSpace();
                    return;
                }

                _tab = (Tab)GUILayout.Toolbar((int)_tab, new[] { "Edit Board", "Bake", "Preview", "Prefab" });
                EditorGUILayout.Space(4);

                _panelScroll = EditorGUILayout.BeginScrollView(_panelScroll);
                switch (_tab)
                {
                    case Tab.Edit: DrawEditPanel(); break;
                    case Tab.Bake: DrawBakePanel(); break;
                    case Tab.Preview: DrawPreviewPanel(); break;
                    case Tab.Prefab: DrawPrefabPanel(); break;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAssets()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _board = (PinballBoard)EditorGUILayout.ObjectField("Board", _board, typeof(PinballBoard), false);
                if (EditorGUI.EndChangeCheck()) InvalidateBoard();

                _table = (SeedTable)EditorGUILayout.ObjectField("Seed Table", _table, typeof(SeedTable), false);

                if (_board == null) return;

                if (_table != null && _table.board == null)
                {
                    _table.board = _board;
                    EditorUtility.SetDirty(_table);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Layout Hash", _board.LayoutHash(), EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"핀 {_board.pegs.Length} · 벽 {_board.segments.Length} · 칸 {_board.SlotCount}",
                        EditorStyles.miniLabel);
                }

                if (_table != null && _table.sampledCount > 0 && _table.IsStale)
                {
                    EditorGUILayout.HelpBox(
                        "판이 베이크 이후 변경되었습니다. 시드 테이블이 무효입니다 — 다시 베이크하세요.\n" +
                        "이 상태로 출시하면 표기 확률과 실제 궤적이 어긋납니다.",
                        MessageType.Error);
                }
            }
        }

        // ──────────────────────────────────────────────── Bake 탭

        private void DrawBakePanel()
        {
            EditorGUILayout.LabelField("Quick Test", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "판을 고칠 때마다 이걸 먼저 돌리세요. 저장 없이 통계만 냅니다.", EditorStyles.miniLabel);

                _probeCount = EditorGUILayout.IntField("Sample Count", _probeCount);

                if (GUILayout.Button("Run Quick Test", GUILayout.Height(24)))
                    RunProbe();

                DrawProbeResult();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Bake Seed Table", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _sampleCount = EditorGUILayout.IntField(
                    new GUIContent("Sample Count", "시드 1..N 을 전부 시뮬한다. 많을수록 궤적이 다양해진다."), _sampleCount);
                _keepPerSlot = EditorGUILayout.IntField(
                    new GUIContent("Keep Per Slot", "fun 상위 몇 개를 저장할지. 0 = 전부.\n" +
                                                    "2000 이면 유저가 반복을 알아채기 어렵다."), _keepPerSlot);

                EditorGUILayout.LabelField("예상 에셋 크기",
                    $"약 {(_keepPerSlot * _board.SlotCount * 8) / 1024f:0.#} KB");

                using (new EditorGUI.DisabledScope(_table == null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Bake", GUILayout.Height(28)))
                    {
                        try
                        {
                            var rep = SeedTableBaker.Bake(_table, _sampleCount, _keepPerSlot);
                            _status = $"완료: {rep.sampled:N0}개 시뮬, 폐기 {rep.discarded:N0}개, {rep.seconds:0.00}초";
                        }
                        catch (System.OperationCanceledException) { _status = "취소됨"; }
                        catch (System.Exception ex) { _status = "실패: " + ex.Message; Debug.LogException(ex); }
                    }

                    if (GUILayout.Button("Verify", GUILayout.Height(28), GUILayout.Width(110)))
                    {
                        int bad = SeedTableBaker.Verify(_table, 64, out string msg);
                        _status = msg;
                        if (bad > 0) Debug.LogError("[Pinball] " + msg); else Debug.Log("[Pinball] " + msg);
                    }
                }

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.Space(6);
            DrawDistribution();
        }

        private void RunProbe()
        {
            var rep = SeedTableProbe.Run(_board, Mathf.Max(100, _probeCount));
            _probe = rep;
            _diagnosis = SeedTableProbe.Diagnose(rep, Mathf.Max(10, _probeCount / 50));
            Repaint();
        }

        private void DrawProbeResult()
        {
            if (_probe == null) return;
            var r = _probe.Value;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("폐기율", $"{r.DiscardRatio * 100:0.#}%   (유효 {r.Valid:N0})");
            EditorGUILayout.LabelField("평균 핀 충돌", $"{r.avgPegHits:0.#}회");
            EditorGUILayout.LabelField("평균 비행", $"{r.avgSeconds:0.0}초");
            EditorGUILayout.LabelField("니어미스 궤적", $"{r.nearMissRatio * 100:0.#}%");

            for (int i = 0; i < r.perSlot.Length; i++)
            {
                float pct = r.Valid > 0 ? r.perSlot[i] / (float)r.Valid * 100f : 0f;
                EditorGUILayout.LabelField($"  slot {i}", $"{r.perSlot[i]:N0}  ({pct:0.0}%)");
            }

            if (r.hasSpecialPegs)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("특수 핀 평균 적중", $"{r.avgSpecialHits:0.00}회");
                EditorGUILayout.LabelField("태그별 자연 적중률 (한 번이라도)", EditorStyles.miniBoldLabel);

                for (int b = 0; b < r.tagHits.Length; b++)
                {
                    int tag = r.tagByBit != null && b < r.tagByBit.Length ? r.tagByBit[b] : b;
                    float pct = r.Valid > 0 ? r.tagHits[b] / (float)r.Valid * 100f : 0f;
                    EditorGUILayout.LabelField($"  태그 {tag}", $"{r.tagHits[b]:N0}  ({pct:0.0}%)");
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("슬롯 × 태그 적중률", EditorStyles.miniBoldLabel);
                for (int slot = 0; slot < r.perSlot.Length; slot++)
                {
                    if (r.perSlot[slot] == 0) continue;
                    var sb = new System.Text.StringBuilder();
                    for (int b = 0; b < r.tagHits.Length; b++)
                        sb.Append($"{r.slotTagHits[slot, b] / (float)r.perSlot[slot] * 100f,7:0.0}%");
                    EditorGUILayout.LabelField($"slot {slot}", sb.ToString(), EditorStyles.miniLabel);
                }
            }

            foreach (var m in _diagnosis)
                EditorGUILayout.HelpBox(m, MessageType.Warning);

            if (_diagnosis.Count == 0)
                EditorGUILayout.HelpBox("합격 — 모든 칸에 충분한 궤적이 있고 연출 지표도 정상입니다.", MessageType.Info);
        }

        private void DrawDistribution()
        {
            if (_table == null || _table.pools == null || _table.pools.Length == 0) return;

            EditorGUILayout.LabelField("Baked Distribution", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "natural = 판이 자연히 만드는 비율 / declared = 실제 지급 확률. 둘은 달라도 된다.",
                    EditorStyles.miniLabel);

                if (!_table.HasSampleCounts)
                {
                    EditorGUILayout.HelpBox(
                        "이 테이블은 자연 분포 정보가 없는 구버전입니다. 다시 Bake 하면 정확한 nat 값이 표시됩니다.",
                        MessageType.Info);
                }

                for (int i = 0; i < _table.SlotCount; i++)
                {
                    var pool = _table.pools[i];
                    float nat = _table.NaturalRatio(i) * 100f;
                    float dec = (_board.declaredProbability != null && i < _board.declaredProbability.Length)
                        ? _board.declaredProbability[i] * 100f : 0f;

                    // pool 은 Keep Per Slot 으로 잘린 개수, sampled 는 자르기 전 개수
                    string pools = pool.sampled > pool.Count
                        ? $"{pool.Count:N0} / {pool.sampled:N0}"
                        : $"{pool.Count:N0}";

                    string warn = pool.Count == 0 ? "  ← 비어있음!" : "";
                    EditorGUILayout.LabelField($"slot {i}",
                        $"pool {pools}   nat {nat:0.0}%   dec {dec:0.0}%{warn}");
                }

                DrawBakedSpecialCoverage();

                if (_board.declaredProbability != null)
                {
                    float sum = 0f;
                    foreach (var p in _board.declaredProbability) sum += p;
                    if (Mathf.Abs(sum - 1f) > 0.001f)
                        EditorGUILayout.HelpBox($"declaredProbability 합이 {sum:0.###} 입니다. 1.0 이어야 합니다.",
                            MessageType.Warning);
                }
            }
        }


        /// <summary>
        /// 구워진 테이블 기준으로, 태그별 목표 확률이 실제로 달성되는지 보여준다.
        /// Quick Test 는 자르기 전 통계라 런타임이 쓰는 것과 다르므로 여기서 따로 계산한다.
        /// </summary>
        private void DrawBakedSpecialCoverage()
        {
            var tags = _board.SpecialTags();
            if (tags.Count == 0) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("특수 핀 목표 달성 검증", EditorStyles.miniBoldLabel);

            if (_board.specialHitPolicy != SpecialHitPolicy.Declared)
            {
                EditorGUILayout.LabelField(
                    "적중 정책이 Natural 입니다. 목표 확률은 적용되지 않습니다.", EditorStyles.miniLabel);
                return;
            }

            var solver = new SpecialHitSolver(_table, _board);
            float worstReuse = 1f;
            float worstError = 0f;

            for (int b = 0; b < tags.Count; b++)
            {
                float target = -1f;
                foreach (var rule in _board.specialRules)
                    if (rule.tag == tags[b]) { target = rule.targetHitProbability; break; }

                if (target < 0f)
                {
                    EditorGUILayout.LabelField($"태그 {tags[b]}", "규칙 없음 (제어하지 않음)", EditorStyles.miniLabel);
                    continue;
                }

                // 슬롯별 달성치를 지급 확률로 가중 평균한 것이 실제 체감 확률이다
                float achieved = 0f, wsum = 0f;
                for (int slot = 0; slot < _table.SlotCount; slot++)
                {
                    float sw = _board.declaredProbability != null && slot < _board.declaredProbability.Length
                        ? _board.declaredProbability[slot] : 1f / _table.SlotCount;
                    achieved += solver.AchievedProbability(slot, b) * sw;
                    wsum += sw;
                }
                if (wsum > 0f) achieved /= wsum;

                worstError = Mathf.Max(worstError, Mathf.Abs(achieved - target));
                EditorGUILayout.LabelField($"태그 {tags[b]}",
                    $"목표 {target * 100:0.0}%  →  달성 {achieved * 100:0.0}%");
            }

            for (int slot = 0; slot < _table.SlotCount; slot++)
                worstReuse = Mathf.Max(worstReuse, solver.WorstReuseFactor(slot));

            EditorGUILayout.LabelField("최악 재사용 배율", $"{worstReuse:0.0}x");

            if (worstError > 0.05f)
                EditorGUILayout.HelpBox(
                    $"목표와 달성치가 {worstError * 100:0.#}%p 어긋납니다.\n" +
                    "그 태그를 맞는 궤적이 부족하거나 반대로 너무 흔합니다. " +
                    "핀 크기·위치를 조정하거나 목표를 현실적인 값으로 낮추세요.",
                    MessageType.Warning);
            else if (worstReuse > 4f)
                EditorGUILayout.HelpBox(
                    $"일부 궤적이 자연 빈도의 {worstReuse:0.0}배로 재사용됩니다. 유저가 반복을 알아챌 수 있습니다.\n" +
                    "Sample Count 를 늘리거나 목표를 자연 적중률에 가깝게 조정하세요.",
                    MessageType.Warning);
            else
                EditorGUILayout.HelpBox("목표 확률이 정상적으로 달성됩니다.", MessageType.Info);
        }

        // ──────────────────────────────────────────────── Preview 탭

        private void DrawPreviewPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _manualSeed = (uint)Mathf.Max(1, EditorGUILayout.IntField("Seed", (int)_manualSeed));
                    if (GUILayout.Button("Run", GUILayout.Width(52))) RunSeed(_manualSeed);
                    if (GUILayout.Button("Rnd", GUILayout.Width(46)))
                        RunSeed((uint)Random.Range(1, Mathf.Max(2, _sampleCount)));
                }

                if (_table != null && _table.pools != null && _table.pools.Length > 0)
                {
                    var names = new string[_table.SlotCount];
                    for (int i = 0; i < names.Length; i++) names[i] = $"slot {i} ({_table.pools[i].Count})";
                    _selectedSlot = EditorGUILayout.Popup("From Pool", _selectedSlot, names);

                    var pool = _table.pools[Mathf.Clamp(_selectedSlot, 0, _table.SlotCount - 1)];
                    using (new EditorGUI.DisabledScope(pool.Count == 0))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Best")) { _seedIndex = 0; RunSeed(pool.seeds[0]); }
                        if (GUILayout.Button("Next")) { _seedIndex = (_seedIndex + 1) % pool.Count; RunSeed(pool.seeds[_seedIndex]); }
                        if (GUILayout.Button("Random")) { _seedIndex = Random.Range(0, pool.Count); RunSeed(pool.seeds[_seedIndex]); }
                    }

                    if (pool.Count > 0)
                        EditorGUILayout.LabelField("Pool Index",
                            $"{_seedIndex} / {pool.Count - 1}   fun {pool.fun[Mathf.Clamp(_seedIndex, 0, pool.Count - 1)]:0.000}");
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Overlay 30 Random Paths"))
                    BuildGhosts(30);
                _showAllPaths = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show Overlay", "여러 궤적을 겹쳐 보면 핀 배치가 구슬을 어디로 몰고 있는지 한눈에 보인다."),
                    _showAllPaths);

                if (_path.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Result", _last.Valid
                        ? $"slot {_last.slot}  ·  {_last.steps}step  ·  peg {_last.pegHits}  ·  wall {_last.wallHits}  ·  switch {_last.slotSwitches}  ·  fun {_last.fun:0.000}"
                        : $"무효 ({_last.steps}step) — 끼임 또는 슬롯 밖 착지");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _scrub = EditorGUILayout.IntSlider(Mathf.Clamp(_scrub, 0, _path.Count - 1), 0, _path.Count - 1);
                        if (GUILayout.Button(_animate ? "Stop" : "Play", GUILayout.Width(56)))
                        {
                            _animate = !_animate;
                            _animStart = EditorApplication.timeSinceStartup;
                        }
                    }
                }
            }
        }

        private void RunSeed(uint seed)
        {
            if (Sim == null) return;
            _manualSeed = seed;
            _path.Clear();
            _last = Sim.Run(seed, _path);
            _scrub = _path.Count - 1;
            _animate = false;
            Repaint();
        }

        private void BuildGhosts(int count)
        {
            if (Sim == null) return;
            _ghostPaths.Clear();
            for (int i = 0; i < count; i++)
            {
                var p = new List<Vector2>(1024);
                Sim.Run((uint)Random.Range(1, 500000), p);
                _ghostPaths.Add(p);
            }
            _showAllPaths = true;
            Repaint();
        }
    }
}
