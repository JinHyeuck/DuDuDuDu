using System;
using System.Collections.Generic;
using OJ.Core;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.Dice;
using OJ.Hunting;
using OJ.Point;
using OJ.Save;
using OJ.Utils;

namespace OJ.Equipment
{
    /// <summary>
    /// 장비 강화와 보석. (MIGRATION_BASELINE 8.3a)
    ///
    /// 씬·프리팹 어디에도 배치돼 있지 않은 순수 런타임 서비스다. 다만 보석 정의는
    /// <c>StaticResource</c> 에서 읽으므로, 이 객체가 만들어졌다고 데이터까지 있는 것은
    /// 아니다 — 그쪽은 2.2 에서 따로 운다.
    ///
    /// <b>전투 쪽으로 뻗던 손을 끊었다.</b> 예전 <c>OnMonsterKilled()</c> 는 인자 없이
    /// <c>GameManager.Instance.wall</c> 을 직접 붙잡아 회복시켰다. 영구 메타 서비스가
    /// 전투 씬 오브젝트를 이름으로 찾는 <b>거꾸로 된 방향</b>이라, 전투 밖에서는 의미가 없고
    /// 테스트도 못 한다. 이제 벽을 인자로 받는다 — 부르는 쪽(<c>Monster</c>)이 전투 씬에
    /// 있으니 거기서 넘기는 것이 맞다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    // 7.5 이후 ISaveOnApplicationLifecycle 이 아니다. 각자 저장할 것이 없어졌기 때문이다 —
    // 저장은 SaveService 가 파일 하나로 한다. 여기에 다시 붙이면 앱이 멈출 때 같은 파일을
    // 한 번 더 쓴다.
    public sealed class EquipmentManager : ISaveStateOwner
    {
        // 7.5: JsonUtility 로 구 키에 넣던 DTO 4종과 그 키를 함께 지웠다.
        // 같은 내용을 OJ.Core.EquipmentSave 가 담고, WriteTo/ReadFrom 이 그것을 다룬다.

        public event Action<EquipmentType> OnEquipmentChanged;
        public event Action OnGemChanged;

        private readonly Dictionary<EquipmentType, int> levels = new Dictionary<EquipmentType, int>();
        private readonly Dictionary<EquipmentType, string[]> equippedGemSlots = new Dictionary<EquipmentType, string[]>();
        private readonly Dictionary<string, int> gemInventory = new Dictionary<string, int>();
        private readonly List<GemDefinition> gemDefinitions = new List<GemDefinition>();
        private readonly Dictionary<string, GemDefinition> gemDefinitionMap = new Dictionary<string, GemDefinition>();
        private GemDefinitionDatabase gemDefinitionDatabase;


        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// 호출부가 112곳(14개 파일)이라 한 번에 못 바꾼다.
        /// </summary>
        public static EquipmentManager Instance { get; internal set; }

        private readonly PointManager points;

        /// <summary>
        /// <b>초기화가 로드 경로 밖에 있는 것이 핵심이다.</b> (7.5)
        ///
        /// 구조상 <c>ReadFrom</c> 은 <b>세이브 파일이 있을 때만</b> 불린다
        /// (<c>SaveService.TryLoadAll</c> 이 파일이 없으면 소유자 루프 전에 돌아간다).
        /// 그래서 컬렉션 생성과 초기 보석 지급이 로드 경로 안에 있으면 <b>신규 설치가
        /// 통째로 깨진다</b> — 장비 6종이 빈 딕셔너리로 남고, 시작 보석이 0개가 되어
        /// 조합도 장착도 시작할 수 없다. 그것도 조용히 깨진다. 예외는 한참 뒤
        /// 보석을 장착하려는 순간에 터진다.
        ///
        /// 구 <c>LoadAll()</c> 이 그 둘을 겸하고 있었기 때문에, 구 키를 지우면서
        /// 같이 지웠다면 그대로 사고가 됐다. 그래서 여기로 끌어올렸다.
        /// 세이브가 있으면 <c>ReadFrom</c> 이 이 위를 덮는다.
        /// </summary>
        public EquipmentManager(PointManager points)
        {
            this.points = points;

            // 순서가 있다. 보석 정의를 먼저 읽어야 초기 지급이 무엇을 줄지 안다.
            BuildGemDefinitionsFromDatabase();
            InitializeCollections();
            SeedInitialGemInventory();
        }

