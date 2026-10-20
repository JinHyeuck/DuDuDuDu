using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Stage;
using OJ.Tutorial;

namespace OJ.Tower
{
    /// <summary>
    /// 스테이지와 무한의 탑을 잇는 "봉인" 이야기의 연출. 규칙은 <see cref="TowerGateRules"/> 에 있고
    /// 여기는 <b>언제 무엇을 보여 줄지</b>만 안다.
    ///
    /// <list type="number">
    /// <item>스테이지 5 를 처음 깨면(탑이 6 의 요구를 못 채움) 결과창을 닫은 뒤 그림자가 아랫줄을 봉인한다(긴 버전).</item>
    /// <item>로비에서 탑이 열리고(ContentUnlock 가이드), 탑 첫 판은 아랫줄이 봉인된 채 시작한다(UIBoard).</item>
    /// <item>탑을 깨면 봉인이 풀리는 장면을 보여 준다(처음만 긴 버전).</item>
    /// <item>그 뒤로 탑이 뒤처진 채 새 스테이지를 열면 짧은 버전으로 다시 봉인한다.</item>
    /// </list>
    /// 화면은 인게임 튜토리얼 오버레이를 쓴다(<see cref="UITutorialOverlay"/>) — 딤드·말풍선이 이미 있다.
    /// NPC 아트가 없어 대사는 말풍선으로만 한다. 아트가 생기면 <see cref="Speaker"/> 자리를 바꾼다.
    /// </summary>
    public static class TowerSealDirector
    {
        private const string Speaker = "<b><color=#C08CFF>수상한 그림자</color></b>\n";
        private const string SealLabel = "봉인";

        /// <summary>이 스테이지를 막 클리어했을 때 봉인 장면을 보여 줄 차례인가.</summary>
        public static bool ShouldPlaySeal(int clearedStage)
        {
            TowerProgressManager tower = TowerProgressManager.Instance;
            int cleared = tower != null ? tower.HighestClearedFloor : 0;
            return TowerGateRules.ShouldPlaySeal(clearedStage, cleared);
        }

        /// <summary>
        /// 방금 탑을 깬 것이 지금 도전 가능한 최고 스테이지의 봉인을 풀었는가.
        /// <paramref name="floorBefore"/> 는 기록을 갱신하기 <b>전</b>의 최고 클리어 층이다.
        /// </summary>
        public static bool ShouldPlayRelease(int floorBefore)
        {
            TowerProgressManager tower = TowerProgressManager.Instance;
            StageProgressManager stage = StageProgressManager.Instance;
            if (tower == null || stage == null)
                return false;

            return TowerGateRules.ReleasedByTowerClear(
                stage.GetHighestUnlockedStageIndex(), floorBefore, tower.HighestClearedFloor);
        }

        /// <summary>아랫줄이 봉인되는 장면. 끝까지 보면 이야기를 앞으로 진행시킨다.</summary>
        public static async UniTask PlaySealAsync(UIBoard board, int clearedStage, CancellationToken token)
        {
            TowerProgressManager tower = TowerProgressManager.Instance;
            bool first = tower == null || tower.SealStory == 0;
            int required = TowerGateRules.RequiredTowerFloor(clearedStage + 1);

            await RunAsync(async overlay =>
            {
                if (first)
                {
                    await Say(overlay, Speaker + "크크크… 첫 구간을 넘었다고 방심했나?", token);
                    if (board != null)
                        await board.PlaySealVisualAsync(SealLabel);
                    await Say(overlay, Speaker + "아랫줄은 내가 봉인했다!", token);
                    await Say(overlay, Speaker + "풀고 싶다면 <b>무한의 탑 " + required + "층</b>을 클리어해 보라고!", token);
                }
                else
                {
                    if (board != null)
                        await board.PlaySealVisualAsync(SealLabel);
                    await Say(overlay,
                        Speaker + "크크, 아랫줄은 다시 봉인했다!\n<b>무한의 탑 " + required + "층</b>을 클리어하면 풀려요.", token);
                }
            }, token);

            // 끝까지 봤을 때만 적는다. 도중에 나가면 다음에 처음부터 다시 나온다.
            tower?.AdvanceSealStory(1);
        }

        /// <summary>아랫줄의 봉인이 풀리는 장면. 처음만 대사를 길게 한다.</summary>
        public static async UniTask PlayReleaseAsync(UIBoard board, CancellationToken token)
        {
            TowerProgressManager tower = TowerProgressManager.Instance;
            bool first = tower != null && tower.SealStory <= 1;

            await RunAsync(async overlay =>
            {
                if (board != null)
                    await board.PlayReleaseVisualAsync(SealLabel);

                if (first)
                {
                    await Say(overlay, Speaker + "으윽… 내 봉인이 풀리다니!", token);
                    await Say(overlay,
                        "이제 아랫줄도 마음껏 쓸 수 있어요.\n탑이 스테이지를 따라오지 못하면 또 막힐 수 있으니 틈틈이 올라오세요!", token);
                }
                else
                {
                    await Say(overlay, "아랫줄의 봉인이 풀렸어요!", token);
                }
            }, token);

            tower?.AdvanceSealStory(2);
        }

        private static async UniTask RunAsync(Func<UITutorialOverlay, UniTask> body, CancellationToken token)
        {
            UITutorialOverlay overlay = GameContainer.UI != null ? GameContainer.UI.Get<UITutorialOverlay>() : null;
            if (overlay == null)
            {
                // 화면이 없으면 연출 없이 넘어간다. 이야기는 진행시키지 않으므로 다음에 다시 나온다.
                Debug.LogError("[봉인] 오버레이를 열지 못해 연출을 건너뛴다.");
                return;
            }

            overlay.Enter();
            try
            {
                await body(overlay);
            }
            finally
            {
                overlay.ClearAll();
                overlay.Exit();
            }
        }

        /// <summary>
        /// 화면 전체를 막은 채 말풍선을 띄우고 탭을 기다린다. 말풍선은 위쪽에 둔다 —
        /// 아래쪽 보드의 봉인이 보여야 하기 때문이다.
        /// </summary>
        private static async UniTask Say(UITutorialOverlay overlay, string text, CancellationToken token)
        {
            overlay.SetFinger(null);
            overlay.SetDim();
            overlay.SetTalk(text, tapToContinue: true, UITutorialOverlay.TalkPlacement.Top);
            await UniTask.WaitUntil(overlay.ConsumeTap, PlayerLoopTiming.Update, token);
        }
    }
}
