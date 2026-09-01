using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ.Equipment
{
    [Serializable]
    public class GemEffect
    {
        public GemStatType statType;
        public DiceType targetDiceType = DiceType.Max;
        public ElementType targetElementType = ElementType.Max;
        public float percentValue;
        public int flatValue;
        public int intParam;
    }

    [Serializable]
    public class GemDefinition
    {
        public string gemId;
        public string displayName;
        public Rarity rarity;
        public EquipmentType equipableType;
        public int initialCount;
        public List<GemEffect> effects = new List<GemEffect>();
    }

    [CreateAssetMenu(fileName = "GemDefinitionDatabase", menuName = "Equipment/Gem Definition Database")]
    public class GemDefinitionDatabase : ScriptableObject
    {
        [SerializeField] private List<GemDefinition> gemDefinitions = new List<GemDefinition>();

        public IReadOnlyList<GemDefinition> GemDefinitions => gemDefinitions;
    }
}
