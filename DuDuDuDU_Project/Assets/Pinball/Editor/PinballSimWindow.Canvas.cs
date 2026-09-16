using UnityEditor;
using UnityEngine;

namespace Pinball.EditorTools
{
    public sealed partial class PinballSimWindow
    {
        // 보드 좌표 <-> GUI 좌표 변환에 쓰는 캐시
        private Rect _canvas;
        private float _scale = 1f;
        private Vector2 _originGui;

        private static readonly Color ColBg = new Color(0.12f, 0.11f, 0.18f);
        private static readonly Color ColWall = new Color(0.95f, 0.62f, 0.25f);
        private static readonly Color ColDivider = new Color(0.55f, 0.85f, 0.55f);
        private static readonly Color ColPeg = new Color(0.78f, 0.80f, 0.95f);
        private static readonly Color ColBumper = new Color(1.00f, 0.55f, 0.85f);
        private static readonly Color ColSpecial = new Color(1.00f, 0.85f, 0.25f);
        private static readonly Color ColSelected = new Color(0.40f, 1.00f, 0.40f);
        private static readonly Color ColPath = new Color(0.35f, 0.90f, 1.00f);
        private static readonly Color ColGhost = new Color(0.35f, 0.90f, 1.00f, 0.13f);
        private static readonly Color ColBall = new Color(1.00f, 0.95f, 0.45f);
        private static readonly Color ColLaunch = new Color(1.00f, 0.40f, 0.40f);
        private static readonly Color ColCatch = new Color(0.40f, 1.00f, 0.55f, 0.55f);
        private static readonly Color ColArm = new Color(1.00f, 0.85f, 0.30f, 0.30f);
        private static readonly Color ColHover = new Color(1.00f, 1.00f, 1.00f, 0.95f);

        private Vector2 ToGui(Vector2 p) =>
            new Vector2(_originGui.x + p.x * _scale, _originGui.y + (_board.size.y - p.y) * _scale);

        private Vector2 ToBoard(Vector2 g) =>
            new Vector2((g.x - _originGui.x) / _scale, _board.size.y - (g.y - _originGui.y) / _scale);

        /// <summary>GUI 픽셀 허용오차를 보드 단위로 환산.</summary>
        private float Tol(float pixels = 9f) => pixels / Mathf.Max(0.0001f, _scale);

        private Vector2 Snap(Vector2 p)
        {
            if (!_snap || _snapSize <= 0f) return p;
            return new Vector2(Mathf.Round(p.x / _snapSize) * _snapSize,
                               Mathf.Round(p.y / _snapSize) * _snapSize);
        }

        private void DrawCanvasColumn()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                _canvas = GUILayoutUtility.GetRect(
                    100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                if (_board == null)
                {
                    EditorGUI.DrawRect(_canvas, ColBg);
                    return;
                }

                ComputeTransform(_canvas);

                if (_tab == Tab.Edit) HandleEditInput(_canvas);

                if (Event.current.type == EventType.Repaint) DrawBoard(_canvas);

                DrawCanvasFooter();
            }
        }

        private void ComputeTransform(Rect area)
        {
            var size = _board.size;
            _scale = Mathf.Min(area.width / Mathf.Max(0.01f, size.x), area.height / Mathf.Max(0.01f, size.y));
            _originGui = new Vector2(
                area.x + (area.width - size.x * _scale) * 0.5f,
                area.y + (area.height - size.y * _scale) * 0.5f);
        }

        private void DrawCanvasFooter()
        {
            string hint = _tab switch
            {
                Tab.Edit => _tool switch
                {
                    EditTool.Move => "드래그: 이동  ·  휠: 핀 크기  ·  Alt+클릭: 삭제",
                    EditTool.AddPeg => "클릭: 핀 추가  ·  휠: 새 핀 크기",
                    EditTool.AddWall => _wallStart.HasValue ? "한 번 더 클릭해 벽을 완성  ·  Esc: 취소" : "클릭: 벽 시작점",
                    EditTool.Erase => "클릭: 핀/벽 삭제",
                    _ => ""
                },
                Tab.Preview => "Play 로 재생, 슬라이더로 스크럽",
                _ => "Quick Test 로 판을 점검한 뒤 Bake"
            };

            EditorGUILayout.LabelField(hint, EditorStyles.centeredGreyMiniLabel);
        }

