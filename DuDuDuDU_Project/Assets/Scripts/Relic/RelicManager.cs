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

namespace OJ.Relic
{
    /// <summary>
    /// 유물 보유·레벨과 유물 효과. (MIGRATION_BASELINE 8.3b)
    ///
    /// 이 클래스는 두 가지를 한 몸에 갖고 있다 — 저장되는 <i>메타 상태</i>(레벨, 소환 횟수)와
    /// 전투 중 <i>효과 적용</i>(보드에 주사위 소환, SP 추가, 몬스터 조회). 뒤쪽이
    /// <c>UIBoard</c> · <c>DiceTypeStarManager</c> · <c>GameManager</c> · <c>MonsterManager</c> ·
    /// <c>UIDiceSummonSystem</c> 다섯을 <b>이름으로</b> 붙잡고 있었다. 8.3a 에서 수명만 먼저
    /// 컨테이너로 옮기고 전투 참조는 남겨 뒀는데, 그 다섯이 8.3b 에서
    /// <see cref="IBattleRefs"/> 창구로 바뀌었다. 두 가지를 한 커밋에 섞지 않은 이유가
    /// 이것이다 — 수명과 참조가 같이 흔들리면 무엇이 깨졌는지 가릴 수 없다.
    ///
    /// <b>이것은 루트에 사는 영구 서비스다.</b> 로비·타이틀에도 살아 있고 거기서도 불린다
    /// (유물 소환, 효과 문구, 세이브). 그때 <c>battle.Board</c> 같은 참조는
    /// <b>정상적으로 null</b> 이다. 그래서 아래에 남아 있는 null 검사들은 사고 방지가 아니라
    /// "전투 밖에서는 할 일이 없다"는 뜻이다 — 지우면 로비에서 터진다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class RelicManager : ISaveStateOwner
    {
        private static readonly DiceType[] BasicDiceTypes =
        {
            DiceType.Normal,
            DiceType.Fire,
            DiceType.Ice,
            DiceType.Poison,
            DiceType.Thunder,
        };

        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// 호출부가 95곳(21개 파일)이다.
        /// </summary>
        public static RelicManager Instance { get; internal set; }

        public event Action OnRelicChanged;
        public event Action OnSummonCountChanged;

        // "하나도 안 가진 상태"가 곧 신규 설치의 정상 상태다. 그래서 이 필드 초기화 하나로
        // 첫 실행 준비가 끝나고, 생성자에서 따로 해 둘 일이 없다. (7.5 에서 구 로드 경로를
        // 지울 때 거기 섞여 있던 초기화를 같이 잃지 않았는지 확인한 결과다 — 유물은
        // 초기 지급도, 미리 채워 둘 칸도 없어서 잃을 것이 없었다.)
        private readonly Dictionary<RelicId, int> levels = new Dictionary<RelicId, int>();

        /// <summary>
        /// BattleScene 매니저로 가는 창구. (8.3b)
        ///
        /// 창구 자체는 루트 컨테이너가 소유하므로 <b>언제나 있다.</b> 반면 그 안의 참조는
        /// 배틀 스코프가 채우고 비우므로 <b>전투 밖에서는 전부 null 이다.</b> 이 서비스는
        /// 로비에서도 살아 있으니 그 null 은 정상 상태이고, 아래 호출부의 null 검사가
        /// 그것을 뜻한다.
        /// </summary>
        private readonly IBattleRefs battle;

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

        /// <summary>
        /// 컨테이너가 부른다. <b>여기서 <paramref name="battleRefs"/> 안을 들여다보지 마라</b> —
        /// 루트 컨테이너가 세워지는 시점에는 BattleScene 이 아직 없어서 전부 null 이다.
        /// 창구는 들고만 있고, 읽는 것은 실제로 효과를 적용하는 순간에 한다.
        /// </summary>
        public RelicManager(IBattleRefs battleRefs)
        {
            battle = battleRefs;
        }

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        public void WriteTo(OJ.Core.SaveState state)
        {
            if (state == null)
                return;

            state.Relics.SummonCount = summonCount;

            // 통합 세이브의 컬렉션은 계속 재사용되는 인스턴스다. 지우지 않으면 지난번에 쓴
            // 유물이 남아 "잃은 유물"이 되살아난다. 아래 루프는 덮어쓰기만 하지 지우지
            // 않으므로, 여기서 비우지 않으면 그 잔재를 걷어낼 곳이 없다.
            state.Relics.Levels.Clear();

            foreach (KeyValuePair<RelicId, int> pair in levels)
            {
                // 레벨 0 은 "안 가진 것"과 같은 뜻이라 적을 이유가 없고, None 은 유물이 아니라
                // enum 기본값이다. 이게 새어 나가면 존재하지 않는 유물 한 칸이 생긴다.
                if (pair.Key == RelicId.None || pair.Value <= 0)
                    continue;

                state.Relics.Levels[pair.Key.ToString()] = Mathf.Clamp(pair.Value, 0, MaxLevel);
            }
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            // 로드는 초기화다. 세이브가 비어 있더라도 이전에 들고 있던 유물이 남으면 안 된다.
            levels.Clear();
            if (state == null)
                return;

            // 소환 횟수가 음수면 소환 비용표 조회가 앞쪽으로 되감겨 비용이 다시 싸진다.
            // 손상된 세이브 한 줄이 무한 저렴 소환이 되지 않도록 바닥을 둔다.
            summonCount = Mathf.Max(0, state.Relics.SummonCount);

            foreach (KeyValuePair<string, int> pair in state.Relics.Levels)
            {
                // 없어진 유물 이름이 옛 세이브에 남아 있을 수 있다. 그 한 줄 때문에 로드 전체가
                // 죽으면 진행도를 통째로 잃으므로 조용히 버린다.
                if (!System.Enum.TryParse(pair.Key, out RelicId relicId) || relicId == RelicId.None)
                    continue;

                // 데이터에서 maxLevel 을 내리면 저장된 레벨이 성장 수식 범위를 벗어나
                // GetPrimaryValue 가 엉뚱한 값을 낸다. 그래서 읽는 순간 잘라 둔다.
                levels[relicId] = Mathf.Clamp(pair.Value, 0, MaxLevel);
            }
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

            // 전투 밖(로비)에서 불리면 board 가 null 이다. 그건 사고가 아니라
            // "지금은 놓을 보드가 없다"는 정상 상태라 조용히 나간다.
            UIBoard board = battle.Board;
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

            battle.DiceStars?.OnDiceSpawn(type, star);
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
            return RelicEffectFormula.CooldownReductionPercent(
                GetPrimaryValue(RelicId.QuickHands),
                GetPrimaryValue(RelicId.LastWall),
                IsLastWallCooldownActive());
        }

        public float GetDamageMultiplier(DiceType diceType)
        {
            return RelicEffectFormula.DamageMultiplier(
                GetPrimaryValue(RelicId.FullBoardPressure),
                HasRelic(RelicId.FullBoardPressure) && IsBoardFull(),
                GetPrimaryValue(RelicId.CrownResonance),
                HasCrownResonance(diceType));
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

            // 위와 같다 — 전투 밖에서는 board 가 null 인 것이 정상이다.
            UIBoard board = battle.Board;
            if (board == null || board.diceMap == null)
                return false;

            int slotIndex = GetRandomEmptySlot(board);
            if (slotIndex < 0)
                return false;

            const int twinStar = 1;
            battle.DiceStars?.OnDiceSpawn(type, twinStar);
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
            // 한 번만 읽어 지역에 담는다. 창구를 두 번 읽는 사이에 배틀 스코프가 파괴되면
            // 검사와 사용이 서로 다른 상태를 보게 되는데, 그 창이 좁다고 없는 것은 아니다.
            UIDiceSummonSystem summon = battle.Summon;
            if (!HasRelic(RelicId.MergeInsurance)
                || summon == null
                || UnityEngine.Random.value * 100f > GetPrimaryValue(RelicId.MergeInsurance))
            {
                return;
            }

            summon.AddSP(Mathf.RoundToInt(GetSecondaryValue(RelicId.MergeInsurance)));
        }

        public void OnMythicCrafted(DiceType mythicType)
        {
            if (firstMythicCrafted || !HasRelic(RelicId.KingBlueprint))
                return;

            if (DiceMetaDataProvider.IsSummonable(mythicType))
                return;

            firstMythicCrafted = true;
            battle.Summon?.AddSP(Mathf.RoundToInt(GetPrimaryValue(RelicId.KingBlueprint)));
        }

        public bool TryTriggerLastWall()
        {
            if (lastWallTriggered || !HasRelic(RelicId.LastWall))
                return false;

            lastWallTriggered = true;

            // -1 은 "쿨다운 걸 웨이브를 모른다"는 뜻이고, IsLastWallCooldownActive 가
            // 그 값을 비활성으로 읽는다. 전투 밖이라면 걸어 둘 웨이브 자체가 없으니 맞는 값이다.
            GameManager game = battle.Game;
            lastWallCooldownWaveIndex = game != null ? game.CurrentWaveIndex : -1;
            return true;
        }

        public void OnMonsterKilled(Monster killedMonster, bool wasPoisoned, Vector3 deathPosition)
        {
            if (!wasPoisoned || !HasRelic(RelicId.PoisonIncense))
                return;

            // 몬스터가 죽는 것은 전투 안에서뿐이지만, 이 서비스는 루트에 살아서
            // 씬이 내려가는 도중에도 불릴 수 있다. 그때는 창구가 이미 비어 있다.
            MonsterManager monsters = battle.Monsters;
            if (monsters == null || monsters.activeMonsters == null)
                return;

            int targetCount = Mathf.Max(1, Mathf.RoundToInt(GetPrimaryValue(RelicId.PoisonIncense)));
            float poisonMultiplier = Mathf.Max(1f, GetSecondaryValue(RelicId.PoisonIncense));
            List<Monster> candidates = monsters.activeMonsters;

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
            return RelicEffectFormula.PercentToInt(GetPrimaryValue(RelicId.FrostNail));
        }

        public int GetTornadoDamageTakenBonusPercent()
        {
            return RelicEffectFormula.PercentToInt(GetPrimaryValue(RelicId.TornadoAnchor));
        }

        public float GetTornadoDamageTakenBonusDuration()
        {
            return RelicEffectFormula.DurationWithFloor(GetDuration(RelicId.TornadoAnchor));
        }

        public float GetStunChanceBonusPercent()
        {
            return GetPrimaryValue(RelicId.ParalysisNeedle);
        }

        public int GetStunDamageTakenBonusPercent()
        {
            return RelicEffectFormula.PercentToInt(GetSecondaryValue(RelicId.ParalysisNeedle));
        }

        public int GetArmorBreakPercentBonus()
        {
            return RelicEffectFormula.PercentToInt(GetPrimaryValue(RelicId.CrackHammer));
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
            return RelicEffectFormula.PercentToInt(GetSecondaryValue(RelicId.TailwindFeather));
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
            // 전투가 없으면 쿨다운도 없다. game == null 이 곧 그 뜻이다.
            GameManager game = battle.Game;
            return lastWallCooldownWaveIndex >= 0
                && game != null
                && game.inGameState == InGameState.Wave
                && game.CurrentWaveIndex == lastWallCooldownWaveIndex;
        }

        private bool HasCrownResonance(DiceType diceType)
        {
            // 다섯 갈래가 같은 대상을 보게 지역에 담는다. 전투 밖이면 null 이고,
            // 그때 공명은 성립할 수 없으니 false 가 맞는 답이다.
            DiceTypeStarManager diceStars = battle.DiceStars;
            if (!HasRelic(RelicId.CrownResonance) || diceStars == null)
                return false;

            switch (diceType)
            {
                case DiceType.Normal:
                    return diceStars.GetTypeCount(DiceType.KingNormal) > 0;
                case DiceType.Fire:
                    return diceStars.GetTypeCount(DiceType.KingFire) > 0;
                case DiceType.Ice:
                    return diceStars.GetTypeCount(DiceType.KingIce) > 0;
                case DiceType.Thunder:
                    return diceStars.GetTypeCount(DiceType.KingThunder) > 0;
                case DiceType.Poison:
                    return diceStars.GetTypeCount(DiceType.KingPoison) > 0;
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

        /// <summary>
        /// 8.3b 에서 <c>static</c> 을 뗐다. 보드를 <c>UIBoard.Instance</c> 라는 전역에서
        /// 꺼내던 것이 인스턴스 필드인 창구를 통하게 바뀌었기 때문이다 — 정적 메서드는
        /// 창구를 볼 수 없다. 하는 일은 그대로다.
        /// </summary>
        private bool IsBoardFull()
        {
            UIBoard board = battle.Board;
            if (board == null || board.diceMap == null || board.diceMap.Length <= 0)
                return false;

            UIDice[] map = board.diceMap;
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

        /// <summary>
        /// 거래가 끝난 그 자리에서 통합 세이브를 파일에 쓴다.
        ///
        /// <b>왜 호출 지점을 그대로 뒀는가.</b> 7.5 에서 바뀐 것은 저장 매체뿐이다 —
        /// 소환과 유물 획득은 재화를 <b>먼저</b> 깎고 결과를 나중에 주는 거래라, 여기서
        /// 즉시 쓰지 않으면 앱이 백그라운드로 갈 때까지 그 결과가 메모리에만 남는다.
        /// 모바일에서 OS 가 프로세스를 죽이는 것은 사고가 아니라 일상이고, 그러면
        /// <b>골드와 티켓만 사라지고 유물은 없는</b> 상태로 되돌아간다.
        ///
        /// <b>왜 <c>?.</c> 인가.</b> 컨테이너는 매니저를 전부 만든 뒤에야 SaveService 를
        /// 해석한다. 매니저 생성자가 도는 동안 간접적으로 여기 들어오면 SaveService 가
        /// 아직 null 이고, 그때는 조용히 건너뛰는 것이 맞다 — 아직 아무 거래도 일어나지
        /// 않았으니 굳혀 둘 진행도가 없다.
        /// </summary>
        private void Save()
        {
            GameContainer.SaveService?.SaveAll();
        }
    }
}
