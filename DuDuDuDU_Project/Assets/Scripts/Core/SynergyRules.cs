using System;
using System.Collections.Generic;

namespace OJ.Core
{
    /// <summary>시너지 계열. 계열마다 슬롯이 1칸 고정이다.</summary>
    public enum SynergyCategory
    {
        Dice = 0,
        Combat = 1,
        Action = 2,
        Unified = 3,
    }

    /// <summary>옵션이 고르는 대상의 종류. 대상 값은 정수로 들고, 이름으로 바꾸는 일은 게임 쪽이 한다.</summary>
    public enum SynergyTargetKind
    {
        None = 0,

        /// <summary><c>ElementType</c> 정수.</summary>
        Element = 1,

        /// <summary><c>DiceType</c> 정수.</summary>
        Dice = 2,
    }

    /// <summary>
    /// 발동 조건. <b>저장은 이름으로 한다.</b> 번호는 계열 × 100 대역으로 나눠 두었다.
    /// </summary>
    public enum SynergyConditionId
    {
        None = 0,

        ElementAttack = 100,
        DiceAttack = 101,
        EvolvedDiceAttack = 102,

        MonsterKill = 200,
        WaveEnter = 201,
        BossKill = 202,
        BountyKill = 203,
        WallHit = 204,

        SpendSp = 300,
        SpendStone = 301,
        Merge = 302,
        Evolve = 303,
        Upgrade = 304,
        Summon = 305,
        Exchange = 306,

        DiceSynergyFired = 400,
        CombatSynergyFired = 401,
        ActionSynergyFired = 402,
        AnySynergyFired = 403,
    }

    /// <summary>발동 효과. <b>저장은 이름으로 한다.</b></summary>
    public enum SynergyEffectId
    {
        None = 0,

        SingleFixedDamage = 100,
        SinglePercentDamage = 101,
        AllFixedDamage = 102,
        AllPercentDamage = 103,
        ExecuteBelowHp = 104,
        BossDamageBuff = 105,

        OpenSlot = 200,
        HealWall = 201,
        GrantSp = 202,
        GrantSpPercent = 203,
        GrantStone = 204,
        GrantStonePercent = 205,
        WallGuard = 206,

        NextSummonStar = 300,
        RandomDiceBuff = 301,

        FireDiceSynergy = 400,
        FireCombatSynergy = 401,
        FireActionSynergy = 402,
        FillCounters = 403,

        // 네 계열 공통 버프.
        AttackBuff = 900,
        AttackBuffStack = 901,
        ElementBuff = 902,
        ElementBuffStack = 903,
        DiceBuff = 904,
        DiceBuffStack = 905,
    }

    /// <summary>
    /// 옵션 한 줄의 정의 — 문구, 대상, 수치 범위(Worst~Best), 버프 시간과 중첩.
    ///
    /// <b>min/max 가 아니라 Worst/Best 다.</b> 조건은 N 이 작을수록 좋고 효과는 클수록 좋아서,
    /// min/max 로 부르면 "Best 보정" 이 어느 쪽 끝인지 옵션마다 따져야 한다(사용자 결정 2026-10-28).
    /// </summary>
    public sealed class SynergyOption
    {
        public readonly bool IsCondition;
        public readonly int Id;
        public readonly SynergyTargetKind Target;
        public readonly int Worst;
        public readonly int Best;

        /// <summary>버프 지속 시간(초). 버프가 아니면 0.</summary>
        public readonly float Seconds;

        /// <summary>최대 중첩. 버프가 아니면 0, 중첩 없는 버프는 1.</summary>
        public readonly int MaxStacks;

        /// <summary>문구. <c>{v}</c> 수치, <c>{t}</c> 대상 이름, <c>{s}</c> 초, <c>{c}</c> 중첩.</summary>
        public readonly string Format;

        private readonly int categoryMask;

        public SynergyOption(bool isCondition, int id, int categoryMask, SynergyTargetKind target,
            int worst, int best, string format, float seconds = 0f, int maxStacks = 0)
        {
            IsCondition = isCondition;
            Id = id;
            this.categoryMask = categoryMask;
            Target = target;
            Worst = worst;
            Best = best;
            Format = format;
            Seconds = seconds;
            MaxStacks = maxStacks;
        }

        public bool IsBuff => MaxStacks > 0;

