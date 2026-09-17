using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using OJ.Lobby;
using OJ.Utils;

namespace OJ.UI
{
    /// <summary>열릴 때의 연출.</summary>
    public enum DialogOpenStyle
    {
        /// <summary>
        /// 띄우는 쪽이 정한다. <c>UIService</c> 가 부모를 받았으면 페이지, 아니면 팝업이다.
        /// <b>기본값이 이것이어야 프리팹마다 지정하는 것을 빠뜨릴 수 없다.</b>
        /// </summary>
        Auto = 0,

        /// <summary>가운데서 커지며 또렷해진다.</summary>
        Popup,

        /// <summary>아래에서 올라오며 또렷해진다. 로비 탭 내용물이 이것이다.</summary>
        Page,

        /// <summary>연출 없이 즉시. 문제가 생긴 창을 하나만 빼는 자리다.</summary>
        None,
    }

    public class DialogBase : MonoBehaviour
    {
        protected RectTransform _rt;
        protected string _name;
        public GameObject dialogView;

        [Header("열림 연출")]
        [Tooltip("Auto 면 띄우는 쪽이 정한다 — 로비 탭 내용물은 페이지, 나머지는 팝업.")]
        [SerializeField] private DialogOpenStyle openStyle = DialogOpenStyle.Auto;

        /// <summary>
        /// 팝업 연출 길이(초). <b>짧아야 한다</b> — 창을 여는 것은 유저가 이미 결정을 끝낸
        /// 동작이라, 여기서 기다리게 하면 그 즉시 거슬린다.
        /// </summary>
        private const float PopupDuration = 0.13f;

        /// <summary>
        /// 팝업의 시작 배율. 0.95 는 거의 보이지 않아 밍밍했다 —
        /// <b>움직였다는 것이 보여야 팝이 된다.</b>
        /// </summary>
        private const float PopupStartScale = 0.88f;

        /// <summary>
        /// 페이지 연출 길이(초). 팝업보다 조금 길다 — 이동 거리가 있어서 같은 길이로 두면
        /// 미끄러지듯 빨라 보인다.
        /// </summary>
        private const float PageDuration = 0.17f;

        /// <summary>페이지가 올라오는 거리(px). 1080x1920 기준.</summary>
        private const float PageRiseDistance = 90f;

        private CanvasGroup openGroup;

        /// <summary>
        /// <see cref="DialogOpenStyle.Auto"/> 일 때 띄우는 쪽이 넣어 준 값.
        /// <c>UIService</c> 가 부모를 받았는지로 정한다.
        /// </summary>
        private DialogOpenStyle hintedStyle = DialogOpenStyle.Popup;

        /// <summary>
        /// 페이지가 원래 있어야 할 자리. 연출이 <c>anchoredPosition</c> 을 만지므로,
        /// 끊겼을 때 돌려놓으려면 기억해 둬야 한다.
        /// </summary>
        private Vector2 pageBasePosition;
        private bool pageBaseCaptured;

        /// <summary>
        /// 띄우는 쪽이 연출 종류를 알려 준다. <see cref="openStyle"/> 이 Auto 일 때만 쓰인다 —
        /// 프리팹에서 명시적으로 고른 것을 덮으면 그 설정이 무의미해진다.
        /// </summary>
        internal void HintOpenStyle(DialogOpenStyle style)
        {
            hintedStyle = style;
        }

        private DialogOpenStyle ResolvedOpenStyle =>
            openStyle == DialogOpenStyle.Auto ? hintedStyle : openStyle;

        /// <summary>
        /// 진행 중인 연출을 무효화하는 순번. 닫히거나 다시 열리면 올라간다.
        /// (<c>UIBountyCallout</c>·<c>UIDice</c> 와 같은 방식 — 토큰을 들고 다니는 것보다
        /// 이 코드베이스에서 읽기 쉽다.)
        /// </summary>
        private int openSequence;

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

            // 창 안의 모든 버튼에 눌림 연출을 붙인다. 프리팹 29개를 손으로 고치는 것보다
            // 빠뜨릴 구석이 없고, 새로 만드는 창도 저절로 따라온다.
            UIButtonPress.AttachToChildren(gameObject);

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
            // 파괴될 때 "열려 있음" 표시를 반드시 끈다.
            //
            // 백키 스택(<c>AOSBackBtnManager</c>)은 씬을 넘어 살아 있고, 스택에서 꺼낸
            // 항목이 <c>isEnter</c> 면 살아 있는 창으로 보고 <c>BackKeyCall</c> 을 부른 뒤
            // 그 프레임의 백키를 <b>소비하고 끝낸다.</b> 이 줄이 없으면 씬과 함께 파괴된
            // 창이 그 자리를 차지해 <b>백키 한 번을 조용히 먹는다.</b>
            //
            // <b>지금은 그 증상이 눈에 안 보인다.</b> 스택이 비었을 때의 폴백
            // (던전 이탈·게임 종료 확인)이 <c>AOSBackBtnManager</c> 에 전부 주석으로
            // 남아 있어서, 먹히는 백키가 "어차피 아무 일도 없었을 백키"이기 때문이다.
            // <b>그 주석을 되살리는 순간</b> 씬 전환 직후 첫 백키가 안 먹는 버그로 바로 드러난다.
            // 그때 이 한 줄을 찾느니 지금 막아 둔다.
            _isEnter = false;

