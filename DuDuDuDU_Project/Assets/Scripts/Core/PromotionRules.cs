using System.Collections.Generic;

namespace OJ.Core
{
    /// <summary>
    /// 승급 미션이 세는 행동. <b>이름이 그대로 세이브 키가 된다</b>(<see cref="PromotionRules.Key"/>) —
    /// 정수로 저장하면 중간에 항목을 끼우는 순간 저장된 진행도가 다른 미션을 가리킨다.
    /// 항목을 지우거나 이름을 바꾸면 그 미션의 진행도는 0 부터 다시 시작한다.
    /// </summary>
    public enum PromotionMission
    {
        /// <summary>핀볼 발수 소모. 배율만큼 오른다(x100 한 발은 100).</summary>
        PinballShots = 0,

        /// <summary>시너지 티켓 소모.</summary>
        SynergyTickets,

        /// <summary>소탕 횟수.</summary>
        Sweeps,

        /// <summary>무한의 탑 도전(입장 기준).</summary>
        TowerChallenges,

        /// <summary>본편 스테이지 도전(입장 기준).</summary>
        StageChallenges,

        /// <summary>몬스터 처치.</summary>
        MonsterKills,

        /// <summary>보스 처치.</summary>
        BossKills,

        /// <summary>
        /// 이벤트 재화 투입(미션 B). <b>열린 이벤트의 재화만 인정한다.</b>
        /// 다른 미션과 달리 행동이 아니라 유저가 직접 넣는 것이라 <c>Notify</c> 가 아니라 투입 경로로만 오른다.
        /// </summary>
        EventCurrency,
    }

    /// <summary>승급 등록 가능 여부.</summary>
    public enum PromotionRegisterResult
    {
        Ok = 0,

        /// <summary>이 다이스가 갈 수 있는 마지막 단계까지 이미 올랐다.</summary>
        MaxTier,

        /// <summary>다이스 레벨이 그 단계의 요구 레벨에 못 미친다.</summary>
        LevelTooLow,

        /// <summary>이미 이 다이스가 큐에 있다.</summary>
        AlreadyQueued,

        /// <summary>다른 다이스가 등록된 지 24시간이 안 지나 교체할 수 없다.</summary>
        ReplaceLocked,
    }

    /// <summary>
    /// 다이스 승급의 순수 규칙. <b>목록과 값을 인자로 받는 <c>static</c> 뿐이다</b> —
    /// 매니저·SO 를 참조하지 않아야 헤드리스 러너에서 돈다(<c>DiceUnlockDatabase</c> 와 같은 이유).
    ///
    /// <b>구조 요약</b>(승급 개편, 2026-11)
    /// <code>
    ///   대상   기본 5종: T1 만 / 레어 5종·킹 5종: T1~T3
    ///   레벨   T1 Lv5 / T2 Lv15 / T3 Lv30 이 되어야 등록할 수 있다
    ///   큐     1칸. 등록한 뒤 쓴 분만 인정, 교체는 24시간 잠금, 교체하면 진행도 초기화
    ///   미션   0→1: C 1개 / 1→2: C 1개 + B 1개 / 2→3: C 1개 + B 1개 + A 1개
    ///          <b>C 는 유저가 고르지 않는다</b> — 다이스·단계마다 정해 둔 한 가지다(<see cref="AssignedMission"/>)
    ///   A      보유형 미션(T3 전용). 종류·수량은 테스트 뒤에 정한다 — 지금은 없다
    /// </code>
    /// B 가 <b>유일한 시간 병목</b>이다 — 이벤트 재화는 하루 약 200개(열린 이벤트 하나)라
    /// T2 1,000개는 5일, T3 3,000개는 15일이다. C 쪽 수치는 그 시간 안에 자연히 채워지는 크기다.
    /// B 는 이벤트 재화 한 종류뿐이다.
    /// </summary>
    public static class PromotionRules
    {
        /// <summary>다른 다이스로 교체하려면 등록 뒤 이만큼 지나야 한다.</summary>
        public const int ReplaceLockHours = 24;

        /// <summary>기본 5종이 오를 수 있는 마지막 단계.</summary>
        public const int BasicMaxTier = 1;

        /// <summary>레어·킹이 오를 수 있는 마지막 단계.</summary>
        public const int PromotableMaxTier = 3;

