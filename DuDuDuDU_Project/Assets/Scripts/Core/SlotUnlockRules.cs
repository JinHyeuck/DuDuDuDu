namespace OJ.Core
{
    /// <summary>
    /// 자리구매의 순수 산술. 엔진을 쓰지 않는다.
    ///
    /// 첫 줄만 열려 있고 나머지 칸은 SP 로 연다. 비용은 <b>어느 칸이냐가 아니라 몇 번째 구매냐</b>로
    /// 정해져 50, 55, 60 ... 순으로 오른다. 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class SlotUnlockRules
    {
        /// <summary>첫 자리의 해금 비용(SP).</summary>
        public const int BaseCost = 50;

        /// <summary>새 밸런스(<see cref="BalanceProfile"/>)의 첫 자리 값. 소환 클릭 수가 줄어든 경제에 맞춰 낮췄다.</summary>
        public const int BaseCostV2 = 30;

        /// <summary>자리를 하나 열 때마다 다음 비용이 오르는 양(SP).</summary>
        public const int CostStep = 5;

        /// <summary>
        /// 지금까지 <paramref name="unlockedCount"/> 칸을 샀을 때 <b>다음</b> 칸의 비용.
        /// </summary>
        public static int Cost(int unlockedCount)
        {
            if (unlockedCount < 0)
                unlockedCount = 0;

            return (BalanceProfile.Active ? BaseCostV2 : BaseCost) + CostStep * unlockedCount;
        }
    }
}
