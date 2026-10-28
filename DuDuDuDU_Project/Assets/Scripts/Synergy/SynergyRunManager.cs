using System;
using System.Collections.Generic;
using OJ.Core;
using OJ.Dice;
using OJ.DI;
using OJ.Equipment;
using OJ.Hunting;
using OJ.Utils;
using UnityEngine;

namespace OJ.Synergy
{
    /// <summary>
    /// 한 판 동안의 시너지 — 장착 프리셋을 읽어 조건을 세고, 문턱을 넘으면 효과를 낸다.
    ///
    /// <b>발동은 통지 자리에서 바로 하지 않고 다음 <see cref="Tick"/> 에서 한다.</b> 처치 통지는
    /// <c>Monster.TakeDamage</c> 의 사망 처리 한가운데서 오는데, 거기서 전체 피해를 내면 같은 목록을
    /// 도는 중에 다른 몬스터가 죽고, 그 처치가 다시 통지를 부른다. 큐에 넣어 두면 그 재진입이 없다.
    ///
    /// 통합계 효과로 일어난 발동은 <b>어떤 카운트에도 넣지 않는다</b>(무한 루프 방지, 사용자 결정).
    /// 튜토리얼·무한의 탑에서는 켜지 않는다(유물과 같은 관례).
    /// </summary>
    public sealed class SynergyRunManager
    {
        /// <summary>장착된 계열 슬롯 하나의 상태.</summary>
        public sealed class Slot
        {
            public SynergyCategory Category;
            public readonly SynergyLine Condition = new SynergyLine();
            public SynergyOption ConditionOption;
            public SynergyCounter Counter;
            public readonly List<SynergyLine> Effects = new List<SynergyLine>();
            public readonly List<SynergyOption> EffectOptions = new List<SynergyOption>();

            /// <summary>효과 줄마다 지금 걸린 버프. 버프가 아닌 줄은 null.</summary>
            public readonly List<Buff> Buffs = new List<Buff>();

            /// <summary>이 판에서 발동한 횟수(상세 창에 보인다).</summary>
            public int Fires;
        }

        /// <summary>효과 줄 하나가 건 버프.</summary>
        public sealed class Buff
        {
            public SynergyEffectId Effect;
            public int Target = -1;
            public int ValuePerStack;
            public int Stacks;
            public int MaxStacks;
            public float Remaining;
            public float Duration;

            public bool IsOn => Stacks > 0 && Remaining > 0f;
            public int Total => IsOn ? ValuePerStack * Stacks : 0;
        }

        private struct PendingFire
        {
            public SynergyCategory Category;
            public bool FromUnified;
        }

        /// <summary>계열이 발동했다. 인자: 계열, 통합계가 일으킨 것인가.</summary>
        public event Action<SynergyCategory, bool> OnFired;

        /// <summary>계열의 효과가 몬스터 하나를 쳤다(직선 연출).</summary>
        public event Action<SynergyCategory, Monster> OnStrike;

        /// <summary>통합계가 다른 계열을 움직였다(곡선 연출).</summary>
        public event Action<SynergyCategory> OnUnifiedLink;

        /// <summary>카운터나 버프가 바뀌었다(HUD 갱신).</summary>
        public event Action OnStateChanged;

        private static readonly Color DamageColor = new Color(0.85f, 0.55f, 1f, 1f);

        private readonly IBattleRefs battle;
        private readonly Slot[] slots = new Slot[SynergyRules.CategoryCount];
        private readonly Queue<PendingFire> pending = new Queue<PendingFire>();
        private readonly List<Monster> targetBuffer = new List<Monster>();
        private int pendingSummonStar;

        public bool IsActive { get; private set; }

        public SynergyRunManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        /// <summary>장착된 계열 슬롯. 안 열렸거나 비었으면 null.</summary>
        public Slot GetSlot(SynergyCategory category) => slots[(int)category];

        // ── 수명 ───────────────────────────────────────────────

