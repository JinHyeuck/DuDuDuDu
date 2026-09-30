using UnityEngine;
using UnityEngine.UI;

namespace OJ.Utils
{
    /// <summary>
    /// uGUI <see cref="Image"/> 의 스프라이트를 일정 간격으로 넘긴다. 횃불 불꽃 같은 반복 장식용.
    ///
    /// <see cref="SimpleSpriteAnimator"/> 는 <c>SpriteRenderer</c> 전용이라 캔버스 위에서는
    /// 쓸 수 없다. 여기에 스케일 흔들기 같은 기능을 덧붙이지 않는다 — 장식 하나를 돌리는
    /// 것 이상이 필요해지면 그때 Animator 를 쓴다.
    ///
    /// <b>실제 시간으로 넘긴다.</b> 이 장식이 뜨는 창은 로비에 있지만, 전투 배속이나
    /// 일시정지 중에 열리는 창에 붙으면 불꽃이 멈추거나 빨라진다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public class UIImageFlipbook : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames = new Sprite[0];
        [SerializeField] private float frameSeconds = 0.12f;
        [SerializeField] private int startFrame;

        private Image image;
        private int frame;
        private float nextFlipTime;

        private void Awake()
        {
            image = GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (frames == null || frames.Length == 0)
                return;

            frame = Mathf.Abs(startFrame) % frames.Length;
            Apply();
            nextFlipTime = Time.unscaledTime + frameSeconds;
        }

        private void Update()
        {
            if (frames == null || frames.Length < 2 || Time.unscaledTime < nextFlipTime)
                return;

            frame = (frame + 1) % frames.Length;
            Apply();
            nextFlipTime = Time.unscaledTime + frameSeconds;
        }

        private void Apply()
        {
            if (image != null)
                image.sprite = frames[frame];
        }

        /// <summary>
        /// 에디터 굽기 전용. 프리팹에 저장될 값을 넣는다.
        /// </summary>
        public void BakeSetup(Sprite[] bakedFrames, float seconds, int firstFrame)
        {
            frames = bakedFrames;
            frameSeconds = Mathf.Max(0.02f, seconds);
            startFrame = firstFrame;
        }
    }
}
