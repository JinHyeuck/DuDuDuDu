using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class UIBulletGrowthPage : IDialog
    {
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIBulletGrowthItem itemPrefab;
        [SerializeField] private UIBulletGrowthDetailPanel detailPanel;

        private readonly List<UIBulletGrowthItem> items = new List<UIBulletGrowthItem>();

        protected override void OnEnter()
        {
            BuildIfNeeded();
            RefreshAll();

            if (BulletLevelManager.Instance != null)
                BulletLevelManager.Instance.OnBulletLevelChanged += OnBulletLevelChanged;
        }

        protected override void OnExit()
        {
            if (BulletLevelManager.Instance != null)
                BulletLevelManager.Instance.OnBulletLevelChanged -= OnBulletLevelChanged;
        }

        private void BuildIfNeeded()
        {
            if (itemPrefab == null || listRoot == null || items.Count > 0)
                return;

            for (int i = DiceType.Normal.Enum32ToInt(); i < DiceType.Max.Enum32ToInt(); i++)
            {
                DiceType diceType = i.IntToEnum32<DiceType>();
                UIBulletGrowthItem item = Instantiate(itemPrefab, listRoot);
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

        private void OnBulletLevelChanged(DiceType diceType, int level)
        {
            RefreshAll();
        }
    }
}
