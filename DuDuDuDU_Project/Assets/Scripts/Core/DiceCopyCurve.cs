using System;

namespace OJ.Core
{
    /// <summary>
    /// 다이스 레벨업에 필요한 <b>카피 장수</b>. 엔진을 쓰지 않는다.
    ///
    /// <b>카피 곡선의 정본이다.</b> 레벨마다 몇 장이 드는지를 공식 대신 표로 둔다 — 구간마다 리듬이
    /// 달라서(초반 1·2·4·5, 중반 7, 후반 12) 일차식 하나로 안 떨어지고, 장부(가치 3,500원 / 700원)와
    /// 곱해 레벨당 값어치를 바로 읽을 수 있어야 하기 때문이다.
    ///
    /// <b>카피 한 장의 값어치는 이 표에 없다.</b> 레어·킹 3,500원, 기본 700원이라는 값은 보상표와
    /// 판매가에서 쓰고, 여기서는 장수만 안다. 레어·킹과 기본이 같은 장수를 쓰는 것이 설계다
    /// (기본 다이스 성능이 레어·킹의 20% 라 값어치만 1/5).
    ///
    /// <list type="table">
    /// <item><description>Lv2~5: 1, 2, 4, 5 (누적 12)</description></item>
    /// <item><description>Lv6~10: 7씩 (누적 47)</description></item>
    /// <item><description>Lv11~15: 8씩 (누적 87)</description></item>
    /// <item><description>Lv16~30: 12씩 (누적 267)</description></item>
    /// <item><description>Lv31 이상: 15씩, 10레벨 구간마다 +3 (아직 상한 밖 — 확장용 규칙)</description></item>
    /// </list>
    /// </summary>
    public static class DiceCopyCurve
    {
        /// <summary>Lv2~5 를 올리는 장수. 인덱스 0 이 Lv2.</summary>
        private static readonly int[] EarlySteps = { 1, 2, 4, 5 };

        /// <summary>Lv31 이상의 시작 장수와 10레벨마다 늘어나는 폭.</summary>
        private const int BeyondStart = 15;
        private const int BeyondStep = 3;

        /// <summary>
        /// <paramref name="targetLevel"/> 레벨이 되려고 쓰는 장수 — 즉 "target-1 에서 target 으로" 올리는 값.
        /// 2 미만은 올릴 것이 없어 0 이다.
        /// </summary>
        public static int CopiesToReach(int targetLevel)
        {
            if (targetLevel < 2)
                return 0;

            if (targetLevel <= 5)
                return EarlySteps[targetLevel - 2];

            if (targetLevel <= 10)
                return 7;

            if (targetLevel <= 15)
                return 8;

            if (targetLevel <= 30)
                return 12;

            // 31~40 은 15, 41~50 은 18 … 10레벨 구간마다 +3.
            int bands = (targetLevel - 31) / 10;
            return BeyondStart + bands * BeyondStep;
        }

        /// <summary>
        /// 지금 <paramref name="currentLevel"/> 레벨인 다이스를 한 칸 올리는 데 드는 장수.
        /// <c>GetUpgradeCost</c> 가 부르는 쪽이다.
        /// </summary>
        public static int CopiesToLevelUp(int currentLevel)
        {
            return CopiesToReach(Math.Max(1, currentLevel) + 1);
        }

        /// <summary>1 레벨에서 <paramref name="level"/> 레벨까지 올리는 데 든 누적 장수.</summary>
        public static int CumulativeCopies(int level)
        {
            long total = 0;
            for (int target = 2; target <= level; target++)
                total += CopiesToReach(target);

            return (int)Math.Min(total, int.MaxValue);
        }
    }
}
