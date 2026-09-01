using System;
using UnityEngine;

namespace OJ.Relic
{
    [Serializable]
    public class RelicDefinition
    {
        public RelicId relicId;
        [Min(1)] public int index = 1;
        public Rarity rarity = Rarity.Normal;
        public string displayName;
        [TextArea(2, 4)] public string description;
        [TextArea(2, 4)] public string example;
        public Sprite icon;

        [Header("Value Formula")]
        public float baseValue;
        public float levelUpValue;
        public float baseSecondaryValue;
        public float levelUpSecondaryValue;
        public float baseDuration;
        public float levelUpDuration;

        public float GetPrimaryValue(int level)
        {
            return GetValue(baseValue, levelUpValue, level);
        }

        public float GetSecondaryValue(int level)
        {
            return GetValue(baseSecondaryValue, levelUpSecondaryValue, level);
        }

        public float GetDuration(int level)
        {
            return GetValue(baseDuration, levelUpDuration, level);
        }

        private static float GetValue(float baseAmount, float levelUpAmount, int level)
        {
            if (level <= 0)
                return 0f;

            return baseAmount + levelUpAmount * (level - 1);
        }
    }
}
