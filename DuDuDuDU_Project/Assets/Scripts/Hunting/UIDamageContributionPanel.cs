using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.DI;
using OJ.UI;

namespace OJ.Hunting
{
    /// <summary>
    /// 전투 화면 왼쪽에 상시로 떠 있는 <b>실시간 기여도</b> 패널.
    /// 본편과 무한의 탑이 같이 쓴다.
    ///
    /// <b>아이콘과 막대뿐이다.</b> 전투 중에 곁눈으로 보는 것이라 읽어야 하는 글자가
    /// 있으면 시선을 뺏긴다 — 어느 다이스인지는 아이콘이, 얼마나 때렸는지는 막대 길이가
    /// 말한다. 정확한 숫자가 필요한 자리는 결과 화면이다.
    ///
    /// <b>왜 다이얼로그인가.</b> 상시 UI 라면 씬에 놓는 것이 자연스럽지만, 그러려면
    /// <c>BattleScene.unity</c> 를 편집해야 한다(AGENTS 절대 규칙 3).
    /// <c>UIService</c> 는 자기 캔버스를 런타임에 만들어 주므로 씬을 한 글자도
    /// 안 건드리고 같은 자리에 띄울 수 있다 — <c>UIBountyBanner</c> 와 같은 판단이다.
    ///
    /// <b>루트를 화면 전체로 늘리지 않는다.</b> 늘리면 그 위의 <c>GraphicRaycaster</c> 가
    /// 보드 클릭을 통째로 먹는다. 패널 크기만큼만 차지해야 아래 다이스 조작이 살아 있다.
    ///
    /// <b>접으면 그 자리에 여는 버튼이 남는다.</b> 완전히 사라지게 두면 다시 볼 방법이
    /// 없어지고, 그러면 "닫기" 가 아니라 "영영 끄기" 가 된다.
    /// </summary>
    public class UIDamageContributionPanel : DialogBase
    {
        /// <summary>
        /// 한 번에 보여 주는 다이스 수. 편성이 최대 7개(탑)이고 본편 보드도 종류가
        /// 그 언저리라, 넘치는 일은 드물다. 그래도 상한을 두는 것은 <b>세로로 무한히
        /// 자라지 않게</b> 하기 위해서다 — 왼쪽을 다 덮으면 전투가 안 보인다.
        /// </summary>
        private const int MaxRows = 8;

        /// <summary>
        /// 다시 그리는 주기(초).
        ///
        /// <b>매 타격마다 그리지 않는다.</b> 트래커는 타격마다 이벤트를 울리는데
        /// 그 빈도가 초당 수십 번이라, 그때마다 여덟 줄을 다시 채우면 전투 중에
        /// 프레임을 깎는다. 사람 눈에는 0.2초 간격이면 충분히 "실시간" 이다.
        /// </summary>
        private const float RefreshInterval = 0.2f;

        [Inject] private IBattleRefs battle;

        [Header("펼친 모습")]
        [SerializeField] private GameObject expandedRoot;
        [SerializeField] private Button collapseArea;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private List<UIContributionRow> rows = new List<UIContributionRow>();

        [Header("접힌 모습")]
        [SerializeField] private GameObject collapsedRoot;
        [SerializeField] private Button openButton;

        /// <summary>
        /// 접어 둔 상태를 <b>세션 동안</b> 기억한다.
        ///
        /// <c>static</c> 인 것이 요점이다. 이 창은 씬이 바뀔 때마다 새로 만들어지므로
        /// (<c>UIService</c> 가 씬과 함께 캐시를 버린다) 인스턴스 필드로 두면
        /// <b>전투를 시작할 때마다 다시 펼쳐진다</b> — 한 번 닫은 사람에게는 그게 고장이다.
        ///
        /// <b>세이브에는 넣지 않았다.</b> 앱을 껐다 켜면 다시 보이는데, 그 정도가
        /// 이 값의 무게에 맞다 — 세이브 형식을 UI 취향 하나 때문에 바꾸지 않는다.
        /// </summary>
        private static bool collapsedInSession;

        private readonly List<DiceDamageShare> shareBuffer = new List<DiceDamageShare>();

