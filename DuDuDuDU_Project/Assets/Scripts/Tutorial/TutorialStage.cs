using System.Collections.Generic;
using OJ.Core;
using OJ.Stage;

namespace OJ.Tutorial
{
    /// <summary>
    /// 튜토리얼 판의 수치와 대본. (Docs/IngameTutorialSteps.md "수치 조건")
    ///
    /// <b>왜 <c>StageDatabase</c> 에 넣지 않았나.</b> 거기 넣으면 스테이지 선택 화면·별의 시련·
    /// 퍼펙트 보상·소탕이 전부 이 판을 "스테이지" 로 센다. 튜토리얼은 한 번 지나가는 판이라
    /// 그 어느 진행도에도 남으면 안 된다 — 탑이 <c>InitializeTowerFloor</c> 에서 StageData 를
    /// 코드로 만드는 것과 같은 판단이다.
    ///
    /// <b>웨이브가 넷인 이유.</b> 마지막 웨이브(보스)에는 현상금이 나오지 않는다
    /// (<c>BountyFormula.CanSpawnOnWave</c>). 그래서 "상위 현상금을 걸고 웨이브 실행" 은
    /// 보스 앞에 자기 웨이브가 하나 있어야 뜻이 있다.
    /// </summary>
    public static class TutorialStage
    {
        public const int TotalWaves = 4;

        /// <summary>현상금을 가르치는 웨이브. 보스 앞이어야 한다(마지막 웨이브에는 현상금이 안 나온다).</summary>
        public const int BountyWave = 2;

        /// <summary>속성강화·행운 주사위(마석)를 가르치는 웨이브.</summary>
        public const int StoneWave = 2;

        /// <summary>3성 진화를 가르치는 웨이브. 보스 바로 앞이다.</summary>
        public const int EvolveWave = 3;

        /// <summary>처음 소환 비용. <c>UIDiceSummonSystem.SetStageStartSp</c> 의 기본값과 같아야 한다.</summary>
        public const int StartSummonCost = 10;

        /// <summary>웨이브 하나를 막고 받는 SP 가 소환 몇 번어치인가(실시간이라 처치마다 나눠 받는다).</summary>
        public const int RetrySummonCount = 3;

        /// <summary>
        /// 첫 소환 여섯 번. 순서와 칸이 고정이다.
        ///
        /// 노멀 둘(2·3)은 <b>같은 줄에 붙여</b> 둔다. 머지 단계의 딤드 구멍이 두 칸을 감싸는
        /// 사각형 하나라, 떨어뜨려 두면 그 사이 칸까지 구멍에 들어간다.
        /// </summary>
        public static readonly IReadOnlyList<TutorialRunManager.ScriptedSummon> OpeningSummons =
            new[]
            {
                new TutorialRunManager.ScriptedSummon(DiceType.Normal, 2),
                new TutorialRunManager.ScriptedSummon(DiceType.Normal, 3),
                new TutorialRunManager.ScriptedSummon(DiceType.Normal, 5),
                new TutorialRunManager.ScriptedSummon(DiceType.Fire, 4),
                new TutorialRunManager.ScriptedSummon(DiceType.Ice, 6),
                new TutorialRunManager.ScriptedSummon(DiceType.Poison, 1),
            };

        /// <summary>
        /// 한 줄의 칸 수. 자리 구매가 켜져 있어 첫 줄만 열려 있으므로 <b>대본 소환은 전부 이 안에 놓아야 한다</b> —
        /// 잠긴 칸에는 다이스를 놓을 수 없다(<c>UIBoard.SpawnDice</c>). 테스트가 지킨다.
        /// </summary>
        public const int FirstRowSlotCount = 7;

        /// <summary>자리 구매를 가르칠 때 여는 칸. 첫 줄 바로 다음 칸이다.</summary>
        public const int UnlockSlot = FirstRowSlotCount;

        /// <summary>머지 단계에서 합칠 두 칸. <see cref="OpeningSummons"/> 의 첫 두 줄이다.</summary>
        public const int MergeSlotA = 2;
        public const int MergeSlotB = 3;

        /// <summary>노멀 둘을 합치면 나오는 것. 진화 단계에서 다시 만날 계통이다.</summary>
        public const DiceType MergeResult = DiceType.Thunder;

        /// <summary>보스 앞에서 주는 3성(진화가 3성부터라서다). 이것을 진화시켜 <see cref="EvolveTarget"/> 를 만든다.</summary>
        public const DiceType GiftDice = DiceType.Thunder;

        /// <summary>선물의 성급. 진화 조건과 같아야 한다 — 낮추면 진화 버튼이 안 먹고, 높이면 가르치는 규칙이 틀린다.</summary>
        public const int GiftStar = OJ.Dice.DiceEvolution.EvolveRequiredStar;

        /// <summary>
        /// 진화로 만들어 보이는 다이스. <b>처음부터 보유하는 특수 5종 중 하나여야 한다</b>
        /// (<c>DiceEvolution.IsOwnedFromStart</c>) — <c>MergeSystem.TryEvolve</c> 가 대상
        /// 보유를 보고, 거짓이면 진화 버튼이 조용히 아무 일도 하지 않는다. 테스트가 지킨다.
        /// </summary>
        public const DiceType EvolveTarget = DiceType.Time;

        /// <summary>4성을 놓을 칸 후보. 앞에서부터 빈 칸을 쓴다(그 사이 유저가 칸을 채웠을 수 있다).</summary>
        public static readonly IReadOnlyList<int> GiftSlotCandidates = new[] { UnlockSlot, 4, 5, 6, 0, 1 };

        /// <summary>
        /// 이 판의 StageData. 시작 SP 와 웨이브 클리어 SP 는 대본의 소환 횟수에서 <b>계산한다</b> —
        /// "SP 를 다 쓰면 다음" 단계가 정확히 맞아떨어져야 하기 때문이다(<see cref="TutorialRules.SummonCostSum"/>).
        /// </summary>
        /// <param name="summonsPerCostIncrease">씬의 소환 시스템이 쓰는 값.</param>
        public static StageData Build(int summonsPerCostIncrease)
        {
            int opening = OpeningSummons.Count;

            int openingSp = TutorialRules.SummonCostSum(StartSummonCost, 0, summonsPerCostIncrease, opening);
            int retrySp = TutorialRules.SummonCostSum(StartSummonCost, 0, summonsPerCostIncrease, opening + RetrySummonCount)
                          - openingSp;

            return new StageData
            {
                // 성장식(ResolvedBaseDefense)이 스테이지 번호를 본다. 1 이 가장 무르다.
                stageIndex = 1,
                theme = StageTheme.DarkForest,
                totalWaves = TotalWaves,
                monstersPerWave = 8,
                wallHp = 100,
                initialSP = openingSp,

                // 실시간이라 웨이브 하나의 몫을 처치마다 나눠 받는다. 소환 몇 번어치로 맞춰 두면
                // 디렉터가 "SP 가 이 정도 쌓였다" 를 전제로 안내할 수 있다.
                waveClearSP = retrySp,

                // 5 로는 벽이 한 번도 안 맞았다(플레이 확인). 현상금·보스 HP 도 이 값에서 나오므로 같이 오른다.
                baseMonsterHp = 10,
                baseMonsterDefense = 0,
                waveHpLinearFactor = 0.145f,
                waveHpQuadraticFactor = 0.018f,
                bossHpMultiplier = 6f,
            };
        }
    }
}
