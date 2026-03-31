using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OJ
{
    public class ElementUpgradeManager : MonoBehaviour
    {
        public static ElementUpgradeManager Instance;
        [SerializeField] private Button ElementUpgrade;
        [SerializeField] private Image coinIconImage;
        [SerializeField] private TMP_Text coinAmountText;
        [SerializeField] private UIElementUpgradePanel elementUpgradePanel;

        private readonly Dictionary<ElementType, int> levels = new Dictionary<ElementType, int>();

        public event Action<ElementType, int> OnElementLevelChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            ResetAll(false);
            BindUI();
            RefreshCoinUI();
        }

        private void OnDestroy()
        {
            UnbindUI();

            if (Instance == this)
                Instance = null;
        }

        public int GetLevel(ElementType elementType)
        {
            if (elementType == ElementType.Max)
                return 0;

            return levels.TryGetValue(elementType, out int level) ? Mathf.Max(0, level) : 0;
        }

        public void SetLevel(ElementType elementType, int level)
        {
            if (elementType == ElementType.Max)
                return;

            int clamped = Mathf.Max(0, level);
            levels[elementType] = clamped;
            OnElementLevelChanged?.Invoke(elementType, clamped);
        }

        public int GetNextUpgradeCost(ElementType elementType)
        {
            return GetLevel(elementType) + 1;
        }

        public bool TryLevelUp(ElementType elementType)
        {
            if (elementType == ElementType.Max || PointManager.Instance == null)
                return false;

            int cost = GetNextUpgradeCost(elementType);
            if (!PointManager.Instance.TrySpend(PointType.Coin, cost))
                return false;

            SetLevel(elementType, GetLevel(elementType) + 1);
            return true;
        }

        public void ResetRunState()
        {
            ResetAll();

            if (PointManager.Instance != null)
                PointManager.Instance.Set(PointType.Coin, 0);
            else
                RefreshCoinUI();
        }

        public void ResetAll(bool notify = true)
        {
            foreach (ElementType elementType in Enum.GetValues(typeof(ElementType)))
            {
                if (elementType == ElementType.Max)
                    continue;

                levels[elementType] = 0;
                if (notify)
                    OnElementLevelChanged?.Invoke(elementType, 0);
            }
        }

        public float GetTotalBonusMultiplier(DiceType diceType)
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(diceType);
            if (meta == null || meta.elementType == null || meta.elementType.Length == 0)
                return 1f;

            int totalLevel = 0;
            for (int i = 0; i < meta.elementType.Length; i++)
            {
                ElementType elementType = meta.elementType[i];
                if (elementType == ElementType.Max)
                    continue;

                totalLevel += GetLevel(elementType);
            }

            return 1f + (totalLevel * 0.1f);
        }

        public float GetTotalBonusPercent(DiceType diceType)
        {
            return Mathf.Max(0f, (GetTotalBonusMultiplier(diceType) - 1f) * 100f);
        }

        private void BindUI()
        {
            if (ElementUpgrade != null)
            {
                ElementUpgrade.onClick.RemoveListener(OnClickElementUpgrade);
                ElementUpgrade.onClick.AddListener(OnClickElementUpgrade);
            }

            if (PointManager.Instance != null)
            {
                PointManager.Instance.OnPointChanged -= OnPointChanged;
                PointManager.Instance.OnPointChanged += OnPointChanged;
            }
        }

        private void UnbindUI()
        {
            if (ElementUpgrade != null)
                ElementUpgrade.onClick.RemoveListener(OnClickElementUpgrade);

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        private void OnClickElementUpgrade()
        {
            elementUpgradePanel?.Open();
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            if (pointType != PointType.Coin)
                return;

            RefreshCoinUI();
        }

        private void RefreshCoinUI()
        {
            if (coinAmountText != null)
                coinAmountText.SetText("{0}", PointManager.Instance != null ? PointManager.Instance.Get(PointType.Coin) : 0);

            if (coinIconImage != null && StaticResource.Instance != null && StaticResource.Instance.PointMetadataDatabase != null)
            {
                PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(PointType.Coin);
                coinIconImage.sprite = metadata != null ? metadata.icon : null;
            }
        }
    }
}
