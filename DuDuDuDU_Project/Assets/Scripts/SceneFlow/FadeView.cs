using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Point;

namespace OJ.SceneFlow
{
    /// <summary>
    /// 화면 전체를 덮는 페이드. (MIGRATION_BASELINE 9.1)
    ///
    /// <b>왜 씬이 아니라 코드로 만드나.</b> 씬마다 페이드 오브젝트를 두면 <b>씬이 바뀌는
    /// 바로 그 순간</b> 페이드가 같이 사라진다 — 가장 필요한 시점에 없는 것이다.
    /// 그래서 <c>DontDestroyOnLoad</c> 로 하나만 두고 씬을 넘어 산다.
    /// 씬 편집 없이 코드로 만드는 것은 <see cref="GameContainer"/> 와 같은 이유이기도 하다.
    ///
    /// <b>정렬 순서를 최대에 가깝게 둔다.</b> 페이드가 UI 아래로 깔리면 전환 중에
    /// 다이얼로그가 비쳐 보인다. 다만 개발용 치트 오버레이(<c>PointCheatController</c>)가
    /// <c>short.MaxValue - 1</c> 을 쓰므로 그보다 낮게 잡아 치트를 가리지 않는다.
    /// </summary>
    public sealed class FadeView : MonoBehaviour
    {
        private const int SortingOrder = short.MaxValue - 100;

        private CanvasGroup group;

        /// <summary>지금 화면을 덮고 있는가. 전환 게이트가 이걸 본다.</summary>
        public bool IsCovering => group != null && group.alpha > 0f;

        public static FadeView Create()
        {
            var go = new GameObject(nameof(FadeView));
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var view = go.AddComponent<FadeView>();
            view.group = go.AddComponent<CanvasGroup>();
            view.group.alpha = 0f;

            // 투명할 때 클릭을 먹으면 안 된다. 별의 시련 버그가 정확히 그런 종류였다 —
            // 보이지 않는 Graphic 이 클릭을 가로채고 있었다.
            view.group.blocksRaycasts = false;

            var image = new GameObject("Blocker").AddComponent<Image>();
            image.transform.SetParent(go.transform, false);
            image.color = Color.black;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return view;
        }

        /// <summary>
        /// <paramref name="to"/> 까지 서서히 바꾼다.
        ///
        /// <c>Time.timeScale</c> 이 0 이어도 돌아야 하므로 <c>unscaledDeltaTime</c> 을 쓴다 —
        /// 전투 중 일시정지 상태에서 로비로 나가는 경로가 실제로 있다.
        /// </summary>
        public IEnumerator FadeTo(float to, float duration)
        {
            if (group == null)
                yield break;

            // 덮기 시작하는 순간부터 클릭을 막는다. 전환 중 입력이 통과하면
            // 사라질 화면의 버튼을 누르게 된다.
            group.blocksRaycasts = to > 0f;

            float from = group.alpha;
            if (duration <= 0f)
            {
                Apply(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Apply(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            Apply(to);
        }

        private void Apply(float alpha)
        {
            group.alpha = Mathf.Clamp01(alpha);
            group.blocksRaycasts = group.alpha > 0f;
        }
    }
}
