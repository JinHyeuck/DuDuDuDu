namespace OJ.Core
{
    /// <summary>
    /// 테스트 모드(자리 해금·공속 UP)의 순수 산술. 엔진을 쓰지 않는다.
    ///
    /// <b>수치가 여기 있는 이유.</b> 비용·단계 표가 UI 코드에 흩어져 있으면 "200 / 400 / 600" 이
    /// 버튼 문구와 차감 로직에서 따로 늙는다. 한 곳에 두고 양쪽이 같은 함수를 부른다.
    /// 밸런스 값이라 정식 규칙으로 승격할 때는 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class TestModeRules
    {
        // ── 자리 해금 ──────────────────────────────────────────────

        /// <summary>첫 자리의 해금 비용(SP).</summary>
        public const int SlotUnlockBaseCost = 50;

        /// <summary>자리를 하나 열 때마다 다음 비용이 오르는 양(SP).</summary>
        public const int SlotUnlockCostStep = 5;

        /// <summary>
        /// 지금까지 <paramref name="unlockedCount"/> 칸을 샀을 때 <b>다음</b> 칸의 비용.
        /// 어느 칸을 여느냐와 무관하다 — 50, 55, 60 ... 순서로 오른다.
        /// </summary>
        public static int SlotUnlockCost(int unlockedCount)
        {
            if (unlockedCount < 0)
                unlockedCount = 0;

            return SlotUnlockBaseCost + SlotUnlockCostStep * unlockedCount;
        }

        // ── 공속 UP ────────────────────────────────────────────────

        /// <summary>공속을 올릴 수 있는 횟수.</summary>
        public const int FireRateUpgradeCount = 3;

        /// <summary>
        /// <paramref name="level"/> 단계(0 = 처음)에서 다음 단계로 가는 비용(SP). 200 · 400 · 600.
        /// 마지막 단계를 넘으면 -1 — 더 올릴 수 없다.
        /// </summary>
        public static int FireRateUpgradeCost(int level)
        {
            if (level < 0 || level >= FireRateUpgradeCount)
                return -1;

            return 200 * (level + 1);
        }

        /// <summary>
        /// <paramref name="level"/> 단계의 발사 딜레이(초). 0 단계는 호출한 쪽이 가진 원래 값
        /// (<paramref name="baseDelay"/>, 씬의 0.6)이고, 이후 0.4 · 0.2 · 0.1 이다.
        /// </summary>
        public static float FireRateDelay(int level, float baseDelay)
        {
            switch (level)
            {
                case 1: return 0.4f;
                case 2: return 0.2f;
                case 3: return 0.1f;
                default: return baseDelay;
            }
        }
    }
}
