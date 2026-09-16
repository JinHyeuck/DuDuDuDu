using UnityEditor;
using UnityEngine;

namespace Pinball.EditorTools
{
    public sealed partial class PinballSimWindow
    {
        private SerializedObject _boardSo;

        /// <summary>
        /// 프리팹 설정은 SerializedObject 로 그린다.
        /// 필드 하나하나 수동으로 대입하면 Undo 가 제대로 안 걸리고,
        /// PrefabBakeSettings 에 붙은 [Tooltip] 도 살아나지 않는다.
        /// </summary>
        private SerializedObject BoardSo
        {
            get
            {
                if (_boardSo == null || _boardSo.targetObject != _board)
                    _boardSo = _board != null ? new SerializedObject(_board) : null;
                return _boardSo;
            }
        }

        private void DrawPrefabPanel()
        {
            EditorGUILayout.HelpBox(
                "Edit Board 탭에서 배치한 판을 그대로 UI 캔버스용 프리팹으로 굽습니다.\n" +
                "핀 인덱스가 보존되므로, 시뮬이 기록한 '몇 번 핀에 맞았는지'로 그 핀만 반짝이게 할 수 있습니다.",
                MessageType.Info);

            var so = BoardSo;
            if (so == null) return;

            so.Update();
            var prop = so.FindProperty("prefabSettings");
            if (prop != null)
            {
                prop.isExpanded = true;
                EditorGUILayout.PropertyField(prop, true);
            }
            so.ApplyModifiedProperties();   // Undo 는 여기서 자동 처리된다

            var s = _board.prefabSettings;
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("결과 크기",
                    $"{_board.size.x * s.pixelsPerUnit:0} × {_board.size.y * s.pixelsPerUnit:0} px");
                EditorGUILayout.LabelField("구슬 지름",
                    $"{_board.ballRadius * 2f * s.pixelsPerUnit:0} px");
                EditorGUILayout.LabelField("생성될 오브젝트",
                    $"핀 {_board.pegs.Length} · 벽 {_board.segments.Length} · 칸막이 {_board.dividersX.Length} · 칸 {_board.SlotCount}");
            }

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Bake UI Prefab", GUILayout.Height(32)))
                BakePrefab();

            if (_lastPrefab != null)
            {
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.ObjectField("마지막 결과", _lastPrefab, typeof(GameObject), false);
                    EditorGUILayout.LabelField(
                        "씬의 Canvas 아래로 드래그한 뒤,\nPinballPlayback 의 Board View 에 루트를 연결하세요.",
                        EditorStyles.miniLabel, GUILayout.Height(28));
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "다시 구우면 프리팹이 통째로 교체됩니다. 프리팹에 직접 붙인 아트나 자식 오브젝트는 사라집니다.\n" +
                "스프라이트·색은 위 설정에 넣어두면 재베이크해도 유지됩니다. 그 외 장식은 프리팹을 씬에 올린 뒤 " +
                "Slot_N 아래에 붙이는 쪽이 안전합니다.",
                MessageType.Warning);
        }

        private void BakePrefab()
        {
            string dir = "Assets";
            string existing = AssetDatabase.GetAssetPath(_board);
            if (!string.IsNullOrEmpty(existing)) dir = System.IO.Path.GetDirectoryName(existing);

            string path = EditorUtility.SaveFilePanelInProject(
                "Bake UI Prefab", _board.name + "_View", "prefab",
                "구워진 프리팹을 저장할 위치를 고르세요.", dir);

            if (string.IsNullOrEmpty(path)) return;

            _lastPrefab = PinballPrefabBaker.Bake(_board, path);

            if (_lastPrefab != null)
            {
                EditorGUIUtility.PingObject(_lastPrefab);
                Debug.Log($"[Pinball] 프리팹 생성 완료: {path}\n" +
                          $"핀 {_board.pegs.Length}개 · 벽 {_board.segments.Length}개 · 칸 {_board.SlotCount}개",
                          _lastPrefab);
            }
        }
    }
}
