
namespace OJ.Core
{
    /// <summary>
    /// 피해를 <b>받는 쪽</b>의 산술. Monster.TakeDamage 와 Wall.TakeDamage 에서 글자 그대로 내려온 것이다.
    ///
    /// 왜 이 파일이 먼저 생겼는가(MIGRATION_BASELINE 5.1-a):
    /// 골든 기준선 2581키 중 이 경로의 <b>합성식을 밟는 키는 0개</b>다. 방어력 감쇄
    /// 100/(100+armor), 상태 피해증가 합산, OJMath.CeilToInt 를 통째로 지우고 <c>return dmg;</c> 로
    /// 바꿔도 EditMode 496개와 골든이 전부 초록이었다. 덤퍼는 로비에서 돌고, 이 경로를 뜨려면
    /// Monster 인스턴스가 필요한데 그 인스턴스의 사망 분기가 세이브 쓰기·풀 반환·싱글톤 5종을
    /// 건드려서 원리적으로 덤프가 불가능하다. <b>산술을 여기로 내리는 것 외에 이 사각지대를
    /// 닫을 방법이 없다.</b>
    ///
    /// 이 파일을 고치기 전에 알아야 할 것:
    ///  - 목표는 개선이 아니라 현행 동작 고정이다. 식을 "정리"하지 마라.
    ///  - float 곱셈·덧셈은 결합법칙이 성립하지 않는다. (a*b)*c 와 a*(b*c) 는 다른 비트가 된다.
    ///    마지막이 OJMath.CeilToInt 라 1 ULP 차이가 정수 1 차이로 그대로 새어 나온다.
    ///  - Mathf 를 System.Math 로 바꾸면 안 된다. Math 쪽은 double 로 올려서 계산한다.
    ///    (11.1 에서 OJ.Core 는 noEngineReferences: true 가 됐다. Mathf 대신 OJMath 를 쓰는데,
    ///     그것이 Unity 구현을 그대로 옮긴 것이라 값은 보존된다 — DamageFormula.cs 참조.)
    ///  - 시간과 싱글톤 조회는 여기 들어올 수 없다. 만료 판정은 <c>now</c> 를 인자로 받고,
    ///    싱글톤에서 나오는 값은 호출부가 미리 뽑아 기본형으로 넘긴다.
    ///
    /// <b>UIBattleDiceDetailPanel.CalculateAppliedDamage 를 이 함수로 바꾸지 마라.</b> 식이 다르다:
    ///   Monster.cs        : <c>(dmg * defMul) * incMul</c>, 하한 없음, 보너스 클램프는 write 시점
    ///   UIBattleDiceDetail: <c>dmg * (defMul * incMul)</c>, <c>OJMath.Max(1, ·)</c>, 보너스 클램프를 그 자리에서
    /// 둘은 실제로 값이 갈릴 수 있다("전투 상세 패널의 예상 피해" != "실제 피해"). 그 발산을 고치는
    /// 것은 산술 변경이므로 이 단계의 범위 밖이다 — MIGRATION_BASELINE 의 알려진 항목으로 남긴다.
    /// </summary>
    public static class IncomingDamageFormula
    {
        // 왕얼음/왕독 시너지의 피해증가. 원본 Monster.cs:154 / :156 의 리터럴 15 를 그대로 옮겼다.
        // 값이 같지만 상수를 하나로 합치지 않는다 — 둘은 서로 다른 시너지이고, 한쪽만 바뀌는 날
        // 합쳐 둔 상수가 조용히 양쪽을 같이 움직인다.
        //
        // private 인 것도 의도다. public 으로 열면 UIBattleDiceDetailPanel 의 +15 들이 이 상수를
        // 참조하게 되고, 위 주석이 "합치지 말라"고 적어 둔 두 식이 상수를 통해 결합된다.
        private const int KingIceDamageTakenBonusPercent = 15;
        private const int KingPoisonDamageTakenBonusPercent = 15;

        // ── 몬스터: 방어력 감쇄 ────────────────────────────────────────────────────────

        /// <summary>
        /// Monster.cs:147-150 원문. 방어력이 음수면 감쇄가 아니라 증폭이 되고, 상한이 2배다.
        ///
        /// 음수 방어력은 이론상 값이 아니다 — ArmorBreak(level>=6, 40%) 에 CrackHammer 보너스가
        /// 얹혀 <c>_defenseDownAmount &gt; _baseDefense</c> 가 되면 실제로 도달한다.
        ///
        /// <c>float armor = defense;</c> 라는 중간 변수를 지우지 마라. int 를 그 자리에서 승격시키면
        /// 승격 시점이 달라진다.
        ///
        /// 두 분기 다 0 나눗셈이 나지 않는다. <c>armor == -100</c> 은 분모가 0 이 되는 유일한 값이지만
        /// 그 값은 음수 분기로 가고(<c>2f - 100f/200f = 1.5f</c>), 음수 분기의 분모 <c>100f - armor</c> 는
        /// armor &lt; 0 이므로 항상 100 보다 크다. 즉 <b>반환값은 항상 유한하고 0 보다 크다</b>
        /// (양수 분기는 (0, 1], 음수 분기는 (1, 2)). Mono 실측으로 확인했다.
        /// </summary>
        public static float DefenseMultiplier(int defense)
        {
            float armor = defense;
            return armor >= 0f
                ? 100f / (100f + armor)
                : 2f - (100f / (100f - armor));
        }

