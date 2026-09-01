
namespace OJ.Core
{
    /// <summary>
    /// 한 발이 맞기 직전 <b>데미지에 곱해지는 배수 3단</b>. AttackContent.PlayHit 의
    /// 140~147 줄에서 글자 그대로 내려온 것이다. (MIGRATION_BASELINE 5.1-b)
    ///
    /// 3단은 순서대로 이렇다 — <b>이 순서가 밸런스다</b>:
    ///   1) 전역 크리티컬  <c>OJMath.RoundToInt(damage * 크리배수)</c>       하한 없음
    ///   2) 일반 다이스 lv≥12 더블  <c>damage *= 2</c>                      정수 곱
    ///   3) 유물 공격 배수  <c>OJMath.Max(1, OJMath.RoundToInt(damage * 배수))</c>  하한 1
    /// 세 단계는 각각 <b>따로 정수로 접힌다.</b> 1단과 2단을 <c>RoundToInt(d * 크리배수 * 2f)</c> 로
    /// 합치면 값이 갈린다 — Mono 실측으로 damage 2 / 3 / 7 / 8 / 12 에서 이미 갈리는 것을 확인했다.
    /// 아래 <see cref="ApplyCritical"/> 가 그 순서와 접는 지점을 통째로 잠근다.
    ///
    /// <b>여기 들어올 수 없는 것 (전부 호출부에 남겼다):</b>
    ///  - <c>Random.value</c>. 난수는 순수 함수가 아니다. 그래서 확률 판정은
    ///    "굴린 값"을 인자로 받는 술어(<see cref="RollHitsCritical"/> /
    ///    <see cref="RollHitsDoubleHit"/>)로 쪼갰고, <b>굴릴지 말지</b>는 호출부가 정한다.
    ///    이유는 정확히 <b>단축평가</b>다 — 원본은 <c>critChance &gt; 0f &amp;&amp; Random.value * 100f &lt;= critChance</c>
    ///    라서 크리 확률이 0 이면 난수를 <b>안 뽑는다.</b> 두 조건을 한 함수에 넣으면 인자를
    ///    만들려고 <c>Random.value</c> 를 먼저 평가하게 되고, 그 순간 난수열이 한 칸 밀린다.
    ///    같은 이유로 <see cref="IsCriticalChanceActive"/> 는 <see cref="RollHitsCritical"/> 와
    ///    <b>반드시 두 함수여야 한다.</b> 합치지 마라.
    ///  - <c>DiceMetaDataProvider.GetGlobalCritical*</c> / <c>DiceLevelManager</c> /
    ///    <c>DiceTypeStarManager</c> / <c>RelicManager</c> 조회. 이 프로젝트의 MonoSingleton
    ///    getter 는 읽기가 아니라 <c>FindObjectOfType</c> + 생성 시도다(Utils/Singleton.cs:63-82).
    ///  - <c>RelicManager.ConsumeAttackDamageMultiplier()</c>. 이것은 특히 <b>읽기가 아니라 쓰기</b>다 —
    ///    <c>firstWaveAttackUsed</c> 를 세우고 LuckyMagazine 판정으로 난수를 하나 더 뽑는다.
    ///    순수 함수 안에서 부르면 "계산했다"가 곧 "유물 1회성 효과를 소모했다"가 된다.
    ///    호출부가 <b>3단의 차례가 됐을 때</b> 불러서 결과 float 만 넘긴다.
    ///  - <c>attackType == DiceType.Normal</c>. DiceType 은 Assembly-CSharp 타입이라
    ///    OJ.Core 에서 볼 수 없다. 타입 판정은 호출부에 남고 <b>레벨 임계만</b>
    ///    <see cref="IsDoubleHitLevel"/> 로 내려왔다.
    ///
    /// <b>이 파일을 고치기 전에:</b> 목표는 개선이 아니라 현행 동작 고정이다.
    /// Mathf 를 System.Math 로 바꾸지 마라(Math 는 double 로 올려 계산해 반올림 경계가 달라진다).
    /// <c>OJMath.RoundToInt</c> 는 <b>은행가 반올림</b>이다 — Mono 실측으로 0.5→0, 2.5→2, 4.5→4.
    /// <c>(int)(x + 0.5f)</c> 로 바꾸면 이 격자에서 27점이 어긋난다.
    /// </summary>
    public static class CriticalFormula
    {
        // 일반 다이스 더블히트 임계 레벨과 확률. AttackContent.cs:143 의 리터럴 12 / 0.2f 다.
        //
        // <b>바로 아래 줄(:149)의 SP 획득 0.2f 와 합치지 마라.</b> 값이 같지만 서로 다른 기능이고
        // (한쪽은 데미지 2배, 한쪽은 SP +5), 한쪽만 조정되는 날 합쳐 둔 상수가 조용히 양쪽을
        // 같이 움직인다. 같은 이유로 SP 쪽 임계 9 도 여기 끌어오지 않았다 —
        // 그쪽은 배수가 아니라서 이 파일의 담당이 아니다.
        public const int DoubleHitMinLevel = 12;
        public const float DoubleHitChance01 = 0.2f;

