using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIElementUpgradePanel : IDialog
    {
        [Header("Header")]
        [SerializeField] private Image coinIconImage;
        [SerializeField] private TMP_Text coinAmountText;

        [Header("List")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIElementUpgradeItem itemPrefab;

        private readonly List<UIElementUpgradeItem> items = new List<UIElementUpgradeItem>();
        private readonly ElementType[] elementOrder =
        {
            ElementType.Normal,
            ElementType.Fire,
            ElementType.Water,
            ElementType.Light,
            ElementType.Dark
        };

        protected override void OnLoad()
        {
            BuildIfNeeded();
        }

        protected override void OnEnter()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            if (ElementUpgradeManager.Instance != null)
                ElementUpgradeManager.Instance.OnElementLevelChanged += OnElementLevelChanged;

            Refresh();
        }

        protected override void OnExit()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;

            if (ElementUpgradeManager.Instance != null)
                ElementUpgradeManager.Instance.OnElementLevelChanged -= OnElementLevelChanged;
        }

        public void Open()
        {
            Enter();
            Refresh();
        }

        public void Refresh()
        {
            RefreshHeader();

            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();
        }

        private void BuildIfNeeded()
        {
            if (listRoot == null || itemPrefab == null || items.Count > 0)
                return;

            for (int i = 0; i < elementOrder.Length; i++)
            {
                UIElementUpgradeItem item = Instantiate(itemPrefab, listRoot);
                item.Bind(elementOrder[i], OnClickUpgrade);
                items.Add(item);
            }
        }

        private void RefreshHeader()
        {
            if (coinAmountText != null)
                coinAmountText.SetText("{0}", PointManager.Instance != null ? PointManager.Instance.Get(PointType.Coin) : 0);

            if (coinIconImage != null && StaticResource.Instance != null && StaticResource.Instance.PointMetadataDatabase != null)
            {
                PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(PointType.Coin);
                coinIconImage.sprite = metadata != null ? metadata.icon : null;
            }
        }

        private void OnClickUpgrade(ElementType elementType)
        {
            if (ElementUpgradeManager.Instance == null)
                return;

            if (ElementUpgradeManager.Instance.TryLevelUp(elementType))
                Refresh();
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            if (pointType == PointType.Coin)
                Refresh();
        }

        private void OnElementLevelChanged(ElementType elementType, int level)
        {
            Refresh();
        }
    }
}
