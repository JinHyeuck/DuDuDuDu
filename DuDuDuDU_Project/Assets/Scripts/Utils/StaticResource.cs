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

    [Serializable]
    public class RarityResource
    {
        public Rarity Rarity;
        public Color Color; 
    }

    public class StaticResource : MonoSingleton<StaticResource>
    {
        public PointMetadataDatabase PointMetadataDatabase;
        public DiceMetaDataDatabase DiceMetaDataDatabase;
        public GemDefinitionDatabase GemDefinitionDatabase;
        public StageDatabase StageDatabase;
        [FormerlySerializedAs("ChapterRewardDatabase")]
        public StageRewardDatabase StageRewardDatabase;
        public List<ElementResource> ElementResources;
        
        public List<RarityResource> RarityResources;

        private Dictionary<ElementType, ElementResource> elementResourceMap = new Dictionary<ElementType, ElementResource>();
        private Dictionary<Rarity, RarityResource> rarityResourceMap = new Dictionary<Rarity, RarityResource>();

        protected override void Init()
        {
            BuildElementResourceMap();
            BuildRarityResourceMap();
        }

        private void BuildElementResourceMap()
        {
            elementResourceMap.Clear();
            foreach (var elementResource in ElementResources)
            {
                elementResourceMap[elementResource.ElementType] = elementResource;
            }
        }

        private void BuildRarityResourceMap()
        {
            rarityResourceMap.Clear();
            foreach (var rarityResource in RarityResources)
            {
                rarityResourceMap[rarityResource.Rarity] = rarityResource;
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

        public RarityResource GetRarityResource(Rarity rarity)
        {
            if (rarityResourceMap.TryGetValue(rarity, out RarityResource rarityResource))
            {
                return rarityResource;
            }

            return null;
        }
    }
}
