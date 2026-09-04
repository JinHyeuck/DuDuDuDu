
namespace OJ.Core
{
    /// <summary>
    /// StageData 의 웨이브 성장식을 담는 순수 함수 모음.
    /// </summary>
    /// <remarks>
    /// 이 클래스의 목적은 "개선"이 아니라 "현행 동작 고정"이다.
    /// Tests/Golden/formula_baseline.txt 에 스테이지별 monsterHp/monsterDefense/bossHp/
    /// bossDefense/bossSpawnThreshold 가 정수로 박제돼 있고, 값이 하나라도 달라지면 실패다.
    ///
    /// 그래서 아래 규칙을 지킨다:
    /// - 표현식을 StageData.cs 원본에서 문자 그대로 옮겼다. 연산 순서와 괄호를 바꾸지 않는다.
    ///   float 덧셈은 결합법칙이 성립하지 않아 (a + b) + c 와 a + (b + c) 의 결과가 다를 수 있고,
    ///   그 차이가 OJMath.RoundToInt 의 .5 경계에서 정수 1 차이로 증폭된다.
    /// - Mathf 를 System.Math 로 바꾸지 않는다. System.Math 는 double 로 계산하므로
    ///   OJMath.Pow(float,float) / OJMath.RoundToInt(float) 와 반올림 결과가 갈릴 수 있다.
    ///   특히 OJMath.RoundToInt 는 은행가 반올림(중간값 → 짝수)이라 Math.Round 기본 동작과 겹치지만
    ///   입력이 float 인지 double 인지에 따라 경계값 자체가 달라진다.
    /// - 중간 변수의 타입(int waveOffset 이 아니라 float waveOffset 등)도 원본 그대로다.
    ///   승격 시점이 바뀌면 곱셈이 int 곱으로 잘리거나 정밀도가 달라진다.
    /// - 상수(1f, 4f, 1.35f, 1.12f, 0.65f, 0.96f, 0.5f)는 값을 그대로 옮겼다.
    ///
    /// OJ.Core 는 Assembly-CSharp 을 참조할 수 없으므로 StageData 를 인자로 받지 않는다.
    /// 필요한 필드 값을 호출부에서 기본형으로 풀어서 넘긴다.
    /// </remarks>
    public static class StageGrowthFormula
    {
        /// <summary>
        /// 웨이브별 일반 몬스터 체력. 원본: StageData.GetMonsterHpForWave.
        /// </summary>
        public static int MonsterHp(int baseMonsterHp, float linearFactor, float quadraticFactor, int waveIndex)
        {
            // waveIndex 가 0 이하로 들어오는 호출부가 있어 Max(1, ...) 로 방어한다. 제거하면 waveOffset 이 음수가 된다.
            int validWave = OJMath.Max(1, waveIndex);
            // int 뺄셈 결과를 float 에 담는다. 이후 곱셈이 전부 float 로 진행되도록 하는 승격 지점이다.
            float waveOffset = validWave - 1;
            float multiplier = 1f + (waveOffset * linearFactor) + (waveOffset * waveOffset * quadraticFactor);
            return OJMath.Max(1, OJMath.RoundToInt(baseMonsterHp * multiplier));
        }

        /// <summary>
        /// 웨이브별 일반 몬스터 방어력. 원본: StageData.GetMonsterDefenseForWave.
        /// resolvedBaseDefense 는 <see cref="ResolvedBaseDefense"/> 로 이미 확정된 값을 받는다.
        /// </summary>
        public static int MonsterDefense(int resolvedBaseDefense, float linearFactor, float quadraticFactor, int waveIndex)
        {
            // HP 식과 형태가 같지만 하한이 0 이다(방어력 0 은 정상값). 두 식을 공통 함수로 합치지 않는다.
            int validWave = OJMath.Max(1, waveIndex);
            float waveOffset = validWave - 1;
            float multiplier = 1f + (waveOffset * linearFactor) + (waveOffset * waveOffset * quadraticFactor);
            return OJMath.Max(0, OJMath.RoundToInt(resolvedBaseDefense * multiplier));
        }

        /// <summary>
        /// baseMonsterDefense 가 0 일 때 스테이지 인덱스로 방어력을 역산한다.
        /// 원본: StageData.GetResolvedBaseMonsterDefense.
        /// </summary>
        public static int ResolvedBaseDefense(int baseMonsterDefense, int stageIndex, int totalWaves)
        {
            // 데이터에 값이 박혀 있으면 그대로 쓴다. 0 은 "미입력"의 뜻이라 아래 역산식으로 내려간다.
            if (baseMonsterDefense > 0)
                return baseMonsterDefense;

            // OJMath.Max(int, int) 오버로드가 먼저 int 로 계산되고 그 뒤 float 로 승격된다.
            // OJMath.Max(0f, stageIndex - 1) 로 바꾸면 승격 시점이 앞당겨져 표현식이 달라진다.
            float stageOffset = OJMath.Max(0, stageIndex - 1);
            float defenseValue = 4f + (stageOffset * 1.35f) + (OJMath.Pow(stageOffset, 1.12f) * 0.65f);
            // totalWaves 10/20 만 보정한다. 그 외 길이는 보정 없음 — 범위 조건으로 일반화하면 15웨이브 값이 바뀐다.
            if (totalWaves == 10)
                defenseValue *= 1.12f;
            else if (totalWaves == 20)
                defenseValue *= 0.96f;

            return OJMath.Max(0, OJMath.RoundToInt(defenseValue));
        }

        /// <summary>
        /// 보스 체력. 원본: StageData.GetBossHpForWave.
        /// </summary>
        public static int BossHp(int monsterHpForWave, float bossHpMultiplier)
        {
            // 반올림된 정수 몬스터 체력에 배수를 곱한다. 반올림 전 float 체력을 쓰면 값이 달라지므로
            // 반드시 MonsterHp 의 int 결과를 받아야 한다.
            return OJMath.Max(1, OJMath.RoundToInt(monsterHpForWave * bossHpMultiplier));
        }

        /// <summary>
        /// 보스 방어력. 원본: StageData.GetBossDefenseForWave.
        /// </summary>
        public static int BossDefense(int monsterDefenseForWave, float bossDefenseMultiplier)
        {
            // BossHp 와 마찬가지로 반올림된 정수 방어력을 입력으로 받는다(이중 반올림이 원본 동작이다).
            return OJMath.Max(0, OJMath.RoundToInt(monsterDefenseForWave * bossDefenseMultiplier));
        }

        /// <summary>
        /// 보스가 등장하는 처치 수 임계값. 원본: StageData.GetBossSpawnThreshold.
        /// </summary>
        public static int BossSpawnThreshold(int monstersPerWave)
        {
            // monstersPerWave / 2 (정수 나눗셈)가 아니라 * 0.5f 후 올림이다. 홀수일 때 결과가 다르다.
            return OJMath.Clamp(OJMath.CeilToInt(monstersPerWave * 0.5f), 1, OJMath.Max(1, monstersPerWave));
        }
    }
}