        // ──────────────────────────────────────────────── 그리기

        private void DrawBoard(Rect area)
        {
            EditorGUI.DrawRect(area, ColBg);
            Handles.BeginGUI();

            DrawGrid();
            DrawSlotBands();
            DrawWalls();
            DrawGuideLines();
            DrawPegs();
            DrawLaunch();
            DrawPaths();
            if (_tab == Tab.Edit) DrawEditHandles();

            Handles.EndGUI();
        }

        private void DrawGrid()
        {
            if (!_snap || _snapSize < 0.2f) return;      // 너무 촘촘하면 안 그림
            Handles.color = new Color(1f, 1f, 1f, 0.045f);
            for (float x = 0f; x <= _board.size.x; x += _snapSize)
                Handles.DrawAAPolyLine(1f, ToGui(new Vector2(x, 0f)), ToGui(new Vector2(x, _board.size.y)));
            for (float y = 0f; y <= _board.size.y; y += _snapSize)
                Handles.DrawAAPolyLine(1f, ToGui(new Vector2(0f, y)), ToGui(new Vector2(_board.size.x, y)));
        }

        /// <summary>보상 칸을 번갈아 음영 처리하고, 퀵테스트 결과가 있으면 비율을 얹는다.</summary>
        private void DrawSlotBands()
        {
            float y0 = _board.dividerBottomY, y1 = _board.dividerTopY;
            int n = _board.SlotCount;

            for (int i = 0; i < n; i++)
            {
                float left = i == 0 ? _board.slotMinX : _board.dividersX[i - 1];
                float right = i == n - 1 ? _board.slotMaxX : _board.dividersX[i];

                var tl = ToGui(new Vector2(left, y1));
                var br = ToGui(new Vector2(right, y0));
                var rect = new Rect(tl.x, tl.y, br.x - tl.x, br.y - tl.y);

                EditorGUI.DrawRect(rect, (i & 1) == 0
                    ? new Color(1f, 1f, 1f, 0.05f)
                    : new Color(1f, 1f, 1f, 0.02f));

                string label = $"#{i}";
                if (_probe.HasValue && _probe.Value.Valid > 0)
                    label += $"\n{_probe.Value.perSlot[i] / (float)_probe.Value.Valid * 100f:0.0}%";

                GUI.Label(new Rect(rect.x, rect.yMax + 2f, rect.width, 30f),
                    label, EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawWalls()
        {
            Handles.color = ColWall;
            foreach (var s in _board.segments)
                Handles.DrawAAPolyLine(2.5f, ToGui(s.a), ToGui(s.b));

            // 자동 생성되는 칸막이는 색을 달리해 구분한다 (편집 대상이 아님을 표시)
            Handles.color = ColDivider;
            foreach (var s in _board.GenerateDividerSegments())
                Handles.DrawAAPolyLine(2.5f, ToGui(s.a), ToGui(s.b));
        }

        private void DrawGuideLines()
        {
            // 캐치 라인
            Handles.color = ColCatch;
            Handles.DrawAAPolyLine(1.5f,
                ToGui(new Vector2(_board.slotMinX, _board.catchLineY)),
                ToGui(new Vector2(_board.slotMaxX, _board.catchLineY)));
            GUI.Label(new Rect(ToGui(new Vector2(_board.slotMaxX, _board.catchLineY)).x + 4f,
                               ToGui(new Vector2(0f, _board.catchLineY)).y - 8f, 90f, 16f),
                      "catch", EditorStyles.miniLabel);

            // arm 라인
            Handles.color = ColArm;
            Handles.DrawAAPolyLine(1f,
                ToGui(new Vector2(0f, _board.armHeightY)),
                ToGui(new Vector2(_board.size.x, _board.armHeightY)));
            GUI.Label(new Rect(_canvas.x + 4f, ToGui(new Vector2(0f, _board.armHeightY)).y - 8f, 90f, 16f),
                      "arm", EditorStyles.miniLabel);
        }

        private void DrawPegs()
        {
            var pegs = _board.pegs;
            for (int i = 0; i < pegs.Length; i++)
            {
                bool special = pegs[i].specialTag != 0;
                bool custom = pegs[i].restitution > 0f;

                Handles.color = i == _hoverPeg ? ColHover
                              : special ? ColSpecial
                              : custom ? ColBumper
                              : ColPeg;

                var c = ToGui(pegs[i].center);
                float r = pegs[i].radius * _scale;
                DrawCircle(c, r, i == _hoverPeg ? 2.5f : 1.6f);

                // 특수 핀은 이중 원으로 한눈에 구분되게
                if (special)
                {
                    DrawCircle(c, r * 0.55f, 1.4f);
                    GUI.Label(new Rect(c.x - 12f, c.y - r - 15f, 24f, 14f),
                              pegs[i].specialTag.ToString(), EditorStyles.centeredGreyMiniLabel);
                }

                if (i == _selectedPeg)
                {
                    Handles.color = ColSelected;
                    DrawCircle(c, r + 5f, 2f);
                }
            }
        }

        /// <summary>발사 지점과 각도 지터 범위를 부채꼴로 표시. 지터를 얼마나 줬는지 눈으로 보인다.</summary>
        private void DrawLaunch()
        {
            var o = ToGui(_board.launchPos);
            Handles.color = ColLaunch;
            DrawCircle(o, Mathf.Max(4f, _board.ballRadius * _scale), 2f);

            float len = 1.4f * _scale;
            for (int s = -1; s <= 1; s += 2)
            {
                float a = (_board.launchAngleDeg + s * _board.angleJitterDeg) * Mathf.Deg2Rad;
                Handles.DrawAAPolyLine(1.2f, o,
                    new Vector2(o.x + Mathf.Cos(a) * len, o.y - Mathf.Sin(a) * len));
            }
            float c = _board.launchAngleDeg * Mathf.Deg2Rad;
            Handles.color = new Color(1f, 0.4f, 0.4f, 0.45f);
            Handles.DrawAAPolyLine(1f, o, new Vector2(o.x + Mathf.Cos(c) * len, o.y - Mathf.Sin(c) * len));
        }

        private void DrawPaths()
        {
            if (_showAllPaths)
            {
                Handles.color = ColGhost;
                foreach (var g in _ghostPaths)
                {
                    if (g.Count < 2) continue;
                    var pts = new Vector3[g.Count];
                    for (int i = 0; i < g.Count; i++) pts[i] = ToGui(g[i]);
                    Handles.DrawAAPolyLine(1.5f, pts);
                }
            }

            if (_path.Count < 2) return;

            int upto = Mathf.Clamp(_scrub < 0 ? _path.Count - 1 : _scrub, 1, _path.Count - 1);
            var line = new Vector3[upto + 1];
            for (int i = 0; i <= upto; i++) line[i] = ToGui(_path[i]);

            Handles.color = ColPath;
            Handles.DrawAAPolyLine(2f, line);

            Handles.color = ColBall;
            DrawCircle(ToGui(_path[upto]), _board.ballRadius * _scale, 2f);
        }

        private static void DrawCircle(Vector2 center, float radius, float thickness)
        {
            const int N = 18;
            var pts = new Vector3[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                pts[i] = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, 0f);
            }
            Handles.DrawAAPolyLine(thickness, pts);
        }

        private static void DrawHandleBox(Vector2 c, float half, Color col)
        {
            EditorGUI.DrawRect(new Rect(c.x - half, c.y - half, half * 2f, half * 2f), col);
        }
    }
}