        /// <summary>이벤트 재화 몇 개가 핀볼 1발로 환급되는가. 소수점은 버린다.</summary>
        public const int EventCurrencyPerRefundShot = 3;

        /// <summary>단계(1~3) → 등록에 필요한 다이스 레벨. 마일스톤(10/20/25)과 별개다.</summary>
        private static readonly int[] RequiredLevels = { 5, 15, 30 };

        /// <summary>승급 대상 다이스 칸 수. 기본 5 + 레어 5 + 킹 5. 칸 번호는 그 순서(Normal=0 … KingPoison=14)다.</summary>
        public const int SlotCount = 15;

        /// <summary>
        /// 다이스 칸(0~14) × 단계(1~3) → 그 단계에서 채울 <b>C 미션 한 가지</b>. 기획이 정해 둔 고정표다.
        /// 기본 5종은 T1 만 쓰므로 T2·T3 칸은 자리만 채웠다(<see cref="MaxTier"/> 가 막는다).
        /// T2·T3 는 요구량이 있는 네 가지(핀볼·시너지·소탕·스테이지)에서만 고른다.
        /// 한 가지로 몰리지 않게 다이스마다 어긋나게 흩었다.
        /// </summary>
        private static readonly PromotionMission[][] AssignedC =
        {
            /*  0 Normal     */ new[] { PromotionMission.MonsterKills,    PromotionMission.MonsterKills,    PromotionMission.MonsterKills },
            /*  1 Fire       */ new[] { PromotionMission.PinballShots,    PromotionMission.PinballShots,    PromotionMission.PinballShots },
            /*  2 Ice        */ new[] { PromotionMission.SynergyTickets,  PromotionMission.SynergyTickets,  PromotionMission.SynergyTickets },
            /*  3 Thunder    */ new[] { PromotionMission.Sweeps,          PromotionMission.Sweeps,          PromotionMission.Sweeps },
            /*  4 Poison     */ new[] { PromotionMission.StageChallenges, PromotionMission.StageChallenges, PromotionMission.StageChallenges },
            /*  5 Tornado    */ new[] { PromotionMission.TowerChallenges, PromotionMission.Sweeps,          PromotionMission.PinballShots },
            /*  6 Stun       */ new[] { PromotionMission.BossKills,       PromotionMission.StageChallenges, PromotionMission.SynergyTickets },
            /*  7 ArmorBreak */ new[] { PromotionMission.PinballShots,    PromotionMission.SynergyTickets,  PromotionMission.Sweeps },
            /*  8 Wind       */ new[] { PromotionMission.MonsterKills,    PromotionMission.PinballShots,    PromotionMission.StageChallenges },
            /*  9 Time       */ new[] { PromotionMission.SynergyTickets,  PromotionMission.StageChallenges, PromotionMission.Sweeps },
            /* 10 KingNormal */ new[] { PromotionMission.Sweeps,          PromotionMission.PinballShots,    PromotionMission.SynergyTickets },
            /* 11 KingFire   */ new[] { PromotionMission.BossKills,       PromotionMission.Sweeps,          PromotionMission.StageChallenges },
            /* 12 KingIce    */ new[] { PromotionMission.TowerChallenges, PromotionMission.SynergyTickets,  PromotionMission.PinballShots },
            /* 13 KingThunder*/ new[] { PromotionMission.StageChallenges, PromotionMission.PinballShots,    PromotionMission.Sweeps },
            /* 14 KingPoison */ new[] { PromotionMission.MonsterKills,    PromotionMission.StageChallenges, PromotionMission.SynergyTickets },
        };

        /// <summary>그 다이스 칸이 그 단계(1~3)에서 채울 C 미션. 범위 밖이면 PinballShots(쓰이지 않는다).</summary>
        public static PromotionMission AssignedMission(int slot, int targetTier)
        {
            if (slot < 0 || slot >= AssignedC.Length || targetTier < 1 || targetTier > PromotableMaxTier)
                return PromotionMission.PinballShots;

            return AssignedC[slot][targetTier - 1];
        }

