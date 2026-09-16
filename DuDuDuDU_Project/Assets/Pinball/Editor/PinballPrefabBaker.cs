using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Pinball.EditorTools
{
    /// <summary>
    /// 에디터에서 배치한 판을 UI 캔버스용 프리팹으로 굽는다.
    ///
    /// 좌표 규약: 모든 자식은 anchor (0,0) + pivot (0.5,0.5).
    /// 그래서 anchoredPosition 이 곧 "판 좌하단으로부터의 픽셀 좌표"가 되고,
    /// 보드 좌표 -> UI 좌표 변환이 곱셈 하나(pixelsPerUnit)로 끝난다.
    /// </summary>
    public static class PinballPrefabBaker
    {
        private const string CircleSprite = "UI/Skin/Knob.psd";
        private const string RectSprite = "UI/Skin/UISprite.psd";
        private const string BgSprite = "UI/Skin/Background.psd";

        public static GameObject Bake(PinballBoard board, string assetPath)
        {
            var s = board.prefabSettings;
            float ppu = Mathf.Max(1f, s.pixelsPerUnit);

            var circle = s.pegSprite != null ? s.pegSprite : Builtin(CircleSprite);
            var bar = s.wallSprite != null ? s.wallSprite : Builtin(RectSprite);
            var ballSprite = s.ballSprite != null ? s.ballSprite : Builtin(CircleSprite);
            var slotSprite = s.slotSprite != null ? s.slotSprite : Builtin(RectSprite);
            var bgSprite = s.backgroundSprite != null ? s.backgroundSprite : Builtin(BgSprite);

            // ── 루트
            var rootGo = new GameObject(board.name + "_View", typeof(RectTransform));
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = board.size * ppu;

            var view = rootGo.AddComponent<PinballBoardView>();
            view.board = board;
            view.pixelsPerUnit = ppu;
            view.pegPunchEnabled = s.pegPunchEnabled;
            view.pegPunchScale = s.pegPunchScale;
            view.pegPunchDuration = s.pegPunchDuration;

            // ── 배경
            if (s.createBackground)
            {
                var bg = NewRect("Background", root);
                bg.anchorMin = Vector2.zero;
                bg.anchorMax = Vector2.one;
                bg.pivot = new Vector2(0.5f, 0.5f);
                bg.offsetMin = Vector2.zero;
                bg.offsetMax = Vector2.zero;
                AddImage(bg, bgSprite, s.backgroundColor, Image.Type.Sliced);
                bg.SetAsFirstSibling();
            }

            // ── 보상 칸 (배경보다 위, 핀보다 아래)
            var slots = new List<RectTransform>();
            if (s.createSlotAreas)
            {
                var slotGroup = NewRect("Slots", root);
                int n = board.SlotCount;

                for (int i = 0; i < n; i++)
                {
                    float left = i == 0 ? board.slotMinX : board.dividersX[i - 1];
                    float right = i == n - 1 ? board.slotMaxX : board.dividersX[i];

                    var slot = NewRect($"Slot_{i}", slotGroup);
                    slot.sizeDelta = new Vector2((right - left) * ppu,
                                                 (board.dividerTopY - board.dividerBottomY) * ppu);
                    slot.anchoredPosition = new Vector2(
                        (left + right) * 0.5f * ppu,
                        (board.dividerBottomY + board.dividerTopY) * 0.5f * ppu);

                    AddImage(slot, slotSprite, s.slotColor, Image.Type.Sliced);
                    slots.Add(slot);
                }
            }

            // ── 벽
            var wallGroup = NewRect("Walls", root);
            for (int i = 0; i < board.segments.Length; i++)
                MakeBar(wallGroup, $"Wall_{i}", board.segments[i], ppu, s.wallThicknessPx, bar, s.wallColor);

            // ── 칸막이 (자동 생성분 — 별도 그룹으로 빼서 아트가 구분해 쓸 수 있게)
            var divGroup = NewRect("Dividers", root);
            int d = 0;
            foreach (var seg in board.GenerateDividerSegments())
                MakeBar(divGroup, $"Divider_{d++}", seg, ppu, s.wallThicknessPx, bar, s.dividerColor);

            // ── 핀 (인덱스가 board.pegs 와 1:1 대응해야 한다. 순서를 바꾸지 말 것)
            var pegGroup = NewRect("Pegs", root);
            var pegs = new RectTransform[board.pegs.Length];
            for (int i = 0; i < board.pegs.Length; i++)
            {
                var p = board.pegs[i];
                var rt = NewRect($"Peg_{i}", pegGroup);
                rt.sizeDelta = Vector2.one * (p.radius * 2f * ppu);
                rt.anchoredPosition = p.center * ppu;

                bool special = p.specialTag != 0;
                bool bumper = p.restitution > 0f;
                AddImage(rt, circle,
                    special ? s.specialColor : bumper ? s.bumperColor : s.pegColor,
                    Image.Type.Simple);

                // 특수 핀은 이름으로도 구분되게 해서 아트가 찾기 쉽게
                if (special) rt.name = $"Peg_{i}_Special{p.specialTag}";

                pegs[i] = rt;
            }

            // ── 발사 지점 마커 (이미지 없음. 스프링 아트를 자식으로 붙이면 된다)
            var launch = NewRect("LaunchPoint", root);
            launch.sizeDelta = Vector2.one * (board.ballRadius * 2f * ppu);
            launch.anchoredPosition = board.launchPos * ppu;

            // ── 구슬 (최상단)
            var ball = NewRect("Ball", root);
            ball.sizeDelta = Vector2.one * (board.ballRadius * 2f * ppu);
            ball.anchoredPosition = board.launchPos * ppu;
            AddImage(ball, ballSprite, s.ballColor, Image.Type.Simple);
            ball.SetAsLastSibling();

            view.pegs = pegs;
            view.slots = slots.ToArray();
            view.ball = ball;
            view.launchPoint = launch;

            // ── 저장
            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGo, assetPath, out bool ok);
            Object.DestroyImmediate(rootGo);

            if (!ok)
            {
                Debug.LogError($"[Pinball] 프리팹 저장 실패: {assetPath}");
                return null;
            }

            AssetDatabase.SaveAssets();
            return prefab;
        }

        // ──────────────────────────────────────────────── 헬퍼

        private static Sprite Builtin(string path) =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            // anchor (0,0) + pivot 중앙 => anchoredPosition 이 좌하단 기준 픽셀 좌표가 된다
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.localScale = Vector3.one;
            return rt;
        }

        private static Image AddImage(RectTransform rt, Sprite sprite, Color color, Image.Type type)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = false;   // 판 전체가 터치를 먹지 않도록
            return img;
        }

        /// <summary>선분 하나를 회전된 가로 막대 RectTransform 으로 만든다.</summary>
        private static void MakeBar(Transform parent, string name, Segment seg,
                                    float ppu, float thicknessPx, Sprite sprite, Color color)
        {
            var a = seg.a * ppu;
            var b = seg.b * ppu;
            var delta = b - a;
            float len = delta.magnitude;

            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(len + thicknessPx, thicknessPx);   // 끝을 둥글게 이어주려고 두께만큼 더 길게
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            AddImage(rt, sprite, color, Image.Type.Sliced);
        }
    }
}
