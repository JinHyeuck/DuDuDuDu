namespace OJ.Core
{
    /// <summary>
    /// 스테이지 진행에 무한의 탑 진행을 묶는 순수 규칙. 엔진을 쓰지 않는다.
    ///
    /// <b>탑이 스테이지를 <see cref="StageOffset"/> 칸 뒤에서 따라온다.</b> 스테이지 S 에서 자리를 다 쓰려면
    /// 탑을 <c>S - StageOffset</c> 층까지 깨 둬야 하고, 못 깼으면 보드 맨 아래 줄 전체의 자리 구매가 막힌다.
    /// 예: 스테이지 6 → 탑 1층, 10 → 5층, 23 → 18층. 스테이지 5 를 깨면 6 이 탑 1층을 요구하므로
    /// 튜토리얼(탑 1층 클리어로 봉인 해제)이 예외 없이 이 규칙과 맞는다.
    /// 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class TowerGateRules
    {
        /// <summary>탑이 스테이지보다 몇 칸 뒤에 있어도 되는가. 이 수만큼은 탑 없이 갈 수 있다.</summary>
        public const int StageOffset = 5;

        /// <summary>스테이지 <paramref name="stageIndex"/> 에서 자리를 다 쓰려면 탑에서 깨 둬야 하는 층.</summary>
        public static int RequiredTowerFloor(int stageIndex)
        {
            int required = stageIndex - StageOffset;
            return required > 0 ? required : 0;
        }

        /// <summary>탑 진행이 모자라 맨 아래 줄이 막히는가.</summary>
        public static bool IsBottomRowLocked(int stageIndex, int highestClearedTowerFloor)
        {
            return highestClearedTowerFloor < RequiredTowerFloor(stageIndex);
        }

        /// <summary>
        /// <paramref name="slotIndex"/> 가 맨 아래 줄 칸인가. 한 줄의 칸 수(<paramref name="columns"/>)는
        /// 하드코딩하지 않고 호출자가 실제 그리드에서 읽어 넘긴다.
        /// </summary>
        public static bool IsBottomRowSlot(int slotIndex, int totalSlots, int columns)
        {
            if (columns <= 0 || totalSlots < columns)
                return false;

            return slotIndex >= totalSlots - columns && slotIndex < totalSlots;
        }

        /// <summary>
        /// 이 스테이지를 막 클리어했을 때 "아랫줄이 봉인되는" 연출을 보여 줄 차례인가.
        /// <b>풀려 있던 줄이 새로 막히는 순간에만</b> 참이다 — 방금 깬 스테이지까지는 탑이 따라왔는데
        /// 다음 스테이지부터 모자란 경우. 이미 뒤처져 있는 동안의 클리어마다 연출을 반복하면 질리므로
        /// 그 뒤는 로비의 요구 문구가 맡는다.
        /// </summary>
        public static bool ShouldPlaySeal(int clearedStage, int highestClearedTowerFloor)
        {
            return !IsBottomRowLocked(clearedStage, highestClearedTowerFloor)
                && IsBottomRowLocked(clearedStage + 1, highestClearedTowerFloor);
        }

        /// <summary>
        /// 탑 클리어로 <b>지금 도전 가능한 최고 스테이지</b>의 봉인이 풀렸는가.
        /// 클리어 전에는 막혀 있었고 클리어 후에는 풀렸을 때만이다.
        /// </summary>
        public static bool ReleasedByTowerClear(int highestUnlockedStage, int floorBefore, int floorAfter)
        {
            return IsBottomRowLocked(highestUnlockedStage, floorBefore)
                && !IsBottomRowLocked(highestUnlockedStage, floorAfter);
        }
    }
}