        /// <summary>
        /// 미션 × 단계(1~3) 기본 요구량. <b>0 은 그 단계에서 쓸 수 없는 미션</b>이다.
        /// 행은 <see cref="PromotionMission"/> 순서와 같다. 다이스별로 쓰이는 것은 <see cref="AssignedMission"/> 한 가지다.
        /// </summary>
        private static readonly int[][] Requirements =
        {
            /* PinballShots    */ new[] { 300, 3000, 15000 },
            /* SynergyTickets  */ new[] { 50, 500, 2500 },
            /* Sweeps          */ new[] { 10, 100, 500 },
            /* TowerChallenges */ new[] { 5, 0, 0 },
            /* StageChallenges */ new[] { 3, 30, 150 },
            /* MonsterKills    */ new[] { 50, 0, 0 },
            /* BossKills       */ new[] { 3, 0, 0 },
            /* EventCurrency   */ new[] { 0, 1000, 3000 },
        };

        private static readonly string[] Keys = BuildKeys();

        private static string[] BuildKeys()
        {
            var values = (PromotionMission[])System.Enum.GetValues(typeof(PromotionMission));
            var keys = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                keys[(int)values[i]] = values[i].ToString();

            return keys;
        }

        /// <summary>세이브 키. 전투 중 처치마다 불리는 경로라 문자열을 미리 만들어 둔다.</summary>
        public static string Key(PromotionMission mission)
        {
            int index = (int)mission;
            return index >= 0 && index < Keys.Length ? Keys[index] : mission.ToString();
        }

        /// <summary>미션 개수.</summary>
        public static int MissionCount => Keys.Length;

        /// <summary>이 다이스가 오를 수 있는 마지막 단계.</summary>
        public static int MaxTier(bool isBasic)
        {
            return isBasic ? BasicMaxTier : PromotableMaxTier;
        }

        /// <summary>그 단계(1~3)를 등록하려면 필요한 다이스 레벨. 범위 밖이면 0.</summary>
        public static int RequiredDiceLevel(int targetTier)
        {
            return targetTier >= 1 && targetTier <= RequiredLevels.Length ? RequiredLevels[targetTier - 1] : 0;
        }

        /// <summary>
        /// 그 다이스 칸이 그 단계에서 <b>채워야 하는</b> 미션의 요구량. B(이벤트 재화)는 단계에 값이 있으면 항상 요구하고,
        /// C 는 <see cref="AssignedMission"/> 한 가지만 요구한다. 그 밖의 미션이거나 범위 밖이면 0.
        /// </summary>
        public static int Requirement(int slot, PromotionMission mission, int targetTier)
        {
            if (mission != PromotionMission.EventCurrency && AssignedMission(slot, targetTier) != mission)
                return 0;

            return BaseRequirement(mission, targetTier);
        }

        /// <summary>미션 종류별 기본 요구량(다이스와 무관). 쓸 수 없는 단계거나 범위 밖이면 0.</summary>
        public static int BaseRequirement(PromotionMission mission, int targetTier)
        {
            int row = (int)mission;
            if (row < 0 || row >= Requirements.Length || targetTier < 1 || targetTier > Requirements[row].Length)
                return 0;

            return Requirements[row][targetTier - 1];
        }

        /// <summary>그 단계에서 이 미션을 채워야 하는가.</summary>
        public static bool IsRequired(int slot, PromotionMission mission, int targetTier)
        {
            return Requirement(slot, mission, targetTier) > 0;
        }

        /// <summary>
        /// 진행도를 요구량으로 자른다. <b>저장은 요구량에서 멈춘다</b> — 넘친 분은 인정하지 않는다
        /// (소탕 500회 요구에 600회가 쌓여 다음 단계로 이월되면 "등록 후 쓴 분만" 규칙이 깨진다).
        /// </summary>
        public static int Clamp(int progress, int requirement)
        {
            if (progress < 0)
                return 0;

            return progress > requirement ? requirement : progress;
        }

        /// <summary>
        /// 진행도를 더한다. 요구량을 넘기지 않고, 제외된 미션이면 아무것도 하지 않는다.
        /// 돌려주는 값은 <b>실제로 더해진 양</b>이다.
        /// </summary>
        public static int Add(IDictionary<string, int> progress, int slot, PromotionMission mission, int targetTier, int amount)
        {
            int need = Requirement(slot, mission, targetTier);
            if (progress == null || need <= 0 || amount <= 0)
                return 0;

            string key = Key(mission);
            progress.TryGetValue(key, out int current);

            int next = Clamp(current + amount, need);
            int added = next - Clamp(current, need);
            if (added > 0)
                progress[key] = next;

            return added;
        }

