using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class EquipmentManager : MonoSingleton<EquipmentManager>
    {
        [Serializable]
        private class EquipmentLevelData
        {
            public EquipmentType equipmentType;
            public int level;
        }

        [Serializable]
        private class EquipmentSlotsData
        {
            public EquipmentType equipmentType;
            public List<string> slots = new List<string>();
        }

        [Serializable]
        private class GemInventoryData
        {
            public string gemId;
            public int count;
        }

        [Serializable]
        private class EquipmentSaveData
        {
            public List<EquipmentLevelData> levels = new List<EquipmentLevelData>();
            public List<EquipmentSlotsData> slots = new List<EquipmentSlotsData>();
            public List<GemInventoryData> inventory = new List<GemInventoryData>();
        }

        private struct EquipmentUpgradeRule
        {
            public int baseGold;
            public int goldPerLevel;
            public int baseScroll;
            public int scrollPerLevel;
            public int baseAttack;
            public int attackPerLevel;
        }

        private const string SaveKey = "OJ.Equipment.Save";

        public event Action<EquipmentType> OnEquipmentChanged;
        public event Action OnGemChanged;

        private readonly Dictionary<EquipmentType, int> levels = new Dictionary<EquipmentType, int>();
        private readonly Dictionary<EquipmentType, string[]> equippedGemSlots = new Dictionary<EquipmentType, string[]>();
        private readonly Dictionary<string, int> gemInventory = new Dictionary<string, int>();
        private readonly List<GemDefinition> gemDefinitions = new List<GemDefinition>();
        private readonly Dictionary<string, GemDefinition> gemDefinitionMap = new Dictionary<string, GemDefinition>();
        private GemDefinitionDatabase gemDefinitionDatabase;


        protected override void Init()
        {
            BuildGemDefinitionsFromDatabase();
            InitializeCollections();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                SaveAll();
        }

        private void OnApplicationQuit()
        {
            SaveAll();
        }

        public int GetLevel(EquipmentType equipmentType)
        {
            return levels.TryGetValue(equipmentType, out int value) ? Mathf.Max(1, value) : 1;
        }

        public int GetEquipmentAttack(EquipmentType equipmentType)
        {
            EquipmentUpgradeRule rule = GetRule(equipmentType);
            int level = GetLevel(equipmentType);
            if (level <= 1)
                return 0;

            return Mathf.Max(0, rule.baseAttack + ((level - 1) * rule.attackPerLevel));
        }

        public int GetTotalEquipmentAttack()
        {
            int sum = 0;
            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
                sum += GetEquipmentAttack(equipmentType);
            return sum;
        }

        public (int goldCost, int scrollCost) GetUpgradeCost(EquipmentType equipmentType, int currentLevel)
        {
            EquipmentUpgradeRule rule = GetRule(equipmentType);
            int level = Mathf.Max(1, currentLevel);
            int gold = Mathf.Max(0, rule.baseGold + ((level - 1) * rule.goldPerLevel));
            int scroll = Mathf.Max(0, rule.baseScroll + ((level - 1) * rule.scrollPerLevel));
            return (gold, scroll);
        }

        public (int goldCost, int scrollCost) GetNextUpgradeCost(EquipmentType equipmentType)
        {
            return GetUpgradeCost(equipmentType, GetLevel(equipmentType));
        }

        public bool TryLevelUp(EquipmentType equipmentType)
        {
            if (PointManager.Instance == null)
                return false;

            (int goldCost, int scrollCost) = GetNextUpgradeCost(equipmentType);
            if (!PointManager.Instance.TrySpendEquipmentUpgrade(equipmentType, goldCost, scrollCost))
                return false;

            levels[equipmentType] = GetLevel(equipmentType) + 1;
            SaveAll();

            OnEquipmentChanged?.Invoke(equipmentType);
            OnGemChanged?.Invoke();
            return true;
        }

        public int GetSlotUnlockLevel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Define.MaxEquipmentSlot)
                return int.MaxValue;

            if (Define.EquipmentSlotUnlockLevels != null && slotIndex < Define.EquipmentSlotUnlockLevels.Length)
                return Define.EquipmentSlotUnlockLevels[slotIndex];

            return (slotIndex * 10) + 1;
        }

        public bool IsSlotUnlocked(EquipmentType equipmentType, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Define.MaxEquipmentSlot)
                return false;

            return GetLevel(equipmentType) >= GetSlotUnlockLevel(slotIndex);
        }

        public int GetUnlockedSlotCount(EquipmentType equipmentType)
        {
            int count = 0;
            for (int i = 0; i < Define.MaxEquipmentSlot; i++)
            {
                if (IsSlotUnlocked(equipmentType, i))
                    count++;
            }
            return count;
        }

        public string GetEquippedGemId(EquipmentType equipmentType, int slotIndex)
        {
            if (!equippedGemSlots.TryGetValue(equipmentType, out string[] slots))
                return string.Empty;
            if (slotIndex < 0 || slotIndex >= slots.Length)
                return string.Empty;

            return slots[slotIndex] ?? string.Empty;
        }

        public bool TryEquipGem(EquipmentType equipmentType, int slotIndex, string gemId)
        {
            if (string.IsNullOrEmpty(gemId))
                return false;
            if (!IsSlotUnlocked(equipmentType, slotIndex))
                return false;
            if (!gemDefinitionMap.TryGetValue(gemId, out GemDefinition definition))
                return false;
            if (definition.equipableType != equipmentType)
                return false;
            if (GetGemCount(gemId) <= 0)
                return false;

            string[] slots = equippedGemSlots[equipmentType];
            string previousGem = slots[slotIndex];

            if (!string.IsNullOrEmpty(previousGem))
                AddGem(previousGem, 1);

            slots[slotIndex] = gemId;
            AddGem(gemId, -1);

            SaveAll();
            OnGemChanged?.Invoke();
            return true;
        }

        public bool UnequipGem(EquipmentType equipmentType, int slotIndex)
        {
            if (!equippedGemSlots.TryGetValue(equipmentType, out string[] slots))
                return false;
            if (slotIndex < 0 || slotIndex >= slots.Length)
                return false;

            string gemId = slots[slotIndex];
            if (string.IsNullOrEmpty(gemId))
                return false;

            slots[slotIndex] = string.Empty;
            AddGem(gemId, 1);

            SaveAll();
            OnGemChanged?.Invoke();
            return true;
        }

        public IReadOnlyList<GemDefinition> GetGemDefinitions()
        {
            return gemDefinitions;
        }

        public bool TryGetGemDefinition(string gemId, out GemDefinition definition)
        {
            return gemDefinitionMap.TryGetValue(gemId, out definition);
        }

        public int GetGemCount(string gemId)
        {
            return gemInventory.TryGetValue(gemId, out int count) ? Mathf.Max(0, count) : 0;
        }

        public bool HasMergeMaterials(EquipmentType equipmentType)
        {
            Dictionary<Rarity, int> counts = new Dictionary<Rarity, int>();

            for (int i = 0; i < gemDefinitions.Count; i++)
            {
                GemDefinition definition = gemDefinitions[i];
                if (definition == null ||
                    definition.equipableType != equipmentType ||
                    definition.rarity == Rarity.Mythic)
                {
                    continue;
                }

                int count = GetGemCount(definition.gemId);
                if (count <= 0)
                    continue;

                counts.TryGetValue(definition.rarity, out int current);
                counts[definition.rarity] = current + count;
            }

            foreach (var pair in counts)
            {
                if (pair.Value >= 4)
                    return true;
            }

            return false;
        }

        public bool TryMergeGems(EquipmentType equipmentType, IReadOnlyList<string> materialGemIds, out List<string> resultGemIds)
        {
            resultGemIds = new List<string>();
            if (materialGemIds == null || materialGemIds.Count < 4)
                return false;

            Dictionary<string, int> requiredCounts = new Dictionary<string, int>();
            Dictionary<Rarity, List<string>> groupedMaterials = new Dictionary<Rarity, List<string>>();

            for (int i = 0; i < materialGemIds.Count; i++)
            {
                string gemId = materialGemIds[i];
                if (string.IsNullOrEmpty(gemId))
                    continue;
                if (!gemDefinitionMap.TryGetValue(gemId, out GemDefinition definition) || definition == null)
                    continue;
                if (definition.equipableType != equipmentType || definition.rarity == Rarity.Mythic)
                    continue;

                requiredCounts.TryGetValue(gemId, out int requiredCount);
                requiredCounts[gemId] = requiredCount + 1;

                if (!groupedMaterials.TryGetValue(definition.rarity, out List<string> group))
                {
                    group = new List<string>();
                    groupedMaterials[definition.rarity] = group;
                }

                group.Add(gemId);
            }

            foreach (var pair in requiredCounts)
            {
                if (GetGemCount(pair.Key) < pair.Value)
                    return false;
            }

            List<string> consumedGemIds = new List<string>();

            foreach (var pair in groupedMaterials)
            {
                int consumeCount = (pair.Value.Count / 4) * 4;
                if (consumeCount <= 0)
                    continue;
                if (!TryGetNextRarity(pair.Key, out Rarity nextRarity))
                    continue;

                int resultCount = consumeCount / 4;
                for (int i = 0; i < resultCount; i++)
                {
                    if (!TryGetRandomGemDefinition(equipmentType, nextRarity, out GemDefinition resultDefinition))
                        return false;

                    resultGemIds.Add(resultDefinition.gemId);
                }

                for (int i = 0; i < consumeCount; i++)
                    consumedGemIds.Add(pair.Value[i]);
            }

            if (consumedGemIds.Count <= 0 || resultGemIds.Count <= 0)
                return false;

            for (int i = 0; i < consumedGemIds.Count; i++)
            {
                string gemId = consumedGemIds[i];
                gemInventory[gemId] = Mathf.Max(0, GetGemCount(gemId) - 1);
            }

            for (int i = 0; i < resultGemIds.Count; i++)
            {
                string gemId = resultGemIds[i];
                gemInventory[gemId] = GetGemCount(gemId) + 1;
            }

            SaveAll();
            OnGemChanged?.Invoke();
            return true;
        }

        public float GetAttackPercentBonus(DiceType diceType)
        {
            return Mathf.Max(0f, SumPercent(GemStatType.AttackPercent, diceType));
        }

        public int GetAttackFlatBonus(DiceType diceType)
        {
            return Mathf.Max(0, SumFlat(GemStatType.AttackFlat, diceType));
        }

        public float GetCooldownReductionPercent(DiceType diceType)
        {
            return Mathf.Clamp(SumPercent(GemStatType.CooldownReducePercent, diceType), 0f, 0.8f);
        }

        public int GetFirstNWavesDamageFlatBonus(DiceType diceType, int waveIndex)
        {
            if (waveIndex <= 0)
                return 0;

            int sum = 0;
            foreach (GemEffect effect in EnumerateActiveEffects(diceType))
            {
                if (effect == null || effect.statType != GemStatType.FirstNWavesDamageFlat)
                    continue;

                int limit = Mathf.Max(0, effect.intParam);
                if (limit <= 0 || waveIndex > limit)
                    continue;

                sum += Mathf.Max(0, effect.flatValue);
            }

            return Mathf.Max(0, sum);
        }

        public float GetFireExplosionRangeBonus(DiceType diceType)
        {
            return Mathf.Max(0f, SumPercent(GemStatType.FireExplosionRangePercent, diceType));
        }

        public int GetWellHpOnKill()
        {
            int sum = 0;
            foreach (GemEffect effect in EnumerateActiveEffects(DiceType.Max))
            {
                if (effect == null || effect.statType != GemStatType.WellHpOnKill)
                    continue;

                sum += Mathf.Max(0, effect.flatValue);
            }

            return Mathf.Max(0, sum);
        }

        public float GetFinalDamagePercentBonus(DiceType diceType)
        {
            return Mathf.Max(0f, SumPercent(GemStatType.FinalDamagePercent, diceType));
        }

        public int GetFireExplosionExtraTargetCount(DiceType diceType)
        {
            return Mathf.Max(0, SumFlat(GemStatType.FireExplosionTargetCountFlat, diceType));
        }

        public int GetThunderChainExtraCount(DiceType diceType)
        {
            return Mathf.Max(0, SumFlat(GemStatType.ThunderChainCountFlat, diceType));
        }

        public int GetGoldOnKill()
        {
            int sum = 0;
            foreach (GemEffect effect in EnumerateActiveEffects(DiceType.Max))
            {
                if (effect == null || effect.statType != GemStatType.GoldOnKill)
                    continue;

                sum += Mathf.Max(0, effect.flatValue);
            }

            return Mathf.Max(0, sum);
        }

        public void OnMonsterKilled()
        {
            int heal = GetWellHpOnKill();
            if (heal > 0 && GameManager.Instance != null && GameManager.Instance.wall != null)
                GameManager.Instance.wall.Heal(heal);

            int gold = GetGoldOnKill();
            if (gold > 0 && PointManager.Instance != null)
                PointManager.Instance.Add(PointType.Gold, gold);
        }

        public void SaveAll()
        {
            EquipmentSaveData saveData = new EquipmentSaveData();

            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                saveData.levels.Add(new EquipmentLevelData
                {
                    equipmentType = equipmentType,
                    level = GetLevel(equipmentType)
                });

                EquipmentSlotsData slotsData = new EquipmentSlotsData { equipmentType = equipmentType };
                if (equippedGemSlots.TryGetValue(equipmentType, out string[] slots))
                {
                    for (int i = 0; i < slots.Length; i++)
                        slotsData.slots.Add(slots[i] ?? string.Empty);
                }

                saveData.slots.Add(slotsData);
            }

            foreach (var pair in gemInventory)
            {
                if (pair.Value <= 0)
                    continue;

                saveData.inventory.Add(new GemInventoryData
                {
                    gemId = pair.Key,
                    count = pair.Value
                });
            }

            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        public void LoadAll()
        {
            InitializeCollections();

            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                SeedInitialGemInventory();
                return;
            }

            EquipmentSaveData saveData = JsonUtility.FromJson<EquipmentSaveData>(json);
            if (saveData == null)
            {
                SeedInitialGemInventory();
                return;
            }

            if (saveData.levels != null)
            {
                for (int i = 0; i < saveData.levels.Count; i++)
                {
                    EquipmentLevelData levelData = saveData.levels[i];
                    if (levelData == null)
                        continue;

                    levels[levelData.equipmentType] = Mathf.Max(1, levelData.level);
                }
            }

            if (saveData.slots != null)
            {
                for (int i = 0; i < saveData.slots.Count; i++)
                {
                    EquipmentSlotsData slotsData = saveData.slots[i];
                    if (slotsData == null)
                        continue;
                    if (!equippedGemSlots.TryGetValue(slotsData.equipmentType, out string[] slots))
                        continue;

                    for (int slot = 0; slot < slots.Length && slot < slotsData.slots.Count; slot++)
                        slots[slot] = slotsData.slots[slot] ?? string.Empty;
                }
            }

            if (saveData.inventory != null)
            {
                for (int i = 0; i < saveData.inventory.Count; i++)
                {
                    GemInventoryData inventoryData = saveData.inventory[i];
                    if (inventoryData == null || string.IsNullOrEmpty(inventoryData.gemId))
                        continue;

                    gemInventory[inventoryData.gemId] = Mathf.Max(0, inventoryData.count);
                }
            }
        }

        private void InitializeCollections()
        {
            levels.Clear();
            equippedGemSlots.Clear();
            gemInventory.Clear();

            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                levels[equipmentType] = 1;
                equippedGemSlots[equipmentType] = new string[Define.MaxEquipmentSlot];
            }
        }

        private void SeedInitialGemInventory()
        {
            for (int i = 0; i < gemDefinitions.Count; i++)
            {
                GemDefinition definition = gemDefinitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.gemId))
                    continue;

                int seedCount = Mathf.Max(0, definition.initialCount);

                if (seedCount > 0)
                    gemInventory[definition.gemId] = seedCount;
            }
        }

        public void AddGem(string gemId, int amount)
        {
            if (string.IsNullOrEmpty(gemId) || amount == 0)
                return;

            int current = GetGemCount(gemId);
            int next = Mathf.Max(0, current + amount);
            gemInventory[gemId] = next;

            SaveAll();
            OnGemChanged?.Invoke();
        }

        private bool TryGetRandomGemDefinition(EquipmentType equipmentType, Rarity rarity, out GemDefinition result)
        {
            List<GemDefinition> candidates = new List<GemDefinition>();

            for (int i = 0; i < gemDefinitions.Count; i++)
            {
                GemDefinition definition = gemDefinitions[i];
                if (definition == null)
                    continue;
                if (definition.equipableType == equipmentType && definition.rarity == rarity)
                    candidates.Add(definition);
            }

            if (candidates.Count <= 0)
            {
                result = null;
                return false;
            }

            result = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return result != null;
        }

        private static bool TryGetNextRarity(Rarity rarity, out Rarity nextRarity)
        {
            if (rarity >= Rarity.Mythic)
            {
                nextRarity = rarity;
                return false;
            }

            nextRarity = (Rarity)((int)rarity + 1);
            return true;
        }

        private float SumPercent(GemStatType statType, DiceType diceType)
        {
            float sum = 0f;
            foreach (GemEffect effect in EnumerateActiveEffects(diceType))
            {
                if (effect == null || effect.statType != statType)
                    continue;

                sum += Mathf.Max(0f, effect.percentValue);
            }

            return Mathf.Max(0f, sum);
        }

        private int SumFlat(GemStatType statType, DiceType diceType)
        {
            int sum = 0;
            foreach (GemEffect effect in EnumerateActiveEffects(diceType))
            {
                if (effect == null || effect.statType != statType)
                    continue;

                sum += Mathf.Max(0, effect.flatValue);
            }

            return Mathf.Max(0, sum);
        }

        private IEnumerable<GemEffect> EnumerateActiveEffects(DiceType diceType)
        {
            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                if (!equippedGemSlots.TryGetValue(equipmentType, out string[] slots) || slots == null)
                    continue;

                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    if (!IsSlotUnlocked(equipmentType, slotIndex))
                        continue;

                    string gemId = slots[slotIndex];
                    if (string.IsNullOrEmpty(gemId))
                        continue;
                    if (!gemDefinitionMap.TryGetValue(gemId, out GemDefinition definition) || definition == null)
                        continue;

                    for (int effectIndex = 0; effectIndex < definition.effects.Count; effectIndex++)
                    {
                        GemEffect effect = definition.effects[effectIndex];
                        if (effect == null)
                            continue;
                        if (!IsTargetMatched(effect, diceType))
                            continue;

                        yield return effect;
                    }
                }
            }
        }

        private static bool IsTargetMatched(GemEffect effect, DiceType diceType)
        {
            if (effect == null)
                return false;

            if (diceType == DiceType.Max)
                return true;

            DiceType baseType = DiceMetaDataProvider.GetBaseElementType(diceType);

            if (effect.targetDiceType != DiceType.Max && effect.targetDiceType != baseType)
                return false;

            if (effect.targetElementType == ElementType.Max)
                return true;

            ElementType elementType = ToElementType(baseType);
            return effect.targetElementType == elementType;
        }

        private static ElementType ToElementType(DiceType diceType)
        {
            switch (DiceMetaDataProvider.GetBaseElementType(diceType))
            {
                case DiceType.Normal:
                    return ElementType.Normal;
                case DiceType.Fire:
                    return ElementType.Fire;
                case DiceType.Ice:
                    return ElementType.Water;
                case DiceType.Thunder:
                    return ElementType.Light;
                case DiceType.Poison:
                    return ElementType.Dark;
                default:
                    return ElementType.Max;
            }
        }

        private static EquipmentUpgradeRule GetRule(EquipmentType equipmentType)
        {
            switch (equipmentType)
            {
                case EquipmentType.Weapon:
                    return new EquipmentUpgradeRule { baseGold = 120, goldPerLevel = 52, baseScroll = 3, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case EquipmentType.Helmet:
                    return new EquipmentUpgradeRule { baseGold = 95, goldPerLevel = 48, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case EquipmentType.Armor:
                    return new EquipmentUpgradeRule { baseGold = 100, goldPerLevel = 48, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case EquipmentType.Ring:
                    return new EquipmentUpgradeRule { baseGold = 110, goldPerLevel = 50, baseScroll = 3, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case EquipmentType.Shoes:
                    return new EquipmentUpgradeRule { baseGold = 90, goldPerLevel = 46, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case EquipmentType.Necklace:
                    return new EquipmentUpgradeRule { baseGold = 100, goldPerLevel = 50, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                default:
                    return new EquipmentUpgradeRule { baseGold = 100, goldPerLevel = 50, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
            }
        }

        private void BuildGemDefinitionsFromDatabase()
        {
            gemDefinitions.Clear();
            gemDefinitionMap.Clear();

            var database = GetGemDefinitionDatabase();
            if (database == null || database.GemDefinitions == null)
            {
                Debug.LogWarning("EquipmentManager: GemDefinitionDatabase is missing. Gem effects will be inactive.");
                return;
            }

            for (int i = 0; i < database.GemDefinitions.Count; i++)
            {
                AddDefinition(database.GemDefinitions[i]);
            }
        }

        private GemDefinitionDatabase GetGemDefinitionDatabase()
        {
            if (gemDefinitionDatabase != null)
                return gemDefinitionDatabase;

            if (StaticResource.Instance != null && StaticResource.Instance.GemDefinitionDatabase != null)
            {
                gemDefinitionDatabase = StaticResource.Instance.GemDefinitionDatabase;
                return gemDefinitionDatabase;
            }

            gemDefinitionDatabase = Resources.Load<GemDefinitionDatabase>("GemDefinitionDatabase");
            return gemDefinitionDatabase;
        }

        private void AddDefinition(GemDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.gemId))
                return;

            if (gemDefinitionMap.ContainsKey(definition.gemId))
            {
                Debug.LogWarning($"EquipmentManager: Duplicate gem id '{definition.gemId}' ignored.");
                return;
            }

            gemDefinitions.Add(definition);
            gemDefinitionMap[definition.gemId] = definition;
        }
    }
}
