using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pinball.EditorTools
{
    public sealed partial class PinballSimWindow
    {
        // ──────────────────────────────────────────────── Edit 패널 UI

        private void DrawEditPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel);
                _tool = (EditTool)GUILayout.Toolbar((int)_tool,
                    new[] { "Move", "+ Peg", "+ Wall", "Erase" });

                using (new EditorGUILayout.HorizontalScope())
                {
                    _snap = EditorGUILayout.ToggleLeft("Snap", _snap, GUILayout.Width(60f));
                    using (new EditorGUI.DisabledScope(!_snap))
                        _snapSize = EditorGUILayout.FloatField(_snapSize);
                }

                _newPegRadius = EditorGUILayout.Slider("New Peg Radius", _newPegRadius, 0.05f, 0.45f);

                EditorGUILayout.HelpBox(
                    "Move: 핀·벽 끝점·발사점·칸막이를 드래그. 핀 위에서 휠을 굴리면 크기가 바뀝니다.\n" +
                    "Alt+클릭으로 바로 삭제할 수 있습니다.",
                    MessageType.None);
            }

            EditorGUILayout.Space(4);
            DrawSelectedPeg();
            EditorGUILayout.Space(4);
            DrawSlotEditor();
            EditorGUILayout.Space(4);
            DrawGenerators();
            EditorGUILayout.Space(4);
            DrawTunables();
        }


        /// <summary>Move 도구로 핀을 클릭하면 그 핀의 개별 속성을 여기서 만진다.</summary>
        private void DrawSelectedPeg()
        {
            EditorGUILayout.LabelField("선택된 핀", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_selectedPeg < 0 || _selectedPeg >= _board.pegs.Length)
                {
                    EditorGUILayout.LabelField("Move 도구로 핀을 클릭하면 여기서 편집할 수 있습니다.",
                        EditorStyles.miniLabel);
                    _selectedPeg = -1;
                }
                else
                {
                    var p = _board.pegs[_selectedPeg];
                    EditorGUILayout.LabelField("Index", $"Peg_{_selectedPeg}  (프리팹의 이름과 같음)");

                    EditorGUI.BeginChangeCheck();
                    var center = EditorGUILayout.Vector2Field("Center", p.center);
                    float radius = EditorGUILayout.Slider("Radius", p.radius, 0.03f, 0.6f);
                    float rest = EditorGUILayout.Slider(
                        new GUIContent("Restitution", "0 이면 보드 기본값을 쓴다."), p.restitution, 0f, 1f);
                    int tag = EditorGUILayout.IntField(
                        new GUIContent("Special Tag", "0 이면 평범한 핀. 0 이 아니면 맞을 때마다 카운트된다."),
                        p.specialTag);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_board, "Edit Peg");
                        var pegs = _board.pegs;
                        pegs[_selectedPeg].center = center;
                        pegs[_selectedPeg].radius = radius;
                        pegs[_selectedPeg].restitution = rest;
                        pegs[_selectedPeg].specialTag = tag;
                        _board.pegs = pegs;
                        Dirty();
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(p.specialTag == 0 ? $"특수 핀으로 지정 (tag {_newSpecialTag})" : "특수 지정 해제"))
                        {
                            Undo.RecordObject(_board, "Toggle Special Peg");
                            var pegs = _board.pegs;
                            pegs[_selectedPeg].specialTag = p.specialTag == 0 ? _newSpecialTag : 0;
                            _board.pegs = pegs;
                            Dirty();
                        }
                        _newSpecialTag = EditorGUILayout.IntField(_newSpecialTag, GUILayout.Width(40f));
                    }
                }

                int specialCount = 0;
                foreach (var pg in _board.pegs) if (pg.specialTag != 0) specialCount++;
                EditorGUILayout.LabelField("특수 핀 개수", specialCount.ToString(), EditorStyles.miniLabel);

                if (specialCount > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    var policy = (SpecialHitPolicy)EditorGUILayout.EnumPopup(
                        new GUIContent("적중 정책", "Natural: 물리가 만드는 대로.\nDeclared: 확률표대로 적중 횟수를 지정."),
                        _board.specialHitPolicy);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_board, "Special Hit Policy");
                        _board.specialHitPolicy = policy;
                        EditorUtility.SetDirty(_board);
                        // 정책은 시뮬에 영향이 없으므로 시드 테이블을 무효화하지 않는다
                    }

                    if (_board.specialHitPolicy == SpecialHitPolicy.Declared)
                        DrawSpecialRules();
                }
            }
        }

        /// <summary>태그별 목표 적중 확률. 서로 합이 1이 될 필요가 없다 — 각각 독립 목표값이다.</summary>
        private void DrawSpecialRules()
        {
            var tags = _board.SpecialTags();
            if (tags.Count == 0) return;

            EditorGUILayout.LabelField("태그별 목표 적중 확률", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("'한 번이라도 맞을 확률'입니다. 합이 1일 필요 없습니다.",
                EditorStyles.miniLabel);

            var rules = new List<SpecialPegRule>(_board.specialRules ?? Array.Empty<SpecialPegRule>());

            // 판에 있는 태그와 규칙 목록을 맞춰준다
            bool changed = false;
            for (int i = rules.Count - 1; i >= 0; i--)
                if (!tags.Contains(rules[i].tag)) { rules.RemoveAt(i); changed = true; }

            foreach (var tag in tags)
            {
                bool found = false;
                foreach (var r in rules) if (r.tag == tag) { found = true; break; }
                if (found) continue;

                rules.Add(new SpecialPegRule { tag = tag, label = $"Tag {tag}", targetHitProbability = 0.2f });
                changed = true;
            }

            EditorGUI.BeginChangeCheck();
            var buf = rules.ToArray();
            for (int i = 0; i < buf.Length; i++)
            {
                int count = 0;
                foreach (var pg in _board.pegs) if (pg.specialTag == buf[i].tag) count++;

                buf[i].targetHitProbability = EditorGUILayout.Slider(
                    new GUIContent($"태그 {buf[i].tag} ({count}개)",
                                   "이 태그의 핀에 한 번이라도 맞을 목표 확률"),
                    buf[i].targetHitProbability, 0f, 1f);
            }

            if (changed || EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_board, "Edit Special Rules");
                _board.specialRules = buf;
                EditorUtility.SetDirty(_board);
            }

            EditorGUILayout.HelpBox(
                "목표가 실제로 달성되는지는 Bake 탭 아래의 '특수 핀 목표 달성 검증'에서 확인하세요.",
                MessageType.None);
        }

        private void DrawSlotEditor()
        {
            EditorGUILayout.LabelField("보상 칸", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "칸막이 벽은 경계 좌표에서 자동 생성됩니다. 물리와 판정이 어긋날 수 없습니다.",
                    EditorStyles.miniLabel);

                EditorGUI.BeginChangeCheck();
                float top = EditorGUILayout.FloatField(
                    new GUIContent("Divider Top Y", "높일수록 칸이 깊어져 한번 들어간 구슬이 안 넘어간다.\n" +
                                                    "낮추면 니어미스 연출이 늘어난다."), _board.dividerTopY);
                float bottom = EditorGUILayout.FloatField("Divider Bottom Y", _board.dividerBottomY);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_board, "Edit Dividers");
                    _board.dividerTopY = top;
                    _board.dividerBottomY = bottom;
                    Dirty();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("칸 추가")) AddSlot();
                    using (new EditorGUI.DisabledScope(_board.SlotCount <= 2))
                        if (GUILayout.Button("칸 제거")) RemoveSlot();
                    if (GUILayout.Button("균등 배치")) DistributeDividersEvenly();
                }

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("표기 확률 (합 1.0)", EditorStyles.miniBoldLabel);

                var prob = _board.declaredProbability;
                if (prob == null || prob.Length != _board.SlotCount)
                {
                    if (GUILayout.Button($"확률 배열을 슬롯 수({_board.SlotCount})에 맞추기"))
                        ResizeProbabilities();
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    var buf = new float[prob.Length];
                    float sum = 0f;
                    for (int i = 0; i < prob.Length; i++)
                    {
                        buf[i] = EditorGUILayout.Slider($"slot {i}", prob[i], 0f, 1f);
                        sum += buf[i];
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_board, "Edit Probability");
                        _board.declaredProbability = buf;
                        Dirty();
                    }

                    EditorGUILayout.LabelField("합계", $"{sum:0.###}");
                    if (Mathf.Abs(sum - 1f) > 0.001f && GUILayout.Button("합계 1.0 으로 정규화"))
                        NormalizeProbabilities();
                }
            }
        }

        private void DrawGenerators()
        {
            EditorGUILayout.LabelField("핀 생성기", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("지그재그 격자", EditorStyles.miniBoldLabel);
                _gRows = EditorGUILayout.IntField("Rows", _gRows);
                _gCols = EditorGUILayout.IntField("Cols (짝수행)", _gCols);
                _gTopY = EditorGUILayout.FloatField("Top Y", _gTopY);
                _gRowGap = EditorGUILayout.FloatField("Row Gap", _gRowGap);
                _gColGap = EditorGUILayout.FloatField("Col Gap", _gColGap);
                _gLeftX = EditorGUILayout.FloatField("Left X", _gLeftX);
                _gMaxX = EditorGUILayout.FloatField("Max X", _gMaxX);
                _gRadius = EditorGUILayout.Slider("Radius", _gRadius, 0.05f, 0.35f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("추가")) AddPegs(PinballBoardFactory.StaggeredGrid(
                        _gRows, _gCols, _gTopY, _gRowGap, _gColGap, _gLeftX, _gRadius, _gMaxX));
                    if (GUILayout.Button("교체")) ReplacePegs(PinballBoardFactory.StaggeredGrid(
                        _gRows, _gCols, _gTopY, _gRowGap, _gColGap, _gLeftX, _gRadius, _gMaxX));
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("가로 한 줄", EditorStyles.miniBoldLabel);
                _rowCount = EditorGUILayout.IntField("Count", _rowCount);
                _rowY = EditorGUILayout.FloatField("Y", _rowY);
                _rowFrom = EditorGUILayout.FloatField("From X", _rowFrom);
                _rowTo = EditorGUILayout.FloatField("To X", _rowTo);
                if (GUILayout.Button("줄 추가"))
                    AddPegs(PinballBoardFactory.Row(_rowCount, _rowY, _rowFrom, _rowTo, _gRadius));

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _mirrorAxis = EditorGUILayout.FloatField("Mirror Axis X", _mirrorAxis);
                    if (GUILayout.Button("좌→우 대칭", GUILayout.Width(90f)))
                        AddPegs(PinballBoardFactory.MirrorAcross(_board.pegs, _mirrorAxis));
                }

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("핀 전체 삭제")) ReplacePegs(new List<Peg>());
                    if (GUILayout.Button("기본 판으로 리셋") &&
                        EditorUtility.DisplayDialog("리셋", "현재 레이아웃과 파라미터를 모두 덮어씁니다.", "리셋", "취소"))
                    {
                        Undo.RecordObject(_board, "Reset Board");
                        PinballBoardFactory.Populate(_board);
                        Dirty();
                    }
                }
            }
        }

        /// <summary>가장 자주 만지게 되는 물리 파라미터만 추려서 캔버스 옆에 둔다.</summary>
        private void DrawTunables()
        {
            EditorGUILayout.LabelField("자주 쓰는 파라미터", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();

                float gy = EditorGUILayout.FloatField(
                    new GUIContent("Gravity Y", "↑크게: 빨리 떨어져 연출이 짧아진다. launchSpeed 도 같이 올려야 한다."),
                    _board.gravity.y);
                float drag = EditorGUILayout.Slider(
                    new GUIContent("Linear Drag", "↑올리면 빨리 잠잠해진다. 0 이면 영원히 튀어 전부 폐기된다."),
                    _board.linearDrag, 0f, 1.2f);
                float spd = EditorGUILayout.FloatField(
                    new GUIContent("Launch Speed", "낮으면 레인을 못 벗어나 전 시드가 폐기된다. 폐기율 90%↑ 면 제일 먼저 의심할 값."),
                    _board.launchSpeed);
                float jit = EditorGUILayout.Slider(
                    new GUIContent("Angle Jitter", "궤적 다양성의 핵심. 특정 칸 풀이 비면 제일 먼저 올려본다."),
                    _board.angleJitterDeg, 0f, 12f);
                float sjit = EditorGUILayout.Slider("Speed Jitter", _board.speedJitter, 0f, 4f);
                float pegE = EditorGUILayout.Slider(
                    new GUIContent("Peg Restitution", "통통 튀는 느낌. 0.95↑ 는 구슬이 날뛰어 폐기율이 치솟는다."),
                    _board.pegRestitution, 0f, 1f);
                float wallE = EditorGUILayout.Slider("Wall Restitution", _board.wallRestitution, 0f, 1f);
                float catchY = EditorGUILayout.FloatField(
                    new GUIContent("Catch Line Y", "바닥 정지 높이(바닥 + 구슬 반지름)보다 충분히 위여야 한다."),
                    _board.catchLineY);
                float armY = EditorGUILayout.FloatField(
                    new GUIContent("Arm Height Y", "이 높이를 넘어야 착지 판정이 켜진다. 발사 직후 오판정을 막는다."),
                    _board.armHeightY);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_board, "Tune Board");
                    _board.gravity = new Vector2(_board.gravity.x, gy);
                    _board.linearDrag = drag;
                    _board.launchSpeed = spd;
                    _board.angleJitterDeg = jit;
                    _board.speedJitter = sjit;
                    _board.pegRestitution = pegE;
                    _board.wallRestitution = wallE;
                    _board.catchLineY = catchY;
                    _board.armHeightY = armY;
                    Dirty();
                }

                EditorGUILayout.LabelField(
                    $"구슬 바닥 정지 높이 ≈ {_board.dividerBottomY + _board.ballRadius:0.00}", EditorStyles.miniLabel);
                if (_board.catchLineY <= _board.dividerBottomY + _board.ballRadius)
                    EditorGUILayout.HelpBox(
                        "Catch Line 이 구슬 정지 높이보다 낮거나 같습니다. 착지 판정이 영영 안 납니다.",
                        MessageType.Error);
            }
        }

        // ──────────────────────────────────────────────── 마우스 처리

        private void HandleEditInput(Rect area)
        {
            var e = Event.current;
            bool dragging = _dragPeg >= 0 || _dragWall >= 0 || _dragDivider >= 0 || _dragLaunch;

            if (!area.Contains(e.mousePosition) && !dragging) return;

            var bp = ToBoard(e.mousePosition);

            // 호버 표시
            if (e.type == EventType.MouseMove || e.type == EventType.Repaint)
            {
                int h = HitPeg(bp);
                if (h != _hoverPeg) { _hoverPeg = h; Repaint(); }
            }

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    OnMouseDown(bp, e);
                    break;

                case EventType.MouseDrag when e.button == 0 && dragging:
                    OnMouseDrag(bp, e);
                    break;

                case EventType.MouseUp when e.button == 0 && dragging:
                    _dragPeg = _dragWall = _dragDivider = -1;
                    _dragLaunch = false;
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    OnScroll(bp, e);
                    break;

                case EventType.KeyDown when e.keyCode == KeyCode.Escape && _wallStart.HasValue:
                    _wallStart = null;
                    e.Use();
                    Repaint();
                    break;
            }
        }

        private void OnMouseDown(Vector2 bp, Event e)
        {
            // Alt+클릭은 어느 도구에서든 삭제
            if (e.alt) { EraseAt(bp); e.Use(); return; }

            switch (_tool)
            {
                case EditTool.Move:
                    if (BeginDrag(bp)) e.Use();
                    break;

                case EditTool.AddPeg:
                    Undo.RecordObject(_board, "Add Peg");
                    var list = new List<Peg>(_board.pegs)
                    {
                        new Peg { center = Snap(bp), radius = _newPegRadius }
                    };
                    _board.pegs = list.ToArray();
                    Dirty();
                    e.Use();
                    break;

                case EditTool.AddWall:
                    if (!_wallStart.HasValue)
                    {
                        _wallStart = Snap(bp);
                    }
                    else
                    {
                        Undo.RecordObject(_board, "Add Wall");
                        var ws = new List<Segment>(_board.segments)
                        {
                            new Segment { a = _wallStart.Value, b = Snap(bp) }
                        };
                        _board.segments = ws.ToArray();
                        _wallStart = null;
                        Dirty();
                    }
                    e.Use();
                    break;

                case EditTool.Erase:
                    EraseAt(bp);
                    e.Use();
                    break;
            }
        }

        private bool BeginDrag(Vector2 bp)
        {
            // 우선순위: 핀 > 벽 끝점 > 칸막이 > 발사점
            int peg = HitPeg(bp);
            if (peg >= 0) { _dragPeg = peg; _selectedPeg = peg; return true; }

            for (int i = 0; i < _board.segments.Length; i++)
            {
                if (Vector2.Distance(bp, _board.segments[i].a) < Tol()) { _dragWall = i; _dragWallIsB = false; return true; }
                if (Vector2.Distance(bp, _board.segments[i].b) < Tol()) { _dragWall = i; _dragWallIsB = true; return true; }
            }

            for (int i = 0; i < _board.dividersX.Length; i++)
            {
                var handle = new Vector2(_board.dividersX[i], _board.dividerTopY);
                if (Vector2.Distance(bp, handle) < Tol(12f)) { _dragDivider = i; return true; }
            }

            if (Vector2.Distance(bp, _board.launchPos) < Tol(12f)) { _dragLaunch = true; return true; }

            return false;
        }

        private void OnMouseDrag(Vector2 bp, Event e)
        {
            var p = Snap(bp);
            Undo.RecordObject(_board, "Move Element");

            if (_dragPeg >= 0)
            {
                var pegs = _board.pegs;
                pegs[_dragPeg].center = p;
                _board.pegs = pegs;
            }
            else if (_dragWall >= 0)
            {
                var segs = _board.segments;
                if (_dragWallIsB) segs[_dragWall].b = p; else segs[_dragWall].a = p;
                _board.segments = segs;
            }
            else if (_dragDivider >= 0)
            {
                // 이웃을 넘지 못하게 클램프 -> 정렬이 절대 깨지지 않는다
                float lo = _dragDivider == 0 ? _board.slotMinX + 0.1f : _board.dividersX[_dragDivider - 1] + 0.1f;
                float hi = _dragDivider == _board.dividersX.Length - 1
                    ? _board.slotMaxX - 0.1f
                    : _board.dividersX[_dragDivider + 1] - 0.1f;
                _board.dividersX[_dragDivider] = Mathf.Clamp(p.x, lo, hi);
            }
            else if (_dragLaunch)
            {
                _board.launchPos = p;
            }

            Dirty();
            e.Use();
        }

        private void OnScroll(Vector2 bp, Event e)
        {
            float delta = -e.delta.y * 0.01f;

            if (_tool == EditTool.AddPeg)
            {
                _newPegRadius = Mathf.Clamp(_newPegRadius + delta, 0.05f, 0.45f);
                e.Use();
                Repaint();
                return;
            }

            int i = HitPeg(bp);
            if (i < 0) return;

            Undo.RecordObject(_board, "Resize Peg");
            var pegs = _board.pegs;
            pegs[i].radius = Mathf.Clamp(pegs[i].radius + delta, 0.03f, 0.6f);
            _board.pegs = pegs;
            Dirty();
            e.Use();
        }

        private int HitPeg(Vector2 bp)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < _board.pegs.Length; i++)
            {
                float d = Vector2.Distance(bp, _board.pegs[i].center);
                if (d < _board.pegs[i].radius + Tol(4f) && d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private void EraseAt(Vector2 bp)
        {
            int peg = HitPeg(bp);
            if (peg >= 0)
            {
                Undo.RecordObject(_board, "Erase Peg");
                var list = new List<Peg>(_board.pegs);
                list.RemoveAt(peg);
                _board.pegs = list.ToArray();
                _hoverPeg = -1;
                _selectedPeg = -1;
                Dirty();
                return;
            }

            for (int i = 0; i < _board.segments.Length; i++)
            {
                var c = PinballSimulator.ClosestOnSegment(_board.segments[i].a, _board.segments[i].b, bp);
                if (Vector2.Distance(bp, c) < Tol())
                {
                    Undo.RecordObject(_board, "Erase Wall");
                    var list = new List<Segment>(_board.segments);
                    list.RemoveAt(i);
                    _board.segments = list.ToArray();
                    Dirty();
                    return;
                }
            }
        }

        // ──────────────────────────────────────────────── 편집 핸들 그리기

        private void DrawEditHandles()
        {
            // 벽 끝점
            foreach (var s in _board.segments)
            {
                DrawHandleBox(ToGui(s.a), 3f, new Color(1f, 0.8f, 0.4f, 0.9f));
                DrawHandleBox(ToGui(s.b), 3f, new Color(1f, 0.8f, 0.4f, 0.9f));
            }

            // 칸막이 핸들
            foreach (var d in _board.dividersX)
                DrawHandleBox(ToGui(new Vector2(d, _board.dividerTopY)), 4f, new Color(0.55f, 0.95f, 0.55f));

            // 벽 그리는 중
            if (_wallStart.HasValue)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.7f);
                Handles.DrawAAPolyLine(2f, ToGui(_wallStart.Value), Event.current.mousePosition);
            }
        }

        // ──────────────────────────────────────────────── 편집 헬퍼

        private void Dirty()
        {
            EditorUtility.SetDirty(_board);
            InvalidateBoard();
            Repaint();
        }

        private void AddPegs(List<Peg> pegs)
        {
            Undo.RecordObject(_board, "Add Pegs");
            var list = new List<Peg>(_board.pegs);
            list.AddRange(pegs);
            _board.pegs = list.ToArray();
            Dirty();
        }

        private void ReplacePegs(List<Peg> pegs)
        {
            Undo.RecordObject(_board, "Replace Pegs");
            _board.pegs = pegs.ToArray();
            Dirty();
        }

        private void AddSlot()
        {
            Undo.RecordObject(_board, "Add Slot");

            // 가장 넓은 칸을 반으로 쪼갠다
            var xs = new List<float>(_board.dividersX);
            float bestGap = -1f, bestPos = 0f;
            for (int i = 0; i <= xs.Count; i++)
            {
                float lo = i == 0 ? _board.slotMinX : xs[i - 1];
                float hi = i == xs.Count ? _board.slotMaxX : xs[i];
                if (hi - lo > bestGap) { bestGap = hi - lo; bestPos = (lo + hi) * 0.5f; }
            }
            xs.Add(bestPos);
            xs.Sort();
            _board.dividersX = xs.ToArray();

            ResizeProbabilities();
            Dirty();
        }

        private void RemoveSlot()
        {
            Undo.RecordObject(_board, "Remove Slot");
            var xs = new List<float>(_board.dividersX);
            xs.RemoveAt(xs.Count - 1);
            _board.dividersX = xs.ToArray();
            ResizeProbabilities();
            Dirty();
        }

        private void DistributeDividersEvenly()
        {
            Undo.RecordObject(_board, "Distribute Dividers");
            int n = _board.dividersX.Length;
            float span = _board.slotMaxX - _board.slotMinX;
            for (int i = 0; i < n; i++)
                _board.dividersX[i] = _board.slotMinX + span * (i + 1) / (n + 1);
            Dirty();
        }

        private void ResizeProbabilities()
        {
            int n = _board.SlotCount;
            var old = _board.declaredProbability;
            var next = new float[n];

            for (int i = 0; i < n; i++)
                next[i] = (old != null && i < old.Length) ? old[i] : 0f;

            _board.declaredProbability = next;
            NormalizeProbabilities();
        }

        private void NormalizeProbabilities()
        {
            Undo.RecordObject(_board, "Normalize Probability");
            var p = _board.declaredProbability;
            float sum = 0f;
            foreach (var v in p) sum += v;

            if (sum <= 0.0001f)
                for (int i = 0; i < p.Length; i++) p[i] = 1f / p.Length;
            else
                for (int i = 0; i < p.Length; i++) p[i] /= sum;

            Dirty();
        }
    }
}
