namespace OJ.Core
{
    /// <summary>웨이브가 끝난 뒤 유저가 고르는 보상(웨이브 테마 · 2026-10-27).</summary>
    public enum WaveInterlude
    {
        None = 0,

        /// <summary>영혼 보너스: SP 와 마석 중 하나.</summary>
        SoulBonus,

        /// <summary>현상금 도전 여부를 묻는다. 도전하면 현상금 선택 창 → 연출 → 플레이.</summary>
        BountyChallenge,

        /// <summary>자리 4칸 늘리기 / 영혼 보너스 중 하나. 광고를 보면 둘 다(스테이지당 <see cref="WaveTheme.AdBonusLimitPerStage"/>회).</summary>
        SlotsOrSoul,

        /// <summary>보스를 잡은 보상: 자리 4칸 <b>그리고</b> 영혼 보너스(고르는 것이 아니라 둘 다).</summary>
        BossReward,

        /// <summary>행운 주사위: 굴려서 마석 5~15개 / 100 SP 받기.</summary>
        LuckyDice,

        /// <summary>보스전: 마석 10개 받고 지나가기 / 30초 안에 보스를 잡고 마석 20개(못 잡으면 없음).</summary>
        BossChallenge,

        /// <summary>벽 체력 10% 회복 / 100 SP 받기.</summary>
        WallHeal,

        /// <summary>같은 등급대(기본 다이스 2성)에서 무작위 둘을 보여 주고 하나를 받는다.</summary>
        DiceGiftBasic,

        /// <summary>같은 등급대(레어 다이스)에서 무작위 둘을 보여 주고 하나를 받는다.</summary>
        DiceGiftRare,

        /// <summary>SP 도박: 100 SP 확정 / 50% 로 250 SP(실패 0).</summary>
        SpGamble,

        /// <summary>마석 도박: 마석 6개 확정 / 40% 로 18개(실패 0).</summary>
        StoneGamble,

        /// <summary>무료 소환 3회 / 100 SP.</summary>
        FreeSummons,

        /// <summary>확률업 레벨 +1(무료) / 100 SP.</summary>
        ProbUp,

        /// <summary>벽 최대 체력 +15%(같은 만큼 회복) / 벽 25% 회복.</summary>
        WallUpgrade,

        /// <summary>환전: 100 SP 를 내고 마석 6개 / 지나가기.</summary>
        Exchange,

        /// <summary>다음 웨이브 동안 공격 속도 +30% / 100 SP.</summary>
        AttackSpeedBuff,

        /// <summary>다음 웨이브 몬스터 이동속도 -30% / 마석 6개.</summary>
        SlowBuff,

        /// <summary>다음 웨이브 몬스터 체력 -25% / 100 SP.</summary>
        WeakenBuff,

        /// <summary>위험한 거래: 다음 웨이브 몬스터 체력 +30% 를 받고 마석 15개 / 지나가기.</summary>
        RiskyDeal,

        /// <summary>상인: SP 70 / 마석 7개 / 벽 7% 회복 중 하나(3지선다).</summary>
        Merchant,
    }

    /// <summary>
    /// 웨이브 테마: 10웨이브가 한 바퀴이고 같은 패턴을 30웨이브까지 되풀이한다. 엔진을 쓰지 않는다.
    ///
    /// <code>
    ///   1 2 4 7 8 웨이브   물량전 → 끝나면 이벤트(영혼 보너스 · 행운 주사위 · 다이스 선물 · 벽 회복 · 보스전) — 배치는 <see cref="After"/> 의 표
    ///   3 6 9 웨이브       물량전 → 끝나면 현상금 도전할래요? (도전/패스)
    ///   5 웨이브           물량전 → 끝나면 자리 +4 / 영혼 보너스 (광고로 둘 다)
    ///   10 웨이브          보스 한 마리(60초 안에) → 자리 +4 와 영혼 보너스
    /// </code>
    /// 보스 웨이브 규칙(일반 몬스터 없이 한 마리)은 <see cref="SoloBossRules"/>(<c>StageData.bossWaveInterval = 10</c>)가 정한다.
    /// 스테이지 데이터의 <c>waveTheme</c> 가 켜진 스테이지에만 쓴다(1스테이지 시험).
    /// </summary>
    public static class WaveTheme
    {
        public const int Cycle = 10;

        /// <summary>보스를 이 시간 안에 못 잡으면 진다(초).</summary>
        public const float BossTimeLimitSeconds = 60f;

        /// <summary>자리 보상 한 번에 늘어나는 칸 수.</summary>
        public const int SlotsPerGrant = 4;

        /// <summary>5웨이브 보상을 광고로 둘 다 받을 수 있는 횟수(스테이지당).</summary>
        public const int AdBonusLimitPerStage = 2;

