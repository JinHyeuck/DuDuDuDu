using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using OJ.Dice;
using OJ.Point;
using OJ.UI;

namespace OJ.Hunting
{
    /// <summary>
    /// 재화도 다이스도 아닌 것을 결과창에 그릴 때 쓰는 한 칸. 아이콘과 수량이 전부다.
    /// </summary>
    public struct IconRewardView
    {
        public Sprite Icon;
        public int Amount;

        public IconRewardView(Sprite icon, int amount)
        {
            Icon = icon;
            Amount = amount;
        }
    }

    public class UIRewardResultDialog : DialogBase
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private RectTransform rewardRoot;
        [SerializeField] private UIRewardElement rewardElementTemplate;

        private readonly List<UIRewardElement> rewardElements = new List<UIRewardElement>();
        private Action closeAction;

        // ── 여러 줄 배치 ──────────────────────────────────────────────

        /// <summary>이만큼까지는 판을 늘려 보여 준다. 더 나오면 늘리지 않는다.</summary>
        private const int MaxVisibleRows = 5;

        private const float CellSpacing = 16f;

        /// <summary>
        /// 판이 화면보다 넓게 그려져 있어(2156 &gt; 1080) 열 수를 판 폭으로 계산하면
        /// <b>화면 밖에 칸을 놓는다</b> — 실제로 양끝 보석이 잘려 보였다. 그래서 캔버스 폭을
        /// 기준으로 잡고 좌우에 이만큼 비운다.
        /// </summary>
        private const float SideMargin = 80f;

        private GridLayoutGroup grid;

        /// <summary><see cref="rewardRoot"/> 를 감싼 배경 판. 줄 수에 따라 이것의 높이를 바꾼다.</summary>
        private RectTransform panelRect;

        /// <summary>판에서 칸이 차지하지 않는 위아래 여백. 프리팹에 적힌 값을 그대로 읽는다.</summary>
        private float verticalInset;

        /// <summary>
        /// 판의 <b>위쪽 모서리</b>. 늘어나도 이 값을 지킨다.
        ///
        /// "보상" 타이틀은 판의 <b>자식이 아니라 형제</b>이고 판의 위쪽 모서리에 걸쳐 놓여 있다.
        /// 판은 가운데 기준(pivot 0.5)이라 높이를 키우면 위아래로 함께 자라는데, 그러면
        /// 위 모서리가 타이틀을 지나쳐 올라가 <b>타이틀이 판 한가운데 떠 보인다.</b>
        /// 그래서 높이를 키운 만큼 아래로 내려 위 모서리를 제자리에 둔다.
        /// </summary>
        private float panelTopEdge;

        /// <summary>판 아래쪽이 화면 밖으로 나가지 않게 남겨 두는 여백.</summary>
        private const float BottomMargin = 100f;

        private Vector2 cellSize = new Vector2(230f, 230f);

        /// <summary>이번에 그릴 칸 수. 판 크기 조정은 <see cref="OnEnter"/> 로 미룬다.</summary>
        private int pendingCount;

        protected override void OnLoad()
        {
            if (rewardRoot == null && rewardElementTemplate != null)
                rewardRoot = rewardElementTemplate.transform.parent as RectTransform;

            if (rewardElementTemplate != null)
                rewardElementTemplate.gameObject.SetActive(false);

            SetupGrid();
        }