        public bool BelongsTo(SynergyCategory category) => (categoryMask & (1 << (int)category)) != 0;

        public int Low => Math.Min(Worst, Best);
        public int High => Math.Max(Worst, Best);
    }

    /// <summary>
    /// 시너지 옵션 표. <b>화면의 확률표와 실제 굴림이 같은 표를 읽는다.</b>
    ///
    /// 수치는 사용자가 준 초안이다(2026-10-28). % 피해는 현재 HP 가 아니라 <b>공격력</b>의 % 다(300~1200%). 행운 주사위처럼 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// 재화를 주는 효과는 <b>전투계에만</b> 둔다 — 행동계 조건이 재화를 소모하므로, 같은 계열에서
    /// 재화를 돌려주면 스스로를 먹이는 고리가 된다(사용자 설계).
    /// </summary>
    public static class SynergyCatalog
    {
        private const int DiceMask = 1 << (int)SynergyCategory.Dice;
        private const int CombatMask = 1 << (int)SynergyCategory.Combat;
        private const int ActionMask = 1 << (int)SynergyCategory.Action;
        private const int UnifiedMask = 1 << (int)SynergyCategory.Unified;
        private const int AllMask = DiceMask | CombatMask | ActionMask | UnifiedMask;

        private static readonly SynergyOption[] options =
        {
            Cond(SynergyConditionId.ElementAttack, DiceMask, SynergyTargetKind.Element, 20, 10, "{t} 속성 다이스 {v}회 공격 시"),
            Cond(SynergyConditionId.DiceAttack, DiceMask, SynergyTargetKind.Dice, 20, 10, "{t} 다이스 {v}회 공격 시"),
            Cond(SynergyConditionId.EvolvedDiceAttack, DiceMask, SynergyTargetKind.None, 20, 10, "특수/킹 다이스 {v}회 공격 시"),

            Cond(SynergyConditionId.MonsterKill, CombatMask, SynergyTargetKind.None, 20, 8, "몬스터 {v}마리 처치 시"),
            Cond(SynergyConditionId.WaveEnter, CombatMask, SynergyTargetKind.None, 3, 1, "웨이브 {v}회 입장 시"),
            Cond(SynergyConditionId.BossKill, CombatMask, SynergyTargetKind.None, 2, 1, "보스 {v}회 처치 시"),
            Cond(SynergyConditionId.BountyKill, CombatMask, SynergyTargetKind.None, 3, 1, "현상금 몬스터 {v}마리 처치 시"),
            Cond(SynergyConditionId.WallHit, CombatMask, SynergyTargetKind.None, 10, 4, "벽 {v}회 피격 시"),

            Cond(SynergyConditionId.SpendSp, ActionMask, SynergyTargetKind.None, 300, 120, "SP {v} 소모 시"),
            Cond(SynergyConditionId.SpendStone, ActionMask, SynergyTargetKind.None, 20, 8, "마석 {v}개 소모 시"),
            Cond(SynergyConditionId.Merge, ActionMask, SynergyTargetKind.None, 8, 3, "머지 {v}번 시"),
            Cond(SynergyConditionId.Evolve, ActionMask, SynergyTargetKind.None, 4, 1, "진화 {v}번 시"),
            Cond(SynergyConditionId.Upgrade, ActionMask, SynergyTargetKind.None, 4, 1, "강화 {v}번 시"),
            Cond(SynergyConditionId.Summon, ActionMask, SynergyTargetKind.None, 8, 3, "소환 {v}번 시"),
            Cond(SynergyConditionId.Exchange, ActionMask, SynergyTargetKind.None, 4, 1, "교환 {v}번 시"),

            Cond(SynergyConditionId.DiceSynergyFired, UnifiedMask, SynergyTargetKind.None, 6, 2, "다이스계 시너지 {v}번 발동 시"),
            Cond(SynergyConditionId.CombatSynergyFired, UnifiedMask, SynergyTargetKind.None, 6, 2, "전투계 시너지 {v}번 발동 시"),
            Cond(SynergyConditionId.ActionSynergyFired, UnifiedMask, SynergyTargetKind.None, 6, 2, "행동계 시너지 {v}번 발동 시"),
            Cond(SynergyConditionId.AnySynergyFired, UnifiedMask, SynergyTargetKind.None, 8, 3, "모든 시너지 {v}번 발동 시"),

            Effect(SynergyEffectId.SingleFixedDamage, DiceMask, SynergyTargetKind.None, 50, 400, "맨 앞 적에게 {v} 고정 피해"),
            Effect(SynergyEffectId.SinglePercentDamage, DiceMask, SynergyTargetKind.None, 300, 1200, "맨 앞 적에게 공격력의 {v}% 피해"),
            Effect(SynergyEffectId.AllFixedDamage, DiceMask, SynergyTargetKind.None, 20, 150, "적 전체에게 {v} 고정 피해"),
            Effect(SynergyEffectId.AllPercentDamage, DiceMask | ActionMask, SynergyTargetKind.None, 100, 500, "적 전체에게 공격력의 {v}% 피해"),
            Effect(SynergyEffectId.ExecuteBelowHp, DiceMask, SynergyTargetKind.None, 5, 20, "HP {v}% 미만 적 1마리 처치(보스 제외)"),
            Effect(SynergyEffectId.BossDamageBuff, DiceMask | UnifiedMask, SynergyTargetKind.None, 5, 25, "{s}초간 보스 대미지 {v}% 증가({c}회 중첩)", 8f, 5),

            Effect(SynergyEffectId.OpenSlot, CombatMask, SynergyTargetKind.None, 1, 2, "자리 {v}개 오픈"),
            Effect(SynergyEffectId.HealWall, CombatMask, SynergyTargetKind.None, 2, 10, "벽 HP {v}% 회복"),
            Effect(SynergyEffectId.GrantSp, CombatMask, SynergyTargetKind.None, 10, 60, "SP {v} 지급"),
            Effect(SynergyEffectId.GrantSpPercent, CombatMask, SynergyTargetKind.None, 3, 10, "보유 SP {v}% 지급"),
            Effect(SynergyEffectId.GrantStone, CombatMask, SynergyTargetKind.None, 1, 4, "마석 {v}개 지급"),
            Effect(SynergyEffectId.GrantStonePercent, CombatMask, SynergyTargetKind.None, 3, 10, "보유 마석 {v}% 지급"),
            Effect(SynergyEffectId.WallGuard, CombatMask, SynergyTargetKind.None, 10, 40, "{s}초간 벽 받는 피해 {v}% 감소", 5f, 1),

            Effect(SynergyEffectId.NextSummonStar, ActionMask, SynergyTargetKind.None, 2, 3, "다음 소환 다이스 {v}성으로 등장"),
            Effect(SynergyEffectId.RandomDiceBuff, ActionMask, SynergyTargetKind.None, 10, 50, "보드의 무작위 다이스 1종 {s}초간 공격력 {v}% 증가", 8f, 1),

            Effect(SynergyEffectId.FireDiceSynergy, UnifiedMask, SynergyTargetKind.None, 1, 3, "다이스계 시너지 {v}회 발동"),
            Effect(SynergyEffectId.FireCombatSynergy, UnifiedMask, SynergyTargetKind.None, 1, 3, "전투계 시너지 {v}회 발동"),
            Effect(SynergyEffectId.FireActionSynergy, UnifiedMask, SynergyTargetKind.None, 1, 3, "행동계 시너지 {v}회 발동"),
            Effect(SynergyEffectId.FillCounters, UnifiedMask, SynergyTargetKind.None, 10, 50, "다른 시너지 카운터 {v}% 채움"),

            Effect(SynergyEffectId.AttackBuff, AllMask, SynergyTargetKind.None, 5, 30, "{s}초간 공격력 {v}% 증가", 8f, 1),
            Effect(SynergyEffectId.AttackBuffStack, AllMask, SynergyTargetKind.None, 5, 30, "{s}초간 공격력 {v}% 증가({c}회 중첩)", 8f, 5),
            Effect(SynergyEffectId.ElementBuff, AllMask, SynergyTargetKind.Element, 5, 30, "{s}초간 {t} 속성 공격력 {v}% 증가", 8f, 1),
            Effect(SynergyEffectId.ElementBuffStack, AllMask, SynergyTargetKind.Element, 5, 30, "{s}초간 {t} 속성 공격력 {v}% 증가({c}회 중첩)", 8f, 5),
            Effect(SynergyEffectId.DiceBuff, AllMask, SynergyTargetKind.Dice, 5, 30, "{s}초간 {t} 다이스 공격력 {v}% 증가", 8f, 1),
            Effect(SynergyEffectId.DiceBuffStack, AllMask, SynergyTargetKind.Dice, 5, 30, "{s}초간 {t} 다이스 공격력 {v}% 증가({c}회 중첩)", 8f, 5),
        };