        public int GetLevel(EquipmentType equipmentType)
        {
            return levels.TryGetValue(equipmentType, out int value) ? Mathf.Max(1, value) : 1;
        }

        public int GetEquipmentAttack(EquipmentType equipmentType)
        {
            return EquipmentUpgradeFormula.AttackOf(ToRuleIndex(equipmentType), GetLevel(equipmentType));
        }

        /// <summary>
        /// 6종 합계. <b>순회만 여기 남고 항은 전부 순수 함수다.</b>
        /// <c>Enum.GetValues</c> 는 EquipmentType 을 봐야 해서 OJ.Core 로 못 내려간다.
        /// int 덧셈은 unchecked 에서도 순서에 무관하므로 순회 순서는 값에 영향이 없다.
        /// </summary>
        public int GetTotalEquipmentAttack()
        {
            int sum = 0;
            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
                sum += GetEquipmentAttack(equipmentType);
            return sum;
        }

        public (int goldCost, int scrollCost) GetUpgradeCost(EquipmentType equipmentType, int currentLevel)
        {
            int ruleIndex = ToRuleIndex(equipmentType);
            return (EquipmentUpgradeFormula.UpgradeGoldCostOf(ruleIndex, currentLevel),
                    EquipmentUpgradeFormula.UpgradeScrollCostOf(ruleIndex, currentLevel));
        }

        public (int goldCost, int scrollCost) GetNextUpgradeCost(EquipmentType equipmentType)
        {
            return GetUpgradeCost(equipmentType, GetLevel(equipmentType));
        }

        public bool TryLevelUp(EquipmentType equipmentType)
        {
            (int goldCost, int scrollCost) = GetNextUpgradeCost(equipmentType);
            if (!points.TrySpendEquipmentUpgrade(equipmentType, goldCost, scrollCost))
                return false;

            levels[equipmentType] = GetLevel(equipmentType) + 1;
            SaveAll();

            OnEquipmentChanged?.Invoke(equipmentType);
            OnGemChanged?.Invoke();
            return true;
        }

        public int GetSlotUnlockLevel(int slotIndex)
        {
            return EquipmentUpgradeFormula.SlotUnlockLevel(
                slotIndex, Define.MaxEquipmentSlot, Define.EquipmentSlotUnlockLevels);
        }

        public bool IsSlotUnlocked(EquipmentType equipmentType, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Define.MaxEquipmentSlot)
                return false;

            return GetLevel(equipmentType) >= GetSlotUnlockLevel(slotIndex);
        }

        // GetUnlockedSlotCount 는 여기 있었다. 호출처가 0개라 지웠다 (grep 확인, 5.2).
        // 다시 필요해지면 IsSlotUnlocked 를 0..MaxEquipmentSlot 로 세면 된다 —
        // 죽은 코드로 남겨 두면 "쓰이는 줄 알고" 유지보수 대상이 된다.

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

        /// <summary>
        /// 몬스터가 죽었을 때 보석 효과를 적용한다.
        ///
        /// <paramref name="wall"/> 을 인자로 받는다. 예전에는 여기서
        /// <c>GameManager.Instance.wall</c> 을 붙잡았는데, 그건 영구 메타 서비스가 전투 씬
        /// 오브젝트를 이름으로 찾는 것이라 방향이 거꾸로였다. 부르는 쪽이 전투 안에 있으니
        /// 거기서 넘기면 된다. 전투 밖에서는 null 을 넘기면 회복만 건너뛴다.
        /// </summary>
        public void OnMonsterKilled(Wall wall)
        {
            int heal = GetWellHpOnKill();
            if (heal > 0 && wall != null)
                wall.Heal(heal);

            int gold = GetGoldOnKill();
            if (gold > 0)
                points.Add(PointType.Gold, gold);
        }

