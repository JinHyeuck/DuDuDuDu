#if UNITY_EDITOR || DEV_DEFINE
using UnityEngine;
using OJ.ContentUnlock;
using OJ.Core;
using OJ.DI;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.Tower;

namespace OJ.Dev
{
    /// <summary>
    /// 개발용 "탑 봉인 이야기 다시 보기". 스테이지 4 까지만 깬 신규 상태로 되돌려, 스테이지 5 를 깨는 순간부터
    /// 봉인 연출 → 탑 해금 가이드 → 탑 첫 판 → 해제 연출을 처음부터 다시 볼 수 있게 한다(<see cref="TowerSealDirector"/>).
    ///
    /// <b>새 경로를 뚫지 않는다.</b> 세이브가 읽힐 때 쓰는 <c>ReadFrom</c> 을 그대로 불러, 이 상태가 세이브에서 읽혀 온 상태와 같게 한다.
    /// 다른 진행(재화·다이스·장비)은 건드리지 않는다.
    ///
    /// <b>에디터와 DEV_DEFINE 빌드에만 존재한다.</b> 릴리스 빌드에는 컴파일되지 않는다.
    /// </summary>
    internal static class TowerSealStoryCheat
    {
        /// <summary>마지막으로 깬 스테이지. 다음 스테이지(5)를 깨면 봉인 연출이 나온다.</summary>
        private const int ClearedStage = TowerGateRules.StageOffset - 1;

        /// <summary>되돌린 뒤 로비를 다시 불러 입구·스테이지 표시를 맞춘다. 무엇을 했는지 한 줄로 돌려준다.</summary>
        internal static string Reset()
        {
            var state = new SaveState();
            state.Stage.SelectedIndex = ClearedStage + 1;
            state.Stage.HighestUnlockedIndex = ClearedStage + 1;
            for (int stage = 1; stage <= ClearedStage; stage++)
            {
                state.Stage.Records[stage.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                    new StageRecordSave { BestClearGrade = (int)StageClearGrade.Minimum, BestClearedWave = 1 };
            }

            state.Tower.HighestClearedFloor = 0;
            state.Tower.SealStory = 0;

            // 이미 열렸을 컨텐츠의 연출은 본 것으로 적는다. 안 그러면 되돌린 직후 지난 해금 연출이 줄줄이 다시 나온다.
            state.ContentUnlock.Initialized = true;
            foreach (ContentUnlockDefinition d in ContentUnlockDatabaseProvider.Database.Definitions)
            {
                if (d != null && d.requiredClear <= ClearedStage)
                    state.ContentUnlock.Announced.Add(d.contentId.ToString());
            }

            StageProgressManager.Instance?.ReadFrom(state);
            TowerProgressManager.Instance?.ReadFrom(state);
            ContentUnlockManager.Instance?.ReadFrom(state);
            GameContainer.SaveService?.SaveAll();

            SceneFlowManager.LoadLobby();

            string message = "[개발 치트] 탑 봉인 이야기 초기화 - 스테이지 " + ClearedStage + " 까지 클리어, 탑 0층";
            Debug.Log(message);
            return message;
        }
    }
}
#endif
