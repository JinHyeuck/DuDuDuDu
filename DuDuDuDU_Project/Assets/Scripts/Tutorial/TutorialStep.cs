namespace OJ.Tutorial
{
    /// <summary>
    /// 인게임 첫 튜토리얼의 단계. (Docs/IngameTutorialSteps.md)
    ///
    /// <b>세이브에 들어가지 않는다.</b> 판 도중에 앱이 죽으면 처음부터 다시 하므로
    /// 값을 바꾸거나 끼워 넣어도 깨지는 것이 없다. 로그와 개발용 표시에만 쓴다.
    /// </summary>
    public enum TutorialStep
    {
        None = 0,

        // ── 1웨이브 전: 소환 · 머지 ─────────────
        IntroGoal,
        Summon,
        Merge,
        StartWave1,

        // ── 1웨이브 ────────────────────────────
        Wave1,
        Wave1Reward,

        // ── 2웨이브 전: 현상금 ─────────────────
        BountyOpen,
        BountySelect,
        StartWave2,

        // ── 2웨이브: 되돌리기 ──────────────────
        Wave2Crisis,
        RewindTalk,
        Rewind,
        RewindConfirm,
        SpendSp,
        StartWave2Retry,

        // ── 2웨이브(재): 딜미터기 ──────────────
        DamageMeterTip,
        Wave2Retry,

        // ── 3웨이브: 손 놓기 ───────────────────
        SummonHint,
        BountyHint,
        StartWave3,
        Wave3,

        // ── 4웨이브 전: 진화(변신) ─────────────
        EvolveGift,
        EvolveSelect,
        EvolveConfirm,
        EvolveClose,
        StartWave4,

        // ── 보스 ───────────────────────────────
        BossWave,
        Complete,

        Failed,
    }
}
