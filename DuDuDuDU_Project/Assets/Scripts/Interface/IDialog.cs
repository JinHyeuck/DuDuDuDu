using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace OJ
{
    public class IDialog : MonoBehaviour
    {
        protected RectTransform _rt;
		protected string _name;
		public GameObject dialogView;

        public bool isEnter { get { return _isEnter; } }

        protected bool _isEnter = false;

		public bool UseBackBtn = false;

		[SerializeField]
		private List<Button> _exitBtn;

        private void Awake()
        {
            Load();
        }

        void InitDialog()
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
			InitDialog();

			dialogView.SetActive(false);

			OnLoad();
		}

        public void Load_Element()
        {
			InitDialog();
			dialogView.SetActive(false);

			OnLoad();
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

		public virtual void BackKeyCall()
		{
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