        // 2단의 곱수. 정수 곱이라 float 로 쓰면 안 된다 —
        // <c>damage * 2</c> 와 <c>OJMath.RoundToInt(damage * 2f)</c> 는 2^24 위와 오버플로에서 갈린다.
        private const int DoubleHitFactor = 2;

        // ── 확률 판정 (난수는 인자로 받는다) ──────────────────────────────────────────

        /// <summary>
        /// AttackContent.cs:141 의 <b>왼쪽</b> 피연산자 <c>critChance &gt; 0f</c>.
        ///
        /// 이것이 <see cref="RollHitsCritical"/> 와 <b>따로 있는 이유가 단축평가</b>다.
        /// 위 클래스 주석을 볼 것 — 합치면 크리 확률 0 에서도 난수를 뽑게 되어 난수열이 밀린다.
        ///
        /// 원본이 지금 실제로 내놓는 값은 0f 아니면 10f 뿐이다(왕일반 소환 + lv≥9).
        /// 그래서 이 게이트는 사실상 "왕일반이 lv9 미만이거나 안 나왔다"를 뜻한다.
        /// <c>&gt;=</c> 로 바꾸면 그 상태에서 난수를 뽑기 시작한다 — 데미지는 안 바뀌는데
        /// <b>난수열이 통째로 밀려</b> 그 뒤의 모든 확률 판정이 달라진다. 골든에서는
        /// core.crit.active[0] 한 줄이 그것을 잡는다.
        /// </summary>
        public static bool IsCriticalChanceActive(float criticalChancePercent)
        {
            return criticalChancePercent > 0f;
        }

        /// <summary>
        /// AttackContent.cs:141 의 <b>오른쪽</b> 피연산자 <c>Random.value * 100f &lt;= critChance</c>.
        /// <paramref name="roll01"/> 은 <c>Random.value</c>(0 이상 1 이하)가 들어올 자리다.
        ///
        /// <b>중간 결과를 float 지역변수에 대입하지 마라.</b> 이 프로젝트에서 가장 값비싼 함정이
        /// 바로 여기 있다 — Mono 는 <c>roll01 * 100f</c> 를 float32 로 접지 않고 더 높은
        /// 정밀도로 들고 가서 비교한다(C# 명세가 허용한다). 그래서 <b>게임의 실제 크리 경계는
        /// 10% 가 아니다</b>:
        /// <code>
        ///   roll = 0.099999994f (0.1f-1ulp) : 0.1f 미만 → 크리   True
        ///   roll = 0.1f                     : 0.1f*100f 는 float32 로 접으면 정확히 10f 인데도
        ///                                     확장 정밀도에서는 10.0000001490116 이라  False
        /// </code>
        /// 즉 <c>float t = roll01 * 100f; return t &lt;= critChance;</c> 로 "정리"하면 t 가 10f 로
        /// 접혀 <b>같은 입력이 크리가 된다.</b> 골든의 core.crit.roll[0.1][10] 과
        /// [0.33][33] 두 줄이 정확히 이 변이를 잡는다. 두 줄을 지우면 통과한다.
        ///
        /// <c>roll01 &lt;= critChance * 0.01f</c> 로 바꾸는 변이도 다르다(무작위 500만 표본 중 15만 건
        /// 갈렸고, 이 격자에서는 [0.5][50] 과 [1][100] 이 잡는다).
        /// 반면 <c>roll01 &lt;= critChance / 100f</c> 는 <b>변수 나눗셈이면</b> Mono 확장 정밀도에서
        /// 같은 답을 낸다(500만 표본 갈림 0). 못 잡는 축으로 덤퍼에 적어 뒀다.
        /// </summary>
        public static bool RollHitsCritical(float roll01, float criticalChancePercent)
        {
            return roll01 * 100f <= criticalChancePercent;
        }

        /// <summary>
        /// AttackContent.cs:143 의 <c>diceLevel &gt;= 12</c>. 타입 판정(Normal)은 호출부에 남았다.
        /// </summary>
        public static bool IsDoubleHitLevel(int diceLevel)
        {
            return diceLevel >= DoubleHitMinLevel;
        }

        /// <summary>
        /// AttackContent.cs:143 의 <c>Random.value &lt;= 0.2f</c>.
        ///
        /// 위 <see cref="RollHitsCritical"/> 와 달리 여기는 곱셈이 없어서 확장 정밀도가 끼어들
        /// 자리가 없다 — <c>roll01 = 0.2f</c> 는 <b>더블히트다</b>(경계 포함). 다만 리터럴을
        /// <c>0.2</c>(double)로 바꾸면 갈린다: 0.2f 는 0.20000000298 이고 0.2 는 0.2000000000000000111
        /// 이라 그 한 줄이 뒤집힌다. 골든 core.crit.doubleRoll[0.2] 이 그것을 잡는다.
        /// </summary>
        public static bool RollHitsDoubleHit(float roll01)
        {
            return roll01 <= DoubleHitChance01;
        }

