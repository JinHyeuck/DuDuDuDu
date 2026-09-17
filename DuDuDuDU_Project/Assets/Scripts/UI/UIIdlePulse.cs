using UnityEngine;

namespace OJ.UI
{
    /// <summary>
    /// 켜져 있는 동안 계속 맥동한다. <b>화면이 멎어 보이지 않게 하는 장치다.</b>
    ///
    /// <b>아무 데나 붙이지 않는다.</b> 전부 움직이면 어디를 봐야 할지 알 수 없어 산만해질
    /// 뿐이다. <b>지금 누를 수 있는 것</b>에만 붙여, 움직임이 곧 안내가 되게 한다 —
    /// 수령 가능한 보상 버튼, 새로 열린 항목 같은 것들이다.
    ///
    /// <c>Update</c> 로 돈다. 사인파는 시작도 끝도 없어서 시퀀스로 관리할 것이 없고,
    /// 매 프레임 값 하나를 쓰는 것이 가장 짧다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIIdlePulse : MonoBehaviour
    {
        [Tooltip("한 번 부풀었다 돌아오는 데 걸리는 시간(초).")]
        [SerializeField] private float period = 1.1f;

        [Tooltip("얼마나 커지는가. 0.06 이면 최대 6% 커진다.")]
        [SerializeField] private float amplitude = 0.06f;

        private Vector3 baseScale = Vector3.one;
        private bool captured;

        /// <summary>
        /// 시작 위상. 같은 화면에 여럿 붙으면 <b>다 같이 숨쉬어서</b> 화면이 통째로
        /// 펄떡이는데, 그건 강조가 아니라 소음이다. 오브젝트마다 위상을 흩어 둔다.
        /// </summary>
        private float phase;

        private void Awake()
        {
            baseScale = transform.localScale;
            captured = true;

            // 인스턴스 id 로 흩는다 — 난수와 달리 같은 오브젝트가 항상 같은 위상을 갖는다.
            phase = Mathf.Repeat(GetInstanceID() * 0.618f, 1f);
        }

        private void OnDisable()
        {
            // 부푼 채로 꺼지면 그 크기로 굳는다.
            if (captured)
                transform.localScale = baseScale;
        }

        private void Update()
        {
            if (period <= 0f)
                return;

            // 일시정지(timeScale 0)에서도 계속 뛴다. 전투 중 팝업이 그런 상태다.
            float t = Mathf.Repeat(Time.unscaledTime / period + phase, 1f);

            // 0 → 1 → 0. Punch 가 아니라 사인이어야 끊김 없이 이어진다.
            float wave = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f);

            transform.localScale = baseScale * (1f + wave * amplitude);
        }

        /// <summary>
        /// 켜고 끈다. 끄면 원래 크기로 돌아간다.
        /// <b>오브젝트를 끄는 것과 다르다</b> — 버튼은 계속 보여야 하고 맥동만 멎어야 한다.
        /// </summary>
        public void SetPulsing(bool on)
        {
            if (enabled == on)
                return;

            enabled = on;

            if (!on && captured)
                transform.localScale = baseScale;
        }

        /// <summary>
        /// 대상에 맥동을 붙이고 켜거나 끈다. 없으면 만든다.
        /// 화면이 "지금 누를 수 있는가" 에 따라 부르면 된다.
        /// </summary>
        public static void Apply(Component target, bool on)
        {
            if (target == null)
                return;

            var pulse = target.GetComponent<UIIdlePulse>();
            if (pulse == null)
            {
                if (!on)
                    return;     // 끌 거면 굳이 만들 필요가 없다

                pulse = target.gameObject.AddComponent<UIIdlePulse>();
            }

            pulse.SetPulsing(on);
        }
    }
}
