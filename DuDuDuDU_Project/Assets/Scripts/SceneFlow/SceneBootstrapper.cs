using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJ
{
    public static class SceneBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneControllers()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.name == SceneNames.Title)
            {
                if (Object.FindFirstObjectByType<TitleSceneController>() == null)
                {
                    var go = new GameObject(nameof(TitleSceneController));
                    go.AddComponent<TitleSceneController>();
                    SceneManager.MoveGameObjectToScene(go, scene);
                }
            }
            else if (scene.name == SceneNames.Lobby)
            {
                if (Object.FindFirstObjectByType<LobbySceneController>() == null)
                {
                    var go = new GameObject(nameof(LobbySceneController));
                    go.AddComponent<LobbySceneController>();
                    SceneManager.MoveGameObjectToScene(go, scene);
                }
            }
        }
    }
}