        /// <summary>판을 시작한다. 장착 프리셋을 그대로 복사해 둔다 — 판 도중에 로비 값이 바뀌어도 이 판은 그대로다.</summary>
        public void Begin()
        {
            SynergyManager manager = SynergyManager.Instance;
            if (manager == null)
                return;

            int openLines = manager.OpenEffectLines;
            bool any = false;
            for (int c = 0; c < SynergyRules.CategoryCount; c++)
            {
                var category = (SynergyCategory)c;
                slots[c] = null;
                if (!manager.IsOpen(category))
                    continue;

                SynergyPreset preset = manager.GetEquipped(category);
                SynergyOption condition = SynergyRules.Resolve(preset.Condition, true);
                if (condition == null)
                    continue;

                var slot = new Slot { Category = category, ConditionOption = condition };
                slot.Condition.CopyFrom(preset.Condition);
                slot.Counter = new SynergyCounter(preset.Condition.Value);

                for (int i = 0; i < openLines; i++)
                {
                    SynergyOption option = SynergyRules.Resolve(preset.Effects[i], false);
                    if (option == null)
                        continue;

                    var line = new SynergyLine();
                    line.CopyFrom(preset.Effects[i]);
                    slot.Effects.Add(line);
                    slot.EffectOptions.Add(option);
                    slot.Buffs.Add(option.IsBuff ? new Buff { Effect = (SynergyEffectId)option.Id } : null);
                }

                slots[c] = slot;
                any = true;
            }

            IsActive = any;
            if (!any)
                return;

            battle.BattlePoints.OnSpent += OnSpent;
            SynergyBuffs.Reset();
            UISynergyHud.Create(this);
        }

        /// <summary>판이 끝나거나 씬이 내려간다.</summary>
        public void End()
        {
            if (IsActive && battle.BattlePoints != null)
                battle.BattlePoints.OnSpent -= OnSpent;

            IsActive = false;
            pending.Clear();
            SynergyBuffs.Reset();
        }

        /// <summary>되감았다. 카운터·버프·다음 소환 예약을 처음으로 돌린다(되감은 웨이브의 진행은 무효다).</summary>
        public void ResetForRewind()
        {
            if (!IsActive)
                return;

            pending.Clear();
            pendingSummonStar = 0;
            foreach (Slot slot in slots)
            {
                if (slot == null)
                    continue;

                slot.Counter.Reset();
                foreach (Buff buff in slot.Buffs)
                {
                    if (buff != null)
                        buff.Stacks = 0;
                }
            }

            RebuildBuffs();
            OnStateChanged?.Invoke();
        }

        /// <summary>매 프레임. 큐에 쌓인 발동을 내고 버프 시간을 깎는다.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsActive)
                return;

            bool changed = false;

            // 한 프레임에 내는 수에 바닥을 둔다. 통합계가 큐를 다시 채우므로 무한히 돌 수는 없지만
            // (통합계발 발동은 카운트에 안 들어간다) 한 프레임이 길어지지 않게 한다.
            for (int guard = 0; guard < 32 && pending.Count > 0; guard++)
            {
                PendingFire fire = pending.Dequeue();
                Execute(fire);
                changed = true;
            }

            bool buffsChanged = false;
            foreach (Slot slot in slots)
            {
                if (slot == null)
                    continue;

                foreach (Buff buff in slot.Buffs)
                {
                    if (buff == null || buff.Stacks <= 0)
                        continue;

                    buff.Remaining -= deltaTime;
                    if (buff.Remaining <= 0f)
                    {
                        buff.Stacks = 0;
                        buff.Remaining = 0f;
                        buffsChanged = true;
                    }
                }
            }

            if (buffsChanged)
                RebuildBuffs();

            if (changed || buffsChanged)
                OnStateChanged?.Invoke();
        }

        // ── 통지 (한 줄씩 꽂힌 자리에서 부른다) ─────────────────

        public void NotifyShot(DiceType diceType)
        {
            Slot slot = slots[(int)SynergyCategory.Dice];
            if (!IsActive || slot == null)
                return;

            switch ((SynergyConditionId)slot.Condition.OptionId)
            {
                case SynergyConditionId.ElementAttack:
                    if (HasElement(diceType, slot.Condition.Target))
                        Count(slot, 1);
                    break;
                case SynergyConditionId.DiceAttack:
                    if ((int)diceType == slot.Condition.Target)
                        Count(slot, 1);
                    break;
                case SynergyConditionId.EvolvedDiceAttack:
                    if (DiceEvolution.GetTier(diceType) != DiceTier.Base)
                        Count(slot, 1);
                    break;
            }
        }

        public void NotifyKilled(Monster monster)
        {
            if (!IsActive || monster == null)
                return;

            Count(SynergyCategory.Combat, SynergyConditionId.MonsterKill, 1);
            if (monster.IsStageBoss)
                Count(SynergyCategory.Combat, SynergyConditionId.BossKill, 1);
            if (monster.IsBounty)
                Count(SynergyCategory.Combat, SynergyConditionId.BountyKill, 1);
        }

