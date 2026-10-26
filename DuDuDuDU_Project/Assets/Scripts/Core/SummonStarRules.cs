namespace OJ.Core
{
    /// <summary>
    /// 확률업(소환 성급 확률)의 순수 규칙. 엔진을 쓰지 않는다.
    ///
    /// SP 소환으로 나오는 다이스의 성급을 레벨이 정한다. 시작은 Lv1 = 1성 100% 이고,
    /// SP 를 내고 올릴수록 2성이, Lv4 부터는 3성이 섞여 나온다. <b>레벨은 10단계</b>이고 최대는 Lv10(1성 40% · 2성 35% · 3성 25%).
    /// 5단계였을 때 최대 강화에 너무 빨리 닿아 10단계로 나눴다 — 비용은 레벨당 +30 이다(30 → 270, 합계 1350 — 5 씩 올리던 때는 450 이라 너무 싸서 올렸다).
    ///
    /// <b>표를 한 곳에 둔다.</b> 화면의 확률 띠와 실제 굴림이 같은 표를 읽어야 "표기와 다르다" 는 사고가
    /// 안 난다. 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class SummonStarRules
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 10;

        /// <summary>Lv1 → Lv2 로 올리는 비용(SP).</summary>
        public const int BaseUpgradeCost = 30;

        /// <summary>새 밸런스(<see cref="BalanceProfile"/>)의 확률업 첫 값과 간격. 클릭 수가 줄어든 경제에 맞췄다.</summary>
        public const int BaseUpgradeCostV2 = 20;

        /// <summary>한 레벨 올릴 때마다 다음 비용이 오르는 양(SP).</summary>
        public const int UpgradeCostStep = 30;
        public const int UpgradeCostStepV2 = 15;

        /// <summary>소환될 수 있는 가장 높은 성급.</summary>
        public const int MaxStar = 3;

        // [레벨 - 1][성급 - 1] = 퍼센트. 각 줄의 합은 100 이고, 레벨이 오를수록 1성은 줄고 3성은 늘기만 한다
        // (둘 다 테스트가 지킨다). Lv10 은 예전 5단계의 최대 확률과 같다.
        private static readonly int[][] chance =
        {
            new[] { 100, 0, 0 },    // Lv1
            new[] { 85, 15, 0 },    // Lv2
            new[] { 72, 28, 0 },    // Lv3
            new[] { 64, 30, 6 },    // Lv4  — 3성이 열린다
            new[] { 58, 31, 11 },   // Lv5
            new[] { 53, 32, 15 },   // Lv6
            new[] { 49, 33, 18 },   // Lv7
            new[] { 45, 34, 21 },   // Lv8
            new[] { 42, 34, 24 },   // Lv9
            new[] { 40, 35, 25 },   // Lv10
        };

        public static int ClampLevel(int level)
        {
            if (level < MinLevel)
                return MinLevel;

            return level > MaxLevel ? MaxLevel : level;
        }

        /// <summary>
        /// <paramref name="level"/> 에서 한 레벨 올리는 비용. 최대 레벨이면 -1(더 못 올린다).
        /// 30 · 60 · 90 · 120 · 150 · 180 · 210 · 240 · 270.
        /// </summary>
        public static int UpgradeCost(int level)
        {
            level = ClampLevel(level);
            if (level >= MaxLevel)
                return -1;

            return BalanceProfile.Active
                ? BaseUpgradeCostV2 + UpgradeCostStepV2 * (level - MinLevel)
                : BaseUpgradeCost + UpgradeCostStep * (level - MinLevel);
        }

        /// <summary><paramref name="level"/> 에서 <paramref name="star"/> 성이 나올 확률(퍼센트).</summary>
        public static int Chance(int level, int star)
        {
            if (star < 1 || star > MaxStar)
                return 0;

            return chance[ClampLevel(level) - 1][star - 1];
        }

        /// <summary>
        /// 성급을 굴린다. <paramref name="roll"/> 은 0 이상 100 미만의 정수 — 난수는 부르는 쪽이 준다
        /// (엔진 난수를 여기서 부르면 테스트에서 분포를 못 잰다).
        /// </summary>
        public static int RollStar(int level, int roll)
        {
            int[] row = chance[ClampLevel(level) - 1];

            int cumulative = 0;
            for (int star = 1; star <= MaxStar; star++)
            {
                cumulative += row[star - 1];
                if (roll < cumulative)
                    return star;
            }

            return MaxStar;
        }
    }
}
