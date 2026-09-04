namespace OJ.Core
{
    /// <summary>
    /// 현상금(바운티) 몬스터의 순수 산술. 엔진도 <c>DiceType</c> 같은 enum 도 쓰지 않는다.
    ///
    /// <b>왜 고정 체력이 아닌가.</b> 스크린샷 기획안은 등급마다 체력을 정수로 박아 두는
    /// 안이었는데, 그러면 1스테이지에서는 최고 등급이 영영 잡히지 않고 20스테이지에서는
    /// 최저 등급이 몬스터 한 마리보다 약해진다. 즉 <b>스테이지가 올라갈수록 시스템이
    /// 저절로 무의미해진다.</b>
    ///
    /// 그래서 기준을 <b>그 스테이지의 웨이브 체력</b>으로 잡는다. 스테이지마다
    /// <c>baseMonsterHp</c> 가 다르므로(1스테이지 7 → 10스테이지 32) 배수 하나로
    /// 전 스테이지가 따라 올라간다.
    ///
    /// <b>기준을 웨이브 번호가 아니라 비율로 받는 이유.</b> <c>totalWaves</c> 가
    /// 스테이지마다 10·15·20 으로 다르다. "4웨이브 체력의 6배" 처럼 번호를 박으면
    /// 10웨이브 스테이지에서는 중반인 값이 20웨이브 스테이지에서는 초반이 된다.
    /// 비율로 받으면 "그 스테이지의 30% 지점" 이라는 뜻이 길이와 무관하게 유지된다.
    /// </summary>
    public static class BountyFormula
    {
        /// <summary>등급 수. 0 은 "소환 X" 이므로 유효 등급은 1..<see cref="GradeCount"/> 다.</summary>
        public const int GradeCount = 5;

        /// <summary>"현상금 몬스터를 부르지 않는다" 를 뜻하는 등급 값.</summary>
        public const int NoneGrade = 0;

        /// <summary>
        /// 체력 기준이 되는 웨이브 번호. <paramref name="waveRatio"/> 는 0~1 이다.
        ///
        /// <c>CeilToInt</c> 라서 아주 작은 비율도 1웨이브로 내려앉고 0 이 되지 않는다.
        /// 0 이 나오면 <see cref="StageGrowthFormula.MonsterHp"/> 가 자체 <c>Max(1, ...)</c>
        /// 로 삼켜서 <b>비율을 잘못 넣어도 조용히 1웨이브가 된다</b> — 그 침묵을 여기서 막는다.
        /// </summary>
        public static int ReferenceWave(int totalWaves, float waveRatio)
        {
            int validTotal = OJMath.Max(1, totalWaves);
            float ratio = OJMath.Clamp01(waveRatio);
            return OJMath.Clamp(OJMath.CeilToInt(validTotal * ratio), 1, validTotal);
        }

        /// <summary>
        /// 현상금 몬스터 체력. <paramref name="monsterHpAtReferenceWave"/> 는
        /// <see cref="StageGrowthFormula.MonsterHp"/> 가 <b>이미 반올림해 돌려준 정수</b>다.
        /// 보스 체력(<see cref="StageGrowthFormula.BossHp"/>)이 그렇게 하고 있어 맞춘다 —
        /// 반올림 전 float 을 쓰면 같은 배수인데 값이 갈린다.
        /// </summary>
        public static int Hp(int monsterHpAtReferenceWave, float hpMultiplier)
        {
            return OJMath.Max(1, OJMath.RoundToInt(monsterHpAtReferenceWave * hpMultiplier));
        }

        /// <summary>
        /// 벽에 닿았을 때 주는 피해. <b>현재</b> 벽 체력의 1/3 이다.
        ///
        /// <b>이 식은 벽을 절대 0 으로 만들지 못한다.</b> 100 → 67 → 45 → 30 … 으로
        /// 수렴만 한다. 의도된 성질이다 — 현상금은 곁가지 시스템이라 여기서 판이
        /// 끝나면 안 된다. 대신 벽 체력은 <see cref="StageRewardFormula.ClearGradeTier"/>
        /// 가 그대로 읽으므로 <b>클리어 등급이 떨어지는 것</b>이 실제 대가다.
        ///
        /// <b>정수 나눗셈이 그 성질을 혼자서는 못 지킨다.</b> 체력 1 에서 1/3 은 0 인데
        /// 하한 1 을 씌우면 1 을 깎아 0 이 된다 — 즉 "수렴만 한다"는 말이 체력 한 자리에서
        /// 깨진다. 그래서 하한(때려도 아무 일 없어 보이지 않게)과 상한(벽을 못 죽이게)을
        /// 둘 다 건다. 체력 1 에서는 상한이 이겨 피해가 0 이다.
        /// </summary>
        public static int WallDamage(int currentWallHp)
        {
            if (currentWallHp <= 1)
                return 0;

            return OJMath.Min(OJMath.Max(1, currentWallHp / 3), currentWallHp - 1);
        }

        /// <summary>
        /// 지금 고를 수 있는 최고 등급. 아직 하나도 못 잡았으면 1 이다.
        ///
        /// 해금은 <b>런 범위</b>다 — 판마다 1등급부터 다시 뚫는다. 영구 해금으로 두면
        /// 두 번째 판부터는 최고 등급만 켜 두면 되어서 <b>고를 것이 없어진다.</b>
        /// 매 판 "어디까지 올라갈까"를 다시 재는 것이 이 시스템의 전부다.
        /// </summary>
        public static int HighestSelectableGrade(int highestDefeatedGrade)
        {
            return OJMath.Clamp(highestDefeatedGrade + 1, 1, GradeCount);
        }

        /// <summary>이 등급을 지금 고를 수 있는가. 0(소환 X)은 언제나 고를 수 있다.</summary>
        public static bool IsSelectable(int grade, int highestDefeatedGrade)
        {
            if (grade == NoneGrade)
                return true;

            if (grade < NoneGrade || grade > GradeCount)
                return false;

            return grade <= HighestSelectableGrade(highestDefeatedGrade);
        }

        /// <summary>
        /// 현상금을 내보내기 전에 먼저 뽑을 일반 몬스터 수.
        ///
        /// <b>이 함수가 있는 이유는 데드락이다.</b> 스포너는 "일반 몬스터를
        /// <paramref name="desiredCount"/> 마리 뽑은 뒤에 현상금" 이라는 규칙으로 도는데,
        /// 그 웨이브의 일반 몬스터가 그보다 적으면 조건이 <b>영영 참이 되지 않는다.</b>
        /// 그러면 현상금이 안 나오고, 웨이브 종료 조건은 "현상금이 정리될 때까지" 이므로
        /// <b>웨이브가 끝나지 않는다</b> — 나오지도 않은 것을 기다린다.
        ///
        /// 지금 데이터는 <c>monstersPerWave</c> 가 전부 20 이라 걸리지 않지만,
        /// 그 값을 1 로 내리는 순간 판이 멈춘다. 스테이지 데이터가 아직 없어
        /// 목표가 0 인 폴백 경로도 같다. 값 하나로 게임이 멈추는 규칙은 남겨 두지 않는다.
        /// </summary>
        public static int SpawnThreshold(int desiredCount, int regularSpawnTarget)
        {
            return OJMath.Clamp(desiredCount, 0, OJMath.Max(0, regularSpawnTarget));
        }

        /// <summary>
        /// 이 웨이브에 현상금이 나올 수 있는가.
        ///
        /// <b>보스 웨이브에는 나오지 않는다.</b> 보상이 SP·강화석이라 마지막 웨이브에
        /// 받아 봐야 쓸 곳이 없고, 보스와 겹쳐 화면이 무엇을 요구하는지 알 수 없게 된다.
        /// </summary>
        public static bool CanSpawnOnWave(int waveIndex, int totalWaves)
        {
            return waveIndex >= 1 && waveIndex < OJMath.Max(1, totalWaves);
        }
    }
}
