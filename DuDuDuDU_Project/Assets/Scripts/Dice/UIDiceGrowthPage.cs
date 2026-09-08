using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Point;
using OJ.UI;

namespace OJ.Dice
{
    public class UIDiceGrowthPage : DialogBase
    {
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIDiceGrowthItem itemPrefab;

        [Header("속성별 배경 라인")]
        // 다이스 3칸(= 한 속성 계열) 뒤에 가로줄을 하나씩 깐다. 위치는 그리드가
        // 실제로 앉힌 칸에서 읽는다 — 셀 크기·간격·정렬을 코드가 다시 계산하면
        // 프리팹에서 그 값을 만지는 순간 어긋난다. 줄은 LayoutElement.ignoreLayout 을
        // 달아 그리드 칸을 밀지 않고, 맨 뒤 형제로 놓여 다이스 아래에 깔린다.
        [SerializeField] private bool showRowLines = true;
        [SerializeField] private Sprite rowLineSprite;

        // 색은 행마다 다르다 — 그 속성(계열)의 색을 DiceMetaDataProvider.GetColor 에서
        // 그대로 가져온다(킹·2세대는 기본 원소 색으로 폴백). rowLineTint 는 그 위에
        // 곱해져 톤·알파를 한 곳에서 조절한다. useElementColor 를 끄면 rowLineTint 를
        // 색 그대로 쓴다.
        [SerializeField] private bool useElementColor = true;
        [SerializeField] private Color rowLineTint = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private float rowLineThickness = 4f;
        [SerializeField] private float rowLineYOffset = 0f;
        [SerializeField] private float rowLineWidthDelta = 0f;

        [SerializeField] private DiceType[] diceTypesToShow = new DiceType[]
        {
            DiceType.Normal,
            DiceType.Tornado,
            DiceType.KingNormal,
            DiceType.Fire,
            DiceType.ArmorBreak,
            DiceType.KingFire,
            DiceType.Ice,
            DiceType.Wind,
            DiceType.KingIce,
            DiceType.Thunder,
            DiceType.Time,
            DiceType.KingThunder,
            DiceType.Poison,
            DiceType.Stun,
            DiceType.KingPoison,
        };

        private readonly List<UIDiceGrowthItem> items = new List<UIDiceGrowthItem>();
        private readonly List<Image> rowLines = new List<Image>();
        private RectTransform rowLineRoot;

        protected override void OnEnter()
        {
            BuildIfNeeded();
            RefreshAll();
            BuildRowLines();

            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged += OnDiceLevelChanged;
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            // 구매 직후 목록이 그 자리에서 바뀌어야 한다. 안 걸면 팝업을 닫아도
            // 자물쇠가 그대로 남아 "샀는데 아무 일도 없었다"로 보인다.
            if (DiceOwnershipManager.Instance != null)
                DiceOwnershipManager.Instance.OnOwnershipChanged += OnOwnershipChanged;
        }

        protected override void OnExit()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged -= OnDiceLevelChanged;
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;

            if (DiceOwnershipManager.Instance != null)
                DiceOwnershipManager.Instance.OnOwnershipChanged -= OnOwnershipChanged;
        }

        private void BuildIfNeeded()
        {
            if (itemPrefab == null || listRoot == null || items.Count > 0)
                return;


            //foreach (DiceType diceType in System.Enum.GetValues(typeof(DiceType)))
            foreach (DiceType diceType in diceTypesToShow)
            {
                if (diceType == DiceType.Max)
                    continue;

                UIDiceGrowthItem item = Instantiate(itemPrefab, listRoot);
                item.Bind(diceType, OnClickItem);
                items.Add(item);
            }
        }

        public void RefreshAll()
        {
            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();
        }

        /// <summary>
        /// 속성 계열(가로 한 줄 = 다이스 3칸)마다 뒤에 가로줄을 하나씩 놓는다.
        ///
        /// 세로 위치는 <b>그리드가 실제로 앉힌 칸에서 읽는다</b>. 셀 크기·간격·정렬은
        /// 프리팹에 있고, 코드가 그 식을 하나 더 가지면 프리팹을 만지는 순간 어긋난다.
        /// 줄은 <see cref="rowLineRoot"/>(레이아웃 무시) 밑으로 들어가 그리드 칸을
        /// 밀지 않고, <c>Content</c> 의 맨 앞 형제라 다이스 아래에 깔린다.
        ///
        /// 행 수는 고정이라 여러 번 불러도 풀을 재사용하며 위치만 다시 잡는다.
        /// </summary>
        private void BuildRowLines()
        {
            RectTransform content = listRoot as RectTransform;
            if (content == null)
                return;

            if (!showRowLines || items.Count == 0)
            {
                if (rowLineRoot != null)
                    rowLineRoot.gameObject.SetActive(false);
                return;
            }

            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            int columns = grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? Mathf.Max(1, grid.constraintCount)
                : 3;

            // 그리드는 다음 레이아웃 패스에서야 칸을 앉힌다. 지금 좌표를 읽으려면 한 번 강제로 돌린다.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            EnsureRowLineRoot(content);
            rowLineRoot.gameObject.SetActive(true);

            float width = (grid != null
                ? grid.cellSize.x * columns + grid.spacing.x * (columns - 1)
                : content.rect.width) + rowLineWidthDelta;

            int rowCount = Mathf.CeilToInt(items.Count / (float)columns);
            Vector3[] corners = new Vector3[4];

            for (int row = 0; row < rowCount; row++)
            {
                RectTransform cell = items[row * columns].transform as RectTransform;
                if (cell == null)
                    continue;

                cell.GetWorldCorners(corners);
                Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
                float localY = rowLineRoot.InverseTransformPoint(worldCenter).y;

                Image line = GetRowLine(row);
                line.color = ResolveRowColor(items[row * columns].DiceType);
                RectTransform lineRect = line.rectTransform;
                lineRect.anchorMin = lineRect.anchorMax = lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.sizeDelta = new Vector2(width, rowLineThickness);
                lineRect.anchoredPosition = new Vector2(0f, localY + rowLineYOffset);
            }

            for (int i = rowCount; i < rowLines.Count; i++)
                rowLines[i].gameObject.SetActive(false);
        }

