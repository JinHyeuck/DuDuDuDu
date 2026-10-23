namespace OJ.Core
{
    /// <summary>
    /// 어느 웨이브에 스테이지 보스가 나오는가. 엔진을 쓰지 않는다.
    ///
    /// <b>마지막 웨이브는 언제나 보스다</b>(원래 규칙). 거기에 더해 <see cref="PeriodicFromStage"/>
    /// 스테이지부터는 <see cref="Period"/> 웨이브마다 중간 보스가 나온다 — 5웨이브, 10웨이브…
    /// 중간 보스 웨이브는 보스를 잡아도 스테이지가 끝나지 않는다. 스테이지 클리어는 이 규칙이
    /// 아니라 "마지막 웨이브를 끝냈는가"(<c>GameManager.HandleWaveCompleted</c>)가 정한다.
    ///
    /// 튜토리얼(1스테이지)과 탑(1웨이브짜리 층)은 중간 보스 조건에 걸리지 않아 마지막 웨이브 규칙만 탄다.
    /// </summary>
    public static class BossWaveRules
    {
        /// <summary>중간 보스가 나오기 시작하는 스테이지.</summary>
        public const int PeriodicFromStage = 5;

        /// <summary>중간 보스 웨이브의 간격.</summary>
        public const int Period = 5;

        public static bool IsBossWave(int stageIndex, int waveIndex, int totalWaves)
        {
            if (waveIndex >= totalWaves)
                return true;

            return stageIndex >= PeriodicFromStage
                && waveIndex >= Period
                && waveIndex % Period == 0;
        }
    }
}
