using UnityEngine;
using System.Collections.Generic;

namespace OJ
{
    public class AOSBackBtnManager : MonoSingleton<AOSBackBtnManager>
    {
        private Stack<IDialog> m_backBtnActionPool = new Stack<IDialog>();

        public bool QuickExitGame = false;

        //------------------------------------------------------------------------------------
        void Update()
        {
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                if (QuickExitGame == true)
                {
                    GameManager.Instance.OnApplicationQuit();
                    return;
                }

                while (m_backBtnActionPool.Count > 0)
                {
                    IDialog action = m_backBtnActionPool.Pop();

                    if (action.isEnter == false)
                        continue;

                    action.BackKeyCall();

                    return;
                }

                //if (BattleSceneManager.isAlive == true)
                //{
                    //if (BattleSceneManager.Instance.BattleType == Enum_Dungeon.LobbyScene)
                    //{
                    //    ShowExitGame();
                    //}
                    //else
                        //BattleSceneManager.Instance.VisibleDungeonExitPopup(true);
                //}
                //else
                //    ShowExitGame();
            }
        }
        //------------------------------------------------------------------------------------
        public void EnterBackBtnAction(IDialog action)
        {
            m_backBtnActionPool.Push(action);
        }
        //------------------------------------------------------------------------------------
        public void RemoveForwardBackBtnAction()
        {
            if (m_backBtnActionPool.Count > 0)
                m_backBtnActionPool.Pop();
        }
        //------------------------------------------------------------------------------------
        public void AllHidePopupPool()
        {
            while (m_backBtnActionPool.Count > 0)
            {
                IDialog action = m_backBtnActionPool.Pop();

                if (action.isEnter == false)
                    continue;

                action.BackKeyCall();
            }
        }
        //------------------------------------------------------------------------------------
        public void ShowExitGame()
        {
            //ProjectNoticeContent.Instance.ShowExitGameDialog();
        }
        //------------------------------------------------------------------------------------
    }    
}