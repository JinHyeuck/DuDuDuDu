using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJ.UI
{
    /// <summary>
    /// 노치·카메라 구멍·홈 막대에 UI 가 가리지 않게 한다(세이프 에어리어).
    ///
    /// <b>방식.</b> 씬의 UI 는 거의 전부 점 앵커(위·아래·가운데)라서, 캔버스를 통째로 줄이는 대신
    /// <b>위에 붙은 요소는 위 여백만큼 내리고, 아래에 붙은 요소는 아래 여백만큼 올린다.</b>
    /// 화면 전체를 덮는 그림(그라데이션·배경)은 건드리지 않는다 — 노치 뒤까지 깔려야 보기 좋다.
    /// 위·아래로 늘어나는 상자(로비 레이아웃)는 그 안으로 들어가 자식에게 같은 규칙을 적용한다.
    ///
    /// <b>두 번 불러도 안전하다.</b> 옮긴 요소는 기억해 두었다가 다시 옮기지 않는다.
    /// 나중에 코드로 만든 요소(웨이브 현황 등)는 만든 뒤 <see cref="Apply"/> 를 한 번 더 부른다.
    /// 여백이 0 인 화면(에디터, 노치 없는 폰)에서는 아무것도 달라지지 않는다.
    /// </summary>
    public static class SafeAreaFitter
    {
        /// <summary>이미 옮긴 요소. 두 번 옮기지 않으려는 표식이다(씬이 바뀌어 사라진 요소는 훑을 때 치운다).</summary>
        private static readonly HashSet<RectTransform> shifted = new HashSet<RectTransform>();

        /// <summary>시험용: 비어 있지 않으면 <see cref="Screen.safeArea"/> 대신 이 값을 쓴다(픽셀).</summary>
        public static Rect? OverrideSafeArea;

        /// <summary>전체 화면 폭의 이 배수를 넘는 요소는 배경으로 보고 옮기지 않는다.</summary>
        private const float BackgroundWidthFactor = 1.2f;

        private const float Edge = 0.01f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var host = new GameObject("SafeAreaRunner");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<SafeAreaRunner>().Run(scene);
        }

        /// <summary>씬 안의 루트 캔버스를 전부 훑는다. <see cref="SafeAreaRunner"/> 가 부른다.</summary>
        internal static void ApplyScene(Scene scene)
        {
            shifted.RemoveWhere(r => r == null);

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Canvas[] canvases = roots[i].GetComponentsInChildren<Canvas>(true);
                for (int c = 0; c < canvases.Length; c++)
                {
                    if (canvases[c].isRootCanvas)
                        Apply(canvases[c]);
                }
            }
        }

        /// <summary>새로 연 창(팝업 캔버스 아래)의 요소를 같은 규칙으로 옮긴다. 창 루트는 화면을 채우는 상자여야 한다.</summary>
        public static void ApplyTree(RectTransform dialogRoot)
        {
            if (dialogRoot == null)
                return;

            Canvas canvas = dialogRoot.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            canvas = canvas.rootCanvas;
            GetInsets(canvas, out float top, out float bottom);
            if (top <= 0.5f && bottom <= 0.5f)
                return;

            float width = (canvas.transform as RectTransform).rect.width;
            Walk(dialogRoot, top, bottom, width, 0);
        }

        /// <summary>위·아래 여백을 캔버스 단위로 돌려준다.</summary>
        public static void GetInsets(Canvas canvas, out float top, out float bottom)
        {
            top = bottom = 0f;
            if (canvas == null || canvas.scaleFactor <= 0f)
                return;

            Rect safe = OverrideSafeArea ?? Screen.safeArea;
            top = Mathf.Max(0f, Screen.height - safe.yMax) / canvas.scaleFactor;
            bottom = Mathf.Max(0f, safe.yMin) / canvas.scaleFactor;
        }

        /// <summary>한 캔버스의 직계 요소를 훑어 위·아래 여백만큼 옮긴다.</summary>
        public static void Apply(Canvas canvas)
        {
            if (canvas == null)
                return;

            canvas = canvas.rootCanvas;
            GetInsets(canvas, out float top, out float bottom);
            if (top <= 0.5f && bottom <= 0.5f)
                return;

            var root = canvas.transform as RectTransform;
            if (root != null)
                Walk(root, top, bottom, root.rect.width, 0);
        }

        /// <summary>코드로 만든 요소 하나(위나 아래에 붙은)를 바로 옮긴다.</summary>
        public static void Apply(RectTransform rect)
        {
            if (rect == null)
                return;

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            canvas = canvas.rootCanvas;
            GetInsets(canvas, out float top, out float bottom);
            Shift(rect, top, bottom, (canvas.transform as RectTransform).rect.width);
        }

        private static void Walk(RectTransform parent, float top, float bottom, float canvasWidth, int depth)
        {
            if (depth > 4)
                return;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i) as RectTransform;
                if (child == null)
                    continue;

                if (Shift(child, top, bottom, canvasWidth))
                    continue;

                // 위아래로 늘어난 상자는 자기가 옮겨지지 않고 안의 요소가 각자 붙은 쪽으로 옮겨진다.
                if (child.anchorMin.y <= Edge && child.anchorMax.y >= 1f - Edge)
                    Walk(child, top, bottom, canvasWidth, depth + 1);
            }
        }

        /// <summary>위나 아래에 점으로 붙은 요소면 옮기고 true. 이미 옮긴 요소도 true(더는 들어가지 않는다).</summary>
        private static bool Shift(RectTransform rect, float top, float bottom, float canvasWidth)
        {
            if (shifted.Contains(rect))
                return true;

            float min = rect.anchorMin.y;
            float max = rect.anchorMax.y;

            float delta;
            if (min >= 1f - Edge && max >= 1f - Edge)
                delta = -top;
            else if (min <= Edge && max <= Edge)
                delta = bottom;
            else
                return false;

            // 화면을 덮는 배경은 노치 뒤까지 깔려야 한다.
            if (rect.rect.width > canvasWidth * BackgroundWidthFactor)
                return false;

            if (Mathf.Abs(delta) < 0.5f)
                return false;

            rect.anchoredPosition += new Vector2(0f, delta);
            shifted.Add(rect);
            return true;
        }
    }
}
