namespace OJ.Core
{
    /// <summary>
    /// 통장의 순수 규칙. 엔진을 쓰지 않는다.
    ///
    /// SP 를 맡기면 시간이 지난 뒤 <b>원금보다 더 많이</b> 돌려받는다. 맡길 때 <b>셋 중 하나를 고른다</b> —
    /// 적게 맡고 빨리 받을지, 많이 맡고 오래 기다려 더 크게 받을지다. 고르는 것이 이 시스템의 재미다.
    /// 보유 SP 에 비례해 이자가 붙는 방식으로 하지 않은 이유는 "이자를 아는 유저" 와 모르는 유저의 격차가 벌어져서다.
    ///
    /// 선택지는 <b>맡길수록, 오래 기다릴수록 비율이 좋아진다</b>(원금 대비 +50% → +62% → +80%).
    /// 초당 이익도 0.50 → 0.56 → 0.67 SP 로 오른다 — 큰 선택지가 항상 낫다면 고를 이유가 없으니,
    /// 대신 <b>지금 가진 SP 와 웨이브 압박</b>이 고르는 기준이 된다(큰 것은 SP 를 오래 묶는다).
    /// 한 번에 하나만 맡긴다. 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class SavingsRules
    {
        /// <summary>선택지 수.</summary>
        public const int OptionCount = 3;

        // [선택지] = 맡기는 SP / 돌려받는 SP / 걸리는 시간(초, 게임 시간).
        private static readonly int[] deposits = { 30, 40, 50 };
        private static readonly int[] payouts = { 45, 65, 90 };
        private static readonly float[] durations = { 30f, 45f, 60f };

        /// <summary>범위 밖 번호를 가장 가까운 선택지로 맞춘다.</summary>
        public static int ClampOption(int option)
        {
            if (option < 0)
                return 0;

            return option >= OptionCount ? OptionCount - 1 : option;
        }

        /// <summary>맡기는 금액(SP).</summary>
        public static int Deposit(int option)
        {
            return deposits[ClampOption(option)];
        }

        /// <summary>돌려받는 총액(SP). 원금을 포함한다.</summary>
        public static int Payout(int option)
        {
            return payouts[ClampOption(option)];
        }

        /// <summary>순이익(SP) = 돌려받는 총액 - 원금.</summary>
        public static int Gain(int option)
        {
            return Payout(option) - Deposit(option);
        }

        /// <summary>돌려받기까지 걸리는 시간(초).</summary>
        public static float Duration(int option)
        {
            return durations[ClampOption(option)];
        }

        /// <summary>가장 싼 선택지의 금액. "통장을 쓸 수 있는가" 의 기준이다.</summary>
        public static int CheapestDeposit => deposits[0];
    }
}