        // ── 배수 적용 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 1단. AttackContent.cs:142 <c>OJMath.RoundToInt(damage * 크리배수)</c>.
        ///
        /// <b>하한이 없다.</b> <c>OJMath.Max(1, ·)</c> 를 넣지 마라 — 3단(유물)에만 하한이 있고,
        /// 유물 매니저가 없는 판에서는 그 하한조차 안 걸린다. 그것이 현행 동작이다.
        /// (하한을 넣는 변이는 이 격자에서 43점 어긋난다.)
        ///
        /// <paramref name="damage"/> 는 곱하기 전에 <b>double 이 아니라 float 로</b> 승격된다.
        /// C# 이항 수치 승격이 int→float 를 먼저 하기 때문이다. 그래서 2^24+1 은 곱하기 전에
        /// 이미 16777216 으로 깎인다 — 골든 core.crit.apply[16777217][*] 이 그 깎임을 박제한다.
        /// </summary>
        public static int CriticalDamage(int damage, float criticalDamageMultiplier)
        {
            return OJMath.RoundToInt(damage * criticalDamageMultiplier);
        }

        /// <summary>
        /// 2단. AttackContent.cs:144 <c>damage *= 2</c>.
        ///
        /// <b>정수 곱이다.</b> <c>OJMath.RoundToInt(damage * 2f)</c> 로 바꾸면 float 를 거치면서
        /// 2^24 위에서 깎이고 오버플로 동작도 달라진다(이 격자에서 3점). C# 기본 unchecked 라
        /// 1073741824 는 -2147483648 로 넘어간다 — 그 부호 전환도 골든이 들고 있다.
        /// </summary>
        public static int DoubleHitDamage(int damage)
        {
            return damage * DoubleHitFactor;
        }

        /// <summary>
        /// 3단. AttackContent.cs:147 <c>OJMath.Max(1, OJMath.RoundToInt(damage * 유물배수))</c>.
        ///
        /// <b>하한 1 은 여기에만 있다.</b> 1단과 격자를 똑같이 맞춰 뜨는 이유가 그것이다 —
        /// 골든에서 core.crit.apply[d][m] 과 core.crit.relic[d][m] 을 나란히 놓으면
        /// 하한이 실제로 무는 자리가 diff 로 보인다(43줄).
        ///
        /// <paramref name="relicDamageMultiplier"/> 는 <c>ConsumeAttackDamageMultiplier()</c> 가
        /// 이미 돌아온 값이다. 그 함수는 <b>쓰기</b>라 여기서 부를 수 없다 — 클래스 주석을 볼 것.
        /// </summary>
        public static int RelicDamage(int damage, float relicDamageMultiplier)
        {
            return OJMath.Max(1, OJMath.RoundToInt(damage * relicDamageMultiplier));
        }

        // ── 3단 합성 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// AttackContent.cs:141-147 전체. <b>이 함수의 존재 이유는 값이 아니라 순서다.</b>
        ///
        /// 세 인자쌍이 전부 "판정이 끝난 bool + 이미 뽑아 온 float" 인 것은 일부러다.
        /// 판정과 조회는 부작용(난수 소모 / 싱글톤 생성 / 유물 소진)이라 호출부에 남는다.
        /// 호출부는 <b>이 함수를 부르기 전에</b> 원본과 같은 차례로 그것들을 평가해야 한다:
        /// 크리 난수 → 크리배수 조회 → 더블 난수 → 유물 소모. 차례가 바뀌면 값은 같아 보여도
        /// 난수열이 밀리고 유물 1회성 효과가 다른 발에서 터진다.
        ///
        /// <paramref name="relicMultiplierApplies"/> 를 <c>relicDamageMultiplier = 1f</c> 로
        /// 대신할 수 없다. 3단은 곱만 하는 것이 아니라 <b>하한 1 을 같이 건다</b> —
        /// damage=0 이면 "유물 없음"은 0 을 내고 "유물 배수 1f"는 1 을 낸다. 실제로 갈린다.
        ///
        /// 세 단계를 <b>합치지 마라.</b> 각 단계가 따로 int 로 접히는 것이 사양이다.
        /// Mono 실측: <c>crit</c>+<c>double</c> 을 <c>RoundToInt(d * critMul * 2f)</c> 로 합치면
        /// damage 2/3/7/8/12 에서 이미 갈린다(이 격자에서 10점).
        /// </summary>
        public static int ApplyCritical(
            int damage,
            bool criticalHit,
            float criticalDamageMultiplier,
            bool doubleHit,
            bool relicMultiplierApplies,
            float relicDamageMultiplier)
        {
            if (criticalHit)
                damage = CriticalDamage(damage, criticalDamageMultiplier);
            if (doubleHit)
                damage = DoubleHitDamage(damage);
            if (relicMultiplierApplies)
                damage = RelicDamage(damage, relicDamageMultiplier);

            return damage;
        }
    }
}
