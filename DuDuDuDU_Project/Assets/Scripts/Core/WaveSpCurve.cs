using System;

namespace OJ.Core
{
    /// <summary>
    /// 웨이브별 SP 수급 곡선. 엔진을 쓰지 않는다.
    ///
    /// <b>왜 필요한가.</b> 웨이브 클리어 SP 가 매 웨이브 같고 소환 비용은 10 에서 시작해 두 번 소환마다 +1 씩 오르면
    /// 웨이브당 소환(뽑기) 횟수가 <b>초반이 가장 많고 계속 줄어든다</b>(시뮬레이션 15 · 7 · 5 · 5 · 4 …). 초반에 뽑기에 쫓겨
    /// 보드·확률업·자리·현상금 같은 전략을 생각할 틈이 없다. 그래서 SP 수급을 <b>종 모양</b>으로 바꾼다 —
    /// 초반에는 적게(웨이브당 2~3회), 6~8웨이브에 정점(7회), 후반에 다시 적게(1~2회).
    ///
    /// <b>표는 계산으로 도출했다.</b> 소환 비용 진행을 그대로 두고 목표 횟수 <c>3 3 4 5 6 7 7 7 6 5 4 3 2 2 1</c>(15웨이브)에서
    /// 웨이브별 필요 SP 를 거꾸로 구했다. 시작 SP <see cref="StartSp"/>(= 소환 2회)와 함께 쓴다. 표를 바꾸면 모양이 깨질 수 있으니
    /// 클릭 곡선 테스트(<c>WaveSpCurveTests</c>)가 웨이브별 횟수를 잠근다.
    ///
    /// 웨이브 수가 15가 아닌 스테이지(10 · 20 · 30웨이브)는 표를 상대 위치로 <b>선형 보간해 재표본</b>하고 평균을 같게 맞춘다 —
    /// 모든 스테이지가 같은 모양의 종이다. 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class WaveSpCurve
    {
        /// <summary>이 곡선이 전제하는 시작 SP(소환 2회분). 스테이지 데이터의 <c>initialSP</c> 가 이 값이어야 모양이 맞는다.</summary>
        public const int StartSp = 20;

        /// <summary>15웨이브 기준 웨이브 클리어 SP 배율(퍼센트, 기준값 = 스테이지의 <c>waveClearSP</c>).</summary>
        private static readonly int[] table = { 10, 30, 47, 69, 97, 132, 154, 175, 167, 151, 129, 101, 78, 60, 37 };

        private static readonly int tableSum = Sum(table);

        /// <summary>
        /// <paramref name="waveIndex"/>(1부터) 번째 웨이브의 SP 배율(퍼센트). 100 이면 <c>waveClearSP</c> 그대로다.
        /// 범위 밖 웨이브는 가장 가까운 웨이브로 본다. 웨이브 수가 1 이하이면 100 이다.
        /// </summary>
        public static int Multiplier(int waveIndex, int totalWaves)
        {
            if (totalWaves <= 1)
                return 100;

            if (waveIndex < 1)
                waveIndex = 1;
            if (waveIndex > totalWaves)
                waveIndex = totalWaves;

            if (totalWaves == table.Length)
                return table[waveIndex - 1];

            // 재표본: 각 웨이브의 상대 위치를 표 위의 위치로 옮겨 선형 보간하고, 합이 (표 평균 × 웨이브 수)가 되게 비율을 맞춘다.
            double raw = Sample(waveIndex, totalWaves);
            double rawSum = 0.0;
            for (int w = 1; w <= totalWaves; w++)
                rawSum += Sample(w, totalWaves);

            double wantedSum = (double)tableSum / table.Length * totalWaves;
            return (int)Math.Round(raw * wantedSum / rawSum);
        }

        /// <summary>표 평균(퍼센트). 곡선이 총 SP 를 얼마나 바꾸는지 — 1~10스테이지는 약 96% 라 총량이 4% 줄어든다.</summary>
        public static double MeanPercent => (double)tableSum / table.Length;

        /// <summary>
        /// 웨이브 클리어 SP 에 곡선을 곱한다(반올림). <paramref name="applyCurve"/> 가 거짓이면(튜토리얼) 그대로 돌려준다 —
        /// 튜토리얼 SP 는 "정확히 N회" 로 맞춰져 있어 곡선을 곱하면 단계가 어긋난다.
        /// </summary>
        public static int WaveClearSp(int waveClearSp, int waveIndex, int totalWaves, bool applyCurve)
        {
            if (!applyCurve)
                return waveClearSp;

            long scaled = (long)waveClearSp * Multiplier(waveIndex, totalWaves);
            return (int)((scaled + 50) / 100);
        }

        private static double Sample(int waveIndex, int totalWaves)
        {
            double position = (waveIndex - 0.5) / totalWaves * table.Length - 0.5;
            if (position <= 0.0)
                return table[0];
            if (position >= table.Length - 1)
                return table[table.Length - 1];

            int low = (int)Math.Floor(position);
            double fraction = position - low;
            return table[low] * (1.0 - fraction) + table[low + 1] * fraction;
        }

        private static int Sum(int[] values)
        {
            int sum = 0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];

            return sum;
        }
    }
}
