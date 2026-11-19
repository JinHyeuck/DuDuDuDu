using System;
using System.Collections.Generic;

namespace OJ.Point
{
    /// <summary>
    /// "랜덤 카피" 지급용 재화를 구체적인 다이스 카피로 푸는 규칙. 엔진을 쓰지 않는다.
    ///
    /// <b>왜 있나.</b> 카피는 다이스마다 따로인데, 보상표는 "레어 카피 3장" 처럼 등급만 말하고 싶은
    /// 자리가 많다(스테이지·탑·시즌 패스·핀볼). 그래서 <see cref="PointType.RandomRareCopy"/> 같은
    /// 지급용 표지를 두고, <b>지급 순간</b>에 장수마다 후보 중 하나를 무작위로 골라 구체적인 카피로
    /// 바꾼다. 보관은 구체적인 카피로만 된다.
    ///
    /// <b>규칙을 인자로 받는 static 으로 둔 이유.</b> 헤드리스 러너에서는 매니저·난수 엔진이 안 돈다 —
    /// 난수는 <c>pick</c> 으로 받아 테스트가 고정값을 넣을 수 있게 한다.
    /// </summary>
    public static class DiceCopyRewards
    {
        private static readonly PointType[] Basic =
        {
            PointType.NormalScroll, PointType.FireScroll, PointType.IceScroll,
            PointType.PoisonScroll, PointType.ThunderScroll,
        };

        private static readonly PointType[] Rare =
        {
            PointType.TornadoCopy, PointType.StunCopy, PointType.ArmorBreakCopy,
            PointType.WindCopy, PointType.TimeCopy,
        };

        private static readonly PointType[] King =
        {
            PointType.KingNormalCopy, PointType.KingFireCopy, PointType.KingIceCopy,
            PointType.KingPoisonCopy, PointType.KingThunderCopy,
        };

        private static readonly PointType[] RareKing = Concat(Rare, King);

        /// <summary>지급용 표지인가. true 면 <see cref="PointManager"/> 가 보관하지 않고 풀어서 넣는다.</summary>
        public static bool IsRandom(PointType pointType)
        {
            return pointType == PointType.RandomBasicCopy
                   || pointType == PointType.RandomRareCopy
                   || pointType == PointType.RandomKingCopy
                   || pointType == PointType.RandomRareKingCopy;
        }

        /// <summary>그 표지가 풀릴 수 있는 구체적인 카피들. 표지가 아니면 빈 배열.</summary>
        public static IReadOnlyList<PointType> Candidates(PointType pointType)
        {
            switch (pointType)
            {
                case PointType.RandomBasicCopy: return Basic;
                case PointType.RandomRareCopy: return Rare;
                case PointType.RandomKingCopy: return King;
                case PointType.RandomRareKingCopy: return RareKing;
                default: return Array.Empty<PointType>();
            }
        }

        /// <summary>
        /// <paramref name="amount"/> 장을 후보에 한 장씩 나눠 구체적인 카피별 장수로 돌려준다.
        /// 합계는 항상 <paramref name="amount"/> 와 같다. 표지가 아니거나 장수가 0 이하면 빈 사전이다.
        /// </summary>
        /// <param name="pick">0 이상 n 미만의 정수를 돌려주는 난수. 실제로는 <c>Random.Range(0, n)</c>.</param>
        public static Dictionary<PointType, int> Distribute(PointType pointType, int amount, Func<int, int> pick)
        {
            var result = new Dictionary<PointType, int>();
            IReadOnlyList<PointType> candidates = Candidates(pointType);
            if (candidates.Count == 0 || amount <= 0 || pick == null)
                return result;

            for (int i = 0; i < amount; i++)
            {
                int index = pick(candidates.Count);
                if (index < 0)
                    index = 0;
                else if (index >= candidates.Count)
                    index = candidates.Count - 1;

                PointType concrete = candidates[index];
                result[concrete] = (result.TryGetValue(concrete, out int held) ? held : 0) + 1;
            }

            return result;
        }

        private static PointType[] Concat(PointType[] first, PointType[] second)
        {
            var all = new PointType[first.Length + second.Length];
            Array.Copy(first, 0, all, 0, first.Length);
            Array.Copy(second, 0, all, first.Length, second.Length);
            return all;
        }
    }
}
