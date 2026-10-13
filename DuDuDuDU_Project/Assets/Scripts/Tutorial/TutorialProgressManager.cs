using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;
using OJ.Save;

namespace OJ.Tutorial
{
    /// <summary>
    /// 튜토리얼을 마쳤는지, 그리고 <b>다음 전투가 튜토리얼 판인지</b>를 든다.
    /// (Docs/IngameTutorialSteps.md)
    ///
    /// <b>예약은 탑과 같은 모양이다</b>(<c>TowerProgressManager.ConsumePendingRun</c>).
    /// 타이틀이 예약하고 전투가 꺼낸다. 전투 씬은 어느 판인지를 스스로 알 방법이 없고,
    /// 씬 인자를 넘기는 경로도 없다 — 그래서 루트에 사는 이 매니저가 한 번만 건네준다.
    /// 예약을 남겨 두면 다음에 본편으로 들어갈 때 튜토리얼이 또 열린다.
    ///
    /// <b>MonoBehaviour 가 아니다.</b> <c>GameContainer</c> 가 만들고 <c>SaveService</c> 가 저장한다.
    /// </summary>
    [Preserve]
    public sealed class TutorialProgressManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static TutorialProgressManager Instance { get; internal set; }

        private bool inGameCompleted;

        /// <summary>세이브에 쓰지 않는다. 앱이 죽으면 다음 실행에서 타이틀이 다시 예약한다.</summary>
        private bool pendingRun;

        /// <summary>인게임 첫 튜토리얼을 마쳤는가(또는 건너뛸 세이브인가).</summary>
        public bool IsInGameCompleted => inGameCompleted;

        /// <summary>다음 전투를 튜토리얼 판으로 연다. 타이틀과, 튜토리얼에서 졌을 때의 재시작이 부른다.</summary>
        public void RequestInGameRun()
        {
            pendingRun = true;
        }

        /// <summary>
        /// 예약을 꺼낸다. <b>한 번만 참이다.</b> 전투의 <c>InitializeStage</c> 가 부른다.
        /// </summary>
        public bool ConsumePendingRun()
        {
            bool pending = pendingRun;
            pendingRun = false;
            return pending;
        }

        /// <summary>
        /// 마쳤다고 적고 <b>그 자리에서</b> 저장한다. 로비로 넘어가기 전에 앱이 죽으면
        /// 다음 실행에서 튜토리얼을 처음부터 다시 하게 되는데, 이미 준 Time 다이스는
        /// 보유로 남아 있어 이번에는 환급이 나간다 — 이중 지급은 아니지만 할 필요가 없는 일이다.
        /// </summary>
        public void MarkInGameCompleted()
        {
            if (inGameCompleted)
                return;

            inGameCompleted = true;
            GameContainer.SaveService?.SaveAll();
        }

        // ── 저장 ───────────────────────────────────────────────────────

        public void WriteTo(SaveState state)
        {
            state.Tutorial.InGameCompleted = inGameCompleted;
        }

        public void ReadFrom(SaveState state)
        {
            // 플래그가 없어도 이미 본편을 한 세이브는 마친 것으로 본다(TutorialRules.ShouldSkip).
            // 스테이지 몫은 남의 것이라 <b>읽기만</b> 한다 — 쓰는 것은 StageProgressManager 다.
            inGameCompleted = TutorialRules.ShouldSkip(
                state.Tutorial.InGameCompleted,
                state.Stage.HighestUnlockedIndex,
                state.Stage.Records.Count);
        }
    }
}
