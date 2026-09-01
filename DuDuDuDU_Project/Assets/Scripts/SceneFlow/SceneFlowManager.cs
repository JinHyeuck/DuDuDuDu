using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.DI;
using OJ.Point;

namespace OJ.SceneFlow
{
    /// <summary>
    /// 씬 전환 호출부가 쓰는 얇은 다리. (MIGRATION_BASELINE 9.4)
    ///
    /// 실제 일은 <see cref="SceneRouter"/> 가 한다. 이 클래스가 남아 있는 이유는
    /// 호출부가 5곳(타이틀·로비·별의 시련·전투 종료·개발 핫키)에 흩어져 있어서
    /// 한 번에 주입으로 바꾸지 않기 위해서다. <c>PointManager.Instance</c> 와 같은 과도기
    /// 다리이고, 호출부가 라우터를 직접 받게 되면 사라진다.
    ///
    /// <b>라우터가 없을 때도 동작해야 한다.</b> 컨테이너보다 먼저 도는 코드나 테스트에서
    /// 이 클래스를 부를 수 있다. 그때는 예전처럼 즉시 로드한다 — 페이드와 연타 차단이
    /// 없을 뿐 화면은 바뀐다. 여기서 아무것도 안 하면 "버튼이 죽었다"가 된다.
    /// </summary>
    public static class SceneFlowManager
    {
        public static void LoadTitle() => Go(SceneId.Title);

        public static void LoadLobby() => Go(SceneId.Lobby);

        public static void LoadBattle() => Go(SceneId.Battle);

        /// <summary>현재 씬을 다시 연다.</summary>
        public static void Reload()
        {
            SceneRouter router = GameContainer.SceneRouter;
            if (router != null)
            {
                router.Reload();
                return;
            }

            LoadImmediate(SceneManager.GetActiveScene().name);
        }

        public static void Go(SceneId id)
        {
            SceneRouter router = GameContainer.SceneRouter;
            if (router != null)
            {
                router.Go(id);
                return;
            }

            Debug.LogWarning("[씬] SceneRouter 가 없어 페이드 없이 즉시 전환한다: " + id);
            LoadImmediate(SceneCatalog.NameOf(id));
        }

        private static void LoadImmediate(string sceneName)
        {
            // 전환 전에 되돌린다. 일시정지 상태로 씬을 떠나면 다음 씬이 멈춘 채 시작한다.
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
    }
}