        // ── 몬스터: 상태 피해증가 ──────────────────────────────────────────────────────

        /// <summary>
        /// Monster.cs:152-158.
        ///
        /// <b>세 인자가 전부 "이미 판정이 끝난 값"인 것은 일부러다.</b> 원본은
        ///   <c>IsSlowed() &amp;&amp; DiceMetaDataProvider.HasKingIceDamageBonus()</c>
        /// 처럼 단축평가로 되어 있는데, 오른쪽 피연산자를 미리 평가하면 <c>DiceLevelManager.Instance</c>
        /// / <c>DiceTypeStarManager.Instance</c> 에 손이 닿는다. 이 프로젝트의 MonoSingleton getter 는
        /// 읽기가 아니다 — <c>FindObjectOfType</c> 를 돌리고, 못 찾으면 생성을 시도하거나 에러 로그를
        /// 남긴다(Utils/Singleton.cs:63-82). 즉 <b>단축평가를 푸는 것 자체가 관측 가능한 변화</b>다.
        /// 그래서 판정은 호출부에 남기고 여기는 결과만 받는다.
        ///
        /// <paramref name="slowRelicBonusPercent"/> 는 원본에서
        /// <c>IsSlowed() &amp;&amp; RelicManager.Instance != null</c> 일 때만 더해지던 값이다.
        /// 조건이 거짓이면 호출부가 0 을 넘긴다 — int 덧셈이라 "안 더한 것"과 비트가 같다.
        /// (RelicManager.GetSlowDamageTakenBonusPercent 는 Database 프로퍼티를 통해 지연 로드를
        /// 유발할 수 있어서, 이쪽도 미리 부르면 안 된다.)
        ///
        /// 원본에는 이 합에 하한이 없다. 6종 필드는 <c>Apply*DamageTakenBonus</c> 가 write 시점에
        /// <c>OJMath.Max(0, percent)</c> 로 눌러 두지만 여기 stateBonus 만 예외라, 유물 primaryValue 가
        /// 음수면 그대로 새어 들어간다. 클램프를 <b>추가하지 마라</b> — 현행 동작이다.
        /// </summary>
        public static int StateBonusPercent(
            bool kingIceBonusApplies,
            bool kingPoisonBonusApplies,
            int slowRelicBonusPercent)
        {
            int stateBonusPercent = 0;
            if (kingIceBonusApplies)
                stateBonusPercent += KingIceDamageTakenBonusPercent;
            if (kingPoisonBonusApplies)
                stateBonusPercent += KingPoisonDamageTakenBonusPercent;
            stateBonusPercent += slowRelicBonusPercent;
            return stateBonusPercent;
        }

        /// <summary>
        /// Monster.cs:160 의 괄호 안. 전부 int 덧셈이라 결과가 정확하고 순서에 영향받지 않지만,
        /// <b>인자 순서는 원본 그대로 두는 것이 규약이다.</b> 여기서 float 이 하나라도 섞이는 날
        /// 순서가 곧 결과가 된다 — 그때 이 시그니처가 이미 옳은 순서로 잠겨 있어야 한다.
        ///
        /// 7개를 개별 인자로 받는 이유: "어떤 필드가 이 합에 참여하는가"가 이 목록이다.
        /// 새 상태이상을 추가하면서 여기 인자를 안 늘리면 컴파일이 통과해 버리는 대신
        /// 그 상태이상이 조용히 무시된다. 인자를 늘려야만 하도록 펼쳐 둔다.
        /// </summary>
        public static int TotalBonusPercent(
            int poisonPercent,
            int stunPercent,
            int armorBreakPercent,
            int thunderPercent,
            int windPercent,
            int relicPercent,
            int statePercent)
        {
            return poisonPercent + stunPercent + armorBreakPercent + thunderPercent + windPercent + relicPercent + statePercent;
        }

        // ── 몬스터: 최종 합성 ──────────────────────────────────────────────────────────