        public void NotifyWaveEntered() => Count(SynergyCategory.Combat, SynergyConditionId.WaveEnter, 1);

        public void NotifyWallHit() => Count(SynergyCategory.Combat, SynergyConditionId.WallHit, 1);

        public void NotifyMerge() => Count(SynergyCategory.Action, SynergyConditionId.Merge, 1);

        public void NotifyEvolve() => Count(SynergyCategory.Action, SynergyConditionId.Evolve, 1);

        public void NotifyUpgrade() => Count(SynergyCategory.Action, SynergyConditionId.Upgrade, 1);

        public void NotifySummon() => Count(SynergyCategory.Action, SynergyConditionId.Summon, 1);

        public void NotifyExchange() => Count(SynergyCategory.Action, SynergyConditionId.Exchange, 1);

        private void OnSpent(BattlePointType type, int amount)
        {
            if (type == BattlePointType.SummonPoint)
                Count(SynergyCategory.Action, SynergyConditionId.SpendSp, amount);
            else if (type == BattlePointType.EnhanceStone)
                Count(SynergyCategory.Action, SynergyConditionId.SpendStone, amount);
        }

        /// <summary>다음 소환의 성급 예약을 꺼낸다(0 이면 없음). 한 번 꺼내면 사라진다.</summary>
        public int ConsumeSummonStar()
        {
            int star = pendingSummonStar;
            pendingSummonStar = 0;
            return star;
        }

        // ── 카운트와 발동 ───────────────────────────────────────

        private void Count(SynergyCategory category, SynergyConditionId condition, int amount)
        {
            Slot slot = slots[(int)category];
            if (!IsActive || slot == null || slot.Condition.OptionId != (int)condition)
                return;

            Count(slot, amount);
        }

        private void Count(Slot slot, int amount)
        {
            int fires = slot.Counter.Add(amount);
            for (int i = 0; i < fires; i++)
                pending.Enqueue(new PendingFire { Category = slot.Category });

            OnStateChanged?.Invoke();
        }

        private void Execute(PendingFire fire)
        {
            Slot slot = slots[(int)fire.Category];
            if (slot == null)
                return;

            slot.Fires++;
            for (int i = 0; i < slot.Effects.Count; i++)
                Apply(slot, i);

            OnFired?.Invoke(fire.Category, fire.FromUnified);

            if (fire.FromUnified || fire.Category == SynergyCategory.Unified)
                return;

            SynergyManager.Instance?.AddFires(1);

            switch (fire.Category)
            {
                case SynergyCategory.Dice:
                    Count(SynergyCategory.Unified, SynergyConditionId.DiceSynergyFired, 1);
                    break;
                case SynergyCategory.Combat:
                    Count(SynergyCategory.Unified, SynergyConditionId.CombatSynergyFired, 1);
                    break;
                case SynergyCategory.Action:
                    Count(SynergyCategory.Unified, SynergyConditionId.ActionSynergyFired, 1);
                    break;
            }

            Count(SynergyCategory.Unified, SynergyConditionId.AnySynergyFired, 1);
        }

