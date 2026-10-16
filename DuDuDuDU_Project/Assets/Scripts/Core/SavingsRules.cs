namespace OJ.Core
{
    /// <summary>
    /// 통장의 순수 규칙. 엔진을 쓰지 않는다.
    ///
    /// SP 를 맡기고 <see cref="DurationSeconds"/> 초가 지나면 <b>원금 + 10%</b> 를 돌려받는다(30 → 33).
    /// 이자가 아니라 <b>통장</b>으로 만든 이유 — 보유 SP 에 비례해 이자가 붙으면 "이자 시스템을 아는 유저"와
    /// 모르는 유저의 격차가 너무 벌어진다. 맡기는 금액을 정해 두고(30 → 40 → 50) 누르는 인터랙션으로 바꾸면
    /// 누구나 같은 값을 얻는다.
    ///
    /// 맡기는 금액은 <b>몇 번째 맡김이냐</b>로 정해지고 <see cref="MaxDeposit"/> 에서 멈춘다(이후 계속 50).
    /// 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class SavingsRules
    {
        /// <summary>첫 맡김 금액(SP).</summary>
        public const int FirstDeposit = 30;

        /// <summary>맡길 때마다 다음 금액이 오르는 양(SP).</summary>
        public const int DepositStep = 10;

        /// <summary>맡기는 금액의 상한(SP). 이 금액에 닿은 뒤에는 계속 이 금액이다.</summary>
        public const int MaxDeposit = 50;

        /// <summary>맡긴 뒤 돌려받기까지 걸리는 시간(초). 게임 시간이다.</summary>
        public const float DurationSeconds = 45f;

        /// <summary>원금에 더해 주는 비율(퍼센트).</summary>
        public const int BonusPercent = 10;

        /// <summary>
        /// 지금까지 <paramref name="completedCount"/> 번 맡겼을 때 <b>다음</b> 맡김 금액.
        /// 30 · 40 · 50 · 50 · 50 …
        /// </summary>
        public static int DepositFor(int completedCount)
        {
            if (completedCount < 0)
                completedCount = 0;

            // 곱셈이 아니라 비교로 막는다 — 횟수가 아주 커져도 넘치지 않는다.
            int steps = (MaxDeposit - FirstDeposit) / DepositStep;
            if (completedCount >= steps)
                return MaxDeposit;

            return FirstDeposit + DepositStep * completedCount;
        }

        /// <summary>
        /// 돌려받는 총액 = 원금 + 10%. 정수 SP 라 내림한다(30 → 33, 40 → 44, 50 → 55).
        /// </summary>
        public static int PayoutFor(int deposit)
        {
            if (deposit <= 0)
                return 0;

            return deposit + deposit * BonusPercent / 100;
        }
    }
}
