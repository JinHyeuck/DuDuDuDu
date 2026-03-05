using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class UIDiceGrowthPage : IDialog
    {
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIDiceGrowthItem itemPrefab;
        [SerializeField] private UIDiceGrowthDetailPanel detailPanel;
        [SerializeField] private LobbyLayoutController lobbyLayoutController;

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

        public override void BackKeyCall()
        {
            lobbyLayoutController?.ShowTab(LobbyTab.Home);
        }

        private void BuildIfNeeded()
        {
            if (itemPrefab == null || listRoot == null || items.Count > 0)
                return;

            for (int i = DiceType.Normal.Enum32ToInt(); i < DiceType.Max.Enum32ToInt(); i++)
            {
                DiceType diceType = i.IntToEnum32<DiceType>();
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

        private void OnClickItem(DiceType diceType)
        {
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
