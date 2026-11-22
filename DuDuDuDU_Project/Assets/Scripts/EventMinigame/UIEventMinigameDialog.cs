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

        private readonly List<UIEventMilestoneRow> rows = new List<UIEventMilestoneRow>();
        private float nextHeaderRefresh;

        /// <summary>이 화면이 맡은 이벤트.</summary>
        protected abstract EventKind Kind { get; }

        protected static DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
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

            OnLoadPlay();
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

        protected void SetListVisible(bool visible)
        {
            if (listRoot != null)
                listRoot.SetActive(visible);

            if (listContent != null)
                listContent.parent.gameObject.SetActive(visible);
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

        /// <summary>지급 팝업. 비어 있으면 띄우지 않는다.</summary>
        protected void ShowRewards(IReadOnlyList<PointRewardEntry> rewards, string message)
        {
            if (rewards == null || rewards.Count == 0)
                return;

            UIRewardResultDialog dialog = GameContainer.UI != null ? GameContainer.UI.Get<UIRewardResultDialog>() : null;
            if (dialog == null)
            {
                Debug.LogError("[이벤트] 보상 창을 열지 못했다. DialogCatalog 에 UIRewardResultDialog 가 등재됐는지 확인할 것.");
                return;
            }

            dialog.Open(rewards, message);
        }
    }
}
