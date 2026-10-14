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
    /// <b>기본 공격력의 정의가 바뀌었다(다이스 퍼센트 개편).</b> 예전에는
    /// <c>BaseAttack + (level * LevelUpAttackIncrease) + EquipmentAttackTotal</c> 이었다 —
    /// 다이스의 정수 공격력과 장비의 정수 공격력을 <b>더하는</b> 구조다. 그 구조에서는 장비가
    /// 조금만 자라도 다이스 항이 묻혀서, 레벨 12 노말(48)과 레벨 1 노말(15)의 차이가 장비
    /// 714 앞에서 4% 로 줄었다. <b>다이스를 키우는 의미가 후반에 사라지는 것</b>이 문제였다.
    ///
    /// 이제 다이스는 <b>장비 공격력의 배율</b>이다. <c>EquipmentAttackTotal * 퍼센트</c> 하나이고
    /// 퍼센트는 다이스 종류와 강화 레벨로 정해진다(<see cref="DamageFormula.AttackPercent"/>).
    /// 그래서 다이스 레벨의 효과가 장비 성장에 비례해 같이 커진다 — 어느 시점에도 같은 비율이다.
    ///
    /// <b>딸린 전제 하나.</b> 곱이므로 <c>EquipmentAttackTotal</c> 이 0 이면 데미지가 전부 0 이
    /// 된다(하한 1 로 눌린다). 그래서 장비 공격력은 레벨 1 에서도 0 이 아니어야 하고,
    /// <see cref="EquipmentUpgradeFormula.Attack"/> 의 'level &lt;= 1 → 0' 조기 반환을 그
    /// 개편에서 같이 걷어냈다. 그 조기 반환을 되살리면 신규 유저의 데미지가 1 로 떨어진다.
    ///
    /// 왜 필드 타입이 이렇게 섞여 있는가: 원본의 중간 변수 타입을 그대로 옮긴 것이다.
    /// AttackFlatBonus / EarlyWaveFlatBonus 가 int 인 것은 원본에서 이 값들이 int 로 더해진
    /// 뒤에야 float 로 승격되기 때문이다. float 로 바꾸면 승격 시점이 앞당겨져 큰 값에서
    /// 중간 결과가 달라진다.
    /// </summary>
    public struct DamageInputs
    {
        /// <summary>레벨 1 에서 장비 공격력에 곱하는 비율. 1f = 100%.</summary>
        public float DiceBaseAttackPercent;

        /// <summary>레벨 한 칸당 더해지는 비율. 0.1f = 레벨마다 +10%p.</summary>
        public float DiceAttackPercentPerLevel;

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
    /// 다이스 1발의 최종 데미지.
    ///
    /// 이 파일을 고치기 전에 알아야 할 것:
    /// 골든 기준선 <c>Tests/Golden/formula_baseline.txt</c> 의 <c>core.damage</c> ·
    /// <c>core.dmgChain</c> 구획이 아래 식에 그대로 묶여 있고, 값이 하나라도 달라지면
    /// 마이그레이션 게이트가 실패한다. <b>기대값을 손으로 계산해 적지 말 것</b> —
    /// AGENTS.md 의 부동소수 절에 근거가 있다(Mono 는 중간 결과를 매 연산마다 접지 않는다).
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
        // 0.5 → 0.75 (2026-10-14, 다이스 딜 1.5배). 0.75 도 2진 유한소수라 오차가 없다.
        public const float GlobalDamageBalanceMultiplier = 0.75f;
        public const float KingDiceDamageMultiplier = 2f;

        /// <summary>
        /// 다이스가 장비 공격력에 곱하는 비율. <b>레벨 1 이 그대로 <paramref name="basePercent"/> 다.</b>
        ///
        /// <c>(level - 1)</c> 인 것이 요점이다. 예전 정수식은 <c>level * levelUpAttackIncrease</c> 라
        /// 레벨 1 에서도 증가분이 한 칸 붙었는데, 그러면 "노말 1레벨 = 장비 공격력 100%" 라는
        /// 기획의 기준점을 식으로 표현할 수가 없다. 1 에서 0 칸, 12 에서 11 칸이다.
        ///
        /// 하한 둘 다 의미가 있다:
        ///  - <c>OJMath.Max(1, level)</c>: 레벨 0·음수로 들어오는 경로가 실제로 있다
        ///    (매니저가 없을 때의 기본값 경로). 원본도 1 로 올린 뒤 계산했다.
        ///  - <c>OJMath.Max(0f, ·)</c>: 퍼센트가 음수면 아래 곱셈 연쇄 전체의 부호가 뒤집혀
        ///    평탄 보너스만 남는 이상한 데미지가 된다. 에셋에서 사람이 음수를 넣을 수 있는
        ///    값이라 여기서 막는다.
        /// </summary>
        public static float AttackPercent(float basePercent, float percentPerLevel, int level)
        {
            int clamped = OJMath.Max(1, level);
            return OJMath.Max(0f, basePercent + ((clamped - 1) * percentPerLevel));
        }

        /// <summary>
        /// 연산 순서를 섞지 말 것.
        ///
        /// 싱글톤이 null 이던 경로는 호출부가 중립값을 채워 대신한다.
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
        /// 즉 "블록을 건너뛰는 것"과 "중립값을 통과시키는 것"은 같다. 다만 그 동일성은 바깥의
        /// OJMath.Max(1, ...) 하한에 기대고 있으니, 하한을 없애거나 float 를 그대로 반환하도록
        /// 바꾸면 -0f 가 새어 나간다. 중립값을 다른 값으로 바꾸는 것도 여전히 금지다.
        ///
        /// <b>EquipmentAttackTotal 이 0 이면 반환이 항상 1 이다.</b> 평탄 보너스가 있어도
        /// 거기까지 가기 전에 곱이 0 이 되기 때문이 아니라 — 실제로는 평탄 보너스가 더해지므로
        /// 0 이 아닐 수 있다 — 장비가 0 인 상태 자체가 사양 밖이라는 뜻이다. 위
        /// <see cref="DamageInputs"/> 주석의 "딸린 전제"를 볼 것.
        /// </summary>
        public static int Calculate(DamageInputs inputs)
        {
            // 클램프가 곱셈보다 앞에 있어야 한다. pip/level 이 0 이나 음수로 들어오는 경로가
            // 실제로 있고, 원본은 그것을 1 로 올린 뒤에 계산했다.
            int pips = OJMath.Max(1, inputs.DicePip);

            // level 클램프는 AttackPercent 안에 있다. 여기서 또 자르지 말 것 —
            // 두 군데서 자르면 한쪽을 고칠 때 다른 쪽이 조용히 남는다.
            float percent = AttackPercent(
                inputs.DiceBaseAttackPercent, inputs.DiceAttackPercentPerLevel, inputs.BulletLevel);

            // int * float. 장비 합계가 int 인 채로 승격된다. 괄호를 풀거나 percent 를
            // 왼쪽으로 옮기지 말 것 — 승격 시점과 곱 순서가 둘 다 바뀐다.
            float attackBase = inputs.EquipmentAttackTotal * percent;

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

            // OJMath.RoundToInt 는 은행가 반올림(짝수로)이다. (int)(scaled + 0.5f) 로 바꾸면
            // .5 에 정확히 떨어지는 케이스에서 1 이 어긋난다.
            // 바깥 OJMath.Max(1, ...) 는 데미지 0 을 막는 하한이다.
            return OJMath.Max(1, OJMath.RoundToInt(scaled));
        }
    }
}