            // Exit 을 거치지 않는 경로다. 진행 중이던 열림 연출을 여기서도 끊어야
            // 파괴된 뒤까지 코루틴이 살아 가짜 null 을 만지려 든다.
            openSequence++;

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
                // 이미 열려 있으면 아무 일도 하지 않는다.
                //
                // <b>isEnter 를 같이 보는 이유.</b> 페이지 퇴장 연출이 도는 동안에는
                // dialogView 가 아직 켜져 있지만 isEnter 는 이미 false 다. activeSelf 만
                // 보면 그 상태를 "열려 있다" 로 읽어 <b>탭을 빠르게 오갈 때 페이지가
                // 영영 안 열린다.</b>
                if (dialogView.activeSelf && _isEnter)
                    return;

                dialogView.SetActive(true);
            }

            BringToFront();

            _isEnter = true;

            // OnEnter 보다 먼저 부른다. 시작값(흐림·축소)을 이 프레임 안에 세워야
            // 또렷한 상태가 한 프레임 보였다가 흐려지는 깜빡임이 안 생긴다.
            BeginOpenAnimation();

            OnEnter();

            EnterFinish();
        }

        /// <summary>
        /// 팝업을 형제 중 맨 뒤로 보낸다 — uGUI 는 뒤 형제를 위에 그린다.
        ///
        /// <b>없으면 나중에 연 창이 가려진다.</b> 팝업은 전부 <c>UIPopupRoot</c> 하나의
        /// 자식이고 <c>UIService</c> 가 인스턴스를 캐시해 재사용하므로, 형제 순서는
        /// <b>처음 만들어진 순서</b>로 굳는다. 먼저 태어난 창을 나중에 열면 뒤에 깔린 채로
        /// 뜬다 — 별 보상에서 받은 결과창이 페이지 뒤에 숨어 있던 것이 이것이다.
        ///
        /// <b>페이지는 건드리지 않는다.</b> 로비 탭 내용물은 <c>Content</c> 안에 들어가고,
        /// 거기서 순서를 바꾸면 레이아웃 그룹의 배치가 달라질 수 있다.
        /// </summary>
        private void BringToFront()
        {
            if (ResolvedOpenStyle == DialogOpenStyle.Page)
                return;

            transform.SetAsLastSibling();
        }

        /// <summary>
        /// 열림 연출을 시작한다. <b>상태 기계는 건드리지 않는다</b> —
        /// <see cref="isEnter"/> 는 이미 true 이고 <c>OnEnter</c> 도 그대로 돈다.
        /// 여기서 하는 일은 보이는 것뿐이라, 연출이 끊겨도 로직은 영향을 받지 않는다.
        /// </summary>
        private void BeginOpenAnimation()
        {
            // 다시 열렸으므로 앞선 연출은 무효다. 끄더라도 이 줄은 지나가야
            // 진행 중이던 연출이 값을 계속 덮어쓰지 않는다.
            int sequence = ++openSequence;

            if (dialogView == null)
                return;

            DialogOpenStyle style = ResolvedOpenStyle;
            if (style == DialogOpenStyle.None)
                return;

            CanvasGroup group = EnsureOpenGroup();
            if (group == null)
                return;

            group.alpha = 0f;

            if (style == DialogOpenStyle.Page)
            {
                // null 이면 좌표를 남이 소유한 것이다(PageRect 주석 참조).
                // 그때는 위치를 건드리지 않고 알파만으로 들어온다 — 스케일로 바꾸지도 않는다.
                // 페이지가 통째로 부풀었다 줄어드는 것은 탭 내용물의 움직임이 아니다.
                RectTransform rect = PageRect();
                if (rect != null)
                {
                    CapturePageBase(rect);
                    rect.anchoredPosition = pageBasePosition + new Vector2(0f, -PageRiseDistance);
                }
            }

            if (style == DialogOpenStyle.Popup)
                dialogView.transform.localScale = Vector3.one * PopupStartScale;

            PlayOpenAsync(sequence, style).Forget();
        }

        private async UniTaskVoid PlayOpenAsync(int sequence, DialogOpenStyle style)
        {
            float duration = style == DialogOpenStyle.Page ? PageDuration : PopupDuration;
            RectTransform rect = style == DialogOpenStyle.Page ? PageRect() : null;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                // 기다리는 사이에 닫혔거나, 다시 열렸거나, 씬이 내려갔다.
                // 파괴된 오브젝트는 == null 이 true 인 가짜 null 이라 이 검사에 걸린다.
                if (this == null || sequence != openSequence || dialogView == null)
                    return;

                // 배속과 일시정지를 타지 않는다. 전투를 3배로 돌리는 중에도 창이 열리는
                // 속도는 같아야 하고, timeScale 0 에서 멈춰 버리면 창이 영영 흐린 채로 남는다.
                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / duration);

                // 알파와 움직임에 다른 곡선을 쓴다.
                //
                // 알파까지 튀게 하면 다 나타난 창이 다시 옅어졌다 돌아오는 셈이라 깜빡여
                // 보인다. 반대로 <b>둘 다 부드럽게만 멎으면 밍밍하다</b> — 움직임이 끝나는
                // 지점이 흐릿해서 창이 "선" 느낌이 안 난다.
                float fade = UIEase.OutQuad(t);
                float move = UIEase.OutBack(t);

                if (openGroup != null)
                    openGroup.alpha = fade;

                if (style == DialogOpenStyle.Page)
                {
                    if (rect != null)
                    {
                        // LerpUnclamped 여야 한다. Lerp 는 1 을 넘는 값을 잘라서
                        // 오버슈트가 통째로 사라진다.
                        rect.anchoredPosition = pageBasePosition +
                            new Vector2(0f, Mathf.LerpUnclamped(-PageRiseDistance, 0f, move));
                    }
                }
                else
                {
                    dialogView.transform.localScale =
                        Vector3.one * Mathf.LerpUnclamped(PopupStartScale, 1f, move);
                }
            }

            // 마지막을 정확한 값으로 못박는다. 루프가 duration 을 지나 끝나므로 위 계산만으로는
            // 1 에 아주 가까울 뿐 정확히 1 이라는 보장이 없고, 0.999 배율이나 몇 픽셀 어긋난
            // 자리로 남은 창은 다음에 열 때까지 그대로 간다.
            RestoreOpenVisual();
        }

        /// <summary>페이지 퇴장 길이(초). 들어올 때보다 짧다 — 나가는 것을 기다릴 이유가 없다.</summary>
        private const float PageCloseDuration = 0.12f;

        /// <summary>
        /// 페이지가 내려가며 사라진다. 끝나면 <c>dialogView</c> 를 끈다.
        ///
        /// <b>중간에 다시 열리면 끄지 않는다.</b> 탭을 빠르게 오가면 이 연출이 도는 중에
        /// <see cref="Enter"/> 가 불리는데, 그때 순번이 바뀌므로 아래 <c>SetActive(false)</c>
        /// 에 닿지 않는다. 이 검사가 없으면 <b>방금 연 페이지가 곧바로 꺼진다.</b>
        /// </summary>
        private async UniTaskVoid PlayPageCloseAsync(int sequence)
        {
            RectTransform rect = PageRect();
            CanvasGroup group = EnsureOpenGroup();

            if (rect != null)
                CapturePageBase(rect);

            float elapsed = 0f;

            while (elapsed < PageCloseDuration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);

                if (this == null || sequence != openSequence || dialogView == null)
                    return;

                elapsed += Time.unscaledDeltaTime;

                float t = Mathf.Clamp01(elapsed / PageCloseDuration);

                // 나갈 때는 오버슈트를 쓰지 않는다. 사라지는 것이 튀면 시선을 붙잡아
                // 새로 들어오는 페이지를 가린다.
                float eased = UIEase.OutQuad(t);

                if (group != null)
                    group.alpha = 1f - eased;

                if (rect != null)
                {
                    rect.anchoredPosition = pageBasePosition +
                        new Vector2(0f, Mathf.Lerp(0f, -PageRiseDistance, eased));
                }
            }

            if (this == null || sequence != openSequence || dialogView == null)
                return;

            dialogView.SetActive(false);
            RestoreOpenVisual();
        }

        /// <summary>
        /// 페이지가 움직일 <c>RectTransform</c>. <c>dialogView</c> 자신이다.
        ///
        /// <b>남이 좌표를 소유하고 있으면 null 을 준다.</b> 부모가 레이아웃 그룹이면
        /// 자식의 <c>anchoredPosition</c> 은 그쪽 것이라, 중간에 끼어들어 쓰면 값이
        /// 눌러앉아 배치가 무너진다 — 목록 칸에서 실제로 그 사고가 났다.
        /// 그때는 알파만으로 들어오고 위치는 건드리지 않는다.
        /// </summary>
        private RectTransform PageRect()
        {
            if (dialogView == null)
                return null;

            var rect = dialogView.transform as RectTransform;
            if (rect == null)
                return null;

            if (rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null)
                return null;

            return rect;
        }

        /// <summary>
        /// 원래 자리를 한 번만 기억한다.
        ///
        /// <b>매번 다시 읽으면 안 된다.</b> 연출 도중에 다시 열면 그때의 자리(내려가 있는
        /// 상태)를 기준으로 삼게 되고, 그러면 열 때마다 창이 조금씩 아래로 내려간다.
        /// </summary>
        private void CapturePageBase(RectTransform rect)
        {
            if (pageBaseCaptured)
                return;

            pageBasePosition = rect.anchoredPosition;
            pageBaseCaptured = true;
        }

        /// <summary>
        /// 연출용 <c>CanvasGroup</c>. 없으면 만든다.
        ///
        /// <b>이미 있으면 그것을 쓴다.</b> 새로 붙이면 같은 오브젝트에 둘이 생겨
        /// 알파가 곱해지고, 그러면 원래 쓰던 쪽이 의도대로 안 보인다.
        /// </summary>
        private CanvasGroup EnsureOpenGroup()
        {
            if (openGroup != null)
                return openGroup;

            openGroup = dialogView.GetComponent<CanvasGroup>();
            if (openGroup == null)
                openGroup = dialogView.AddComponent<CanvasGroup>();

            return openGroup;
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

        /// <summary>
        /// 백키를 <b>부분적으로만</b> 처리했을 때 자기를 스택에 되돌려 놓는다.
        ///
        /// <b>왜 필요한가.</b> <c>AOSBackBtnManager</c> 는 <see cref="BackKeyCall"/> 를
        /// 부르기 <b>전에</b> 스택에서 이 항목을 꺼낸다. 그래서 안쪽 팝업만 닫고 끝내면
        /// 이 창이 스택에서 사라지고 <b>다음 백키가 아무것도 못 찾는다</b> —
        /// 창이 열려 있는데 백키가 죽는다.
        ///
        /// 계층이 있는 UI(창 안의 상세 팝업 등)에서 안쪽만 닫는 오버라이드를 쓸 때
        /// 반드시 같이 부를 것.
        /// </summary>
        protected void KeepOnBackStack()
        {
            if (UseBackBtn && AOSBackBtnManager.Instance != null)
                AOSBackBtnManager.Instance.EnterBackBtnAction(this);
        }

        public void ElementExit()
        {
            Exit();
        }

        public void Exit()
        {
            _isEnter = false;

            // 진행 중이던 열림 연출을 무효화한다. 이 줄이 없으면 닫은 뒤에도 연출이
            // 남은 프레임만큼 알파를 덮어써서, 꺼진 창이 다시 또렷해졌다 사라진다.
            openSequence++;
            RestoreOpenVisual();

            ExitFinish();
        }

        /// <summary>
        /// 연출이 만지던 값을 원래대로 돌린다.
        ///
        /// <b>중간에 끊겼을 때가 문제다.</b> 알파 0.3 에서 닫히면 그 값이 그대로 남고,
        /// 다음에 열 때 <see cref="openStyle"/> 이 <see cref="DialogOpenStyle.None"/> 이라면 <b>반투명한 창이
        /// 영영 그대로</b> 뜬다. 여기서 못박아 두면 그 경로가 생기지 않는다.
        /// </summary>
        private void RestoreOpenVisual()
        {
            if (openGroup != null)
                openGroup.alpha = 1f;

            if (dialogView == null)
                return;

            dialogView.transform.localScale = Vector3.one;

            // 페이지는 자리도 돌려놓는다. 한 번이라도 연출을 탄 뒤에만 유효한 값이라
            // 캡처 여부를 확인하고 만진다 — 안 그러면 원점(0,0)으로 끌어다 놓게 된다.
            if (pageBaseCaptured && dialogView.transform is RectTransform rect)
                rect.anchoredPosition = pageBasePosition;
        }

        private void ExitFinish()
        {
            if (dialogView != null)
            {
                // 페이지는 내려가며 사라진다. 그동안 dialogView 는 켜진 채로 두고,
                // 연출이 끝나면 그때 끈다.
                //
                // <b>로직은 기다리지 않는다.</b> 아래 OnExit 은 지금 그대로 돌고
                // isEnter 도 이미 false 다 — 여기서 미루는 것은 보이는 것뿐이라,
                // 연출 도중에 탭을 또 눌러도 상태가 꼬이지 않는다.
                if (ResolvedOpenStyle == DialogOpenStyle.Page && gameObject.activeInHierarchy)
                    PlayPageCloseAsync(++openSequence).Forget();
                else
                    dialogView.SetActive(false);
            }

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
