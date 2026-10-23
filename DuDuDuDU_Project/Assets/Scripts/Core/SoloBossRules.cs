using System;

namespace OJ.Core
{
    /// <summary>
    /// 보스 웨이브 규칙. 엔진을 쓰지 않는다.
    ///
    /// <b>두 갈래다.</b> 간격(<c>bossWaveInterval</c>)이 0 이면 옛 규칙 그대로 <b>마지막 웨이브에만</b> 보스가 일반 몬스터와 섞여 나온다.
    /// 간격이 있으면(1스테이지 시험 모드: 5) 그 배수 웨이브(5 · 10 · 15)가 <b>보스 단독 웨이브</b>다 — 일반 몬스터는 나오지 않고
    /// 체력이 큰 보스 한 마리만 내려온다. 유저가 자기 화력의 한계를 한 번에 느끼게 하려는 것이다.
    ///
    /// 단독 보스의 체력은 <b>"그 웨이브의 일반 몬스터 전체 체력" 에 배수를 곱한 값</b>이다(<see cref="SoloBossHp"/>).
    /// 일반 웨이브 하나를 통째로 보스 한 마리에 몰아 넣은 셈이라, 배수 1 이 곧 "평소 한 웨이브만큼의 화력을 한 마리에 쏟아야 한다" 이다.
    /// 배수는 보스마다 따로 둔다(<c>bossWaveHpFactors</c>) — 첫 보스는 누구나 넘고, 뒤 보스부터 허들이어야 하기 때문이다.
    /// </summary>
    public static class SoloBossRules
    {
        /// <summary>이 웨이브가 보스 웨이브인가. 간격이 0 이하면 마지막 웨이브(이상)만 참.</summary>
        public static bool IsBossWave(int interval, int totalWaves, int waveIndex)
        {
            if (interval <= 0)
                return waveIndex >= totalWaves;

            return waveIndex >= totalWaves || (waveIndex > 0 && waveIndex % interval == 0);
        }

        /// <summary>
        /// 이 웨이브가 <b>보스만 나오는</b> 웨이브인가. 간격이 0 이하면 어떤 웨이브도 아니다(옛 규칙은 일반 몬스터가 섞인다).
        /// </summary>
        public static bool IsSoloBossWave(int interval, int totalWaves, int waveIndex)
        {
            return interval > 0 && IsBossWave(interval, totalWaves, waveIndex);
        }

        /// <summary>몇 번째 보스인가(0부터). 5 · 10 · 15 웨이브 → 0 · 1 · 2. 마지막 웨이브가 배수가 아니어도 그 앞 보스 다음 번호가 된다.</summary>
        public static int BossOrdinal(int interval, int totalWaves, int waveIndex)
        {
            if (interval <= 0)
                return 0;

            if (waveIndex >= totalWaves && waveIndex % interval != 0)
                return Math.Max(0, totalWaves / interval);

            return Math.Max(0, waveIndex / interval - 1);
        }

        /// <summary>
        /// 단독 보스의 체력(반올림, 최소 1). <paramref name="monsterHp"/> 는 그 웨이브의 일반 몬스터 한 마리 체력이고,
        /// <paramref name="monstersPerWave"/> 는 스테이지의 웨이브당 마릿수(초반 완화는 보스 웨이브가 5 이상이라 걸리지 않는다).
        /// </summary>
        public static int SoloBossHp(int monsterHp, int monstersPerWave, float factor)
        {
            double total = (double)Math.Max(1, monsterHp) * Math.Max(1, monstersPerWave) * Math.Max(0.01f, factor);
            return (int)Math.Max(1.0, Math.Round(total, MidpointRounding.AwayFromZero));
        }
    }
}