        public static IReadOnlyList<SynergyOption> All => options;

        public static SynergyOption GetCondition(SynergyConditionId id) => Find(true, (int)id);

        public static SynergyOption GetEffect(SynergyEffectId id) => Find(false, (int)id);

        /// <summary>그 계열이 굴릴 수 있는 조건(또는 효과) 목록. 표 순서 그대로.</summary>
        public static List<SynergyOption> Pool(SynergyCategory category, bool conditions)
        {
            var list = new List<SynergyOption>();
            foreach (SynergyOption option in options)
            {
                if (option.IsCondition == conditions && option.BelongsTo(category))
                    list.Add(option);
            }

            return list;
        }

        private static SynergyOption Find(bool condition, int id)
        {
            foreach (SynergyOption option in options)
            {
                if (option.IsCondition == condition && option.Id == id)
                    return option;
            }

            return null;
        }

        private static SynergyOption Cond(SynergyConditionId id, int mask, SynergyTargetKind target, int worst, int best, string format)
        {
            return new SynergyOption(true, (int)id, mask, target, worst, best, format);
        }

        private static SynergyOption Effect(SynergyEffectId id, int mask, SynergyTargetKind target, int worst, int best,
            string format, float seconds = 0f, int maxStacks = 0)
        {
            return new SynergyOption(false, (int)id, mask, target, worst, best, format, seconds, maxStacks);
        }
    }

