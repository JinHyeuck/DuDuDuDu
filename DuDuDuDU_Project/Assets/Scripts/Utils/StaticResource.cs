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

    [Serializable]
    public class EquipmentResource
    {
        public EquipmentType EquipmentType;
        public Sprite LargeIcon;
        public Sprite SmallIcon;
    }

        [Serializable]
    public class StageUIResource
    {
        public int StageResourceId;
        public Sprite MainBanner;
        public Sprite StarRewardBanner;
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
        public List<EquipmentResource> EquipmentResources;

        public List<StageUIResource> StageUIResources;

        private Dictionary<ElementType, ElementResource> elementResourceMap = new Dictionary<ElementType, ElementResource>();
        private Dictionary<Rarity, RarityResource> rarityResourceMap = new Dictionary<Rarity, RarityResource>();
        private Dictionary<EquipmentType, EquipmentResource> equipmentResourceMap = new Dictionary<EquipmentType, EquipmentResource>();

        protected override void Init()
        {
            BuildElementResourceMap();
            BuildRarityResourceMap();
            BuildEquipmentResourceMap();
        }

        private void BuildElementResourceMap()
        {
            elementResourceMap.Clear();
            if (ElementResources == null)
                return;

            foreach (var elementResource in ElementResources)
            {
                if (elementResource == null)
                    continue;

                elementResourceMap[elementResource.ElementType] = elementResource;
            }
        }

        private void BuildRarityResourceMap()
        {
            rarityResourceMap.Clear();
            if (RarityResources == null)
                return;

            foreach (var rarityResource in RarityResources)
            {
                if (rarityResource == null)
                    continue;

                rarityResourceMap[rarityResource.Rarity] = rarityResource;
            }
        }

        private void BuildEquipmentResourceMap()
        {
            equipmentResourceMap.Clear();
            if (EquipmentResources == null)
                return;

            foreach (var equipmentResource in EquipmentResources)
            {
                if (equipmentResource == null)
                    continue;

                equipmentResourceMap[equipmentResource.EquipmentType] = equipmentResource;
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

        public EquipmentResource GetEquipmentResource(EquipmentType equipmentType)
        {
            if (equipmentResourceMap.TryGetValue(equipmentType, out EquipmentResource equipmentResource))
            {
                return equipmentResource;
            }

            return null;
        }

        public Sprite GetEquipmentLargeIcon(EquipmentType equipmentType)
        {
            EquipmentResource resource = GetEquipmentResource(equipmentType);
            return resource != null ? resource.LargeIcon : null;
        }

        public Sprite GetEquipmentSmallIcon(EquipmentType equipmentType)
        {
            EquipmentResource resource = GetEquipmentResource(equipmentType);
            return resource != null ? resource.SmallIcon : null;
        }

        public Sprite GetStageBanner(int stageResourceId)
        {
            if (StageUIResources == null)
                return null;

            foreach (var stageUIResource in StageUIResources)
            {
                if (stageUIResource != null && stageUIResource.StageResourceId == stageResourceId)
                {
                    return stageUIResource.MainBanner;
                }
            }

            return null;
        }

        public Sprite GetStageStarRewardBanner(int stageResourceId)
        {
            if (StageUIResources == null)
                return null;

            foreach (var stageUIResource in StageUIResources)
            {
                if (stageUIResource != null && stageUIResource.StageResourceId == stageResourceId)
                {
                    return stageUIResource.StarRewardBanner;
                }
            }

            return null;
        }
    }
}
