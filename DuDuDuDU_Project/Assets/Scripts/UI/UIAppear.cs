using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OJ.UI
{
    /// <summary>
    /// 한 칸이 튀어나오는 연출. 목록의 자식들에 시차를 주면 내용이 "채워지는" 것으로 보인다.
    ///
    /// <b>왜 칸마다 컴포넌트인가.</b> 한 곳에서 여러 칸을 돌리면 목록이 다시 채워질 때
    /// 어느 칸이 어디까지 갔는지 추적해야 한다. 칸이 자기 상태를 들면 목록은 "지금 보여 줘"
    /// 라고만 하면 되고, 칸이 사라지면 그 연출도 같이 사라진다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIAppear : MonoBehaviour
    {
        /// <summary>튀어나오기 시작하는 배율.</summary>
        private const float StartScale = 0.82f;

        private const float Duration = 0.22f;

        /// <summary>
        /// <b>위치는 절대 건드리지 않는다.</b>
        ///
        /// 이 칸들은 거의 전부 <c>GridLayoutGroup</c>·<c>VerticalLayoutGroup</c> 아래에 있고,
        /// 레이아웃 그룹은 자식의 <c>anchoredPosition</c> 을 <b>자기가 소유한다</b> — 재배치는
        /// dirty 일 때만 일어나므로, 중간에 끼어들어 좌표를 쓰면 그 값이 그대로 눌러앉아
        /// <b>목록 배치가 통째로 무너진다.</b> 실제로 장비·유물·별 시련·스테이지 보상이
        /// 한꺼번에 깨졌다.
        ///
        /// 그래서 스케일과 알파만 쓴다. 둘 다 레이아웃 계산에 들어가지 않아 안전하고,
        /// 팝 느낌을 내는 데는 그것으로 충분하다.
        /// </summary>
        private CanvasGroup group;

        private int sequence;

        /// <summary>
        /// 배율을 함께 움직일 것인가. 끄면 알파만으로 들어온다.
        ///
        /// <b>선·틀처럼 크기가 정확해야 하는 것에 쓴다.</b> 주사위 목록의 보조선이 그렇다 —
        /// 줄었다 늘어나면 칸과 칸 사이가 어긋나 보이고, 장식이 본체보다 눈에 띈다.
        /// </summary>
        private bool scaleEnabled = true;

        /// <summary>
        /// 배율 없이 알파만으로 들어오게 한다.
        /// <see cref="PlayChildren"/> 보다 <b>먼저</b> 불러야 한다 — 그쪽은 컴포넌트가 이미
        /// 있으면 그것을 그대로 쓰므로, 여기서 정해 둔 설정이 유지된다.
        /// </summary>
        public void SetScaleEnabled(bool enabled)
        {
            scaleEnabled = enabled;

            if (!enabled)
                transform.localScale = Vector3.one;
        }

        /// <summary>대상에 <see cref="UIAppear"/> 를 붙이고 배율 사용 여부를 정한다.</summary>
        public static UIAppear Configure(Component target, bool useScale)
        {
            if (target == null)
                return null;

            var appear = target.GetComponent<UIAppear>();
            if (appear == null)
                appear = target.gameObject.AddComponent<UIAppear>();

            appear.SetScaleEnabled(useScale);
            return appear;
        }

        /// <summary>대상에 붙이고 바로 재생한다.</summary>
        public static void Play(Component target, float delay, bool useScale = true)
        {
            UIAppear appear = Configure(target, useScale);
            if (appear != null)
                appear.Play(delay);
        }

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnDisable()
        {
            // 연출 도중에 꺼지면 작아지고 흐린 채로 굳는다. 목록이 스크롤로 껐다 켜지는
            // 화면에서 실제로 밟는 경로다.
            Skip();
        }

        /// <summary><paramref name="delay"/> 만큼 늦게 튀어나온다.</summary>
        public void Play(float delay)
        {
            if (!gameObject.activeInHierarchy)
                return;

            PlayAsync(++sequence, delay).Forget();
        }

        /// <summary>연출을 건너뛰고 제자리·또렷한 상태로 못박는다.</summary>
        public void Skip()
        {
            sequence++;

            // 배율을 안 쓰는 경우에도 1 로 못박는다. 설정이 도중에 바뀌었을 때
            // 줄어든 채로 남지 않게 하는 쪽이 안전하다.
            transform.localScale = Vector3.one;

            if (group != null)
                group.alpha = 1f;
        }

        private async UniTaskVoid PlayAsync(int mine, float delay)
        {
            // 시작 상태를 이 프레임에 세운다. 한 프레임이라도 또렷하게 보이면
            // 칸이 깜빡였다가 다시 나타나는 것처럼 보인다.
            ApplyProgress(0f);

            float waited = 0f;
            while (waited < delay)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (this == null || mine != sequence)
                    return;

                waited += Time.unscaledDeltaTime;
            }

            float elapsed = 0f;
            while (elapsed < Duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (this == null || mine != sequence)
                    return;

                // 배속과 일시정지를 타지 않는다.
                elapsed += Time.unscaledDeltaTime;
                ApplyProgress(Mathf.Clamp01(elapsed / Duration));
            }

            if (this == null || mine != sequence)
                return;

            Skip();
        }

        private void ApplyProgress(float t)
        {
            if (scaleEnabled)
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(StartScale, 1f, UIEase.OutBack(t));

            if (group != null)
                group.alpha = UIEase.OutQuad(t);
        }

        // ──────────────────────────────────────────────── 목록 전체

        /// <summary>칸 사이 기본 간격(초).</summary>
        public const float DefaultStep = 0.035f;

        /// <summary>
        /// 시차에 쓸 총 시간의 상한(초).
        ///
        /// <b>칸이 많아도 총 시간이 늘어나지 않게 조인다.</b> 주사위 30개짜리 목록에서
        /// 간격을 그대로 두면 마지막 칸이 1초 뒤에 뜨는데, 그때 유저는 이미 스크롤하고 있다.
        /// </summary>
        public const float MaxSpan = 0.32f;

        /// <summary>
        /// 칸이 <paramref name="count"/> 개일 때 쓸 실제 간격(초).
        ///
        /// <b>여러 목록의 타이밍을 맞출 때 쓴다.</b> 주사위 칸과 그 뒤의 보조선처럼 서로
        /// 다른 부모에 있는 것들을 같은 박자로 들여보내려면, 간격을 같은 식으로 구해야 한다.
        /// </summary>
        public static float StepFor(int count, float step = DefaultStep)
        {
            return count > 1 ? Mathf.Min(step, MaxSpan / (count - 1)) : 0f;
        }

        /// <summary>
        /// 컨테이너의 <b>켜져 있는</b> 자식들을 앞에서부터 하나씩 띄운다.
        /// 컴포넌트가 없으면 붙이므로 프리팹을 미리 고칠 필요가 없다.
        /// </summary>
        public static void PlayChildren(Transform container, float step = DefaultStep)
        {
            if (container == null)
                return;

            int visible = 0;
            for (int i = 0; i < container.childCount; i++)
            {
                if (container.GetChild(i).gameObject.activeSelf)
                    visible++;
            }

            if (visible == 0)
                return;

            float gap = visible > 1 ? Mathf.Min(step, MaxSpan / (visible - 1)) : 0f;
            int order = 0;

            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                if (!child.gameObject.activeSelf)
                    continue;

                var appear = child.GetComponent<UIAppear>();
                if (appear == null)
                    appear = child.gameObject.AddComponent<UIAppear>();

                appear.Play(gap * order);
                order++;
            }
        }
    }
}
