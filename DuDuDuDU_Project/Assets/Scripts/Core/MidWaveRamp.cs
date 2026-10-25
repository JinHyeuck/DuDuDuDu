using System;

namespace OJ.Core
{
    /// <summary>
    /// 중반 체력 가파르게(2026-10-25, 새 밸런스 시험). 엔진을 쓰지 않는다. <see cref="LateWaveSurge"/> 가 마지막 웨이브를 향해 서서히
    /// 올리는 것과 달리, 이것은 <b>초반을 건드리지 않고 중반 구간에서 배수를 확 올려 그대로 유지</b>한다.
    ///
    /// <c>startWave</c> 까지는 100%, <c>endWave</c> 에서 <c>multiplier</c> 배가 되고 그 사이는 직선, 이후는 계속 <c>multiplier</c> 배다.
    /// 목적은 1성 다이스가 중반부터 딜이 의미 없어지게 하는 것이다 — 잘 키운 성급·레어·킹이 있어야 몬스터가 녹는다.
    /// 값은 스테이지가 든다(<c>StageData.hpRamp*</c>). 배수가 1 이하면 아무 일도 안 한다.
    /// </summary>
    public static class MidWaveRamp
    {
        public static int Percent(int waveIndex, int startWave, int endWave, float multiplier)
        {
            if (multiplier <= 1f || waveIndex <= startWave)
                return 100;

            if (endWave <= startWave || waveIndex >= endWave)
                return (int)Math.Round(100.0 * multiplier, MidpointRounding.AwayFromZero);

            double progress = (double)(waveIndex - startWave) / (endWave - startWave);
            return (int)Math.Round(100.0 * (1.0 + (multiplier - 1.0) * progress), MidpointRounding.AwayFromZero);
        }

        public static int Apply(int hp, int waveIndex, int startWave, int endWave, float multiplier)
        {
            int percent = Percent(waveIndex, startWave, endWave, multiplier);
            if (percent == 100)
                return hp;

            long scaled = (long)hp * percent;
            return (int)Math.Max(1L, (scaled + 50) / 100);
        }
    }
}
