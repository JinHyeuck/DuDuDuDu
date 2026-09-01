using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJ.SceneFlow
{
    /// <summary>
    /// <see cref="SceneId"/> 와 실제 씬 이름을 잇는 유일한 곳. (MIGRATION_BASELINE 9.3)
    ///
    /// <b>여기 말고 어디에도 씬 이름 문자열이 있으면 안 된다.</b> 흩어지면 이름을 바꿀 때
    /// 한 군데를 빠뜨리게 되고, 그 빠뜨림은 컴파일에 걸리지 않는다.
    /// </summary>
    public static class SceneCatalog
    {
        public static string NameOf(SceneId id)
        {
            switch (id)
            {
                case SceneId.Title: return "TitleScene";
                case SceneId.Lobby: return "LobbyScene";
                case SceneId.Battle: return "BattleScene";
                default:
                    // 새 SceneId 를 추가하고 여기를 잊으면 즉시 터진다. 조용히 기본 씬으로
                    // 흐르게 두면 "왜 엉뚱한 씬이 뜨지"를 한참 쫓게 된다.
                    throw new ArgumentOutOfRangeException(nameof(id), id, "SceneCatalog 에 이름이 없다.");
            }
        }

        /// <summary>
        /// 빌드 세팅에 실제로 들어 있는가.
        ///
        /// <b>이걸 물어볼 수 있어야 하는 이유.</b> 빌드 목록에서 빠진 씬은 에디터에서는
        /// 잘 열리다가 <b>실기 빌드에서만</b> 못 연다. <c>SceneManager.LoadScene</c> 은
        /// 그때 예외를 던지고, 전환 도중이면 페이드가 걸린 채로 멈춘다.
        /// 미리 물어보면 로드를 시도하기 전에 명시적으로 실패할 수 있다.
        /// </summary>
        public static bool IsInBuild(SceneId id)
        {
            return Application.CanStreamedLevelBeLoaded(NameOf(id));
        }

        /// <summary>씬 이름으로 <see cref="SceneId"/> 를 찾는다. 목록에 없으면 null.</summary>
        public static SceneId? IdOf(string sceneName)
        {
            foreach (SceneId id in Enum.GetValues(typeof(SceneId)))
            {
                if (NameOf(id) == sceneName)
                    return id;
            }

            return null;
        }

        /// <summary>지금 열려 있는 씬이 무엇인가. 목록에 없으면 null.</summary>
        public static SceneId? Current()
        {
            return IdOf(SceneManager.GetActiveScene().name);
        }
    }
}
