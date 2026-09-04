using System.Collections.Generic;
using UnityEngine;
using OJ.DI;
using OJ.Point;
using OJ.UI;

namespace OJ.Dice
{
    public class UIDiceGrowthPage : DialogBase
    {
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIDiceGrowthItem itemPrefab;

        private readonly List<UIDiceGrowthItem> items = new List<UIDiceGrowthItem>();

        protected override void OnEnter()
        {
            BuildIfNeeded();
            RefreshAll();

            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged += OnDiceLevelChanged;
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;
        }

        protected override void OnExit()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged -= OnDiceLevelChanged;
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        private void BuildIfNeeded()
        {
            if (itemPrefab == null || listRoot == null || items.Count > 0)
                return;

            foreach (DiceType diceType in System.Enum.GetValues(typeof(DiceType)))
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
            UIDiceGrowthDetailPanel detailPanel = GameContainer.UI?.Get<UIDiceGrowthDetailPanel>();
            if (detailPanel != null)
                detailPanel.Open(diceType, RefreshAll);
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
