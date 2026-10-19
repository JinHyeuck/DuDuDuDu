using System;

namespace OJ.Core
{
    /// <summary>
    /// 초반 웨이브 완화. 엔진을 쓰지 않는다.
    ///
    /// <b>왜 필요한가.</b> 초반 웨이브에는 보드에 다이스가 한두 개뿐(시작 SP 가 소환 두 번분이다 — <see cref="WaveSpCurve"/>)이라
    /// 몬스터를 다 처리하지 못하고 벽이 깎이는 일이 잦다. 그래서 <b>첫 세 웨이브만</b> 일반 몬스터 수를 줄이고
    /// 나오는 간격을 늘린다. 4웨이브부터는 스테이지 데이터 그대로다.
    ///
    /// 마릿수는 스테이지 값의 비율로 줄이되 <see cref="MinMonsters"/> 아래로는 내리지 않고(스테이지 값이 원래 그보다 작으면 그 값 그대로),
    /// 간격은 비율로 늘린다. <b>처치 목표 수와 스포너가 같은 값을 봐야 한다</b> — 갈리면 웨이브가 안 끝나거나 일찍 끝난다
    /// (<c>GameManager.RegularMonstersFor</c> 한 곳에서 둘이 같이 읽는다). 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class EarlyWaveRelief
    {
        /// <summary>완화가 적용되는 웨이브 수(앞에서부터).</summary>
        public const int ReliefWaves = 3;

        /// <summary>줄인 뒤에도 남기는 최소 마릿수. 스테이지 값이 이보다 작으면 그 값을 쓴다.</summary>
        public const int MinMonsters = 3;

        // [웨이브 - 1] = 퍼센트. 마릿수는 줄이고(<100), 간격은 늘린다(>100).
        private static readonly int[] countPercent = { 50, 65, 80 };
        private static readonly int[] intervalPercent = { 200, 170, 140 };

        /// <summary>이 웨이브에서 일반 몬스터 수를 몇 퍼센트로 하는가. 4웨이브부터 100.</summary>
        public static int CountPercent(int waveIndex)
        {
            return waveIndex >= 1 && waveIndex <= ReliefWaves ? countPercent[waveIndex - 1] : 100;
        }

        /// <summary>이 웨이브에서 몬스터가 나오는 간격을 몇 퍼센트로 하는가(클수록 느리다). 4웨이브부터 100.</summary>
        public static int IntervalPercent(int waveIndex)
        {
            return waveIndex >= 1 && waveIndex <= ReliefWaves ? intervalPercent[waveIndex - 1] : 100;
        }

        /// <summary>
        /// 완화를 적용한 일반 몬스터 수. 스테이지 값이 <see cref="MinMonsters"/> 이하이면 그 값 그대로다(더 늘리지 않는다).
        /// 항상 1 이상이다.
        /// </summary>
        public static int MonstersFor(int stageMonstersPerWave, int waveIndex)
        {
            int baseCount = Math.Max(1, stageMonstersPerWave);
            int percent = CountPercent(waveIndex);
            if (percent >= 100)
                return baseCount;

            int scaled = (int)Math.Round(baseCount * percent / 100.0, MidpointRounding.AwayFromZero);
            return Math.Min(baseCount, Math.Max(MinMonsters, scaled));
        }

        /// <summary>완화를 적용한 간격(초). <paramref name="baseInterval"/> 은 스테이지 콘셉트까지 반영한 값이다.</summary>
        public static float IntervalFor(float baseInterval, int waveIndex)
        {
            return baseInterval * IntervalPercent(waveIndex) / 100f;
        }
    }
}
