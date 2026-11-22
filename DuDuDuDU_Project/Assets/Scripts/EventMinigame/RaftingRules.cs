using System;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 래프팅 이벤트 규칙. 보트로 룰렛을 돌려 거리를 쌓는다. 원본 룰렛 칸 구성을 그대로 쓴다.
    ///
    /// 칸: 1m 2% / 2m 20% / 3m 12% / 4m 12% / 5m 22% / <b>무료 5m 10%</b> / 8m 12% / 20m 10%.
    /// 무료 칸은 5m 를 주고 <b>보트를 쓰지 않고 같은 배율로 한 번 더</b> 돈다.
    /// 보트 1개당 평균 거리는 (532 + 10 × 5) / 90 = 97/15 ≈ 6.47m 다
    /// (원본 문서의 6.29m 는 같은 확률표로 재현되지 않아 쓰지 않는다).
    /// </summary>
    public static class RaftingRules
    {
        public const int CellCount = 8;
        public const int FreeCell = 5;

        /// <summary>무료 칸이 이어져도 이 횟수에서 끊는다(확률 1e-50 이지만 무한 루프를 막는다).</summary>
        public const int MaxChain = 50;

        public static readonly int[] Multipliers = { 1, 10, 100 };

        /// <summary>칸별 거리(m). 5번 칸이 무료 5m.</summary>
        public static readonly int[] CellDistance = { 1, 2, 3, 4, 5, 5, 8, 20 };

        /// <summary>칸별 가중치. 합 100.</summary>
        public static readonly int[] CellWeight = { 2, 20, 12, 12, 22, 10, 12, 10 };

        /// <summary>보트 1개당 평균 거리 = 분자 / 분모 (m).</summary>
        public const int MetersPerBoatNumerator = 97;

        public const int MetersPerBoatDenominator = 15;

        public static bool IsFreeCell(int cell)
        {
            return cell == FreeCell;
        }

        /// <summary>칸을 뽑는다. <paramref name="roll"/> 은 0~1, 1.0 은 마지막 칸으로 가둔다.</summary>
        public static int Spin(float roll)
        {
            int total = 0;
            for (int i = 0; i < CellWeight.Length; i++)
                total += CellWeight[i];

            int target = (int)(roll * total);
            if (target < 0)
                target = 0;
            if (target >= total)
                target = total - 1;

            int accumulated = 0;
            for (int i = 0; i < CellWeight.Length; i++)
            {
                accumulated += CellWeight[i];
                if (target < accumulated)
                    return i;
            }

            return CellWeight.Length - 1;
        }

        /// <summary>
        /// 보트 <paramref name="multiplier"/> 개를 쓴 한 번의 돌리기. 무료 칸이 나오면 보트 없이 같은 배율로 이어 돈다.
        /// 거리는 칸 거리 × 배율의 합이다.
        /// </summary>
        /// <param name="spins">실제로 돈 횟수(무료 연쇄 포함). 연출용.</param>
        public static long SpinChain(int multiplier, Func<float> roll, out int spins)
        {
            return SpinChain(multiplier, roll, null, out spins);
        }

        /// <summary>
        /// <see cref="SpinChain(int, Func{float}, out int)"/> 에 더해 돈 칸 번호를 차례로 <paramref name="cells"/> 에 적는다
        /// (룰렛 연출이 무료 칸 연쇄를 그대로 보여 주려는 것). <paramref name="cells"/> 가 null 이면 적지 않는다.
        /// </summary>
        public static long SpinChain(int multiplier, Func<float> roll, System.Collections.Generic.List<int> cells, out int spins)
        {
            spins = 0;
            if (multiplier <= 0 || roll == null)
                return 0;

            long distance = 0;
            for (int i = 0; i < MaxChain; i++)
            {
                int cell = Spin(roll());
                spins++;
                distance += (long)CellDistance[cell] * multiplier;

                if (cells != null)
                    cells.Add(cell);

                if (!IsFreeCell(cell))
                    break;
            }

            return distance;
        }

        /// <summary>마지막 마일스톤을 넘은 거리(m)가 핀볼 1발이 되는 단위: 보트 3개 상당 = 291/15 m.</summary>
        public static long OverflowShots(long excessMeters)
        {
            if (excessMeters <= 0)
                return 0;

            // 보트 1개 = 97/15 m, 재화 3개 = 핀볼 1발 → 1발 = 291/15 m → 발수 = 초과 × 15 / 291 = 초과 × 5 / 97.
            return excessMeters * 5 / MetersPerBoatNumerator;
        }
    }
}
