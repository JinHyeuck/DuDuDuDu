#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using OJ.Relic;

namespace OJ.EditorTools
{
    public static class RelicDatabaseAssetBuilder
    {
        private const string ResourceFolder = "Assets/Resources";
        private const string AssetPath = "Assets/Resources/RelicDatabase.asset";

        [MenuItem("Tools/Relic/Create Default Relic Database")]
        public static void CreateDefaultRelicDatabase()
        {
            if (!Directory.Exists(ResourceFolder))
                Directory.CreateDirectory(ResourceFolder);

            RelicDatabase database = AssetDatabase.LoadAssetAtPath<RelicDatabase>(AssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<RelicDatabase>();
                AssetDatabase.CreateAsset(database, AssetPath);
            }

            database.maxLevel = 20;
            database.baseGoldCost = 500;
            database.goldCostPerSummon = 100;
            database.baseTicketCost = 1;
            database.ticketCostIncreaseInterval = 10;
            database.ticketCostIncreaseAmount = 1;
            database.rarityWeights = RelicDatabase.CreateDefaultWeights();
            database.normalBackground = LoadSprite("Passive_Normal");
            database.rareBackground = LoadSprite("Passive_Rare");
            database.epicBackground = LoadSprite("Passive_Epic");
            database.mythicBackground = LoadSprite("Passive_Mystic");

            List<RelicDefinition> defaults = RelicDatabase.CreateDefaultRelics();
            for (int i = 0; i < defaults.Count; i++)
                defaults[i].icon = LoadSprite("Relic_" + defaults[i].index);

            database.relics = defaults;

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RelicDatabaseProvider.ClearCache();
            Selection.activeObject = database;
        }

        private static Sprite LoadSprite(string spriteName)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Art/Relic/{spriteName}.png");
        }
    }
}
#endif