        private void Apply(Slot slot, int index)
        {
            SynergyLine line = slot.Effects[index];
            SynergyOption option = slot.EffectOptions[index];
            int value = line.Value;

            if (option.IsBuff)
            {
                AddBuff(slot, index, line, option);
                return;
            }

            switch ((SynergyEffectId)option.Id)
            {
                case SynergyEffectId.SingleFixedDamage:
                    StrikeFront(slot.Category, value);
                    break;
                case SynergyEffectId.SinglePercentDamage:
                    StrikeFront(slot.Category, AttackDamage(value));
                    break;
                case SynergyEffectId.AllFixedDamage:
                    StrikeAll(value);
                    break;
                case SynergyEffectId.AllPercentDamage:
                    StrikeAll(AttackDamage(value));
                    break;
                case SynergyEffectId.ExecuteBelowHp:
                    ExecuteBelow(slot.Category, value);
                    break;

                case SynergyEffectId.OpenSlot:
                    // 다 열렸으면 꽝이다(사용자 결정). GrantFreeSlots 가 열 칸이 없으면 0 을 돌려준다.
                    battle.Board?.GrantFreeSlots(value);
                    break;
                case SynergyEffectId.HealWall:
                    Wall wall = battle.Game != null ? battle.Game.wall : null;
                    if (wall != null)
                        wall.Heal(Mathf.CeilToInt(wall.TotalHp * value / 100f));
                    break;
                case SynergyEffectId.GrantSp:
                    battle.Summon?.AddSP(value);
                    break;
                case SynergyEffectId.GrantSpPercent:
                    battle.Summon?.AddSP(battle.BattlePoints.Get(BattlePointType.SummonPoint) * value / 100);
                    break;
                case SynergyEffectId.GrantStone:
                    battle.BattlePoints.Add(BattlePointType.EnhanceStone, value);
                    break;
                case SynergyEffectId.GrantStonePercent:
                    battle.BattlePoints.Add(BattlePointType.EnhanceStone,
                        battle.BattlePoints.Get(BattlePointType.EnhanceStone) * value / 100);
                    break;

                case SynergyEffectId.NextSummonStar:
                    pendingSummonStar = Mathf.Max(pendingSummonStar, value);
                    break;

                case SynergyEffectId.FireDiceSynergy:
                    FireFromUnified(SynergyCategory.Dice, value);
                    break;
                case SynergyEffectId.FireCombatSynergy:
                    FireFromUnified(SynergyCategory.Combat, value);
                    break;
                case SynergyEffectId.FireActionSynergy:
                    FireFromUnified(SynergyCategory.Action, value);
                    break;
                case SynergyEffectId.FillCounters:
                    FillOthers(value);
                    break;
            }
        }

        private void FireFromUnified(SynergyCategory category, int times)
        {
            if (slots[(int)category] == null)
                return;

            for (int i = 0; i < times; i++)
                pending.Enqueue(new PendingFire { Category = category, FromUnified = true });

            OnUnifiedLink?.Invoke(category);
        }

        private void FillOthers(int percent)
        {
            for (int c = 0; c < (int)SynergyCategory.Unified; c++)
            {
                Slot slot = slots[c];
                if (slot == null)
                    continue;

                int amount = Mathf.Max(1, Mathf.CeilToInt(slot.Counter.Threshold * percent / 100f));
                int fires = slot.Counter.Add(amount);
                for (int i = 0; i < fires; i++)
                    pending.Enqueue(new PendingFire { Category = slot.Category, FromUnified = true });

                OnUnifiedLink?.Invoke(slot.Category);
            }
        }

        // ── 버프 ───────────────────────────────────────────────

        private void AddBuff(Slot slot, int index, SynergyLine line, SynergyOption option)
        {
            Buff buff = slot.Buffs[index];
            buff.ValuePerStack = line.Value;
            buff.MaxStacks = option.MaxStacks;
            buff.Duration = option.Seconds;
            buff.Remaining = option.Seconds;
            buff.Target = line.Target;

            if ((SynergyEffectId)option.Id == SynergyEffectId.RandomDiceBuff)
            {
                // 발동할 때마다 보드에서 새로 고른다. 보드가 비었으면 꽝.
                buff.Target = RandomBoardDice();
                if (buff.Target < 0)
                    return;

                buff.Stacks = 1;
            }
            else
            {
                buff.Stacks = Mathf.Min(buff.Stacks + 1, Mathf.Max(1, option.MaxStacks));
            }

            RebuildBuffs();
        }

        private void RebuildBuffs()
        {
            SynergyBuffs.Reset();
            foreach (Slot slot in slots)
            {
                if (slot == null)
                    continue;

                foreach (Buff buff in slot.Buffs)
                {
                    if (buff == null || !buff.IsOn)
                        continue;

                    switch (buff.Effect)
                    {
                        case SynergyEffectId.AttackBuff:
                        case SynergyEffectId.AttackBuffStack:
                            SynergyBuffs.AddAttack(buff.Total);
                            break;
                        case SynergyEffectId.ElementBuff:
                        case SynergyEffectId.ElementBuffStack:
                            SynergyBuffs.AddElement(buff.Target, buff.Total);
                            break;
                        case SynergyEffectId.DiceBuff:
                        case SynergyEffectId.DiceBuffStack:
                        case SynergyEffectId.RandomDiceBuff:
                            SynergyBuffs.AddDice(buff.Target, buff.Total);
                            break;
                        case SynergyEffectId.BossDamageBuff:
                            SynergyBuffs.AddBoss(buff.Total);
                            break;
                        case SynergyEffectId.WallGuard:
                            SynergyBuffs.AddWallGuard(buff.Total);
                            break;
                    }
                }
            }
        }