        /// <summary>
        /// 한 줄짜리 <see cref="HorizontalLayoutGroup"/> 대신 <see cref="GridLayoutGroup"/> 을 쓰게 만든다.
        ///
        /// <b>왜 프리팹이 아니라 코드인가.</b> 프리팹 YAML 은 손으로 고치지 않는 규약이고
        /// (AGENTS.md 절대 규칙 3), 이 프리팹에는 굽는 도구가 없다.
        ///
        /// <b>왜 같은 오브젝트에 안 붙이나.</b> <c>LayoutGroup</c> 에
        /// <see cref="DisallowMultipleComponent"/> 가 걸려 있어 <c>HorizontalLayoutGroup</c> 이
        /// 있는 오브젝트에는 <c>GridLayoutGroup</c> 이 <b>안 붙는다</b> —
        /// <c>AddComponent</c> 가 조용히 null 을 준다. 그래서 한 겹 안에 새 오브젝트를 만들고
        /// 칸을 거기에 담는다. 바깥의 <c>HorizontalLayoutGroup</c> 은 자식이 하나뿐이라
        /// 꺼 두면 아무 일도 하지 않는다.
        ///
        /// <b>칸이 적을 때 모양은 안 바뀐다.</b> <c>GridLayoutGroup</c> 은 칸 수가 열 수보다
        /// 적으면 그만큼만 폭을 잡고 <see cref="TextAnchor.MiddleCenter"/> 로 가운데 모은다 —
        /// 지금까지의 가로 한 줄과 같다. 넘칠 때만 아래로 접힌다.
        /// </summary>
        private void SetupGrid()
        {
            if (rewardRoot == null || grid != null)
                return;

            // 둘 다 켜져 있으면 서로 자식 위치를 덮어써 결과가 프레임마다 달라진다.
            var horizontal = rewardRoot.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
                horizontal.enabled = false;

            if (rewardElementTemplate != null)
            {
                var templateRect = rewardElementTemplate.transform as RectTransform;
                if (templateRect != null && templateRect.rect.width > 1f)
                    cellSize = templateRect.rect.size;
            }

            // 판과 여백은 <b>안쪽으로 한 겹 들어가기 전에</b> 읽는다.
            // rewardRoot 는 판에 스트레치로 붙어 있어 sizeDelta.y 가 곧 위아래 여백(음수)이다.
            panelRect = rewardRoot.parent as RectTransform;
            verticalInset = -rewardRoot.sizeDelta.y;

            if (panelRect != null)
                panelTopEdge = panelRect.anchoredPosition.y + panelRect.sizeDelta.y * 0.5f;

            grid = rewardRoot.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                var host = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
                host.layer = rewardRoot.gameObject.layer;

                var hostRect = (RectTransform)host.transform;
                hostRect.SetParent(rewardRoot, false);
                hostRect.anchorMin = Vector2.zero;
                hostRect.anchorMax = Vector2.one;
                hostRect.offsetMin = Vector2.zero;
                hostRect.offsetMax = Vector2.zero;

                grid = host.GetComponent<GridLayoutGroup>();

                // 이후 칸은 전부 이 안에 담긴다. EnsureRewardElements 는 그대로 둔다.
                rewardRoot = hostRect;
            }