    /// <summary>옵션 한 줄의 현재 값. <see cref="OptionId"/> 0 은 빈 줄이다.</summary>
    public sealed class SynergyLine
    {
        public int OptionId;

        /// <summary>대상(<c>ElementType</c>/<c>DiceType</c> 정수). 대상이 없는 옵션이면 -1.</summary>
        public int Target = -1;

        public int Value;
        public bool Locked;

        public bool IsEmpty => OptionId == 0;

        public void CopyFrom(SynergyLine other)
        {
            OptionId = other.OptionId;
            Target = other.Target;
            Value = other.Value;
            Locked = other.Locked;
        }
    }

    /// <summary>프리셋 1개 = 조건 1줄 + 효과 최대 3줄.</summary>
    public sealed class SynergyPreset
    {
        public readonly SynergyLine Condition = new SynergyLine();
        public readonly SynergyLine[] Effects = { new SynergyLine(), new SynergyLine(), new SynergyLine() };
    }

    /// <summary>한 번 리롤한 결과.</summary>
    public struct SynergyRerollResult
    {
        public bool Rolled;

        /// <summary>Best 보정이 터졌는가.</summary>
        public bool BestHit;

        /// <summary>Best 가 붙은 줄. -1 은 조건 줄, 0~2 는 효과 줄, 터지지 않았으면 -2.</summary>
        public int BestLine;
    }

    /// <summary>
    /// 시너지의 순수 규칙 — 리롤 비용, Best 보정, 개방 판정, 줄 굴림. 엔진을 쓰지 않는다.
    ///
    /// <b>난수는 부르는 쪽이 준다</b>(<c>next(n)</c> 은 0 이상 n 미만). 엔진 난수를 여기서 부르면
    /// 테스트에서 분포와 보정 확률을 잴 수 없다(<see cref="LuckyDiceRules"/> 와 같은 이유).
    /// </summary>
    public static class SynergyRules
    {
        public const int CategoryCount = 4;
        public const int PresetCount = 4;
        public const int EffectLineCount = 3;

        public const int BaseRerollCost = 10;
        public const int LockedEffectCostStep = 10;
        public const int LockedConditionCostMultiplier = 2;

        /// <summary>소모 티켓 1개당 Best 확률(만분율). 5 = 0.05%.</summary>
        public const int BestChancePerTicketBp = 5;

        /// <summary>Best 확률 상한(만분율). 3000 = 30%.</summary>
        public const int BestChanceCapBp = 3000;

        /// <summary>효과 2·3번째 줄이 열리는 스테이지 클리어.</summary>
        public const int SecondEffectStage = 20;
        public const int ThirdEffectStage = 60;

        /// <summary>계열 개방 조건.</summary>
        public const int CombatUnlockDiceRerolls = 30;
        public const int ActionUnlockTotalFires = 2000;
        public const int UnifiedUnlockTowerFloor = 100;

