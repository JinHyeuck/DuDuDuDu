using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using OJ.DI;
using OJ.UI;

namespace OJ.Utils
{
    /// <summary>
    /// 안드로이드 뒤로가기(Escape) 처리. (MIGRATION_BASELINE 8.3a)
    ///
    /// <b>매 프레임 입력을 봐야 하는데도 MonoBehaviour 가 아니다.</b> VContainer 의
    /// ITickable 진입점을 쓴다 — 컨테이너가 PlayerLoop 에 끼워 넣고 Tick 을 매 프레임
    /// 부른다. 예전 Update() 와 같은 자리다.
    ///
    /// 덕분에 이 클래스를 위해 GameObject 를 만들 이유가 없어졌다. MonoSingleton 은
    /// Instance 게터가 <b>없으면 만들어 내는</b> 성질이 있어서 누가 언제 처음 건드리느냐에
    /// 따라 생성 시점이 달라졌는데, 그 불확실성도 같이 사라진다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class AOSBackBtnManager : ITickable
    {
        /// <summary>과도기 다리. 대입은 <see cref="GameContainer"/> 에서만 한다.</summary>
        public static AOSBackBtnManager Instance { get; internal set; }

        private Stack<DialogBase> m_backBtnActionPool = new Stack<DialogBase>();

        /// <summary>
        /// (8.3b) BattleScene 매니저들로 가는 창구. 컨테이너가 생성자로 넣는다.
        ///
        /// 이 클래스는 <b>루트에 영구히 사는 서비스</b>라 로비·타이틀에서도 매 프레임 Tick 이
        /// 돈다. 그때 <c>battle.Game</c> 은 <b>정상적으로 null</b> 이다 — 전투가 없으니까.
        /// 그러니 여기서 창구를 만질 때는 전투 중인지를 먼저 따져야 한다.
        /// </summary>
        private readonly IBattleRefs battle;

        public bool QuickExitGame = false;

        public AOSBackBtnManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        //------------------------------------------------------------------------------------
        public void Tick()
        {
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                if (QuickExitGame == true)
                {
                    // 8.3b: GameManager.Instance → 창구. 참조를 얻는 경로만 바뀌었다.
                    //
                    // 일부러 IsActive 로 감싸지 않았다. 감싸면 로비에서 이 분기가 조용히
                    // 아무 일도 안 하게 되는데, 그건 "즉시 종료"라는 뜻과 정반대이고
                    // 2단계에서 봉인한 조용한 폴백 그 자체다. 예전에도 로비에서는
                    // Instance 가 null 이라 똑같이 터졌으므로 동작은 그대로다.
                    battle.Game.OnApplicationQuit();
                    return;
                }

                while (m_backBtnActionPool.Count > 0)
                {
                    DialogBase action = m_backBtnActionPool.Pop();

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
        public void EnterBackBtnAction(DialogBase action)
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
                DialogBase action = m_backBtnActionPool.Pop();

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