        /// <summary>
        /// Monster.cs:147-161 전체.
        ///
        /// <b>마지막 줄의 곱 체인을 쪼개지 마라.</b> C# 좌결합으로
        /// <c>(dmg * damageMultiplier) * incomingDamageMultiplier</c> 이고, 중간 결과를 로컬에
        /// 대입하면 float32 로 접히는 지점이 하나 늘어 값이 갈릴 수 있다.
        ///
        /// 하한이 없는 것도 원본 그대로다. <c>OJMath.Max(1, ·)</c> 를 넣지 마라 —
        /// 호출부가 0 을 "표시하지 않음"으로 쓴다(Monster.PlayPoison 이 <c>appliedDamage &gt; 0</c> 으로
        /// 데미지 텍스트를 되돌린다). 방어력만으로는 0 이 안 된다(<see cref="DefenseMultiplier"/> 가
        /// 항상 0 보다 크고 <c>CeilToInt</c> 가 올림이라 <c>defense=100000</c> 에서도 1 이다).
        /// 0 이 나오는 것은 <paramref name="totalBonusPercent"/> 가 -100 일 때이고, -100 보다 작으면
        /// 음수가 나온다. 6종 필드는 write 시점에 <c>OJMath.Max(0, ·)</c> 로 눌리지만 stateBonus 는
        /// 안 눌리므로 유물 primaryValue 가 음수면 실제로 도달한다.
        ///
        /// <c>dmg &lt;= 0</c> 게이트(Monster.cs:144)는 호출부에 남겼다. 여기서 0 을 돌려주면
        /// <c>activeInHierarchy</c> 게이트의 0 과 구분이 사라진다.
        /// </summary>
        public static int AppliedDamage(int dmg, int defense, int totalBonusPercent)
        {
            float damageMultiplier = DefenseMultiplier(defense);
            float incomingDamageMultiplier = 1f + totalBonusPercent * 0.01f;
            return OJMath.CeilToInt(dmg * damageMultiplier * incomingDamageMultiplier);
        }

        // ── 시간 판정 ─────────────────────────────────────────────────────────────────
        //
        // 두 함수의 부등호가 다른 것이 요점이다. 활성은 '<', 만료는 '>=' 이고 경계값
        // (now == untilTime)에서 둘 다 "꺼짐"으로 떨어진다. 초기값 -1f 는 Time.time 이 0 인
        // 첫 프레임에도 만료로 판정된다 — 그래서 OnSpawn 이 -1f 를 넣는다.
        //
        // 상태 write 는 여기 없다. 필드를 0 으로 미는 것은 호출부(Monster)가 한다.

        /// <summary>Monster.cs:416 / :421 / :426 — <c>Time.time &lt; until</c>.</summary>
        public static bool IsStateActive(float now, float untilTime)
        {
            return now < untilTime;
        }

        /// <summary>Monster.cs:431 / :434 / :440 / :446 / :452 / :458 — <c>Time.time &gt;= until</c>.</summary>
        public static bool IsBonusExpired(float now, float untilTime)
        {
            return now >= untilTime;
        }

        // ── 벽 ────────────────────────────────────────────────────────────────────────
        //
        // 벽은 몬스터와 식이 완전히 다르다. 방어력이 없고, 상태이상이 안 걸리고, 감쇄식도
        // 피해증가도 CeilToInt 도 없다. 공통점은 "int 를 뺀다" 하나뿐이다.
        // <b>Monster 쪽 함수와 묶으려는 시도를 하지 마라.</b>

        /// <summary>Wall.cs:32-34. 하한 0 클램프만 있고 상한은 없다.</summary>
        public static int WallHpAfterDamage(int currentHp, int dmg)
        {
            int hp = currentHp - dmg;
            if (hp < 0)
                hp = 0;

            return hp;
        }

        /// <summary>
        /// Wall.cs:39-41 원문. <b>피해 경로 전용이다.</b>
        ///
        /// 같은 파일 안에 체력바 비율이 세 벌 있고 셋 다 다르다(고치지 말고 기록만 — 5.1-a 조사):
        ///   피해   : <c>TotalHp</c> 0 가드 없음, Clamp01 없음   ← 이 함수
        ///   부활   : <c>TotalHp &gt; 0</c> 가드 + Clamp01        ← WallHpBarRatioClamped
        ///   Heal   : 부활과 글자까지 동일                        ← WallHpBarRatioClamped
        ///
        /// 그래서 <c>TotalHp == 0</c> 이면 여기서 <c>0f/0f = NaN</c> 이 나오고, <c>NaN &lt; 0</c> 은
        /// false 라 가드를 그냥 통과해 <c>sizeDelta.x = NaN</c> 이 된다. <c>SetInit(0)</c> 이 불리면
        /// 실제로 재현된다. <b>가드를 추가하지 마라</b> — 현행 동작이고, 고치는 것은 별도 항목이다.
        ///
        /// <c>if (ratio &lt; 0)</c> 은 바로 위 <c>WallHpAfterDamage</c> 의 0 클램프 때문에 도달 불가한
        /// 죽은 가드다. 그래도 남긴다 — 두 함수가 항상 짝지어 불린다는 보장이 없다.
        /// </summary>
        public static float WallHpBarRatioOnDamage(int currentHp, int totalHp)
        {
            float ratio = (float)currentHp / (float)totalHp;
            if (ratio < 0)
                ratio = 0;

            return ratio;
        }

        /// <summary>
        /// Wall.cs:55-56(부활) 원문. Wall.cs:81-82(Heal) 도 글자까지 같은 식이다.
        /// 위 <see cref="WallHpBarRatioOnDamage"/> 와 <b>합치지 마라</b> — 0 가드와 Clamp01 이 있고 없고가 다르다.
        /// </summary>
        public static float WallHpBarRatioClamped(int currentHp, int totalHp)
        {
            float ratio = totalHp > 0 ? (float)currentHp / totalHp : 0f;
            return OJMath.Clamp01(ratio);
        }
    }
}
