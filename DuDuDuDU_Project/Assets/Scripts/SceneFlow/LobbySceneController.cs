using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class LobbySceneController : MonoBehaviour
    {
        [SerializeField] private Button enterBattleButton;

        private void Awake()
        {
            if (enterBattleButton != null)
                enterBattleButton.onClick.AddListener(OnClickEnterBattle);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                OnClickEnterBattle();
        }

        public void OnClickEnterBattle()
        {
            SceneFlowManager.LoadBattle();
        }
    }
}
