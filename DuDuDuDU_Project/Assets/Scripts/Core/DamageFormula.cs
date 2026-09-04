
namespace OJ.Core
{
    /// <summary>
    /// DamageFormula.Calculate 에 넘길 스냅샷.
    ///
    /// 원본 DiceMetaDataProvider.CalculateDamage 는 EquipmentManager / GameManager /
    /// ElementUpgradeManager / RelicManager 싱글톤을 계산 도중에 직접 조회했다. OJ.Core 는
    /// Assembly-CSharp 을 참조할 수 없어 그 타입들을 볼 수 없으므로, 호출부가 값을 전부 모아
    /// 이 구조체로 넘긴다. 그래서 필드가 기본형뿐이다 — DiceType 같은 enum 을 여기 끌어오면
    /// 어셈블리 경계가 다시 무너진다.
    ///
    /// 왜 필드 타입이 이렇게 섞여 있는가: 원본의 중간 변수 타입을 그대로 옮긴 것이다.
    /// BaseAttack / LevelUpAttackIncrease / AttackFlatBonus / EarlyWaveFlatBonus 가 int 인 것은
    /// 원본에서 이 값들이 int 로 더해진 뒤에야 float 로 승격되기 때문이다. float 로 바꾸면
    /// 승격 시점이 앞당겨져 큰 값에서 중간 결과가 달라진다.
    /// </summary>
    public struct DamageInputs
    {
        public int BaseAttack;
        public int LevelUpAttackIncrease;
        public int DicePip;
        public int BulletLevel;
        public int EquipmentAttackTotal;
        public float LevelDamageMultiplier;
        public float KingSynergyMultiplier;
        public bool IsKingDice;
        public float AttackPercentBonus;
        public int AttackFlatBonus;
        public int EarlyWaveFlatBonus;
        public float FinalDamagePercentBonus;
        public float ElementUpgradeMultiplier;
        public float RelicDamageMultiplier;
    }

    /// <summary>
    /// 다이스 1발의 최종 데미지. 원본 CalculateDamage 의 산술을 한 글자도 바꾸지 않고 옮긴 것이다.
    ///
    /// 이 파일을 고치기 전에 알아야 할 것:
    /// 목표는 개선이 아니라 "현행 동작 고정"이다. 골든 기준선
    /// Tests/Golden/formula_baseline.txt 의 dice.damage 섹션이 아래 식에 그대로 묶여 있고,
    /// 값이 하나라도 달라지면 마이그레이션 게이트가 실패한다.
    ///
    /// 왜 "같은 뜻의 다른 식"으로 바꾸면 안 되는가:
    ///  - float 곱셈·덧셈은 결합법칙이 성립하지 않는다. (a*b)*c 와 a*(b*c) 는 다른 비트가 될 수 있다.
    ///  - 마지막이 OJMath.RoundToInt 라 중간값이 1 ULP 만 흔들려도 0.5 경계에서 정수 결과가 1 바뀐다.
    ///    데미지는 정수로 노출되므로 이 1 이 그대로 눈에 보이는 회귀가 된다.
    ///  - <c>System.Math</c> 를 바로 쓰면 안 된다. NaN 처리와 반올림 경계가 다르다.
    ///
    /// (11.1) 위 조건을 채우고 <c>Mathf</c> 를 뗐다. <see cref="OJMath"/> 가 Unity 구현을
    /// 그대로 옮긴 것이라 산술이 보존되고, 골든 테스트가 그것을 검증한다.
    /// 이제 <c>OJ.Core</c> 는 <c>UnityEngine</c> 을 참조하지 않는다.
    /// </summary>
    public static class DamageFormula
    {
        // 원본 DiceMetaDataProvider 의 private const 를 값 그대로 옮겼다.
        // 0.5f 와 2f 는 2의 거듭제곱이라 float 곱셈에서 오차가 전혀 없다.
        // "같은 값"이라는 이유로 1f / 2f 같은 식으로 바꾸지 말 것 — 상수 폴딩에 기대는 순간
        // 컴파일러 설정에 따라 결과가 흔들릴 여지가 생긴다.
        public const float GlobalDamageBalanceMultiplier = 0.5f;
        public const float KingDiceDamageMultiplier = 2f;