        /// <summary>이벤트가 주는 SP(행운 주사위의 "SP 받기", 벽 회복의 "SP 받기").</summary>
        public const int EventSp = 50;

        /// <summary>보스전 이벤트: 도전하지 않고 지나가면 받는 마석.</summary>
        public const int EventBossPassStones = 10;

        /// <summary>보스전 이벤트: 제한 시간 안에 보스를 잡으면 받는 마석. 못 잡으면 없다.</summary>
        public const int EventBossWinStones = 20;

        /// <summary>보스전 이벤트의 제한 시간(초).</summary>
        public const float EventBossSeconds = 30f;

        /// <summary>벽 회복 이벤트가 채우는 비율(최대 체력 대비 %).</summary>
        public const int WallHealPercent = 10;

        /// <summary>벽 회복량. 최대 체력의 <see cref="WallHealPercent"/>% 를 올림한다(체력이 작아도 0 이 되지 않는다).</summary>
        public static int WallHealAmount(int maxHp)
        {
            if (maxHp <= 0)
                return 0;

            return (maxHp * WallHealPercent + 99) / 100;
        }

        /// <summary>몇 번째 바퀴인가(0 부터). 이벤트 배치가 바퀴마다 다르다.</summary>
        public static int CycleIndex(int wave)
        {
            if (wave < 1)
                return 0;

            return (wave - 1) / Cycle;
        }

        /// <summary>
        /// 보스전 이벤트에 나오는 보스의 현상금 등급(체력 기준). 바퀴가 지날수록 단단하다 — 1바퀴 2등급, 2바퀴 3등급, 그 뒤는 4·5등급.
        /// </summary>
        public static int EventBossGrade(int wave)
        {
            int grade = 2 + CycleIndex(wave);
            return grade > 5 ? 5 : grade;
        }

        /// <summary>한 바퀴 안에서의 위치(1~10).</summary>
        public static int Position(int wave)
        {
            if (wave < 1)
                return 1;

            return (wave - 1) % Cycle + 1;
        }

        public static bool IsBossWave(int wave)
        {
            return wave >= 1 && Position(wave) == Cycle;
        }

        // 이벤트 자리(1·2·4·7·8)의 배치. 바퀴마다(짝/홀) 다르게 돌려 같은 이벤트가 같은 자리에서 반복되지 않게 한다.
        // 영혼 보너스만 너무 많이 나오던 것을 다른 이벤트로 나눈 표다(2026-10-27).
        private static readonly WaveInterlude[][] EventSlots =
        {
            // 위치:            1                      2                       4                        7                       8
            new[] { WaveInterlude.SoulBonus, WaveInterlude.LuckyDice, WaveInterlude.DiceGiftBasic, WaveInterlude.WallHeal, WaveInterlude.BossChallenge },
            new[] { WaveInterlude.DiceGiftRare, WaveInterlude.SoulBonus, WaveInterlude.WallHeal, WaveInterlude.LuckyDice, WaveInterlude.BossChallenge },
        };

        private static int EventSlotIndex(int position)
        {
            switch (position)
            {
                case 1: return 0;
                case 2: return 1;
                case 4: return 2;
                case 7: return 3;
                case 8: return 4;
                default: return -1;
            }
        }

        /// <summary>이 웨이브가 끝난 뒤가 <b>이벤트 자리</b>인가(한 바퀴의 1·2·4·7·8). 무슨 이벤트가 나올지는 <see cref="WaveEventPlan"/> 이 정한다.</summary>
        public static bool IsEventSlot(int wave)
        {
            return EventSlotIndex(Position(wave)) >= 0;
        }

        /// <summary>
        /// 이벤트 자리의 <b>기본 배치</b>까지 넣은 보상. 판마다 무작위 계획(<see cref="WaveEventPlan"/>)을 쓰기 전의 고정표이자,
        /// 계획이 없을 때(테스트·폴백)의 값이다.
        /// </summary>
        public static WaveInterlude After(int wave)
        {
            int slot = EventSlotIndex(Position(wave));
            if (slot >= 0)
                return EventSlots[CycleIndex(wave) % EventSlots.Length][slot];

            return FixedAfter(wave);
        }

        /// <summary>이벤트 자리가 아닌 웨이브의 보상: 3·6·9 현상금, 5 자리/영혼, 10 보스 보상.</summary>
        public static WaveInterlude FixedAfter(int wave)
        {
            switch (Position(wave))
            {
                case 3:
                case 6:
                case 9:
                    return WaveInterlude.BountyChallenge;
                case 5:
                    return WaveInterlude.SlotsOrSoul;
                default:
                    return WaveInterlude.BossReward;
            }
        }
    }
}
