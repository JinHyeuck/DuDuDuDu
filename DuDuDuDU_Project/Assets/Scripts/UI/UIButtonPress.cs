using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OJ.UI
{
    /// <summary>
    /// 버튼을 누를 때 눌리고 뗄 때 튕겨 돌아온다.
    ///
    /// <b>왜 컴포넌트인가.</b> 버튼마다 <c>onClick</c> 에 연출을 다는 방법도 있지만, 그러면
    /// <b>누른 순간이 아니라 뗀 순간에</b> 반응하고 — 손가락을 올린 채 끌어서 취소해도
    /// 눌린 티가 안 난다. 포인터 이벤트를 직접 받아야 "지금 이걸 누르고 있다" 가 보인다.
    ///
    /// <b>붙이는 일은 <see cref="DialogBase"/> 가 한다.</b> 창이 처음 만들어질 때 자식의
    /// 버튼을 전부 훑어 없으면 붙인다. 프리팹 29개를 손으로 고치는 것보다 빠뜨릴 구석이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>누르고 있을 때의 배율.</summary>
        private const float PressedScale = 0.93f;

        /// <summary>눌리는 시간(초). <b>떼는 것보다 짧다</b> — 눌림은 즉각 반응해야 한다.</summary>
        private const float PressDuration = 0.05f;

        /// <summary>돌아오는 시간(초). 여기서만 오버슈트가 붙어 "팝" 이 난다.</summary>
        private const float ReleaseDuration = 0.16f;

        private Transform target;
        private Vector3 baseScale = Vector3.one;
        private int sequence;

        /// <summary>
        /// 눌린 채로 비활성화되면 작아진 상태로 굳는다. 켜질 때 되돌린다.
        /// (목록이 스크롤로 껐다 켜지는 화면에서 실제로 밟는 경로다.)
        /// </summary>
        private void OnEnable()
        {
            sequence++;
            if (target != null)
                target.localScale = baseScale;
        }

        private void Awake()
        {
            target = transform;
            baseScale = target.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable())
                return;

            AnimateTo(PressedScale, PressDuration, useOvershoot: false).Forget();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // 비활성 검사를 하지 않는다. 누른 뒤 버튼이 꺼지는 경우(구매 성공 등)가 흔한데,
            // 거기서 되돌리지 않으면 <b>눌린 채로 굳는다.</b>
            AnimateTo(1f, ReleaseDuration, useOvershoot: true).Forget();
        }

        private bool IsInteractable()
        {
            var button = GetComponent<Selectable>();
            return button == null || button.IsInteractable();
        }

        private async UniTaskVoid AnimateTo(float to, float duration, bool useOvershoot)
        {
            if (target == null)
                return;

            int mine = ++sequence;

            Vector3 from = target.localScale;
            Vector3 goal = baseScale * to;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                // 파괴됐거나, 다시 눌렸거나, 꺼졌다 켜졌다.
                if (this == null || target == null || mine != sequence)
                    return;

                // 일시정지(timeScale 0)에서도 눌림이 보여야 한다. 전투 중 팝업이 그런 상태다.
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float eased = useOvershoot ? UIEase.OutBack(t) : UIEase.OutQuad(t);

                // LerpUnclamped 여야 오버슈트가 살아난다.
                target.localScale = Vector3.LerpUnclamped(from, goal, eased);
            }

            if (this == null || target == null || mine != sequence)
                return;

            target.localScale = goal;
        }

        /// <summary>
        /// 씬에 <b>직접 놓인</b> 버튼에 붙인다.
        ///
        /// <b>왜 따로 필요한가.</b> <see cref="AttachToChildren"/> 은 <c>DialogBase</c> 가
        /// 자기 자식을 훑는 것이라, 창 밖에 있는 버튼 — 로비 하단 탭과 입구 버튼들 — 에는
        /// 닿지 않는다. 하필 그것들이 <b>가장 많이 눌리는 버튼</b>이다.
        ///
        /// 씬의 모든 <c>Awake</c> 가 끝난 뒤에 불러야 한다(<c>Start</c> 시점).
        /// </summary>
        internal static void AttachToScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                AttachToChildren(roots[i]);
        }

        /// <summary>
        /// 자식의 모든 버튼에 이 컴포넌트를 붙인다. 이미 있으면 건너뛴다.
        /// <c>DialogBase</c> 가 창을 처음 만들 때 한 번 부른다.
        /// </summary>
        internal static void AttachToChildren(GameObject root)
        {
            if (root == null)
                return;

            // 꺼져 있는 버튼도 포함한다. 탭으로 켜지는 것들이 대부분 꺼진 채로 만들어진다.
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.GetComponent<UIButtonPress>() != null)
                    continue;

                if (HandlesPointerItself(button))
                    continue;

                button.gameObject.AddComponent<UIButtonPress>();
            }
        }

        /// <summary>
        /// 그 오브젝트가 포인터를 <b>직접</b> 다루고 있는가.
        ///
        /// <b>붙이지 말아야 할 곳이 있다.</b> 롱프레스처럼 자기 판정을 가진 컴포넌트
        /// (<c>UIGemInventoryItem</c> 이 그렇다)와 섞이면 같은 누름에 두 반응이 걸리고,
        /// 어느 쪽이 먼저인지가 코드에 드러나지 않는다. 연출 하나 넣자고 입력을
        /// 헷갈리게 만들 이유는 없으니 그런 버튼은 조용히 건너뛴다.
        ///
        /// <c>Button</c> 자신도 <c>IPointerDownHandler</c> 라서 그것과 이 컴포넌트는 센다.
        /// </summary>
        private static bool HandlesPointerItself(Button button)
        {
            var handlers = button.GetComponents<IPointerDownHandler>();
            for (int i = 0; i < handlers.Length; i++)
            {
                if (handlers[i] is Button || handlers[i] is UIButtonPress)
                    continue;

                return true;
            }

            return false;
        }
    }
}
