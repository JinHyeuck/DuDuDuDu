using System;

namespace OJ.Core
{
    /// <summary>
    /// 다이스 레벨 초기화의 값과 환급. 엔진을 쓰지 않는다.
    ///
    /// 초기화는 그 다이스의 레벨만 1 로 되돌린다. 레벨업에 쓴 재화(골드·스크롤)는 전부 돌려주고,
    /// 값은 <see cref="FreeMaxLevel"/> 레벨까지는 공짜, 그 위부터는 <see cref="GemCost"/> 무료젬이다.
    /// 값은 <b>지금 레벨</b>로 정한다 — 12레벨을 7레벨까지 깎는 부분 초기화는 없다.
    /// </summary>
    public static class DiceLevelResetRules
    {
        /// <summary>이 레벨까지는 초기화가 공짜다.</summary>
        public const int FreeMaxLevel = 6;

        /// <summary>공짜 구간을 넘은 다이스를 초기화할 때 드는 무료젬.</summary>
        public const int GemCost = 100;

        /// <summary>1레벨은 되돌릴 것이 없다.</summary>
        public static bool CanReset(int level)
        {
            return level > 1;
        }

        /// <summary><paramref name="level"/> 레벨 다이스를 초기화하는 데 드는 무료젬. 공짜면 0.</summary>
        public static int GemCostFor(int level)
        {
            return level > FreeMaxLevel ? GemCost : 0;
        }

        /// <summary>
        /// 1레벨에서 <paramref name="level"/> 레벨까지 올리며 쓴 골드·스크롤의 합.
        /// <paramref name="costOfLevelUp"/> 은 "그 레벨에서 다음 레벨로 올리는 값" 이다.
        /// </summary>
        public static (int gold, int scroll) Refund(int level, Func<int, (int goldCost, int scrollCost)> costOfLevelUp)
        {
            long gold = 0;
            long scroll = 0;

            for (int from = 1; from < level; from++)
            {
                var cost = costOfLevelUp(from);
                gold += Math.Max(0, cost.goldCost);
                scroll += Math.Max(0, cost.scrollCost);
            }

            return ((int)Math.Min(gold, int.MaxValue), (int)Math.Min(scroll, int.MaxValue));
        }
    }
}