        private DamageContributionTracker subscribed;
        private bool dirty;
        private float refreshTimer;

        protected override void OnLoad()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Collapse);

            // 패널 영역을 눌러도 접힌다. 작은 X 만으로는 손가락으로 맞추기 어렵고,
            // 이 패널에는 <b>누를 것이 따로 없어서</b> 영역 전체를 닫기 버튼으로 써도
            // 잃는 조작이 없다.
            if (collapseArea != null)
                collapseArea.onClick.AddListener(Collapse);

            if (openButton != null)
                openButton.onClick.AddListener(Expand);
        }

        protected override void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Collapse);

            if (collapseArea != null)
                collapseArea.onClick.RemoveListener(Collapse);

            if (openButton != null)
                openButton.onClick.RemoveListener(Expand);

            Unsubscribe();
            base.OnDestroy();
        }

        protected override void OnEnter()
        {
            Subscribe();
            ApplyCollapsed(collapsedInSession);
            dirty = true;
            refreshTimer = 0f;
            Refresh();
        }

        protected override void OnExit()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            DamageContributionTracker tracker = battle != null ? battle.Contribution : null;
            if (tracker == null)
            {
                // <b>조용히 넘어가지 않는다.</b> 이 창이 뜨는 곳은 전투 씬뿐이고 거기서
                // 창구가 비어 있으면 그것은 사고다. 그냥 지나가면 증상이
                // "막대가 하나도 안 나온다" 로만 보이고, 원인이 배선이라는 것을
                // 화면에서는 알 수 없다.
                Debug.LogError("[기여도] 추적기가 없다. 배틀 스코프가 Contribution 을 " +
                               "채웠는지 볼 것 — 이 창은 전투 씬에서만 떠야 한다.");
                return;
            }

            if (subscribed == tracker)
                return;

            Unsubscribe();
            tracker.OnChanged += MarkDirty;
            subscribed = tracker;
        }

        private void Unsubscribe()
        {
            if (subscribed == null)
                return;

            subscribed.OnChanged -= MarkDirty;
            subscribed = null;
        }

        /// <summary>
        /// 타격이 있었다는 표시만 남긴다. 실제로 다시 그리는 것은
        /// <see cref="Update"/> 가 주기마다 한다 — 위 <see cref="RefreshInterval"/> 참조.
        /// </summary>
        private void MarkDirty()
        {
            dirty = true;
        }

        private void Update()
        {
            if (!isEnter || collapsedInSession || !dirty)
                return;

            // 실제 시간으로 잰다. 배속(x2/x3)에서 갱신이 같이 빨라질 이유가 없고,
            // 오히려 배속일수록 그리는 횟수를 줄이는 편이 맞다.
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < RefreshInterval)
                return;

            refreshTimer = 0f;
            dirty = false;
            Refresh();
        }

        // ── 접기 / 펼치기 ───────────────────────────────────────────────

        private void Collapse()
        {
            collapsedInSession = true;
            ApplyCollapsed(true);
        }

        private void Expand()
        {
            collapsedInSession = false;
            ApplyCollapsed(false);

            // 접혀 있는 동안 들어온 피해가 있으므로 즉시 다시 그린다.
            // 주기를 기다리면 여는 순간 <b>지난 내용</b>이 한 번 보인다.
            dirty = false;
            refreshTimer = 0f;
            Refresh();
        }

        private void ApplyCollapsed(bool collapsed)
        {
            if (expandedRoot != null)
                expandedRoot.SetActive(!collapsed);

            if (collapsedRoot != null)
                collapsedRoot.SetActive(collapsed);
        }

        // ── 그리기 ──────────────────────────────────────────────────────

        private void Refresh()
        {
            DamageContributionTracker tracker = battle != null ? battle.Contribution : null;
            if (tracker == null)
                return;

            tracker.FillShares(shareBuffer);

            if (emptyText != null)
                emptyText.gameObject.SetActive(shareBuffer.Count == 0);

            for (int i = 0; i < rows.Count; i++)
            {
                UIContributionRow row = rows[i];
                if (row == null)
                    continue;

                if (i < shareBuffer.Count && i < MaxRows)
                    row.Bind(shareBuffer[i]);
                else
                    row.Hide();
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // 값을 아는 것은 코드이므로 인스펙터에 좌표를 옮겨 적지 않는다.
        // ──────────────────────────────────────────────────────────────

        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.14f, 0.82f);
        private static readonly Color CloseColor = new Color(0.42f, 0.20f, 0.24f, 0.95f);
        private static readonly Color OpenColor = new Color(0.20f, 0.23f, 0.42f, 0.92f);
        private static readonly Color MutedText = new Color(0.70f, 0.76f, 0.88f, 1f);

        /// <summary>
        /// 패널 가로. 이름과 퍼센트를 뺐으므로 아이콘 + 막대만 들어가면 된다 —
        /// 좁을수록 전투를 덜 가린다.
        /// </summary>
        private const float PanelWidth = 240f;

        private const float RowHeight = 40f;
        private const float HeaderHeight = 44f;
        private const float Padding = 10f;

        /// <summary>
        /// 왼쪽 가장자리에서 얼마나 띄울지. 1080 폭 기준이고, 패널이 보드(가운데)와
        /// 겹치지 않는 자리다.
        /// </summary>
        private const float LeftMargin = 16f;

        /// <summary>
        /// 화면 세로 중심에서 얼마나 올릴지.
        ///
        /// 위로는 웨이브 게이지, 아래로는 다이스 보드가 있다. 그 사이의 빈 왼쪽
        /// 영역이 이 자리다 — 현상금 선택 창(중심 +300)과 겹치지 않게 조금 더 위로 올렸다.
        /// </summary>
        private const float CenterY = 430f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UIDamageContributionPanel Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = CreateRect("UIDamageContributionPanel", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();

            // <b>화면 전체로 늘리지 않는다.</b> 늘리면 이 캔버스의 레이캐스터가
            // 보드 클릭을 통째로 먹는다. 왼쪽 가장자리에 붙여 패널 크기만 차지한다.
            rootRect.anchorMin = new Vector2(0f, 0.5f);
            rootRect.anchorMax = new Vector2(0f, 0.5f);
            rootRect.pivot = new Vector2(0f, 0.5f);
            rootRect.sizeDelta = new Vector2(PanelWidth, BodyHeight);
            rootRect.anchoredPosition = new Vector2(LeftMargin, CenterY);

            GameObject view = CreateRect("DialogView", root.transform);
            RectTransform viewRect = view.GetComponent<RectTransform>();
            Stretch(viewRect);

            var panel = root.AddComponent<UIDamageContributionPanel>();
            panel.dialogView = view;

            // 백키로 닫지 않는다. 전투 중 백키는 다른 뜻이고(일시정지·창 닫기),
            // 이 패널은 접기 버튼이 따로 있다.
            panel.UseBackBtn = false;

            BuildExpanded(panel, view.transform, font);
            BuildCollapsed(panel, view.transform, font);

            // 굽는 시점의 기본 모습은 <b>펼침</b>이다. 처음 켠 사람에게는 보여야 한다.
            panel.ApplyCollapsed(false);

            return panel;
        }

        private static float BodyHeight => HeaderHeight + MaxRows * RowHeight + Padding * 2f;

        private static void BuildExpanded(
            UIDamageContributionPanel panel, Transform parent, TMP_FontAsset font)
        {
            GameObject expanded = CreateRect("Expanded", parent);
            Stretch(expanded.GetComponent<RectTransform>());
            panel.expandedRoot = expanded;

            // 배경이 곧 "영역을 누르면 접힌다" 의 그 영역이다. 투명도를 조금 남겨
            // 뒤의 전투가 비쳐 보이게 한다 — 왼쪽을 완전히 막으면 몬스터가 안 보인다.
            Image background = CreateImage("Background", expanded.transform, PanelColor);
            Stretch(background.rectTransform);

            panel.collapseArea = background.gameObject.AddComponent<Button>();
            panel.collapseArea.targetGraphic = background;
            panel.collapseArea.transition = Selectable.Transition.None;

            float top = BodyHeight * 0.5f;

            TMP_Text title = CreateText("Title", expanded.transform, "피해 기여도", 22f,
                TextAlignmentOptions.Left, Color.white, font);
            SetRect(title.rectTransform,
                new Vector2(PanelWidth - Padding * 2f - 44f, HeaderHeight),
                new Vector2(-22f, top - HeaderHeight * 0.5f));

            // 작은 X. 영역 전체가 이미 닫기지만, <b>닫을 수 있다는 사실</b>을 알리는 것이
            // 이 버튼의 일이다 — 영역 클릭만 두면 아무도 그것을 모른다.
            Image close = CreateImage("CloseButton", expanded.transform, CloseColor);
            SetRect(close.rectTransform, new Vector2(34f, 34f),
                new Vector2(PanelWidth * 0.5f - Padding - 17f, top - Padding - 17f));

            panel.closeButton = close.gameObject.AddComponent<Button>();
            panel.closeButton.targetGraphic = close;

            TMP_Text closeLabel = CreateText("Label", close.transform, "×", 24f,
                TextAlignmentOptions.Center, Color.white, font);
            SetRect(closeLabel.rectTransform, new Vector2(34f, 34f), Vector2.zero);

            float rowTop = top - HeaderHeight - Padding * 0.5f;
            var rowSize = new Vector2(PanelWidth - Padding * 2f, RowHeight);

            panel.emptyText = CreateText("Empty", expanded.transform, "아직 피해 없음", 20f,
                TextAlignmentOptions.Center, MutedText, font);
            SetRect(panel.emptyText.rectTransform, rowSize,
                new Vector2(0f, rowTop - RowHeight * 0.5f));

            for (int i = 0; i < MaxRows; i++)
            {
                UIContributionRow row = UIContributionRow.Create(
                    expanded.transform,
                    rowSize,
                    new Vector2(0f, rowTop - RowHeight * 0.5f - i * RowHeight));

                panel.rows.Add(row);
            }
        }

        private static void BuildCollapsed(
            UIDamageContributionPanel panel, Transform parent, TMP_FontAsset font)
        {
            const float ButtonWidth = 180f;
            const float ButtonHeight = 52f;

            GameObject collapsed = CreateRect("Collapsed", parent);
            RectTransform collapsedRect = collapsed.GetComponent<RectTransform>();

            // 펼친 패널의 <b>맨 윗줄 자리</b>에 놓는다. 접었다 폈을 때 버튼과 패널이
            // 같은 곳에서 나타나고 사라져야 "그 자리에 있던 것" 으로 읽힌다.
            collapsedRect.anchorMin = new Vector2(0f, 1f);
            collapsedRect.anchorMax = new Vector2(0f, 1f);
            collapsedRect.pivot = new Vector2(0f, 1f);
            collapsedRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            collapsedRect.anchoredPosition = Vector2.zero;

            panel.collapsedRoot = collapsed;

            Image background = CreateImage("Background", collapsed.transform, OpenColor);
            Stretch(background.rectTransform);

            panel.openButton = background.gameObject.AddComponent<Button>();
            panel.openButton.targetGraphic = background;

            TMP_Text label = CreateText("Label", collapsed.transform, "기여도 확인", 22f,
                TextAlignmentOptions.Center, Color.white, font);
            Stretch(label.rectTransform);
        }

        // ── 조립 도구 ───────────────────────────────────────────────────
        //
        // <c>UITowerUIFactory</c> 를 쓰지 않는다. 저것은 <c>OJ.Tower</c> 의 internal 이고,
        // 본편 전투 UI 가 탑 어셈블리 내부에 손을 뻗는 것은 방향이 거꾸로다.
        // 필요한 것이 넷뿐이라 여기 둔다. <c>UIContributionRow</c> 도 이것을 쓴다.

        private const int UILayer = 5;

        internal static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static TMP_Text CreateText(
            string name, Transform parent, string text, float size,
            TextAlignmentOptions align, Color color, TMP_FontAsset font)
        {
            TextMeshProUGUI label = CreateRect(name, parent).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.font = font;

            // 글자는 클릭을 먹지 않는다. 켜 두면 글자 위를 눌렀을 때 배경의
            // "눌러서 접기" 가 안 먹는 자리가 생긴다.
            label.raycastTarget = false;
            return label;
        }

        internal static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
