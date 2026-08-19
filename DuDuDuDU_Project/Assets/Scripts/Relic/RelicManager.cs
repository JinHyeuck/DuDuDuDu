using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class RelicManager : MonoBehaviour
    {
        [Serializable]
        private class RelicLevelData
        {
            public RelicId relicId;
            public int level;
        }

        [Serializable]
        private class RelicSaveData
        {
            public int summonCount;
            public List<RelicLevelData> levels = new List<RelicLevelData>();
        }

        private const string SaveKey = "OJ.Relic.Save";
        private static readonly DiceType[] BasicDiceTypes =
        {
            DiceType.Normal,
            DiceType.Fire,
            DiceType.Ice,
            DiceType.Poison,
            DiceType.Thunder,
        };

        public static RelicManager Instance { get; private set; }

        public event Action OnRelicChanged;
        public event Action OnSummonCountChanged;

        private readonly Dictionary<RelicId, int> levels = new Dictionary<RelicId, int>();
        private RelicDatabase database;
        private int summonCount;
        private bool stageStartDiceApplied;
        private bool firstWaveAttackUsed;
        private bool firstMythicCrafted;
        private bool lastWallTriggered;
        private int lastWallCooldownWaveIndex = -1;

        public RelicDatabase Database
        {
            get
            {
                if (database == null)
                    database = RelicDatabaseProvider.Database;
                return database;
            }
        }

        public int SummonCount => summonCount;
        public int MaxLevel => Database != null ? Mathf.Max(1, Database.maxLevel) : 20;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var go = new GameObject(nameof(RelicManager));
            go.AddComponent<RelicManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        public IReadOnlyList<RelicDefinition> GetDefinitions()
        {
            return Database.relics;
        }

        public RelicDefinition GetDefinition(RelicId relicId)
        {
            return Database.Get(relicId);
        }

        public int GetLevel(RelicId relicId)
        {
            levels.TryGetValue(relicId, out int level);
            return Mathf.Clamp(level, 0, MaxLevel);
        }

        public bool HasRelic(RelicId relicId)
        {
            return GetLevel(relicId) > 0;
        }

        public float GetPrimaryValue(RelicId relicId)
        {
            RelicDefinition definition = GetDefinition(relicId);
            return definition != null ? definition.GetPrimaryValue(GetLevel(relicId)) : 0f;
        }

        public float GetSecondaryValue(RelicId relicId)
        {
            RelicDefinition definition = GetDefinition(relicId);
            return definition != null ? definition.GetSecondaryValue(GetLevel(relicId)) : 0f;
        }

        public float GetDuration(RelicId relicId)
        {
            RelicDefinition definition = GetDefinition(relicId);
            return definition != null ? definition.GetDuration(GetLevel(relicId)) : 0f;
        }

        public RelicSummonCost GetCurrentSummonCost()
        {
            return Database.GetSummonCost(summonCount);
        }

        public bool CanSummon()
        {
            if (PointManager.Instance == null)
                return false;

            RelicSummonCost cost = GetCurrentSummonCost();
            return PointManager.Instance.Get(PointType.Gold) >= cost.goldCost
                && PointManager.Instance.Get(PointType.RelicTicket) >= cost.ticketCost;
        }

        public bool TrySummon(out RelicSummonResult result)
        {
            result = null;
            if (PointManager.Instance == null)
                return false;

            RelicSummonCost cost = GetCurrentSummonCost();
            var costs = new Dictionary<PointType, int>
            {
                { PointType.Gold, cost.goldCost },
                { PointType.RelicTicket, cost.ticketCost },
            };

            if (!PointManager.Instance.TrySpend(costs))
                return false;

            RelicDefinition picked = PickRelic();
            if (picked == null)
                return false;

            result = GrantRelic(picked);
            summonCount++;
            Save();
            OnSummonCountChanged?.Invoke();
            return true;
        }

        public RelicSummonResult GrantRelic(RelicDefinition definition)
        {
            RelicSummonResult result = new RelicSummonResult
            {
                Definition = definition,
                OldLevel = definition != null ? GetLevel(definition.relicId) : 0,
            };

            if (definition == null)
            {
                result.NewLevel = result.OldLevel;
                return result;
            }

            int newLevel = Mathf.Min(MaxLevel, result.OldLevel + 1);
            levels[definition.relicId] = newLevel;
            result.NewLevel = newLevel;

            Save();
            OnRelicChanged?.Invoke();
            return result;
        }

        public string GetEffectText(RelicId relicId, int levelOverride = -1)
        {
            RelicDefinition definition = GetDefinition(relicId);
            if (definition == null)
                return string.Empty;

            int level = levelOverride >= 0 ? levelOverride : GetLevel(relicId);
            level = Mathf.Clamp(level, 1, MaxLevel);
            string primary = FormatValue(definition.GetPrimaryValue(level));
            string secondary = FormatValue(definition.GetSecondaryValue(level));
            string duration = FormatValue(definition.GetDuration(level));

            if (relicId == RelicId.AdvanceDeployment)
            {
                float upgradeChance = definition.GetSecondaryValue(level);
                if (upgradeChance > 0.001f)
                    return $"전투 시작 시 랜덤 기본 주사위 {primary}성 1개 지급 ({FormatValue(upgradeChance)}% 확률로 3성)";
            }

            try
            {
                return string.Format(definition.description, primary, secondary, duration);
            }
            catch (FormatException)
            {
                return definition.description;
            }
        }

        public string GetExampleText(RelicId relicId)
        {
            RelicDefinition definition = GetDefinition(relicId);
            return definition != null ? definition.example : string.Empty;
        }

        public Sprite GetBackground(Rarity rarity)
        {
            return Database.GetBackground(rarity);
        }

        public void BeginStageRun()
        {
            stageStartDiceApplied = false;
            firstWaveAttackUsed = false;
            firstMythicCrafted = false;
            lastWallTriggered = false;
            lastWallCooldownWaveIndex = -1;
        }

        public void BeginWave(int waveIndex)
        {
            firstWaveAttackUsed = false;
            if (lastWallCooldownWaveIndex != waveIndex)
                lastWallCooldownWaveIndex = -1;
        }

        public void EndWave()
        {
            lastWallCooldownWaveIndex = -1;
        }

        public int GetStageStartSpBonus()
        {
            return Mathf.RoundToInt(GetPrimaryValue(RelicId.BeginnerPouch));
        }

        public void TryApplyStageStartDice()
        {
            if (stageStartDiceApplied || !HasRelic(RelicId.AdvanceDeployment))
                return;

            UIBoard board = UIBoard.Instance;
            if (board == null || board.diceMap == null || board.diceMap.Length <= 0)
                return;

            int slotIndex = GetRandomEmptySlot(board);
            if (slotIndex < 0)
                return;

            DiceType type = BasicDiceTypes[UnityEngine.Random.Range(0, BasicDiceTypes.Length)];
            int star = Mathf.Clamp(Mathf.RoundToInt(GetPrimaryValue(RelicId.AdvanceDeployment)), 1, MergeSystem.MaxStar);
            float upgradeChance = GetSecondaryValue(RelicId.AdvanceDeployment);
            if (upgradeChance > 0f && UnityEngine.Random.value * 100f <= upgradeChance)
                star = Mathf.Min(MergeSystem.MaxStar, star + 1);

            DiceTypeStarManager.Instance?.OnDiceSpawn(type, star);
            board.SpawnDice(type, star, slotIndex);
            stageStartDiceApplied = true;
        }

        public void ApplyWaveClearRelics(Wall wall)
        {
            if (HasRelic(RelicId.BattleVault)
                && PointManager.Instance != null
                && UnityEngine.Random.value * 100f <= GetPrimaryValue(RelicId.BattleVault))
            {
                PointManager.Instance.Add(PointType.Coin, 1);
            }

            if (wall != null && HasRelic(RelicId.RepairHammer))
            {
                int heal = Mathf.CeilToInt(wall.TotalHp * GetPrimaryValue(RelicId.RepairHammer) * 0.01f);
                wall.Heal(heal);
            }
        }

        public List<PointRewardEntry> ApplyStageClearRewardBonus(IReadOnlyList<PointRewardEntry> rewards)
        {
            List<PointRewardEntry> result = new List<PointRewardEntry>();
            if (rewards == null)
                return result;

            float bonusPercent = GetPrimaryValue(RelicId.LootMap);
            float multiplier = 1f + bonusPercent * 0.01f;
            for (int i = 0; i < rewards.Count; i++)
            {
                PointRewardEntry reward = rewards[i];
                if (bonusPercent > 0f && IsStageClearRewardBoostTarget(reward.PointType))
                {
                    int boosted = Mathf.RoundToInt(reward.Amount * multiplier);
                    reward.Amount = Mathf.Max(reward.Amount, boosted);
                }

                result.Add(reward);
            }

            return result;
        }

        public float GetCooldownReductionPercent()
        {
            float percent = GetPrimaryValue(RelicId.QuickHands);
            if (IsLastWallCooldownActive())
                percent += GetPrimaryValue(RelicId.LastWall);

            return Mathf.Clamp(percent, 0f, 80f);
        }

        public float GetDamageMultiplier(DiceType diceType)
        {
            float bonusPercent = 0f;

            if (HasRelic(RelicId.FullBoardPressure) && IsBoardFull())
                bonusPercent += GetPrimaryValue(RelicId.FullBoardPressure);

            if (HasCrownResonance(diceType))
                bonusPercent += GetPrimaryValue(RelicId.CrownResonance);

            return 1f + bonusPercent * 0.01f;
        }

        public float ConsumeAttackDamageMultiplier()
        {
            float multiplier = 1f;

            if (HasRelic(RelicId.JustOneHit) && !firstWaveAttackUsed)
            {
                firstWaveAttackUsed = true;
                multiplier *= 1f + GetPrimaryValue(RelicId.JustOneHit) * 0.01f;
            }

            if (HasRelic(RelicId.LuckyMagazine)
                && UnityEngine.Random.value * 100f <= GetPrimaryValue(RelicId.LuckyMagazine))
            {
                multiplier *= 1f + GetSecondaryValue(RelicId.LuckyMagazine) * 0.01f;
            }

            return multiplier;
        }

        public int RollSummonStar()
        {
            if (HasRelic(RelicId.LuckyGesture)
                && UnityEngine.Random.value * 100f <= GetPrimaryValue(RelicId.LuckyGesture))
            {
                return 2;
            }

            return 1;
        }

        public bool TrySpawnTwinDice(DiceType type)
        {
            if (!HasRelic(RelicId.TwinSummonStone)
                || UnityEngine.Random.value * 100f > GetPrimaryValue(RelicId.TwinSummonStone))
            {
                return false;
            }

            UIBoard board = UIBoard.Instance;
            if (board == null || board.diceMap == null)
                return false;

            int slotIndex = GetRandomEmptySlot(board);
            if (slotIndex < 0)
                return false;

            const int twinStar = 1;
            DiceTypeStarManager.Instance?.OnDiceSpawn(type, twinStar);
            board.SpawnDice(type, twinStar, slotIndex);
            return true;
        }

        public bool ShouldSkipSummonCostIncrease()
        {
            return HasRelic(RelicId.LuckyInvitation)
                && UnityEngine.Random.value * 100f <= GetPrimaryValue(RelicId.LuckyInvitation);
        }

        public void ApplyMergeInsurance()
        {
            if (!HasRelic(RelicId.MergeInsurance)
                || UIDiceSummonSystem.Instance == null
                || UnityEngine.Random.value * 100f > GetPrimaryValue(RelicId.MergeInsurance))
            {
                return;
            }

            UIDiceSummonSystem.Instance.AddSP(Mathf.RoundToInt(GetSecondaryValue(RelicId.MergeInsurance)));
        }

        public void OnMythicCrafted(DiceType mythicType)
        {
            if (firstMythicCrafted || !HasRelic(RelicId.KingBlueprint))
                return;

            if (DiceMetaDataProvider.IsSummonable(mythicType))
                return;

            firstMythicCrafted = true;
            UIDiceSummonSystem.Instance?.AddSP(Mathf.RoundToInt(GetPrimaryValue(RelicId.KingBlueprint)));
        }

        public bool TryTriggerLastWall()
        {
            if (lastWallTriggered || !HasRelic(RelicId.LastWall))
                return false;

            lastWallTriggered = true;
            lastWallCooldownWaveIndex = GameManager.Instance != null ? GameManager.Instance.CurrentWaveIndex : -1;
            return true;
        }

        public void OnMonsterKilled(Monster killedMonster, bool wasPoisoned, Vector3 deathPosition)
        {
            if (!wasPoisoned || !HasRelic(RelicId.PoisonIncense))
                return;

            if (MonsterManager.Instance == null || MonsterManager.Instance.activeMonsters == null)
                return;

            int targetCount = Mathf.Max(1, Mathf.RoundToInt(GetPrimaryValue(RelicId.PoisonIncense)));
            float poisonMultiplier = Mathf.Max(1f, GetSecondaryValue(RelicId.PoisonIncense));
            List<Monster> candidates = MonsterManager.Instance.activeMonsters;

            HashSet<Monster> usedTargets = new HashSet<Monster>();
            for (int applied = 0; applied < targetCount; applied++)
            {
                Monster target = GetNearestPoisonSpreadTarget(candidates, killedMonster, usedTargets, deathPosition);
                if (target == null)
                    return;

                usedTargets.Add(target);
                target.ApplyPoison(4f, poisonMultiplier);
            }
        }

        public float GetFireExplosionRangeMultiplier(DiceType diceType)
        {
            if (!IsFireFamily(diceType))
                return 1f;

            return 1f + GetPrimaryValue(RelicId.EmberJar) * 0.01f;
        }

        public int GetFireExplosionExtraTargetCount(DiceType diceType)
        {
            if (!IsFireFamily(diceType))
                return 0;

            return Mathf.Max(0, Mathf.RoundToInt(GetSecondaryValue(RelicId.EmberJar)));
        }

        public int GetThunderExtraTargetCount(DiceType diceType)
        {
            if (diceType != DiceType.Thunder && diceType != DiceType.KingThunder)
                return 0;

            return Mathf.Max(0, Mathf.RoundToInt(GetPrimaryValue(RelicId.LightningRodRing)));
        }

        public int GetSlowDamageTakenBonusPercent()
        {
            return Mathf.RoundToInt(GetPrimaryValue(RelicId.FrostNail));
        }

        public int GetTornadoDamageTakenBonusPercent()
        {
            return Mathf.RoundToInt(GetPrimaryValue(RelicId.TornadoAnchor));
        }

        public float GetTornadoDamageTakenBonusDuration()
        {
            return Mathf.Max(0.1f, GetDuration(RelicId.TornadoAnchor));
        }

        public float GetStunChanceBonusPercent()
        {
            return GetPrimaryValue(RelicId.ParalysisNeedle);
        }

        public int GetStunDamageTakenBonusPercent()
        {
            return Mathf.RoundToInt(GetSecondaryValue(RelicId.ParalysisNeedle));
        }

        public int GetArmorBreakPercentBonus()
        {
            return Mathf.RoundToInt(GetPrimaryValue(RelicId.CrackHammer));
        }

        public float GetArmorBreakDurationBonus()
        {
            return GetDuration(RelicId.CrackHammer);
        }

        public float GetWindPushChanceBonusPercent()
        {
            return GetPrimaryValue(RelicId.TailwindFeather);
        }

        public int GetWindDamageTakenBonusPercent()
        {
            return Mathf.RoundToInt(GetSecondaryValue(RelicId.TailwindFeather));
        }

        private RelicDefinition PickRelic()
        {
            Rarity rarity = PickRarity();
            List<RelicDefinition> candidates = new List<RelicDefinition>();
            for (int i = 0; i < Database.relics.Count; i++)
            {
                RelicDefinition definition = Database.relics[i];
                if (definition != null && definition.rarity == rarity)
                    candidates.Add(definition);
            }

            if (candidates.Count == 0)
                candidates.AddRange(Database.relics);

            if (candidates.Count == 0)
                return null;

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private Rarity PickRarity()
        {
            List<RelicRarityWeight> weights = Database.rarityWeights;
            if (weights == null || weights.Count == 0)
                weights = RelicDatabase.CreateDefaultWeights();

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] != null)
                    total += Mathf.Max(0f, weights[i].weight);
            }

            if (total <= 0f)
                return Rarity.Normal;

            float roll = UnityEngine.Random.Range(0f, total);
            float cursor = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                RelicRarityWeight weight = weights[i];
                if (weight == null || weight.weight <= 0f)
                    continue;

                cursor += weight.weight;
                if (roll <= cursor)
                    return weight.rarity;
            }

            return Rarity.Normal;
        }

        private bool IsLastWallCooldownActive()
        {
            return lastWallCooldownWaveIndex >= 0
                && GameManager.Instance != null
                && GameManager.Instance.inGameState == InGameState.Wave
                && GameManager.Instance.CurrentWaveIndex == lastWallCooldownWaveIndex;
        }

        private bool HasCrownResonance(DiceType diceType)
        {
            if (!HasRelic(RelicId.CrownResonance) || DiceTypeStarManager.Instance == null)
                return false;

            switch (diceType)
            {
                case DiceType.Normal:
                    return DiceTypeStarManager.Instance.GetTypeCount(DiceType.KingNormal) > 0;
                case DiceType.Fire:
                    return DiceTypeStarManager.Instance.GetTypeCount(DiceType.KingFire) > 0;
                case DiceType.Ice:
                    return DiceTypeStarManager.Instance.GetTypeCount(DiceType.KingIce) > 0;
                case DiceType.Thunder:
                    return DiceTypeStarManager.Instance.GetTypeCount(DiceType.KingThunder) > 0;
                case DiceType.Poison:
                    return DiceTypeStarManager.Instance.GetTypeCount(DiceType.KingPoison) > 0;
                default:
                    return false;
            }
        }

        private static bool IsFireFamily(DiceType diceType)
        {
            return diceType == DiceType.Fire || diceType == DiceType.KingFire;
        }

        private static bool IsStageClearRewardBoostTarget(PointType pointType)
        {
            switch (pointType)
            {
                case PointType.Gold:
                case PointType.NormalScroll:
                case PointType.FireScroll:
                case PointType.IceScroll:
                case PointType.PoisonScroll:
                case PointType.ThunderScroll:
                case PointType.MythicScroll:
                case PointType.SpecialDiceCore:
                case PointType.WeaponScroll:
                case PointType.HelmetScroll:
                case PointType.ArmorScroll:
                case PointType.RingScroll:
                case PointType.ShoesScroll:
                case PointType.NecklaceScroll:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsBoardFull()
        {
            if (UIBoard.Instance == null || UIBoard.Instance.diceMap == null || UIBoard.Instance.diceMap.Length <= 0)
                return false;

            UIDice[] map = UIBoard.Instance.diceMap;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] == null)
                    return false;
            }

            return true;
        }

        private static int GetRandomEmptySlot(UIBoard board)
        {
            if (board == null || board.diceMap == null)
                return -1;

            List<int> emptySlots = new List<int>();
            for (int i = 0; i < board.diceMap.Length; i++)
            {
                if (board.diceMap[i] == null)
                    emptySlots.Add(i);
            }

            return emptySlots.Count > 0 ? emptySlots[UnityEngine.Random.Range(0, emptySlots.Count)] : -1;
        }

        private static Monster GetNearestPoisonSpreadTarget(
            List<Monster> candidates,
            Monster ignored,
            HashSet<Monster> usedTargets,
            Vector3 position)
        {
            Monster best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                Monster candidate = candidates[i];
                if (candidate == null
                    || candidate == ignored
                    || candidate.gameObject.activeInHierarchy == false
                    || (usedTargets != null && usedTargets.Contains(candidate)))
                {
                    continue;
                }

                float sqr = (candidate.transform.position - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    best = candidate;
                    bestSqr = sqr;
                }
            }

            return best;
        }

        private static string FormatValue(float value)
        {
            if (Mathf.Abs(value - Mathf.Round(value)) < 0.001f)
                return Mathf.RoundToInt(value).ToString();

            return value.ToString("0.#");
        }

        private void Load()
        {
            levels.Clear();
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                RelicSaveData saveData = JsonUtility.FromJson<RelicSaveData>(json);
                if (saveData == null)
                    return;

                summonCount = Mathf.Max(0, saveData.summonCount);
                if (saveData.levels == null)
                    return;

                for (int i = 0; i < saveData.levels.Count; i++)
                {
                    RelicLevelData data = saveData.levels[i];
                    if (data == null || data.relicId == RelicId.None)
                        continue;

                    levels[data.relicId] = Mathf.Clamp(data.level, 0, MaxLevel);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load relic save: {ex.Message}");
            }
        }

        private void Save()
        {
            RelicSaveData saveData = new RelicSaveData
            {
                summonCount = summonCount,
                levels = new List<RelicLevelData>()
            };

            foreach (KeyValuePair<RelicId, int> pair in levels)
            {
                if (pair.Key == RelicId.None || pair.Value <= 0)
                    continue;

                saveData.levels.Add(new RelicLevelData
                {
                    relicId = pair.Key,
                    level = Mathf.Clamp(pair.Value, 0, MaxLevel)
                });
            }

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }
    }
}