            grid.cellSize = cellSize;
            grid.spacing = new Vector2(CellSpacing, CellSpacing);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
        }

        /// <summary>
        /// 칸 수에 맞춰 열 수를 정하고 배경 판을 세로로 늘린다.
        ///
        /// <b>활성화된 뒤에 부른다.</b> 캔버스 폭을 읽어야 하는데 꺼져 있는 동안에는
        /// 레이아웃이 아직 안 돌아 0 이 나올 수 있다.
        /// </summary>
        private void ApplyGridLayout(int count)
        {
            if (grid == null || rewardRoot == null || count <= 0)
                return;

            float usable = rewardRoot.rect.width;

            RectTransform canvasRect = GetComponentInParent<Canvas>()?.transform as RectTransform;
            if (canvasRect != null && canvasRect.rect.width > 1f)
                usable = Mathf.Min(usable, canvasRect.rect.width - SideMargin * 2f);

            int columns = Mathf.FloorToInt((usable + CellSpacing) / (cellSize.x + CellSpacing));
            columns = Mathf.Clamp(columns, 1, count);

            grid.constraintCount = columns;

            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            rows = Mathf.Min(rows, MaxVisibleRows);

            if (panelRect == null)
                return;

            // 위 모서리를 고정한 채 아래로만 자라므로, 화면 바닥까지가 쓸 수 있는 전부다.
            // 여기서 안 막으면 줄이 늘수록 아래쪽 칸이 화면 밖으로 나간다.
            if (canvasRect != null && canvasRect.rect.height > 1f)
            {
                float room = panelTopEdge + canvasRect.rect.height * 0.5f - BottomMargin;
                int fits = Mathf.FloorToInt((room - verticalInset + CellSpacing) / (cellSize.y + CellSpacing));
                rows = Mathf.Clamp(rows, 1, Mathf.Max(1, fits));
            }

            float height = rows * cellSize.y + (rows - 1) * CellSpacing + verticalInset;

            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, height);
            panelRect.anchoredPosition = new Vector2(
                panelRect.anchoredPosition.x, panelTopEdge - height * 0.5f);
        }

        protected override void OnEnter()
        {
            ApplyGridLayout(pendingCount);
        }

        protected override void OnExit()
        {
            Action callback = closeAction;
            closeAction = null;
            callback?.Invoke();
        }

        public void Open(IReadOnlyList<PointRewardEntry> rewards, string message, Action onClose = null)
        {
            Open(rewards, null, message, onClose);
        }

        /// <summary>
        /// 재화와 함께 <b>다이스</b>도 보여 준다. (다이스 언락)
        ///
        /// <b>새 칸을 만들지 않았다.</b> <c>UIRewardElement</c> 는 아이콘 + 숫자 칸이라
        /// 재화든 다이스든 같은 모양으로 들어간다 — 그쪽에 <c>BindDice</c> 를 더해
        /// 네 화면이 한 번에 따라오게 했다.
        ///
        /// <b>환급은 재화 칸으로 또 그리지 않는다.</b> 이미 가진 다이스가 재화로 돌아온
        /// 경우 그 사실을 말하는 것은 <b>딤 처리된 다이스 칸</b>이다. 같은 재화를 옆에
        /// 한 번 더 세우면 두 번 받은 것처럼 보인다.
        /// </summary>
        public void Open(
            IReadOnlyList<PointRewardEntry> rewards,
            IReadOnlyList<DiceRewardView> diceRewards,
            string message,
            Action onClose = null)
        {
            if (!_isLoaded)
                Load();

            closeAction = onClose;

            if (messageText != null)
                messageText.SetText(string.IsNullOrEmpty(message) ? "보상을 획득했습니다." : message);

            BindRewards(rewards, diceRewards);
            Enter();
        }

        /// <summary>
        /// 아이콘과 수량만으로 그리는 결과창. 보석뽑기가 쓴다.
        ///
        /// <b>왜 별도 경로인가.</b> 위 <see cref="Open(IReadOnlyList{PointRewardEntry}, string, Action)"/>
        /// 는 <c>PointType</c> 을 받는데 보석은 재화가 아니다(<c>gemId</c> 로 인벤토리에 쌓인다).
        /// 보석을 <c>PointType</c> 인 척 끼워 넣으면 <b>재화 목록에 없는 값</b>이 돌아다니게 된다.
        /// 칸(<see cref="UIRewardElement"/>)은 아이콘 + 숫자라 그대로 재사용된다.
        /// </summary>
        public void OpenIcons(IReadOnlyList<IconRewardView> items, string message, Action onClose = null)
        {
            if (!_isLoaded)
                Load();

            closeAction = onClose;

            if (messageText != null)
                messageText.SetText(string.IsNullOrEmpty(message) ? "보상을 획득했습니다." : message);

            int total = items != null ? items.Count : 0;
            pendingCount = total;
            EnsureRewardElements(total);

            for (int i = 0; i < rewardElements.Count; i++)
            {
                UIRewardElement rewardElement = rewardElements[i];
                if (rewardElement == null)
                    continue;

                bool shouldShow = i < total;
                rewardElement.gameObject.SetActive(shouldShow);

                if (shouldShow)
                    rewardElement.Bind(items[i].Icon, items[i].Amount, "x{0:#,##0}");
            }

            Enter();
        }

        private void BindRewards(IReadOnlyList<PointRewardEntry> rewards, IReadOnlyList<DiceRewardView> diceRewards)
        {
            List<PointRewardEntry> mergedRewards = PointRewardUtility.MergeRewards(rewards);
            int diceCount = diceRewards != null ? diceRewards.Count : 0;

            // 다이스 칸이 말해 줄 환급은 재화 목록에서 뺀다. 지급은 이미 끝났고
            // 여기서 하는 것은 <b>보여 주는 일</b>뿐이라, 빼도 받은 것은 그대로다.
            for (int i = 0; i < diceCount; i++)
            {
                DiceRewardView view = diceRewards[i];
                if (view.WasNew || view.RefundAmount <= 0)
                    continue;

                for (int j = 0; j < mergedRewards.Count; j++)
                {
                    if (mergedRewards[j].PointType != view.RefundCurrency)
                        continue;

                    int left = mergedRewards[j].Amount - view.RefundAmount;
                    if (left > 0)
                        mergedRewards[j] = new PointRewardEntry(view.RefundCurrency, left);
                    else
                        mergedRewards.RemoveAt(j);

                    break;
                }
            }

            int total = mergedRewards.Count + diceCount;
            pendingCount = total;
            EnsureRewardElements(total);

            for (int i = 0; i < rewardElements.Count; i++)
            {
                UIRewardElement rewardElement = rewardElements[i];
                if (rewardElement == null)
                    continue;

                bool shouldShow = i < total;
                rewardElement.gameObject.SetActive(shouldShow);
                if (!shouldShow)
                    continue;

                if (i < mergedRewards.Count)
                {
                    PointRewardEntry reward = mergedRewards[i];
                    rewardElement.Bind(PointRewardUtility.GetPointIcon(reward.PointType), reward.Amount, "x{0:#,##0}");
                    continue;
                }

                // 다이스는 재화 뒤에 이어 붙인다.
                DiceRewardView view = diceRewards[i - mergedRewards.Count];
                Sprite diceSprite = DiceMetaDataProvider.GetIcon(view.DiceType);

                if (view.WasNew)
                    rewardElement.BindDice(diceSprite);
                else
                    rewardElement.BindDice(diceSprite,
                        PointRewardUtility.GetPointIcon(view.RefundCurrency), view.RefundAmount);
            }
        }

        private void EnsureRewardElements(int count)
        {
            if (rewardElementTemplate == null || rewardRoot == null)
                return;

            while (rewardElements.Count < count)
            {
                UIRewardElement rewardElement = Object.Instantiate(rewardElementTemplate, rewardRoot);
                rewardElement.gameObject.SetActive(true);
                rewardElements.Add(rewardElement);
            }
        }
    }
}
