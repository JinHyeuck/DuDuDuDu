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
    }

    /// <summary>
    /// 웨이브 테마: 10웨이브가 한 바퀴이고 같은 패턴을 30웨이브까지 되풀이한다. 엔진을 쓰지 않는다.
    ///
    /// <code>
    ///   1 2 4 7 8 웨이브   물량전 → 끝나면 영혼 보너스 선택
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

        /// <summary>이 웨이브가 끝난 뒤의 보상.</summary>
        public static WaveInterlude After(int wave)
        {
            switch (Position(wave))
            {
                case 1:
                case 2:
                case 4:
                case 7:
                case 8:
                    return WaveInterlude.SoulBonus;
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
