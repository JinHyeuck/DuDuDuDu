namespace OJ.Core
{
    /// <summary>
    /// 몬스터가 벽까지 <b>통과했을 때</b> 벽이 받는 피해. (2026-10-14)
    ///
    /// <b>2026-10-20 에 되돌렸다(사용자 지시).</b> 일반·보스·현상금 몬스터 모두 다시 <b>벽 앞에 남아
    /// <see cref="ContactDamagePerSecond"/> 씩 매초</b> 때린다(<c>Monster.AttackWall</c>). 아래 설명은
    /// 한 대-사라짐을 택했던 이유이고, 그때의 우려(벽 앞에 쌓이면 피해가 늘어나는 양의 피드백)는
    /// 이 구조에서 다시 성립한다. <see cref="LeakDamage"/> 와 <c>BountyFormula.WallDamage</c> 는 지금 게임이 읽지 않는다.
    ///
    /// <b>왜 생겼는가 — 난이도가 계단이 아니라 절벽이었다.</b>
    ///
    /// 예전에는 벽에 닿은 몬스터가 <b>사라지지 않고 0.5초마다 계속 때렸다</b>(죽을 때까지).
    /// 그래서 "잡는 속도 &lt; 나오는 속도" 가 되는 순간 벽 앞에 몬스터가 쌓이고, 쌓인 수만큼
    /// 초당 피해가 늘어 <b>양의 피드백</b>이 걸렸다. 실측(<c>BalanceAutoPlay</c>)에서
    /// 그 결과가 이렇게 나왔다:
    ///
    /// <code>
    ///   압력(요구 화력/보유 화력)  0.78 → 벽 피해 0
    ///                              0.87 → 24
    ///                              0.95 → 85
    ///                              1.00 → 231
    ///                              1.10 → 벽 파괴
    /// </code>
    ///
    /// 즉 <b>화력 15% 차이가 "무피해"와 "전멸"을 갈랐다.</b> 그 구조에서는
    /// "클리어는 되지만 퍼펙트는 안 되는" 난이도를 <b>만들 수가 없다</b> — 등급이 세 개인
    /// 게임인데 실제로 나오는 값은 100% 아니면 0% 였다. 수치를 아무리 맞춰도
    /// 그 창이 너무 좁아서, 스테이지마다 "무피해" 와 "패배" 사이를 오갔다.
    ///
    /// 그래서 <b>통과한 몬스터는 한 대 때리고 사라진다</b>로 바꿨다. 벽 피해가 통과한
    /// <b>마리 수에 비례</b>하므로 난이도 응답이 선형이 되고, 그제서야
    /// "몇 마리까지 허용인가" 를 스테이지가 정할 수 있다 — 그 다이얼이
    /// <c>StageData.wallHp</c> 다(피해 ÷ 벽 체력 = 허용 마리 수).
    ///
    /// <b>이미 있던 규칙을 일반화한 것이다.</b> 현상금 몬스터는 처음부터 이렇게 돌았고
    /// (<c>Monster.HitWallOnceAndLeave</c>), 그 주석이 이유를 이미 적어 두고 있었다 —
    /// "판이 여기서 끝나면 안 되고, 대가는 클리어 등급으로 치른다". 그 판단을 일반
    /// 몬스터에도 적용한 것뿐이다. 현상금은 자기 식(<see cref="BountyFormula.WallDamage"/>,
    /// 남은 체력의 1/3)을 그대로 쓴다.
    ///
    /// <b>딸린 이득 하나.</b> 통과한 몬스터가 사라지므로 <b>웨이브가 반드시 끝난다.</b>
    /// 예전에는 벽 앞의 몬스터를 못 잡으면 웨이브가 영원히 안 끝났고, 벽이 0 이 된 뒤에도
    /// 사망 되돌리기 팝업을 안 누르면 그대로 진행됐다(측정 중에 실제로 그 상태가 나왔다).
    /// </summary>
    public static class WallLeakFormula
    {
        /// <summary>벽에 붙은 일반·보스 몬스터 한 마리가 1초마다 벽에 주는 피해.</summary>
        public const int ContactDamagePerSecond = 1;

        /// <summary>
        /// 프리팹 <c>attackDamage</c> 1점당 벽 피해.
        ///
        /// <b>프리팹 값을 그대로 쓰지 않고 곱하는 이유.</b> 몬스터별 차이(박쥐 1, 고블린·골렘 2)는
        /// 이미 프리팹에 들어 있고 그 상대값은 쓸 만하다. 다만 원래 그 값은 <b>0.5초마다</b>
        /// 들어가던 틱 피해라 한 방 피해로는 너무 작다 — 벽 500 에 1 이면 500마리를 흘려도
        /// 안 지므로 등급이 움직이지 않는다. 그래서 상대값은 보존하고 크기만 올린다.
        /// </summary>
        public const int DamagePerAttackPoint = 10;

        /// <summary>
        /// 보스가 통과했을 때는 일반 몬스터 <b>세 마리</b>분이다(30).
        ///
        /// <b>보스는 프리팹 공격력을 보지 않는다.</b> 보스 프리팹이 테마마다 달라
        /// (늑대 1 / 골렘 2) 그대로 쓰면 <b>테마가 보스 통과 피해를 2배로 가른다</b> —
        /// 설계에서 뜻이 없는 차이다. 보스의 무게는 체력·크기로 이미 표현돼 있으므로
        /// 여기서는 모든 테마가 같은 값을 낸다.
        ///
        /// 3 인 이유는 가장 얇은 벽이 정예 스테이지의 300 이라는 것이다 — 보스 한 마리가
        /// 그 벽의 10% 다. 5(=50)였을 때는 17% 였고, 그러면 <b>보스를 한 번 놓친 것</b>이
        /// 일반 몬스터 다섯 마리를 놓친 것과 같아져서 등급이 보스 한 마리에 끌려다녔다.
        /// </summary>
        public const int BossMultiplier = 3;

        /// <summary>
        /// 한 마리가 통과했을 때의 벽 피해.
        ///
        /// 하한이 <c>attackDamage</c> 쪽에 있다 — 프리팹에 0 이 들어와도 "통과했는데
        /// 아무 일도 없음" 이 되지 않게 한다. 통과는 언제나 대가가 있어야 한다.
        /// </summary>
        public static int LeakDamage(int attackDamage, bool isStageBoss)
        {
            if (isStageBoss)
                return DamagePerAttackPoint * BossMultiplier;

            return OJMath.Max(1, attackDamage) * DamagePerAttackPoint;
        }

        /// <summary>
        /// 이 벽 체력이 통과를 몇 마리까지 버티는가. <b>수치를 읽는 쪽을 위한 역산</b>이고
        /// 게임 로직은 쓰지 않는다 — 스테이지를 설계할 때 <c>wallHp</c> 를 "허용 마리 수"로
        /// 환산해 보려고 둔다.
        /// </summary>
        public static int LeakBudget(int wallHp, int attackDamage, bool isStageBoss)
        {
            int perLeak = LeakDamage(attackDamage, isStageBoss);
            return OJMath.Max(0, wallHp) / perLeak;
        }
    }
}