        /// <summary>
        /// 원본의 연산 순서를 그대로 재현한다. 줄 순서를 섞지 말 것.
        ///
        /// 싱글톤이 null 이던 경로는 호출부가 중립값을 채워 대신한다.
        ///   EquipmentAttackTotal = 0        → attackBase += 0
        ///   AttackPercentBonus = 0f         → scaled *= (1f + 0f)
        ///   AttackFlatBonus / EarlyWaveFlatBonus = 0 → scaled += 0
        ///   FinalDamagePercentBonus = 0f    → scaled *= (1f + 0f)
        ///   ElementUpgradeMultiplier = 1f   → scaled *= 1f
        ///   RelicDamageMultiplier = 1f      → scaled *= 1f
        /// IEEE754 에서 x * 1f 는 항상 비트를 보존한다. x + 0f 도 같고, 유일한 예외가
        /// x = -0f 일 때 +0f 가 되는 것이다. 그 -0f 는 "이 식에서 나올 수 없는" 값이 아니다 —
        /// 배수가 음수면 실제로 만들어진다. 지금은 게임 코드의 배수가 전부 1f 이상이라
        /// 안 생길 뿐이고, DamageFormula 는 public 이라 테스트나 새 호출부가 음수 배수를
        /// 넣으면 바로 나온다. 그래도 반환값은 갈리지 않는다 — 마지막
        /// OJMath.Max(1, OJMath.RoundToInt(scaled)) 가 +0f 와 -0f 를 둘 다 1 로 눌러 버린다.
        /// (차분 대조 6,272,400 케이스: 반올림 전 float 비트가 갈린 것은 이 ±0f 뿐(134,190건),
        /// 반환값 불일치는 0.) 즉 "블록을 건너뛰는 것"과 "중립값을 통과시키는 것"은
        /// 같다. 다만 그 동일성은 바깥의 OJMath.Max(1, ...) 하한에 기대고 있으니,
        /// 하한을 없애거나 float 를 그대로 반환하도록 바꾸면 -0f 가 새어 나간다.
        /// 중립값을 다른 값으로 바꾸는 것도 여전히 금지다.
        /// </summary>
        public static int Calculate(DamageInputs inputs)
        {
            // 클램프가 곱셈보다 앞에 있어야 한다. pip/level 이 0 이나 음수로 들어오는 경로가
            // 실제로 있고, 원본은 그것을 1 로 올린 뒤에 계산했다.
            int pips = OJMath.Max(1, inputs.DicePip);
            int level = OJMath.Max(1, inputs.BulletLevel);

            // int 로 곱하고 더한 결과가 float 로 승격된다. 괄호를 풀면 승격 시점이 바뀐다.
            float attackBase = inputs.BaseAttack + (level * inputs.LevelUpAttackIncrease);
            attackBase += inputs.EquipmentAttackTotal;

            // 왼쪽부터 (attackBase * pips) * 0.5f 로 계산된다. 0.5f 를 앞으로 빼지 말 것.
            float scaled = attackBase * pips * GlobalDamageBalanceMultiplier;
            scaled *= inputs.LevelDamageMultiplier;
            scaled *= inputs.KingSynergyMultiplier;
            if (inputs.IsKingDice)
                scaled *= KingDiceDamageMultiplier;

            // 원본에서 이 세 줄은 EquipmentManager != null 블록 안에 있었다.
            // 곱 → 덧셈 → 곱 순서가 밸런스의 핵심이다. 평탄 보너스를 퍼센트 곱보다 먼저 더하면
            // 그 보너스까지 퍼센트를 타게 되어 값이 커진다. 세 줄을 합치거나 순서를 바꾸지 말 것.
            scaled *= (1f + inputs.AttackPercentBonus);
            scaled += inputs.AttackFlatBonus + inputs.EarlyWaveFlatBonus;
            scaled *= (1f + inputs.FinalDamagePercentBonus);

            scaled *= inputs.ElementUpgradeMultiplier;
            scaled *= inputs.RelicDamageMultiplier;

            // OJMath.RoundToInt 는 은행가 반올림(짝수로)이다. 기준선의 damage[Normal][pip=1][lv=6] 은
            // 16.5 를 16 으로 내린 값이라 (int)(scaled + 0.5f) 로 바꾸면 17 이 되어 어긋난다.
            // 바깥 OJMath.Max(1, ...) 는 데미지 0 을 막는 하한이다.
            return OJMath.Max(1, OJMath.RoundToInt(scaled));
        }
    }
}