        private int RandomBoardDice()
        {
            DiceTypeStarManager stars = battle.DiceStars;
            if (stars == null)
                return -1;

            var onBoard = new List<int>();
            foreach (KeyValuePair<DiceType, int> pair in stars.typeCountTotals)
            {
                if (pair.Value > 0)
                    onBoard.Add((int)pair.Key);
            }

            return onBoard.Count == 0 ? -1 : onBoard[UnityEngine.Random.Range(0, onBoard.Count)];
        }

        // ── 피해 ───────────────────────────────────────────────

        /// <summary>공격력(장비 공격력 합)의 <paramref name="percent"/>%. 사용자 수치 300~1200% 가 이 기준이다.</summary>
        private static int AttackDamage(int percent)
        {
            int attack = EquipmentManager.Instance != null ? EquipmentManager.Instance.GetTotalEquipmentAttack() : 0;
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, attack) * percent / 100f));
        }

        /// <summary>교전선 안에 들어온 살아 있는 몬스터 중 벽에 가장 가까운 것(y 가 가장 작은 것).</summary>
        private Monster FrontMonster(Func<Monster, bool> filter = null)
        {
            Monster front = null;
            float minY = float.MaxValue;
            foreach (Monster m in battle.Monsters.EngagedMonsters)
            {
                if (m == null || !m.gameObject.activeInHierarchy || m.HpPercent <= 0)
                    continue;

                if (filter != null && !filter(m))
                    continue;

                float y = m.transform.position.y;
                if (y < minY)
                {
                    minY = y;
                    front = m;
                }
            }

            return front;
        }

        private void StrikeFront(SynergyCategory category, int damage)
        {
            Monster target = FrontMonster();
            if (target == null)
                return;

            OnStrike?.Invoke(category, target);
            Hit(target, damage);
        }

        private void StrikeAll(int damage)
        {
            // 목록을 복사한다. 때리는 중에 죽은 몬스터가 원본에서 빠진다.
            targetBuffer.Clear();
            targetBuffer.AddRange(battle.Monsters.EngagedMonsters);
            foreach (Monster m in targetBuffer)
                Hit(m, damage);
        }

        private void ExecuteBelow(SynergyCategory category, int belowPercent)
        {
            Monster target = FrontMonster(m =>
                !m.IsStageBoss
                && (battle.Bounty == null || !battle.Bounty.IsEventBoss(m))
                && m.HpPercent < belowPercent);

            // 조건에 맞는 몬스터가 없으면 꽝(사용자 결정).
            if (target == null)
                return;

            OnStrike?.Invoke(category, target);
            Hit(target, int.MaxValue / 4);
        }

        private void Hit(Monster target, int damage)
        {
            if (target == null || damage <= 0 || target.HpPercent <= 0)
                return;

            int applied = target.TakeDamage(SynergyBuffs.ApplyBoss(damage, target.IsStageBoss));
            if (applied <= 0 || battle.DamageTexts == null)
                return;

            GameObject text = battle.DamageTexts.GetDamageText();
            text.transform.position = target.transform.position;
            text.transform.ResetLocalZ();
            text.GetComponent<DamageText>().SetText(applied, DamageColor);
        }

        private static bool HasElement(DiceType diceType, int element)
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(diceType);
            if (meta == null || meta.elementType == null)
                return false;

            foreach (ElementType e in meta.elementType)
            {
                if ((int)e == element)
                    return true;
            }

            return false;
        }

        /// <summary>속성 정수 목록. 데미지 배수 계산에 넘긴다(할당을 피하려고 다이스별로 캐시).</summary>
        private static readonly Dictionary<DiceType, int[]> elementCache = new Dictionary<DiceType, int[]>();

        public static IReadOnlyList<int> ElementsOf(DiceType diceType, DiceMetaDataDatabase.DiceMeta meta)
        {
            if (elementCache.TryGetValue(diceType, out int[] cached))
                return cached;

            int[] elements = new int[meta?.elementType?.Length ?? 0];
            for (int i = 0; i < elements.Length; i++)
                elements[i] = (int)meta.elementType[i];

            elementCache[diceType] = elements;
            return elements;
        }
    }
}
