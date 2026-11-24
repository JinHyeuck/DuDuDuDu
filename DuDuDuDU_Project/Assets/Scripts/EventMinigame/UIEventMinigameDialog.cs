using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.UI;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 이벤트 화면 셋의 공통 몸통. 제목·남은 시간·보유 재화·한 줄 상태·마일스톤 목록과, 열고 닫을 때의 구독을 든다.
    /// 게임별 영역(게이지, 판, 룰렛)은 하위 클래스가 <see cref="RefreshPlay"/> 와 <see cref="RefreshList"/> 에서 그린다.
    ///
    /// <b>이벤트가 닫히면(회차가 넘어가면) 화면이 열린 채로 남을 수 있다.</b> 그때 플레이 버튼을 막는 것은 하위 클래스의
    /// <see cref="IsOpenNow"/> 확인이고, 매니저가 한 번 더 막는다 — 화면이 빠뜨려도 재화는 나가지 않는다.
    /// </summary>
    public abstract class UIEventMinigameDialog : DialogBase
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text currencyText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject listRoot;
        [SerializeField] private RectTransform listContent;
        [SerializeField] private UIEventMilestoneRow rowTemplate;
        [SerializeField] private Button rewardButton;
        [SerializeField] private Button sheetCloseButton;

        /// <summary>킷 버튼 그림 넷(주 행동 / 보조 / 선택됨 / 대박). 굽을 때 채워서 런타임에 그림만 바꿔 끼운다.</summary>
        [SerializeField] private Sprite spritePrimary;
        [SerializeField] private Sprite spriteSecondary;
        [SerializeField] private Sprite spritePositive;
        [SerializeField] private Sprite spriteDanger;

        private readonly List<UIEventMilestoneRow> rows = new List<UIEventMilestoneRow>();
        private float nextHeaderRefresh;
        private int sheetFocusOverride = -1;

        /// <summary>이 화면이 맡은 이벤트.</summary>
        protected abstract EventKind Kind { get; }

        protected static DateTime UtcNow
        {
            get { return EventMinigameClock.UtcNow; }
        }

        protected EventMinigameManager Manager
        {
            get { return EventMinigameManager.Instance; }
        }

        /// <summary>지금 이 이벤트가 열려 있는가.</summary>
        protected bool IsOpenNow
        {
            get { return Manager != null && Manager.IsOpen(Kind, UtcNow); }
        }

        protected PointType CurrencyType
        {
            get { return EventMinigameRules.CurrencyOf(Kind); }
        }

        protected int HeldCurrency
        {
            get { return PointManager.Instance != null ? PointManager.Instance.Get(CurrencyType) : 0; }
        }

        protected TMP_Text StatusText
        {
            get { return statusText; }
        }

        // 굽기 전용 접근자(UIEventMinigameFactory).
        internal TMP_Text TitleText { get { return titleText; } }
        internal TMP_Text TimerText { get { return timerText; } }
        internal TMP_Text CurrencyText { get { return currencyText; } }
        internal TMP_Text StatusTextInternal { get { return statusText; } }
        internal void SetTitle(TMP_Text value) { titleText = value; }
        internal void SetTimer(TMP_Text value) { timerText = value; }
        internal void SetCurrency(TMP_Text value) { currencyText = value; }
        internal void SetStatus(TMP_Text value) { statusText = value; }

        internal void SetRewardButton(Button value) { rewardButton = value; }
        internal void SetSheetClose(Button value) { sheetCloseButton = value; }

        internal void SetButtonSprites(Sprite primary, Sprite secondary, Sprite positive, Sprite danger)
        {
            spritePrimary = primary;
            spriteSecondary = secondary;
            spritePositive = positive;
            spriteDanger = danger;
        }

        /// <summary>킷 버튼 그림. 버튼 상태(선택·활성)가 바뀔 때 <c>Image.sprite</c> 에 끼운다.</summary>
        protected Sprite ButtonSprite(EventButtonKind kind)
        {
            switch (kind)
            {
                case EventButtonKind.Secondary: return spriteSecondary;
                case EventButtonKind.Positive: return spritePositive;
                case EventButtonKind.Danger: return spriteDanger;
                default: return spritePrimary;
            }
        }

        internal void SetList(GameObject root, RectTransform content, UIEventMilestoneRow template)
        {
            listRoot = root;
            listContent = content;
            rowTemplate = template;
        }

        protected override void OnLoad()
        {
            UseBackBtn = true;

            if (rowTemplate != null)
                rowTemplate.gameObject.SetActive(false);

            if (rewardButton != null)
                rewardButton.onClick.AddListener(OpenRewardSheet);

            if (sheetCloseButton != null)
                sheetCloseButton.onClick.AddListener(CloseRewardSheet);

            if (listRoot != null)
                listRoot.SetActive(false);

            OnLoadPlay();
        }

        /// <summary>보상 목록 판을 연다. 열 때 목록을 다시 그리고 <see cref="SheetFocusRow"/> 줄이 위에 오게 스크롤한다.</summary>
        protected void OpenRewardSheet()
        {
            if (listRoot == null)
                return;

            listRoot.SetActive(true);
            RefreshList();

            int row = sheetFocusOverride >= 0 ? sheetFocusOverride : SheetFocusRow();
            sheetFocusOverride = -1;
            ScrollListTo(row);
        }

        protected void CloseRewardSheet()
        {
            if (listRoot != null)
                listRoot.SetActive(false);
        }

        /// <summary>다음에 보상 판을 열 때 위로 올릴 줄(예: 고르지 않은 3택1 상자). 한 번만 쓰인다.</summary>
        protected void FocusSheetOn(int row)
        {
            sheetFocusOverride = row;
        }

        /// <summary>보상 판을 열 때 위에 보일 줄. 기본은 맨 위.</summary>
        protected virtual int SheetFocusRow()
        {
            return 0;
        }

        /// <summary>하위 클래스가 버튼 연결 같은 한 번짜리 준비를 한다.</summary>
        protected virtual void OnLoadPlay()
        {
        }

        protected override void OnEnter()
        {
            EventMinigameManager manager = Manager;
            if (manager != null)
            {
                manager.Tick(UtcNow);
                manager.OnChanged += RefreshAll;
            }

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            nextHeaderRefresh = 0f;
            CloseRewardSheet();
            RefreshAll();
            OnOpened();
        }

        protected override void OnExit()
        {
            if (Manager != null)
                Manager.OnChanged -= RefreshAll;

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        /// <summary>열린 직후 한 번(첫 그리기 뒤).</summary>
        protected virtual void OnOpened()
        {
        }

        private void OnPointChanged(PointType type, int value)
        {
            if (type == CurrencyType)
                RefreshHeader();
        }

        protected virtual void Update()
        {
            if (!_isEnter)
                return;

            if (Time.unscaledTime >= nextHeaderRefresh)
            {
                nextHeaderRefresh = Time.unscaledTime + 1f;
                RefreshHeader();
            }

            OnFrame();
        }

        /// <summary>열려 있는 동안 매 프레임(게이지 표식 같은 연출).</summary>
        protected virtual void OnFrame()
        {
        }

        protected void RefreshAll()
        {
            if (!_isEnter)
                return;

            RefreshHeader();
            RefreshPlay();
            RefreshList();
        }

        protected void RefreshHeader()
        {
            if (titleText != null)
                titleText.text = EventMinigameText.EventName(Kind);

            DateTime now = UtcNow;
            if (timerText != null)
            {
                timerText.text = IsOpenNow
                    ? "종료까지 " + EventMinigameText.Remaining(EventMinigameRules.OccurrenceEndUtc(now) - now)
                    : "이벤트가 끝났어요";
            }

            if (currencyText != null)
                currencyText.text = EventMinigameText.CurrencyName(Kind) + " " + EventMinigameText.Number(HeldCurrency) + "개";
        }

        /// <summary>게임별 영역을 다시 그린다.</summary>
        protected abstract void RefreshPlay();

        /// <summary>마일스톤 목록을 다시 그린다(목록이 있는 화면만).</summary>
        protected virtual void RefreshList()
        {
        }

        protected void SetStatus(string text)
        {
            if (statusText != null)
                statusText.text = text ?? string.Empty;
        }

        /// <summary>줄을 <paramref name="count"/> 개로 맞춰 돌려준다(모자라면 템플릿을 복제한다).</summary>
        protected List<UIEventMilestoneRow> EnsureRows(int count)
        {
            while (rows.Count < count)
            {
                UIEventMilestoneRow row = Instantiate(rowTemplate, listContent);
                row.gameObject.SetActive(true);
                rows.Add(row);
            }

            for (int i = 0; i < rows.Count; i++)
                rows[i].gameObject.SetActive(i < count);

            return rows;
        }

        /// <summary>
        /// 다음에 열리는 줄이 목록 위쪽에 오게 스크롤한다. <b>줄을 바꾼 뒤 한 번만</b> 부른다 —
        /// 유저가 올려 본 스크롤을 매 갱신마다 되돌리면 목록을 못 읽는다.
        /// </summary>
        protected void ScrollListTo(int rowIndex)
        {
            if (listContent == null)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);

            var viewport = listContent.parent as RectTransform;
            float viewportHeight = viewport != null ? viewport.rect.height : 0f;
            float maxScroll = Mathf.Max(0f, listContent.rect.height - viewportHeight);
            float rowStep = UIEventMilestoneRow.RowSize.y + 6f;

            Vector2 position = listContent.anchoredPosition;
            position.y = Mathf.Clamp(rowIndex * rowStep - rowStep, 0f, maxScroll);
            listContent.anchoredPosition = position;
        }

        /// <summary>지급 팝업. 비어 있으면 띄우지 않고 <paramref name="onClose"/> 를 바로 부른다(닫힌 뒤에 이어 할 일이 있을 때).</summary>
        protected void ShowRewards(IReadOnlyList<PointRewardEntry> rewards, string message, Action onClose = null)
        {
            if (rewards == null || rewards.Count == 0)
            {
                if (onClose != null)
                    onClose();
                return;
            }

            UIRewardResultDialog dialog = GameContainer.UI != null ? GameContainer.UI.Get<UIRewardResultDialog>() : null;
            if (dialog == null)
            {
                Debug.LogError("[이벤트] 보상 창을 열지 못했다. DialogCatalog 에 UIRewardResultDialog 가 등재됐는지 확인할 것.");
                if (onClose != null)
                    onClose();
                return;
            }

            dialog.Open(rewards, message, onClose);
        }
    }
}
