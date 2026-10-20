using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OJ.UI
{
    /// <summary>
    /// 누르는 순간 한 번, 누르고 있는 동안 <see cref="Interval"/> 초마다 다시 <see cref="OnFire"/> 를 부른다.
    ///
    /// <b><c>Button.onClick</c> 과 같이 쓰지 말 것.</b> <c>onClick</c> 은 <b>뗄 때</b> 울리므로, 둘을 함께 걸면 꾹 눌렀다
    /// 뗀 순간 한 발이 더 나간다. 이 컴포넌트를 붙인 버튼은 발사를 전부 여기서 받는다.
    ///
    /// 손가락이 버튼 밖으로 나가거나, 버튼이 잠기면(<see cref="Selectable.IsInteractable"/>) 그 틱은 건너뛴다 —
    /// 잠긴 동안 쏘면 호출부의 "못 쏜다" 로그가 0.2초마다 쌓인다. 배속과 무관하게 unscaled 시간을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIHoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        public float Interval = 0.2f;

        public event Action OnFire;

        private Selectable selectable;
        private bool held;
        private bool inside;
        private float nextFireTime;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            held = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            held = true;
            inside = true;
            nextFireTime = Time.unscaledTime + Interval;
            TryFire();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            held = false;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            inside = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            inside = true;
        }

        private void Update()
        {
            if (!held || !inside || Time.unscaledTime < nextFireTime)
                return;

            nextFireTime = Time.unscaledTime + Interval;
            TryFire();
        }

        private void TryFire()
        {
            if (selectable != null && !selectable.IsInteractable())
                return;

            OnFire?.Invoke();
        }
    }
}