        /// <summary>
        /// 7.5: 구 키에 직접 쓰던 것을 통합 세이브 호출로 바꿨다.
        /// <b>호출 지점(이 파일 안 5곳)은 그대로 두는 것이 중요하다</b> — 여기서 즉시
        /// 저장하지 않으면 강화·보석 획득이 앱이 백그라운드로 갈 때까지 메모리에만 남고,
        /// 모바일에서 OS 가 프로세스를 죽이는 것은 일상이다.
        ///
        /// <c>?.</c> 가 필요하다. 컨테이너가 이 매니저를 만든 <b>뒤에</b> SaveService 를
        /// 해석하므로, 생성 도중에 간접적으로 불리면 아직 없다.
        /// </summary>
        public void SaveAll() => GameContainer.SaveService?.SaveAll();

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        public void WriteTo(OJ.Core.SaveState state)
        {
            OJ.Core.EquipmentSave equipment = state.Equipment;

            // 같은 state 에 두 번 쓰일 수 있다. 비우지 않고 채우면 그 사이에 소모된 보석이
            // 지난 번 항목으로 남아 되살아난다.
            equipment.Levels.Clear();
            equipment.GemSlots.Clear();
            equipment.GemInventory.Clear();

            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                string typeName = equipmentType.ToString();

                equipment.Levels[typeName] = GetLevel(equipmentType);

                // 빈 슬롯도 빈 문자열로 자리를 채운다. 위치가 곧 슬롯 번호라 하나를 빼면
                // 뒤가 전부 한 칸씩 당겨져 다른 슬롯에 낀 보석이 된다.
                List<string> slotGemIds = new List<string>();
                if (equippedGemSlots.TryGetValue(equipmentType, out string[] slots))
                {
                    for (int i = 0; i < slots.Length; i++)
                        slotGemIds.Add(slots[i] ?? string.Empty);
                }

                equipment.GemSlots[typeName] = slotGemIds;
            }

            foreach (var pair in gemInventory)
            {
                // 0개는 안 가진 것과 같다. 남겨 두면 한 번 만져 본 보석이 전부 세이브에
                // 쌓여, 로드 때 되살아나는 값도 아닌데 파일만 계속 커진다.
                if (pair.Value <= 0)
                    continue;

                equipment.GemInventory[pair.Key] = pair.Value;
            }
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            InitializeCollections();

            // 기존 LoadAll() 의 "saveData == null" 자리다. 읽을 것이 없을 때 그냥 돌아가면
            // 보석 인벤토리가 통째로 빈 채 시작해 조합도 장착도 못 하는 상태가 된다.
            if (state == null)
            {
                SeedInitialGemInventory();
                return;
            }

            OJ.Core.EquipmentSave equipment = state.Equipment;

            // 셋 다 비어야 "이 칸을 한 번도 쓴 적 없는 세이브" 다 — LoadAll() 의 json 이
            // 빈 경우와 같다. 인벤토리만 보고 판단하면 보석을 다 써 버린 사람에게
            // 로드할 때마다 초기 보석을 다시 주게 된다.
            if (equipment.Levels.Count == 0 &&
                equipment.GemSlots.Count == 0 &&
                equipment.GemInventory.Count == 0)
            {
                SeedInitialGemInventory();
                return;
            }

            foreach (var pair in equipment.Levels)
            {
                // 없어진 EquipmentType 이름이 세이브에 남아 있을 수 있다. 그것 때문에
                // 로드 전체가 죽으면 안 되므로 조용히 건너뛴다.
                if (!Enum.TryParse(pair.Key, out EquipmentType equipmentType))
                    continue;
                // TryParse 는 "9" 같은 숫자 문자열도 통과시켜 정의에 없는 값을 만들어 낸다.
                // InitializeCollections() 가 깔아 둔 실제 장비만 받는다.
                if (!levels.ContainsKey(equipmentType))
                    continue;

                // 레벨 하한 1. 강화 비용·공격력 계산식이 레벨 1부터를 전제하고, GetLevel()
                // 도 항상 1 이상을 돌려준다. 0 이 들어오면 표시와 계산이 어긋난다.
                levels[equipmentType] = Mathf.Max(1, pair.Value);
            }