        private void EnsureRowLineRoot(RectTransform content)
        {
            if (rowLineRoot != null)
                return;

            GameObject go = new GameObject("RowLines", typeof(RectTransform), typeof(LayoutElement));
            go.layer = content.gameObject.layer;
            go.GetComponent<LayoutElement>().ignoreLayout = true;

            rowLineRoot = go.GetComponent<RectTransform>();
            rowLineRoot.SetParent(content, false);
            rowLineRoot.anchorMin = Vector2.zero;
            rowLineRoot.anchorMax = Vector2.one;
            rowLineRoot.offsetMin = Vector2.zero;
            rowLineRoot.offsetMax = Vector2.zero;
            rowLineRoot.pivot = new Vector2(0.5f, 0.5f);
            rowLineRoot.SetAsFirstSibling();
        }

        private Image GetRowLine(int index)
        {
            while (rowLines.Count <= index)
            {
                GameObject go = new GameObject("RowLine " + rowLines.Count, typeof(RectTransform), typeof(Image));
                go.layer = rowLineRoot.gameObject.layer;
                go.GetComponent<RectTransform>().SetParent(rowLineRoot, false);

                Image created = go.GetComponent<Image>();
                created.raycastTarget = false;
                rowLines.Add(created);
            }

            Image line = rowLines[index];
            line.gameObject.SetActive(true);
            line.sprite = rowLineSprite;
            line.type = rowLineSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return line;
        }

        /// <summary>
        /// 그 행이 속한 속성(계열)의 색. 킹·2세대 다이스도 <see cref="DiceMetaDataProvider.GetColor"/>
        /// 가 기본 원소 색으로 폴백하므로 행 첫 칸의 타입만 넘기면 된다.
        /// <see cref="rowLineTint"/> 를 곱해 톤·알파를 조절한다.
        /// </summary>
        private Color ResolveRowColor(DiceType rowType)
        {
            if (!useElementColor)
                return rowLineTint;

            return DiceMetaDataProvider.GetColor(rowType) * rowLineTint;
        }

        /// <summary>
        /// 카탈로그에서 꺼내 띄운다. (10.4)
        ///
        /// 예전에는 씬 인스턴스를 <c>[SerializeField]</c> 로 직접 가리켰고, 그 참조가 비면
        /// <b>아무 로그 없이</b> 아무 일도 일어나지 않았다 — 다이스를 눌렀는데 창이 안 뜨는
        /// 것이 배선 사고인지 기획인지 구분할 방법이 없었다.
        /// <see cref="UIService"/> 는 못 열면 사유를 로그로 남긴다.
        ///
        /// <c>Show</c> 가 아니라 <c>Get</c> 인 이유는 <c>Open</c> 이 어떤 다이스인지 받아
        /// 넣은 뒤 스스로 <c>Enter</c> 를 부르기 때문이다.
        /// </summary>
        private void OnClickItem(DiceType diceType)
        {
            // <b>보유든 미보유든 창은 하나다.</b> 예전에는 미보유일 때 언락 팝업을 따로
            // 겹쳐 띄웠는데, 그러면 "얼마나 센가"(이 창)와 "어떻게 얻나"(팝업)가 갈라져
            // 유저가 둘을 나란히 못 본다. 상세창의 비용 칸이 해금 비용으로 바뀌어 끼워지고,
            // 강화 버튼이 구매 버튼이 된다 — UIDiceGrowthDetailPanel.RefreshCostSection 참조.
            UIDiceGrowthDetailPanel detailPanel = GameContainer.UI?.Get<UIDiceGrowthDetailPanel>();
            if (detailPanel != null)
                detailPanel.Open(diceType, RefreshAll);
        }

        private void OnOwnershipChanged(DiceType diceType)
        {
            RefreshAll();
        }

        private void OnDiceLevelChanged(DiceType diceType, int level)
        {
            RefreshAll();
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            RefreshAll();
        }

    }
}