        /// <summary>한 번의 통지로 터질 수 있는 최대 발동 수. SP 를 한 번에 크게 쓰면 여러 번 넘기 때문이다.</summary>
        public const int MaxFiresPerNotify = 5;

        /// <summary><c>(10 + 잠근 효과 × 10) × (조건 잠금 ? 2 : 1)</c>. 사용자 결정(2026-10-28).</summary>
        public static int RerollCost(bool conditionLocked, int lockedEffects)
        {
            int cost = BaseRerollCost + Math.Max(0, lockedEffects) * LockedEffectCostStep;
            return conditionLocked ? cost * LockedConditionCostMultiplier : cost;
        }

        public static int RerollCost(SynergyPreset preset, int openEffectLines)
        {
            return RerollCost(preset.Condition.Locked, CountLockedEffects(preset, openEffectLines));
        }

        public static int CountLockedEffects(SynergyPreset preset, int openEffectLines)
        {
            int locked = 0;
            for (int i = 0; i < ClampLines(openEffectLines); i++)
            {
                if (preset.Effects[i].Locked)
                    locked++;
            }

            return locked;
        }

        /// <summary>굴릴 줄이 하나라도 남았는가. 전부 잠그면 리롤 버튼을 막는다.</summary>
        public static bool CanReroll(SynergyPreset preset, int openEffectLines)
        {
            if (!preset.Condition.Locked)
                return true;

            for (int i = 0; i < ClampLines(openEffectLines); i++)
            {
                if (!preset.Effects[i].Locked)
                    return true;
            }

            return false;
        }

        /// <summary>지금까지 쌓인 소모 티켓으로 본 Best 확률(만분율).</summary>
        public static int BestChanceBp(int spentTickets)
        {
            long bp = (long)Math.Max(0, spentTickets) * BestChancePerTicketBp;
            return (int)Math.Min(bp, BestChanceCapBp);
        }

        /// <summary>Best 확률이 상한에 닿는 누적 티켓.</summary>
        public static int TicketsToCap => BestChanceCapBp / BestChancePerTicketBp;

        /// <summary>마지막으로 클리어한 스테이지로 본 열린 효과 줄 수(1~3).</summary>
        public static int OpenEffectLines(int lastClearedStage)
        {
            if (lastClearedStage >= ThirdEffectStage)
                return 3;

            return lastClearedStage >= SecondEffectStage ? 2 : 1;
        }

        /// <summary>그 줄이 열리는 스테이지. 0 번 줄은 0.</summary>
        public static int EffectLineUnlockStage(int line)
        {
            switch (line)
            {
                case 1: return SecondEffectStage;
                case 2: return ThirdEffectStage;
                default: return 0;
            }
        }

        public static bool IsCategoryOpen(SynergyCategory category, int diceRerolls, int totalFires, int towerHighestFloor)
        {
            switch (category)
            {
                case SynergyCategory.Dice: return true;
                case SynergyCategory.Combat: return diceRerolls >= CombatUnlockDiceRerolls;
                case SynergyCategory.Action: return totalFires >= ActionUnlockTotalFires;
                case SynergyCategory.Unified: return towerHighestFloor >= UnifiedUnlockTowerFloor;
                default: return false;
            }
        }

        /// <summary>
        /// 리롤한다. 비용 지불은 부르는 쪽이 이미 했다. <paramref name="spentTickets"/> 는 그 계열의 누적이고,
        /// 이번 비용을 먼저 더한 뒤 Best 를 판정한다. 터지면 이번에 굴린 줄 중 <b>1줄만</b> Best 값이 되고
        /// 누적은 0 으로 돌아간다(사용자 결정 2026-10-28).
        /// </summary>
        public static SynergyRerollResult Reroll(SynergyPreset preset, SynergyCategory category, int openEffectLines,
            int cost, ref int spentTickets, Func<int, int> next, Func<SynergyTargetKind, IReadOnlyList<int>> targets)
        {
            var result = new SynergyRerollResult { BestLine = -2 };
            if (!CanReroll(preset, openEffectLines))
                return result;

            var rolled = new List<int>(4);
            if (!preset.Condition.Locked)
            {
                RollLine(preset.Condition, SynergyCatalog.Pool(category, true), next, targets);
                rolled.Add(-1);
            }

            List<SynergyOption> effectPool = SynergyCatalog.Pool(category, false);
            for (int i = 0; i < ClampLines(openEffectLines); i++)
            {
                if (preset.Effects[i].Locked)
                    continue;

                RollLine(preset.Effects[i], effectPool, next, targets);
                rolled.Add(i);
            }

            result.Rolled = true;
            spentTickets = Math.Max(0, spentTickets) + Math.Max(0, cost);

            if (next(10000) < BestChanceBp(spentTickets))
            {
                int line = rolled[next(rolled.Count)];
                SynergyLine target = line < 0 ? preset.Condition : preset.Effects[line];
                SynergyOption option = Resolve(target, line < 0);
                if (option != null)
                    target.Value = option.Best;

                result.BestHit = true;
                result.BestLine = line;
                spentTickets = 0;
            }

            return result;
        }

