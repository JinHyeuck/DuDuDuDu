namespace OJ.IdleReward
{
    /// <summary>
    /// 고기 축제의 표시 판정.
    ///
    /// <b>왜 static 인가.</b> 헤드리스 러너에서는 싱글톤이 돌지 않는다
    /// (<c>AssemblyPilotTests</c> 주석). 특히 <see cref="IsFull"/> 은 틀려도 화면이 조용해서
    /// 눈으로는 안 잡힌다 — 가득 찬 줄 모르고 이틀을 두면 그동안 구워진 고기가 전부 버려진다.
    /// </summary>
    public static class MeatFestivalRules
    {
        /// <summary>받을 수 있는 세트가 있는가. 레드닷과 버튼 활성이 이걸 본다.</summary>
        public static bool CanClaim(int storedSetCount)
        {
            return storedSetCount > 0;
        }

        /// <summary>
        /// 저장이 가득 찼는가. <b>가득 차면 그 뒤로 구워지는 고기는 버려진다.</b>
        ///
        /// 2시간에 한 세트이므로 60세트는 5일이다 — 주말을 한 번 건너뛰면 닿는 거리라
        /// 화면이 반드시 알려야 한다.
        /// </summary>
        public static bool IsFull(int storedSetCount, int maxSetCount)
        {
            return maxSetCount > 0 && storedSetCount >= maxSetCount;
        }

        /// <summary>지금 받으면 들어올 고기 수.</summary>
        public static int TotalMeat(int storedSetCount, int meatPerSet)
        {
            if (storedSetCount <= 0 || meatPerSet <= 0)
                return 0;

            return storedSetCount * meatPerSet;
        }

        /// <summary>
        /// 그 고기로 돌 수 있는 소탕 횟수. 버튼에 "소탕 N회분"으로 적어 주면
        /// 유저가 숫자를 환산하지 않아도 된다 — 고기 1,800 보다 소탕 360회가 바로 읽힌다.
        /// </summary>
        public static int SweepCount(int totalMeat, int staminaCostPerSweep)
        {
            if (totalMeat <= 0 || staminaCostPerSweep <= 0)
                return 0;

            return totalMeat / staminaCostPerSweep;
        }
    }
}
