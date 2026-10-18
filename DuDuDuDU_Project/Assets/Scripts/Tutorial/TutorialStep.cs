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

        // ── 판 시작 전: 소환 · 머지 ─────────────
        IntroGoal,
        Summon,
        Merge,
        StartWaves,

        // ── 1웨이브: 자리 구매 · 확률업 · 저금통 ──
        SlotUnlock,
        SlotUnlocked,
        ProbUp,
        ProbRates,
        Savings,
        SavingsSelect,

        // ── 2웨이브: 현상금 · 행운 주사위 · 속성강화 ──
        BountyOpen,
        BountySelect,
        BountySet,
        LuckyDice,
        LuckyDiceResult,
        UpgradeOpen,
        UpgradeLevelUp,
        UpgradeClose,

        // ── 3웨이브: 진화(변신) ────────────────
        EvolveGift,
        EvolveSelect,
        EvolveConfirm,
        EvolveClose,

        // ── 보스 ───────────────────────────────
        BossWave,
        Complete,

        Failed,
    }
}
