using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using OJ.Dice;
using OJ.Equipment;
using OJ.Hunting;
using OJ.Point;
using OJ.Relic;
using OJ.Stage;
using OJ.StageReward;
using OJ.UI;

namespace OJ.Utils
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
    public class StageThemeResource
    {
        [FormerlySerializedAs("StageResourceId")]
        public StageTheme Theme;

        [Header("Battle")]
        public Sprite MapBackground;
        public Monster[] Monsters;
        public Monster BossMonster;

        [Header("Banners")]
        public Sprite MainBanner;
        public Sprite StarRewardBanner;
    }

    // 이 타입의 정본은 인스펙터에 채워 둔 참조다. 씬에서 못 찾았을 때 빈 객체를
    // 만들면 아래 필드가 전부 null 인 인스턴스가 조용히 생기고, Provider 들이
    // 코드 기본값으로 흘러 배선 사고가 통째로 흡수된다. 그래서 프리팹에서만 만든다.
    // (MIGRATION_BASELINE 2.1)
    // 경로 정본은 이 한 줄이다. 에디터 추출 도구도 이 어트리뷰트를 읽어 저장 위치를
    // 정하므로, 여기만 고치면 둘이 갈라지지 않는다.
    [SingletonPrefab("StaticResource")]
    public class StaticResource : MonoSingleton<StaticResource>
    {
        public PointMetadataDatabase PointMetadataDatabase;
        public DiceMetaDataDatabase DiceMetaDataDatabase;
        public GemDefinitionDatabase GemDefinitionDatabase;
        public RelicDatabase RelicDatabase;
        public StageDatabase StageDatabase;
        [FormerlySerializedAs("ChapterRewardDatabase")]
        public StageRewardDatabase StageRewardDatabase;

        /// <summary>팝업 프리팹 목록. (10.1) 비면 팝업이 하나도 열리지 않는다.</summary>
        public DialogCatalog DialogCatalog;
        public List<ElementResource> ElementResources;
        
        public List<RarityResource> RarityResources;
        public List<EquipmentResource> EquipmentResources;

        [FormerlySerializedAs("StageUIResources")]
        public List<StageThemeResource> StageThemeResources;

        private Dictionary<ElementType, ElementResource> elementResourceMap = new Dictionary<ElementType, ElementResource>();
        private Dictionary<Rarity, RarityResource> rarityResourceMap = new Dictionary<Rarity, RarityResource>();
        private Dictionary<EquipmentType, EquipmentResource> equipmentResourceMap = new Dictionary<EquipmentType, EquipmentResource>();
        private Dictionary<StageTheme, StageThemeResource> stageThemeResourceMap = new Dictionary<StageTheme, StageThemeResource>();

        protected override void Init()
        {
            BuildElementResourceMap();
            BuildRarityResourceMap();
            BuildEquipmentResourceMap();
            BuildStageThemeResourceMap();
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

        private void BuildStageThemeResourceMap()
        {
            stageThemeResourceMap.Clear();
            if (StageThemeResources == null)
                return;

            foreach (StageThemeResource resource in StageThemeResources)
            {
                if (resource == null)
                    continue;

                stageThemeResourceMap[resource.Theme] = resource;
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

        public StageThemeResource GetStageThemeResource(StageTheme theme)
        {
            if (stageThemeResourceMap.Count == 0 && StageThemeResources != null && StageThemeResources.Count > 0)
                BuildStageThemeResourceMap();

            stageThemeResourceMap.TryGetValue(theme, out StageThemeResource resource);
            return resource;
        }

        public Sprite GetStageBanner(StageTheme theme)
        {
            StageThemeResource resource = GetStageThemeResource(theme);
            return resource != null ? resource.MainBanner : null;
        }

        public Sprite GetStageStarRewardBanner(StageTheme theme)
        {
            StageThemeResource resource = GetStageThemeResource(theme);
            return resource != null ? resource.StarRewardBanner : null;
        }
    }
}
