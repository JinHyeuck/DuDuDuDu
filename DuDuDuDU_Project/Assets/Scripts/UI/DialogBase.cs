using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OJ.Lobby;
using OJ.Utils;

namespace OJ.UI
{
    public class DialogBase : MonoBehaviour
    {
        protected RectTransform _rt;
        protected string _name;
        public GameObject dialogView;

        public bool isEnter { get { return _isEnter; } }

        protected bool _isEnter = false;
        protected bool _isLoaded = false;

        public bool UseBackBtn = false;

        [SerializeField]
        private List<Button> _exitBtn;

        /// <summary>
        /// 닫기 버튼을 목록에 더한다. 프리팹을 <b>코드로 구울 때</b> 쓴다 —
        /// 인스펙터에서 끌어다 놓는 것과 같은 일을 하는 자리다.
        /// 굽는 시점에 채워 두면 그 참조는 프리팹에 저장되고, 실제 onClick 연결은
        /// 런타임에 <see cref="Load"/> 가 한다.
        /// </summary>
        public void AddExitButton(Button button)
        {
            if (button == null)
                return;

            if (_exitBtn == null)
                _exitBtn = new List<Button>();

            if (!_exitBtn.Contains(button))
                _exitBtn.Add(button);
        }

        private void Awake()
        {
            if (dialogView != null)
                Load();
        }

        private void InitDialog()
        {
            if (dialogView == null)
                throw new System.NullReferenceException(string.Format("{0} dialogView Null", this.name));

            _name = GetType().Name;
            _rt = GetComponent<RectTransform>();

            if (_exitBtn != null)
            {
                for (int i = 0; i < _exitBtn.Count; ++i)
                {
                    if (_exitBtn[i] != null)
                        _exitBtn[i].onClick.AddListener(Exit);
                }
            }
        }

        public void Load()
        {
            if (_isLoaded)
                return;

            InitDialog();

            dialogView.SetActive(false);

            OnLoad();
            _isLoaded = true;
        }

        public void Load_Element()
        {
            Load();
        }

        protected virtual void OnLoad()
        {
        }

        public void Unload()
        {
            OnExit();
            OnUnload();
        }

        protected virtual void OnUnload()
        {
        }

        public void SetActive(bool active)
        {
            if (active == true)
                Enter();
            else
                Exit();
        }

        public void ElementEnter()
        {
            Enter();
        }

        public void Enter()
        {
            if (dialogView != null)
            {
                if (dialogView.activeSelf)
                    return;
                dialogView.SetActive(true);
            }

            _isEnter = true;
            OnEnter();

            EnterFinish();
        }

        private void EnterFinish()
        {
            if (UseBackBtn == true)
                AOSBackBtnManager.Instance.EnterBackBtnAction(this);
        }

        /// <summary>
        /// 백키를 눌렀을 때 <see cref="Exit"/> 대신 할 일. 비어 있으면 기본대로 닫는다.
        ///
        /// <b>왜 필요한가.</b> 로비 탭 내용물(장비·주사위·유물 페이지)은 팝업이 아니라서
        /// 백키에 <b>닫히면 안 되고 홈 탭으로 돌아가야 한다.</b> 그런데 그 판단은 탭을
        /// 소유한 <c>LobbyLayoutController</c> 만 할 수 있다 — 페이지가 로비를 알면
        /// 다른 화면에서 재사용할 수 없게 된다.
        ///
        /// 그래서 동작을 <b>띄우는 쪽이 넘겨준다.</b> 페이지 클래스는 그대로 두고
        /// 로비만 이 값을 채운다.
        /// </summary>
        public System.Action BackKeyOverride;

        public virtual void BackKeyCall()
        {
            if (BackKeyOverride != null)
            {
                BackKeyOverride();
                return;
            }

            Exit();
        }

        public void ElementExit()
        {
            Exit();
        }

        public void Exit()
        {
            _isEnter = false;

            ExitFinish();
        }

        private void ExitFinish()
        {
            if (dialogView != null)
                dialogView.SetActive(false);

            OnExit();
        }

        protected virtual void OnDestroy()
        {
            Unload();
        }

        protected virtual void OnEnter()
        {
        }

        protected virtual void OnExit()
        {
        }
    }
}
