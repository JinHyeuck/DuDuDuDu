namespace OJ.Core
{
    /// <summary>
    /// 인게임 첫 튜토리얼의 규칙 중 <b>엔진 없이 판정되는 것</b>. (Docs/IngameTutorialSteps.md)
    ///
    /// 여기 두는 이유는 테스트다. 튜토리얼 판은 "SP 를 다 쓰면 다음 단계" 처럼 수치가
    /// <b>정확히</b> 맞아야 굴러가는데, 그 수치를 만드는 쪽(소환 시스템·배틀 스코프)은
    /// 헤드리스 러너에서 못 돈다. 규칙만 떼어 두면 맞는지를 에디터 없이 본다.
    /// </summary>
    public static class TutorialRules
    {
        /// <summary>
        /// 앞으로 <paramref name="count"/> 번 소환하는 데 드는 SP 합계.
        ///
        /// <b><c>UIDiceSummonSystem.OnSummonButton</c> 의 비용 상승을 그대로 따른다</b> —
        /// 소환할 때마다 카운터가 1 오르고, <paramref name="summonsPerIncrease"/> 에 닿으면
        /// 비용이 1 오르고 카운터가 0 으로 돌아간다. 비용은 <b>소환한 뒤에</b> 오른다.
        ///
        /// 튜토리얼은 이 합계로 SP 를 준다. 남거나 모자라면 "SP 를 다 쓰면 딤드가 풀린다"
        /// 단계가 일찍 풀리거나 영영 안 풀린다 — 그래서 어림하지 않고 계산한다.
        /// </summary>
        /// <param name="currentCost">지금 한 번 소환하는 비용.</param>
        /// <param name="summonsSinceIncrease">마지막 비용 상승 뒤로 소환한 횟수.</param>
        /// <param name="summonsPerIncrease">몇 번 소환하면 비용이 오르는가. 1 미만은 1 로 본다.</param>
        /// <param name="count">앞으로 소환할 횟수.</param>
        public static int SummonCostSum(int currentCost, int summonsSinceIncrease, int summonsPerIncrease, int count)
        {
            int perIncrease = OJMath.Max(1, summonsPerIncrease);
            int cost = OJMath.Max(1, currentCost);
            int counter = OJMath.Max(0, summonsSinceIncrease);
            int total = 0;

            for (int i = 0; i < count; i++)
            {
                total += cost;

                counter++;
                if (counter >= perIncrease)
                {
                    cost++;
                    counter = 0;
                }
            }

            return total;
        }

        /// <summary>
        /// 이 세이브가 튜토리얼을 건너뛰어야 하는가.
        ///
        /// 완료 플래그가 없어도 <b>이미 본편 스테이지를 플레이한 기록이 있는 세이브</b>는 튜토리얼로
        /// 끌고 가지 않는다. 플래그는 튜토리얼보다 늦게 생긴 필드라, 그 전부터 플레이하던
        /// 세이브에는 없다 — 그 사람을 첫 판으로 돌려보내는 것은 진행을 빼앗는 일이다.
        /// </summary>
        public static bool ShouldSkip(bool completedFlag, int highestUnlockedStageIndex, int stageRecordCount)
        {
            return completedFlag || highestUnlockedStageIndex > 1 || stageRecordCount > 0;
        }
    }
}
