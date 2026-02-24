using UnityEngine;
using UnityEngine.Serialization;

namespace OJ
{
    public class StaticResource : MonoSingleton<StaticResource>
    {
        public DiceTypeResourceManager DiceTypeResourceManager;
        public PointMetadataDatabase PointMetadataDatabase;
        [FormerlySerializedAs("BulletMetaDataDatabase")]
        public DiceMetaDataDatabase DiceMetaDataDatabase;
    }
}
