using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJ
{
    public static class SceneFlowManager
    {
        public static void LoadTitle()
        {
            Load(SceneNames.Title);
        }

        public static void LoadLobby()
        {
            Load(SceneNames.Lobby);
        }

        public static void LoadBattle()
        {
            Load(SceneNames.Battle);
        }

        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
    }
}
