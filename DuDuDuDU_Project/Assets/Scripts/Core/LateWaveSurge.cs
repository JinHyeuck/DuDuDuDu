using System;

namespace OJ.Core
{
    /// <summary>
    /// 후반 웨이브 체력 가산. 엔진을 쓰지 않는다. <see cref="EarlyWaveRelief"/> 의 반대편이다.
    ///
    /// <b>왜 필요한가.</b> 초반·중반은 유저가 다이스를 모으는 빌드업 구간이라 지금 체력 곡선이 맞다. 그런데
    /// 소환이 정점을 찍는 6~8웨이브(<see cref="WaveSpCurve"/>)를 지나면 보드가 이미 완성돼 있어서 후반이 쉽다.
    /// 스테이지 성장식(<see cref="StageGrowthFormula"/>)의 계수를 올리면 중반까지 같이 오르므로, <b>시작 웨이브
    /// 전까지는 정확히 100%</b> 이고 그 뒤부터만 오르는 배율을 따로 곱한다.
    ///
    /// 시작 웨이브는 전체 웨이브의 <see cref="StartFraction"/> 지점(15웨이브면 8웨이브 — SP 정점의 끝)이고,
    /// 마지막 웨이브에서 <see cref="FinalBonusPercent"/> 만큼 더해진다. 그 사이는 직선이다(15웨이브: 9웨이브 +11% … 15웨이브 +75%).
    /// 다른 웨이브 수(10 · 20 · 30)도 같은 모양이다.
    ///
    /// <b>값은 봇 실측으로 정했다</b>(<c>Docs/StageBalance.md</c> "접촉 피해 재도입"). 벽 접촉 지속 피해(<c>WallLeakFormula</c>, 초당 1)와
    /// 같이 쓰는 값이다 — 접촉 피해는 놓친 마리가 쌓이면 피해가 늘어나는 구조라, +100% 는 1~10스테이지 중 4판(3·6·8·10)이 패배했고
    /// +75% 는 8스테이지 하나만 패배했다. 한 번씩만 쟀다(±40% 흔들림).
    /// 탑(1웨이브)은 시작 웨이브가 곧 마지막이라 걸리지 않는다. 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class LateWaveSurge
    {
        /// <summary>가산이 0 인 마지막 웨이브의 위치(전체 웨이브 대비). 15웨이브 → 8웨이브.</summary>
        public const double StartFraction = 8.0 / 15.0;

        /// <summary>마지막 웨이브에서 더해지는 체력(퍼센트).</summary>
        public const int FinalBonusPercent = 75;

        /// <summary>가산이 시작되기 직전 웨이브. 이 웨이브까지는 100% 다.</summary>
        public static int StartWave(int totalWaves)
        {
            return Math.Max(1, (int)Math.Round(totalWaves * StartFraction, MidpointRounding.AwayFromZero));
        }

        /// <summary>이 웨이브의 체력 배율(퍼센트). 시작 웨이브까지 100, 마지막 웨이브에서 100 + <see cref="FinalBonusPercent"/>.</summary>
        public static int HpPercent(int waveIndex, int totalWaves)
        {
            int start = StartWave(totalWaves);
            if (totalWaves <= start || waveIndex <= start)
                return 100;

            double progress = Math.Min(1.0, (double)(waveIndex - start) / (totalWaves - start));
            return 100 + (int)Math.Round(FinalBonusPercent * progress, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 체력에 배율을 곱한다(반올림, 최소 1). 100% 인 웨이브는 입력을 그대로 돌려준다 —
        /// 가산 전 구간의 체력이 한 자리도 안 바뀐다는 것을 이 분기가 보장한다.
        /// </summary>
        public static int ApplyHp(int hp, int waveIndex, int totalWaves)
        {
            int percent = HpPercent(waveIndex, totalWaves);
            if (percent == 100)
                return hp;

            long scaled = (long)hp * percent;
            return (int)Math.Max(1L, (scaled + 50) / 100);
        }
    }
}
