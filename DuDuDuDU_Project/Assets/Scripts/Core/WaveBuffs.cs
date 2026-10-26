namespace OJ.Core
{
    /// <summary>
    /// 웨이브 이벤트가 거는 <b>다음 웨이브 한 번</b>짜리 효과(공격 속도 · 몬스터 이동속도 · 몬스터 체력). 엔진을 쓰지 않는다.
    ///
    /// 이벤트 선택 창에서 걸리고, 그 웨이브가 끝나 다음 이벤트가 시작될 때 걷힌다. <b>정적 상태</b>라 판이 시작될 때와
    /// 씬이 사라질 때 반드시 <see cref="Reset"/> 한다 — 안 그러면 로비·탑까지 따라간다(GameManager 가 지킨다).
    /// 값은 곱셈 배수이고 아무것도 안 걸렸으면 1 이다.
    /// </summary>
    public static class WaveBuffs
    {
        /// <summary>다이스 쿨타임 배수(작을수록 빠르다). 공격 속도 +30% 는 1 / 1.3.</summary>
        public static float CooldownMultiplier { get; private set; } = 1f;

        /// <summary>몬스터 이동속도 배수.</summary>
        public static float MonsterSpeedMultiplier { get; private set; } = 1f;

        /// <summary>일반 몬스터 체력 배수.</summary>
        public static float MonsterHpMultiplier { get; private set; } = 1f;

        public static bool IsActive =>
            CooldownMultiplier != 1f || MonsterSpeedMultiplier != 1f || MonsterHpMultiplier != 1f;

        /// <summary>공격 속도를 <paramref name="percent"/>% 올린다(30 이면 쿨타임 1/1.3).</summary>
        public static void AddAttackSpeedPercent(int percent)
        {
            if (percent <= 0)
                return;

            CooldownMultiplier /= 1f + percent / 100f;
        }

        /// <summary>몬스터 이동속도를 <paramref name="percent"/>% 낮춘다(30 이면 0.7배).</summary>
        public static void AddMonsterSlowPercent(int percent)
        {
            if (percent <= 0)
                return;

            MonsterSpeedMultiplier *= 1f - percent / 100f;
        }

        /// <summary>몬스터 체력을 <paramref name="percent"/>% 바꾼다(음수면 약해지고 양수면 단단해진다).</summary>
        public static void AddMonsterHpPercent(int percent)
        {
            MonsterHpMultiplier *= 1f + percent / 100f;
            if (MonsterHpMultiplier < 0.1f)
                MonsterHpMultiplier = 0.1f;
        }

        public static void Reset()
        {
            CooldownMultiplier = 1f;
            MonsterSpeedMultiplier = 1f;
            MonsterHpMultiplier = 1f;
        }
    }
}