        /// <summary>이 미션의 지금 진행도(요구량으로 자른 값).</summary>
        public static int Progress(IReadOnlyDictionary<string, int> progress, int slot, PromotionMission mission, int targetTier)
        {
            int need = Requirement(slot, mission, targetTier);
            if (progress == null || need <= 0)
                return 0;

            progress.TryGetValue(Key(mission), out int value);
            return Clamp(value, need);
        }

        /// <summary>그 다이스·단계의 요구 미션(C 한 가지 + B)이 전부 찼는가. 요구가 아닌 미션은 보지 않는다.</summary>
        public static bool AllMissionsMet(IReadOnlyDictionary<string, int> progress, int slot, int targetTier)
        {
            if (targetTier < 1 || targetTier > PromotableMaxTier)
                return false;

            for (int i = 0; i < Keys.Length; i++)
            {
                var mission = (PromotionMission)i;
                int need = Requirement(slot, mission, targetTier);
                if (need <= 0)
                    continue;

                if (Progress(progress, slot, mission, targetTier) < need)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 이 다이스를 큐에 올릴 수 있는가.
        /// <paramref name="queuedIsOther"/> 는 큐가 <b>다른</b> 다이스로 차 있다는 뜻이고,
        /// <paramref name="queuedIsSame"/> 은 이 다이스가 이미 올라 있다는 뜻이다.
        /// </summary>
        public static PromotionRegisterResult CanRegister(
            int currentTier,
            bool isBasic,
            int diceLevel,
            bool queuedIsSame,
            bool queuedIsOther,
            long registeredUtcTicks,
            long nowUtcTicks)
        {
            if (currentTier >= MaxTier(isBasic))
                return PromotionRegisterResult.MaxTier;

            if (queuedIsSame)
                return PromotionRegisterResult.AlreadyQueued;

            if (diceLevel < RequiredDiceLevel(currentTier + 1))
                return PromotionRegisterResult.LevelTooLow;

            if (queuedIsOther && IsReplaceLocked(registeredUtcTicks, nowUtcTicks))
                return PromotionRegisterResult.ReplaceLocked;

            return PromotionRegisterResult.Ok;
        }

        /// <summary>
        /// 등록 뒤 24시간이 안 지났는가. 시계가 거꾸로 간 경우(경과가 음수)는 잠그지 않는다 —
        /// 유저 시계를 믿는 규칙이라, 거꾸로 간 시계 때문에 큐가 영영 잠기는 쪽이 더 나쁘다.
        /// </summary>
        public static bool IsReplaceLocked(long registeredUtcTicks, long nowUtcTicks)
        {
            long elapsed = nowUtcTicks - registeredUtcTicks;
            if (elapsed < 0)
                return false;

            return elapsed < System.TimeSpan.FromHours(ReplaceLockHours).Ticks;
        }

        /// <summary>남은 잠금 시간. 잠겨 있지 않으면 0.</summary>
        public static System.TimeSpan ReplaceLockRemaining(long registeredUtcTicks, long nowUtcTicks)
        {
            if (!IsReplaceLocked(registeredUtcTicks, nowUtcTicks))
                return System.TimeSpan.Zero;

            return System.TimeSpan.FromHours(ReplaceLockHours) - System.TimeSpan.FromTicks(nowUtcTicks - registeredUtcTicks);
        }

        /// <summary>
        /// 이벤트 재화 투입량. 부른 양·가진 양·남은 요구량 중 가장 작은 값이다.
        /// 요구량을 넘겨 넣어 재화를 버리는 일이 없다.
        /// </summary>
        public static int AcceptableEventCurrency(int progress, int requirement, int held, int offered)
        {
            if (requirement <= 0 || held <= 0 || offered <= 0)
                return 0;

            int remaining = requirement - Clamp(progress, requirement);
            if (remaining <= 0)
                return 0;

            int amount = offered < held ? offered : held;
            return amount < remaining ? amount : remaining;
        }

        /// <summary>
        /// 끝난 이벤트의 남은 재화를 핀볼 몇 발로 돌려주는가. <b>3개 = 1발, 소수점은 버린다.</b>
        /// 남은 1~2개는 환급하지 않고 사라진다.
        /// </summary>
        public static int RefundShots(int leftoverCurrency)
        {
            return leftoverCurrency > 0 ? leftoverCurrency / EventCurrencyPerRefundShot : 0;
        }
    }
}
