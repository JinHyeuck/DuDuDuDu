using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIPointItem : MonoBehaviour
    {
        [Header("Resource Type")]
        public PointType pointType = PointType.Gold;

        [Header("UI Refs")]
        public Image BGImage;
        public Image Icon;
        public TMP_Text Amount;

        private void OnEnable()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged += OnPointChanged;

            RefreshAll();
        }

        private void OnDisable()
        {
            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        private void OnPointChanged(PointType changedType, int value)
        {
            if (changedType != pointType)
                return;

            RefreshValue();
        }

        public void RefreshAll()
        {
            RefreshMetadata();
            RefreshValue();
        }

        public void RefreshMetadata()
        {
            PointMetadataDatabase db = StaticResource.Instance.PointMetadataDatabase;
            PointMetadataDatabase.PointMetadata metadata = db != null ? db.Get(pointType) : null;

            if (Icon != null)
                Icon.sprite = metadata != null ? metadata.icon : null;
        }

        public void RefreshValue()
        {
            if (Amount == null || PointManager.Instance == null)
                return;

            Amount.SetText("{0}", PointManager.Instance.Get(pointType));
        }
    }
}
