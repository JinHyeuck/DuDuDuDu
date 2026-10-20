namespace OJ.Core
{
    /// <summary>
    /// 4스테이지 첫 진입의 킹 썬더 유도. 엔진을 쓰지 않는다.
    ///
    /// 킹 썬더는 3스테이지에서 열리고(<c>DiceUnlockDatabase</c>) 4스테이지부터 후반 가산이 붙는다. 그 힘을 체감시키려면
    /// 유저가 <b>진화 사슬(3성 썬더 → 타임 10 → 킹 썬더 20 = 마석 30)</b>을 한 번 끝까지 밟아 봐야 한다.
    /// 그래서 이 판에서만 <b>머지로 처음 3성이 나올 때 두 번을 썬더로 고정</b>한다(1번째 3성 머지·2번째 3성 머지). 마석 수급량은 건드리지 않는다(사용자 지시 2026-10-20 — 한때 영혼 보너스 마석을 30~35로 부풀렸는데 버그처럼 보여 걷었다).
    /// 소환이 아니라 <b>머지 결과</b>를 고정하는 이유: 유저가 직접 합쳐서 3성 썬더를 얻어야 "내가 만들었다" 가 되고, 소환 순서를 건드리면 판 시작이 어색하다.
    ///
    /// <b>걸리는 조건은 좁다.</b> 본편 4스테이지이고, 그 스테이지를 아직 한 번도 깬 적이 없을 때뿐이다 — 깨면(등급이 생기면)
    /// 영영 꺼진다. 세이브에 새 필드를 두지 않고 이미 있는 클리어 기록을 정본으로 쓴다. 튜토리얼·탑·다른 스테이지는 건드리지 않는다.
    /// 지는 판은 기록이 안 남으므로 재도전에도 걸린다(유도가 필요한 유저가 막힌 것이므로 의도다).
    /// 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class KingThunderGuide
    {
        /// <summary>유도가 걸리는 스테이지.</summary>
        public const int Stage = 4;

        /// <summary>썬더로 고정하는 "3성이 나오는 머지" 의 수(앞에서부터).</summary>
        public const int ScriptedThreeStarMerges = 2;

        /// <summary>고정하는 머지 결과의 성급. 진화 조건(<c>DiceEvolution.EvolveRequiredStar</c>)과 같아야 한다 — 테스트가 지킨다.</summary>
        public const int ScriptedStar = 3;

        /// <summary>이 판에 유도를 거는가. <paramref name="stageBestGrade"/> 는 이 스테이지의 최고 등급(0 = 클리어 기록 없음).</summary>
        public static bool Applies(bool isTutorial, bool isTower, int stageIndex, int stageBestGrade)
        {
            return !isTutorial && !isTower && stageIndex == Stage && stageBestGrade <= 0;
        }
    }
}
