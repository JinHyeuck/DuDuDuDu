using System.Collections.Generic;
using OJ.Point;

namespace OJ.Stage
{
    /// <summary>
    /// 소탕(Sweep) 판정 규칙.
    ///
    /// <b>왜 static 인가.</b> 헤드리스 러너 안에서는 <c>ScriptableObject.CreateInstance</c> 도
    /// <c>MonoSingleton.Instance</c> 도 돌지 않는다(<c>AssemblyPilotTests</c> 주석).
    /// 잠금 조건이 틀리면 "한 판도 안 깬 유저가 보상을 받는다"가 되는데 그건 화면을
    /// 띄워 봐야 알 수 있는 종류가 아니다. 그래서 규칙을 여기 상태 없는 static 으로 빼고
    /// <c>SweepRulesTests</c> 가 이것만 두드린다.
    ///
    /// <b>소탕은 진행도를 건드리지 않는다.</b> 보상만 준다 —
    /// <c>StageProgressManager.RecordStageClear</c> 를 부르지 않는다. 소탕으로 등급이
    /// 갱신되면 한 번도 못 깬 등급을 소탕으로 따는 우회로가 열린다.
    /// </summary>
    public static class SweepRules
    {
        /// <summary>소탕할 스테이지가 없다는 뜻. 클리어 기록이 하나도 없을 때의 값이다.</summary>
        public const int NoTargetStage = 0;

        /// <summary>
        /// 소탕 1회가 먹는 고기.
        ///
        /// <b>이 값이 루프 회수율 r 의 분모다</b>(<c>Docs/CurrencyPolicy.md</c> 4장).
        /// 핀볼이 돌려주는 고기를 이 값으로 나눈 것이 r 이므로, 여기를 내리면
        /// r 이 그만큼 올라간다 — 비용을 "완화"하는 변경이 곧 경제 승수를 키우는 변경이다.
        /// 고치려면 r 이 0.3 을 넘지 않는지 같이 확인할 것.
        /// </summary>
        public const int StaminaCostPerSweep = 5;

        /// <summary>
        /// 소탕이 열렸는가. <b>스테이지를 하나도 클리어하지 않았으면 잠긴다.</b>
        ///
        /// 판정 기준이 "해금된 스테이지"가 아니라 <b>클리어한 스테이지</b>인 것이 핵심이다.
        /// 1스테이지는 처음부터 해금돼 있으므로(<c>highestUnlockedStageIndex = 1</c>)
        /// 해금으로 판정하면 신규 유저가 첫 판을 깨기 전에 소탕을 돌린다.
        /// </summary>
        public static bool IsUnlocked(int lastClearedStageIndex)
        {
            return lastClearedStageIndex >= 1;
        }

        /// <summary>
        /// 소탕 대상 스테이지. <b>마지막으로 클리어한 스테이지</b>다.
        ///
        /// 선택 중인 스테이지가 아니라 클리어한 것 중 가장 높은 것을 쓴다. 보상이
        /// 스테이지 번호에 비례하므로(<c>StageRewardFormula.GuaranteedNormalGold</c>)
        /// 최고 스테이지가 언제나 이득이고, 유저가 낮은 스테이지를 골라 둔 채 소탕을
        /// 눌러 손해 보는 일이 없다.
        /// </summary>
        public static int ResolveTargetStageIndex(int lastClearedStageIndex)
        {
            return IsUnlocked(lastClearedStageIndex) ? lastClearedStageIndex : NoTargetStage;
        }

        /// <summary>한 번에 도는 횟수. 0 이하가 들어와도 최소 1회는 돈다.</summary>
        public static int ClampCount(int requestedCount)
        {
            return requestedCount < 1 ? 1 : requestedCount;
        }

        /// <summary>
        /// <paramref name="count"/> 회를 돌 때 드는 고기.
        ///
        /// <b>곱셈을 호출부에 흩지 않는다.</b> 창은 "이만큼 든다"를 그려야 하고
        /// 버튼은 "이만큼 뺀다"를 해야 하는데, 두 곳이 각자 곱하면 언젠가 한쪽만 바뀐다 —
        /// 그때 유저는 표시된 것과 다른 값을 낸다.
        /// </summary>
        public static int TotalStaminaCost(int count)
        {
            return ClampCount(count) * StaminaCostPerSweep;
        }

        /// <summary>
        /// 보유 고기로 돌 수 있는 최대 횟수. 한 번도 못 돌면 0 이다.
        ///
        /// 0 을 <see cref="ClampCount"/> 처럼 1 로 올리지 않는다. "최소 1회"는 입력 보정이고
        /// 이쪽은 <b>지불 능력</b>이라, 여기서 1 을 돌려주면 고기가 모자란 유저에게
        /// 창이 "1회 가능"이라고 말하게 된다.
        /// </summary>
        public static int ResolveMaxCount(int ownedStamina)
        {
            if (ownedStamina < StaminaCostPerSweep)
                return 0;

            return ownedStamina / StaminaCostPerSweep;
        }

        /// <summary>
        /// 창에서 고른 횟수를 [1, <paramref name="maxCount"/>] 로 자른다.
        /// <paramref name="maxCount"/> 가 0 이면 (= 고기 부족) 0 을 돌려준다.
        /// </summary>
        public static int ClampToMax(int value, int maxCount)
        {
            if (maxCount < 1)
                return 0;

            if (value < 1)
                return 1;

            return value > maxCount ? maxCount : value;
        }

        /// <summary>
        /// 소탕 보상. <b>마지막 웨이브까지 다 깬 기준</b>이다.
        ///
        /// <c>GameManager.ClearStage</c> 가 쓰는 것과 <b>같은</b>
        /// <see cref="StageRewardCalculator.BuildNormalClearRewards"/> 를 비율 없이 부른다.
        /// 실패 경로(<c>ScaleRewards</c> 로 웨이브 비율만큼 깎는 쪽)를 타면 안 된다 —
        /// 소탕은 "이미 깬 판을 다시 깬다"이지 "도중까지 갔다"가 아니다.
        ///
        /// 여러 회를 <see cref="PointRewardUtility.MergeRewards"/> 로 합쳐서 돌려준다.
        /// 24회를 돌면 같은 재화가 24줄로 나오는데, 그대로 결과창에 넘기면 칸이 넘친다.
        /// </summary>
        public static List<PointRewardEntry> BuildRewards(int stageIndex, int count)
        {
            var rewards = new List<PointRewardEntry>();
            if (stageIndex < 1)
                return rewards;

            int safeCount = ClampCount(count);
            for (int i = 0; i < safeCount; i++)
                rewards.AddRange(StageRewardCalculator.BuildNormalClearRewards(stageIndex));

            return PointRewardUtility.MergeRewards(rewards);
        }
    }
}
