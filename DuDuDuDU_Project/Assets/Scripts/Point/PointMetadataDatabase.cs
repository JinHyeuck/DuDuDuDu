using System.Collections.Generic;
using UnityEngine;

namespace OJ.Point
{
    [CreateAssetMenu(fileName = "PointMetadataDatabase", menuName = "Point/Metadata Database")]
    public class PointMetadataDatabase : ScriptableObject
    {
        [System.Serializable]
        public class PointMetadata
        {
            public PointType pointType;
            public string displayName;
            [TextArea(2, 4)] public string description;
            public Sprite icon;
        }

        [SerializeField] private List<PointMetadata> metadataList = new List<PointMetadata>();

        private readonly Dictionary<PointType, PointMetadata> metadataMap = new Dictionary<PointType, PointMetadata>();

        private void OnEnable()
        {
            RebuildMap();
        }

        public PointMetadata Get(PointType pointType)
        {
            if (metadataMap.Count != metadataList.Count)
                RebuildMap();

            metadataMap.TryGetValue(pointType, out PointMetadata metadata);
            return metadata;
        }

        public bool TryGet(PointType pointType, out PointMetadata metadata)
        {
            if (metadataMap.Count != metadataList.Count)
                RebuildMap();

            return metadataMap.TryGetValue(pointType, out metadata);
        }

        private void RebuildMap()
        {
            metadataMap.Clear();

            for (int i = 0; i < metadataList.Count; i++)
            {
                PointMetadata metadata = metadataList[i];
                if (metadata == null)
                    continue;

                metadataMap[metadata.pointType] = metadata;
            }
        }
    }
}
