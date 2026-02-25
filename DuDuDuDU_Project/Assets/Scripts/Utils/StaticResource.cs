using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace OJ
{
    [Serializable]
    public class ElementResource
    {
        public ElementType ElementType;
        public Sprite Icon;
        public Color Color; 
    }

    public class StaticResource : MonoSingleton<StaticResource>
    {
        public PointMetadataDatabase PointMetadataDatabase;
        [FormerlySerializedAs("BulletMetaDataDatabase")]
        public DiceMetaDataDatabase DiceMetaDataDatabase;
        public List<ElementResource> ElementResources;

        private Dictionary<ElementType, ElementResource> elementResourceMap = new Dictionary<ElementType, ElementResource>();
        protected override void Init()
        {
            BuildElementResourceMap();
        }

        private void BuildElementResourceMap()
        {
            elementResourceMap.Clear();
            foreach (var elementResource in ElementResources)
            {
                elementResourceMap[elementResource.ElementType] = elementResource;
            }
        }

        public ElementResource GetElementResource(ElementType elementType)
        {
            if (elementResourceMap.TryGetValue(elementType, out ElementResource elementResource))
            {
                return elementResource;
            }

            return null;
        }
    }
}
