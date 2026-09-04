using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace OJ.SceneFlow
{
    /// <summary>
    /// 씬 전환의 유일한 입구. (MIGRATION_BASELINE 9.4)
    ///
    /// <b>예전에는 <c>SceneManager.LoadScene</c> 을 그대로 불렀다.</b> 동기 로드라 프레임이
    /// 멈추고, 페이드가 들어갈 자리가 없고, 무엇보다 <b>전환이 겹치는 것을 막을 수 없었다.</b>
    /// 지금은 로드가 즉시 끝나서 연타가 문제로 보이지 않지만, 페이드를 넣는 순간
    /// 전환에 시간이 생기고 그 사이의 두 번째 요청은 <b>페이드가 반쯤 걸린 채로 다른 씬을
    /// 로드하는</b> 상태를 만든다.
    ///
    /// 그래서 게이트를 먼저 만든다 — 전환 중에 들어온 요청은 <b>조용히 무시하지 않고</b>
    /// 로그로 남긴다. 무시된 요청은 "버튼이 안 먹는다"로 보이는데, 그게 의도된 차단인지
    /// 사고인지 구별할 수 있어야 한다.
    /// </summary>
    [Preserve]
    public sealed class SceneRouter
    {
        /// <summary>
        /// 전환 연출 길이. 짧게 잡았다 — 페이드는 "화면이 바뀐다"를 알리는 장치이지
        /// 감상하는 연출이 아니고, 길수록 조작을 막는 시간이 길어진다.
        /// </summary>
        public const float FadeOutSeconds = 0.18f;
        public const float FadeInSeconds = 0.22f;

        private readonly CoroutineHost host;
        private readonly FadeView fade;

        private bool isTransitioning;

        /// <summary>지금 전환 중인가.</summary>
        public bool IsTransitioning => isTransitioning;

        public SceneRouter(CoroutineHost host, FadeView fade)
        {
            this.host = host;
            this.fade = fade;
        }

        public void Go(SceneId id)
        {
            if (isTransitioning)
            {
                // 조용히 버리지 않는다. 이 로그가 없으면 "가끔 버튼이 안 먹는다"가 되고,
                // 그것이 게이트 때문인지 배선 사고인지 구별할 방법이 없다.
                Debug.LogWarning("[씬] 전환 중이라 " + id + " 요청을 무시했다.");
                return;
            }

            if (!SceneCatalog.IsInBuild(id))
            {
                // 여기서 막지 않으면 페이드가 걸린 뒤에 로드가 실패해 검은 화면에 갇힌다.
                Debug.LogError("[씬] " + id + "(" + SceneCatalog.NameOf(id) +
                               ")가 빌드 세팅에 없다. 전환하지 않는다.");
                return;
            }

            isTransitioning = true;
            host.Run(CoGo(id));
        }

        /// <summary>현재 씬을 다시 연다. 개발용 핫키가 쓴다.</summary>
        public void Reload()
        {
            SceneId? current = SceneCatalog.Current();
            if (current.HasValue)
                Go(current.Value);
            else
                Debug.LogWarning("[씬] 현재 씬이 SceneCatalog 에 없어 다시 열 수 없다.");
        }

        private IEnumerator CoGo(SceneId id)
        {
            yield return fade.FadeTo(1f, FadeOutSeconds);

            // timeScale 을 여기서 되돌린다. 전투 중 일시정지 상태로 씬을 떠나면
            // 다음 씬이 멈춘 채로 시작한다 — 예전 SceneFlowManager.Load 가 하던 일이다.
            Time.timeScale = 1f;

            AsyncOperation op = SceneManager.LoadSceneAsync(SceneCatalog.NameOf(id));

            // 로드가 끝나도 새 씬의 Awake/Start 는 다음 프레임에 돈다. 그 전에 페이드를
            // 걷으면 <b>초기화되기 전의 화면</b>이 한 프레임 비친다.
            while (op != null && !op.isDone)
                yield return null;

            yield return null;

            yield return fade.FadeTo(0f, FadeInSeconds);

            isTransitioning = false;
        }
    }
}