        /// <summary>줄 하나를 새로 굴린다 — 옵션, 대상, 수치(Worst~Best 균등).</summary>
        public static void RollLine(SynergyLine line, IReadOnlyList<SynergyOption> pool, Func<int, int> next,
            Func<SynergyTargetKind, IReadOnlyList<int>> targets)
        {
            if (pool == null || pool.Count == 0)
                return;

            SynergyOption option = pool[next(pool.Count)];
            line.OptionId = option.Id;
            line.Target = -1;
            if (option.Target != SynergyTargetKind.None)
            {
                IReadOnlyList<int> candidates = targets?.Invoke(option.Target);
                line.Target = candidates != null && candidates.Count > 0 ? candidates[next(candidates.Count)] : 0;
            }

            line.Value = option.Low + next(option.High - option.Low + 1);
        }

        public static SynergyOption Resolve(SynergyLine line, bool condition)
        {
            if (line == null || line.IsEmpty)
                return null;

            return condition
                ? SynergyCatalog.GetCondition((SynergyConditionId)line.OptionId)
                : SynergyCatalog.GetEffect((SynergyEffectId)line.OptionId);
        }

        /// <summary>그 값이 범위의 Best 끝인가. 화면의 Best 표시에 쓴다.</summary>
        public static bool IsBest(SynergyLine line, bool condition)
        {
            SynergyOption option = Resolve(line, condition);
            return option != null && option.Worst != option.Best && line.Value == option.Best;
        }

        /// <summary>문구를 만든다. 대상 이름은 게임 쪽이 준다.</summary>
        public static string Describe(SynergyOption option, int value, string targetName)
        {
            if (option == null)
                return string.Empty;

            return option.Format
                .Replace("{v}", value.ToString())
                .Replace("{t}", targetName ?? string.Empty)
                .Replace("{s}", option.Seconds.ToString("0.#"))
                .Replace("{c}", option.MaxStacks.ToString());
        }

        /// <summary>Best 확률을 화면용으로. 1234 → "12.34%".</summary>
        public static string FormatBp(int bp)
        {
            return (bp / 100) + "." + (bp % 100).ToString("00") + "%";
        }

        private static int ClampLines(int lines) => Math.Max(0, Math.Min(EffectLineCount, lines));
    }

    /// <summary>
    /// 조건 카운터 하나. 문턱을 넘으면 발동하고 <b>남는 몫은 다음으로 넘긴다</b>
    /// (SP 300 문턱에서 250 → 100 을 쓰면 발동 1번 + 50 이 남는다).
    /// </summary>
    public sealed class SynergyCounter
    {
        public int Threshold { get; private set; }
        public int Progress { get; private set; }

        public SynergyCounter(int threshold)
        {
            Threshold = Math.Max(1, threshold);
        }

        /// <summary>더하고, 이번에 터진 발동 수를 돌려준다(최대 <see cref="SynergyRules.MaxFiresPerNotify"/>).</summary>
        public int Add(int amount)
        {
            if (amount <= 0)
                return 0;

            Progress += amount;
            int fires = 0;
            while (Progress >= Threshold && fires < SynergyRules.MaxFiresPerNotify)
            {
                Progress -= Threshold;
                fires++;
            }

            // 상한에 걸려 못 터진 몫은 버린다. 남겨 두면 다음 한 번의 통지에 몰아서 터진다.
            if (Progress >= Threshold)
                Progress = Threshold - 1;

            return fires;
        }

        public void Reset() => Progress = 0;
    }
}
