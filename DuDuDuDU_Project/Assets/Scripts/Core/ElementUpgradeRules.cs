using System;

namespace OJ.Core
{
    /// <summary>
    /// 속성강화의 가격과 효과. 엔진을 쓰지 않는다.
    ///
    /// 강화 버튼 한 번이 <b>모든 속성</b>을 한 단계씩 올린다. 한 단계는 그 속성 다이스의 위력
    /// <see cref="BonusPercentPerUpgrade"/>% 이고, 가격은 <see cref="FirstCost"/> 에서 시작해
    /// 올릴 때마다 <see cref="CostStep"/> 씩 오른다. 몇 번째 강화인지는 속성 레벨에서 곧바로 나오므로
    /// (전 속성이 같은 레벨이다) 되돌리기가 레벨을 복원해도 가격이 따로 어긋나지 않는다.
    /// </summary>
    public static class ElementUpgradeRules
    {
        /// <summary>첫 강화의 마석 값.</summary>
        public const int FirstCost = 25;

        /// <summary>강화를 한 번 할 때마다 오르는 마석 값.</summary>
        public const int CostStep = 5;

        /// <summary>강화 한 번이 올려 주는 위력(%).</summary>
        public const int BonusPercentPerUpgrade = 10;

        /// <summary>이미 <paramref name="upgradeCount"/> 번 강화했을 때 다음 강화의 마석 값.</summary>
        public static int NextCost(int upgradeCount)
        {
            return FirstCost + CostStep * Math.Max(0, upgradeCount);
        }

        /// <summary><paramref name="upgradeCount"/> 번 강화해서 쌓인 위력(%).</summary>
        public static int TotalBonusPercent(int upgradeCount)
        {
            return BonusPercentPerUpgrade * Math.Max(0, upgradeCount);
        }
    }
}