            foreach (var pair in equipment.GemSlots)
            {
                if (!Enum.TryParse(pair.Key, out EquipmentType equipmentType))
                    continue;
                if (!equippedGemSlots.TryGetValue(equipmentType, out string[] slots))
                    continue;

                List<string> savedSlots = pair.Value;
                if (savedSlots == null)
                    continue;

                // 겹치는 만큼만 옮긴다. Define.MaxEquipmentSlot 이 줄면 넘치는 보석은 버리고,
                // 늘면 나머지는 InitializeCollections() 가 만든 빈 슬롯 그대로 남는다.
                for (int slot = 0; slot < slots.Length && slot < savedSlots.Count; slot++)
                    slots[slot] = savedSlots[slot] ?? string.Empty;
            }

            foreach (var pair in equipment.GemInventory)
            {
                if (string.IsNullOrEmpty(pair.Key))
                    continue;

                // 음수 개수는 GetGemCount() 가 0 으로 가려 주지만 저장된 값 자체는 음수로
                // 남는다. 그 상태로 AddGem 하면 얻은 만큼이 음수를 메우는 데 먼저 쓰인다.
                gemInventory[pair.Key] = Mathf.Max(0, pair.Value);
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

        /// <summary>
        /// enum → OJ.Core 규칙표 인덱스. 42개 리터럴은
        /// <see cref="EquipmentUpgradeFormula.Rule"/> 로 통째로 내려갔고, 여기 남은 것은
        /// <b>이름 대 이름</b> 매핑뿐이다. (5.2)
        ///
        /// <c>(int)equipmentType</c> 캐스트로 쓰지 않은 것이 이 함수의 존재 이유다.
        /// 캐스트는 <b>enum 선언 순서</b>에 값을 걸어 버린다 — 결정 2번으로 enum 리네임·
        /// 재배열 금지 규약이 풀린 상태라, 누가 Weapon 과 Helmet 을 바꿔 적는 순간
        /// 무기 골드가 조용히 투구 골드가 되고 컴파일러도 테스트도 아무 말을 안 한다.
        /// 이름으로 매핑하면 재배열은 값에 영향이 없고, 새 장비가 늘면 여기서
        /// <b>컴파일 경고 없이 default 로 떨어지는 것</b>이 유일한 위험으로 좁혀진다.
        ///
        /// default 는 원본 <c>GetRule</c> 의 <c>default:</c> 와 같은 자리다. 지금은
        /// EquipmentType 이 6값뿐이라 도달하지 않는다.
        /// </summary>
        private static int ToRuleIndex(EquipmentType equipmentType)
        {
            switch (equipmentType)
            {
                case EquipmentType.Weapon:
                    return EquipmentUpgradeFormula.WeaponIndex;
                case EquipmentType.Helmet:
                    return EquipmentUpgradeFormula.HelmetIndex;
                case EquipmentType.Armor:
                    return EquipmentUpgradeFormula.ArmorIndex;
                case EquipmentType.Ring:
                    return EquipmentUpgradeFormula.RingIndex;
                case EquipmentType.Shoes:
                    return EquipmentUpgradeFormula.ShoesIndex;
                case EquipmentType.Necklace:
                    return EquipmentUpgradeFormula.NecklaceIndex;
                default:
                    return EquipmentUpgradeFormula.UnknownIndex;
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

            StaticResource resource = StaticResource.Instance;
            if (resource != null && resource.GemDefinitionDatabase != null)
            {
                gemDefinitionDatabase = resource.GemDefinitionDatabase;
                return gemDefinitionDatabase;
            }

            // 예전에는 Resources.Load("GemDefinitionDatabase") 로 물러섰다. 그 에셋은
            // Assets/ScriptableObject/ 에 있어 Resources 규약 밖이라 이 폴백은 한 번도
            // 성공한 적이 없고, 그냥 null 을 돌려줘 보석 56종이 통째로 사라졌다. (2.2)
            LogMissingGemDatabaseOnce();
            return null;
        }

        private static bool missingGemDatabaseLogged;

        private static void LogMissingGemDatabaseOnce()
        {
            if (missingGemDatabaseLogged)
                return;

            missingGemDatabaseLogged = true;
            Debug.LogError(
                "GemDefinitionDatabase 를 찾지 못했다. StaticResource 프리팹의 GemDefinitionDatabase " +
                "슬롯이 비었거나 StaticResource 자체가 만들어지지 않았다. 장비 보석이 전부 사라진다.");
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
