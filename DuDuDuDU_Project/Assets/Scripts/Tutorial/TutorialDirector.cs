using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using OJ.Bounty;
using OJ.DI;
using OJ.Dice;
using OJ.Element;
using OJ.Hunting;
using OJ.Point;
using OJ.Rewind;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.UI;

namespace OJ.Tutorial
{
    /// <summary>
    /// 인게임 첫 튜토리얼을 처음부터 끝까지 한 줄로 끌고 간다. (Docs/IngameTutorialSteps.md)
    ///
    /// <b>단계 하나 = "보여 주고, 조건이 될 때까지 기다린다".</b> 조건은 새 이벤트를 만들지
    /// 않고 <b>이미 보이는 상태</b>를 본다 — 웨이브 상태, 보드의 다이스, SP, 창이 열렸는지.
    /// 이벤트를 새로 뚫으면 전투 코드 곳곳에 튜토리얼만을 위한 줄이 생기고, 그 줄은
    /// 튜토리얼을 고칠 때마다 같이 움직여야 한다. 상태를 보는 쪽은 이 파일 하나로 끝난다.
    ///
    /// <b>지면 처음부터.</b> 어느 단계에서 기다리던 중이든 벽이 무너지면
    /// (<see cref="TutorialRunManager.IsFailed"/>) 그 자리에서 빠져나와 판을 다시 연다.
    /// 판 중간 저장이 없으므로(<see cref="TutorialStep"/> 주석) 이어서 할 자리도 없다.
    ///
    /// <b>코루틴이 아니라 UniTask 다</b>(AGENTS Stack). 씬이 내려가면 <c>GameManager</c> 의
    /// 파괴 토큰이 기다림을 끊는다.
    /// </summary>
    public sealed class TutorialDirector
    {
        private readonly IBattleRefs battle;

        private UITutorialOverlay overlay;
        private CancellationToken token;

        public TutorialStep Step { get; private set; }

        public TutorialDirector(IBattleRefs battle)
        {
            this.battle = battle;
        }

        /// <summary>지면 단계 기다림을 통째로 빠져나오는 데만 쓴다.</summary>
        private sealed class TutorialFailedException : Exception
        {
        }

        public async UniTaskVoid RunAsync(CancellationToken cancellationToken)
        {
            token = cancellationToken;

            try
            {
                // 한 프레임 미룬다. 보드는 자기 Start 에서 칸을 만드는데 Start 끼리의 순서는
                // 정해져 있지 않다 — 이 프레임에 칸 좌표를 물으면 아직 칸이 없다.
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                overlay = GameContainer.UI?.Get<UITutorialOverlay>();
                if (overlay == null)
                {
                    // 화면이 없으면 가르칠 방법이 없다. 판을 막아 세우는 대신 평범한 판으로
                    // 흘려보낸다 — 대본도 비어 있으니 그대로 본편 규칙이 된다. 못 연 사유는
                    // UIService 가 이미 로그로 남겼다(카탈로그 등재 누락이 대부분이다).
                    Debug.LogError("[튜토리얼] 오버레이를 열지 못해 튜토리얼 없이 진행한다. " +
                                   "OJ/개발/튜토리얼/UI 프리팹 굽기 → 다이얼로그 카탈로그 갱신을 확인할 것.");
                    return;
                }

                overlay.Enter();
                await RunStepsAsync();
            }
            catch (OperationCanceledException)
            {
                // 씬이 내려갔다. 할 일이 없다.
            }
            catch (TutorialFailedException)
            {
                await HandleFailureAsync();
            }
        }

        // ── 단계 ──────────────────────────────────────────────────────

        private async UniTask RunStepsAsync()
        {
            GameManager game = battle.Game;
            UIDiceSummonSystem summon = battle.Summon;
            RectTransform summonButton = RectOf(summon.summonButton);
            RectTransform playButton = RectOf(game.PlayUI);

            // 1웨이브 전 ─────────────────────────────────────────────
            Enter(TutorialStep.IntroGoal);
            overlay.SetDim();
            await TalkAsync("몬스터가 아래 벽까지 내려오면 져요.\n주사위를 놓아서 막아 봐요!");

            Enter(TutorialStep.Summon);
            battle.Tutorial.ScriptSummons(TutorialStage.OpeningSummons);
            Guide(summonButton, "SP로 주사위를 소환해요.\n소환 버튼을 눌러 SP를 모두 써 보세요!", dim: true);
            await Until(() => battle.Tutorial.PendingScriptedSummons == 0 && summon.currentSP < summon.summonCost);

            Enter(TutorialStep.Merge);
            battle.Tutorial.ForceNextMergeType(TutorialStage.MergeResult);
            RectTransform mergeA = battle.Board.GetSlotRect(TutorialStage.MergeSlotA);
            RectTransform mergeB = battle.Board.GetSlotRect(TutorialStage.MergeSlotB);
            overlay.SetDim(mergeA, mergeB);

            // 누르기가 아니라 끌기다. 손가락이 한 칸에서 옆 칸으로 끌고 가는 모양을 되풀이한다.
            overlay.SetDragFinger(mergeA, mergeB);
            overlay.SetTalk("같은 주사위 둘을 겹치면 합쳐져요.\n노멀 주사위를 옆 노멀 위로 끌어다 놓아요!");
            await Until(() => CountDice(TutorialStage.MergeResult, 2) > 0);

            Enter(TutorialStep.StartWave1);
            Guide(playButton, "번개 주사위가 됐어요! 합치면 별이 오르고 종류가 바뀌어요.\n이제 웨이브를 시작해요.", dim: true);
            await Until(() => game.inGameState == InGameState.Wave);

            Enter(TutorialStep.Wave1);
            overlay.ClearAll();
            await Until(() => game.inGameState == InGameState.Setting);

            Enter(TutorialStep.Wave1Reward);
            overlay.SetDim();
            await TalkAsync("웨이브를 막으면 SP를 받아요.\n받은 SP는 다음 웨이브 전에 쓸 수 있어요.");

            // (현상금 안내 단계 — BountyOpen · BountySelect · BountySet — 는 현상금이 실시간 소환으로 바뀌면서
            //  빠졌다. 튜토리얼은 진행방식이 굳은 뒤 다시 짠다.)
            Enter(TutorialStep.StartWave2);
            Guide(playButton, "이제 웨이브를 시작해요!", dim: true);
            await Until(() => game.inGameState == InGameState.Wave);

            // 2웨이브: 되돌리기 ──────────────────────────────────────
            Enter(TutorialStep.Wave2Crisis);
            overlay.ClearAll();
            await Until(() => battle.Spawner.RegularSpawnCount >= 3);

            Enter(TutorialStep.RewindTalk);
            Time.timeScale = 0f;
            battle.Rewind.NotifyRewindOffered();
            overlay.SetDim();
            await TalkAsync("잠깐! SP를 하나도 안 쓰고 시작했어요.\n이대로면 현상금 몬스터를 놓칠 거예요!");
            battle.Rewind.NotifyOfferClosed();

            Enter(TutorialStep.Rewind);

            // 되돌리기 버튼은 여기서 처음 나타난다. 앞 웨이브에서 보이면 미리 눌러 판에 하나뿐인
            // 무료 되돌리기를 써 버린다(GameManager.ChangeState 가 그때까지 감춘다).
            battle.Tutorial.UnlockRewind();
            GameContainer.UI?.Show<UIWaveRewindButton>();
            UIWaveRewindButton rewindButton = GameContainer.UI?.Get<UIWaveRewindButton>();
            UIConfirmDialog confirm = GameContainer.UI?.Get<UIConfirmDialog>();
            Guide(RectOf(rewindButton), "되돌리기를 쓰면 이번 웨이브를\n시작하기 직전으로 돌아가요.", dim: true);
            await Until(() => confirm != null && confirm.isEnter);

            Enter(TutorialStep.RewindConfirm);
            Guide(confirm.ConfirmButtonRect, "'되돌릴게요'를 눌러요.", dim: true);
            await Until(() => !confirm.isEnter);

            // 되감기 연출 동안은 딤드를 걷는다. 시간이 거꾸로 흐르는 장면이 이 단계의 볼거리인데
            // 어둠에 덮여 있으면 답답하기만 하다. 그 사이 상태는 None 이라 눌러도 될 것이 없다.
            overlay.ClearAll();
            await Until(() => game.inGameState == InGameState.Setting);

            Enter(TutorialStep.SpendSp);
            Guide(summonButton, "이번엔 SP를 모두 써서\n주사위를 늘려요!", dim: true);
            await Until(() => summon.currentSP < summon.summonCost);

            Enter(TutorialStep.StartWave2Retry);
            Guide(playButton, "다시 도전!", dim: true);
            await Until(() => game.inGameState == InGameState.Wave);

            // 2웨이브(재) ─────────────────────────────────────────────
            // 딜미터기 안내는 뺐다. 전투 중에 말풍선이 뜨는 것이 어색했다.
            overlay.ClearAll();

            Enter(TutorialStep.Wave2Retry);
            await Until(() => game.inGameState == InGameState.Setting);

            // 3웨이브 전: 속성강화 ─────────────────────────────────────
            // 진화 직전에 마석을 진화 비용까지 맞춰 주므로(GrantEvolveGift) 여기서 마석을 써도
            // 진화가 막히지 않는다. 이 시점 마석은 웨이브 두 번 클리어분(2개)이고 첫 강화는 1개다.
            await RunUpgradeAsync();

            // 3웨이브: 손 놓기 ───────────────────────────────────────
            await RunHintsAsync(game, summon, summonButton, playButton);

            Enter(TutorialStep.Wave3);
            overlay.ClearAll();
            await Until(() => game.inGameState == InGameState.Setting);

            // 4웨이브 전: 진화(변신) ─────────────────────────────────
            Enter(TutorialStep.EvolveGift);
            overlay.SetDim();
            await TalkAsync("보스가 다가와요!\n특별히 번개 4성과 마석을 줄게요.");

            // 말풍선을 닫는 순간에 준다. 미리 놓아 두면 "줄게요" 라는 말보다 선물이 먼저 와 있다.
            UIDice gift = GrantEvolveGift();
            if (gift != null)
            {
                overlay.SetDim(RectOf(gift));

                // 팝은 <c>UIBoard.SpawnDice</c> 가 이미 걸었다(<c>DiceFx.PlaySummon</c>).
                // 여기서 또 걸면 두 쪽이 같은 <c>localScale</c> 에 써서 서로를 덮는다 —
                // 기다리기만 한다. 뒤의 0.5초는 자리를 잡은 4성을 잠깐 보여 주는 시간이다
                // (연출이 끝나자마자 다음 말풍선이 뜨면 어색하다).
                float resume = Time.unscaledTime + DiceFx.SummonSeconds + 0.5f;
                await Until(() => Time.unscaledTime >= resume);
            }

            await TalkAsync("주사위가 4성이 되면\n마석으로 변신시킬 수 있어요.");

            UIBattleDiceDetailPanel detail = GameContainer.UI?.Get<UIBattleDiceDetailPanel>();
            if (gift != null && detail != null)
            {
                Enter(TutorialStep.EvolveSelect);
                Guide(RectOf(gift), "번개 4성을 눌러 보세요.", dim: true);
                await Until(() => detail.isEnter && detail.CurrentDice == gift);

                Enter(TutorialStep.EvolveConfirm);
                Guide(detail.EvolveButtonRect, "'진화'를 눌러 변신시켜요!", dim: true);
                await Until(() => gift == null || gift.Type == TutorialStage.EvolveTarget);

                Enter(TutorialStep.EvolveClose);
                overlay.ClearAll();
                overlay.SetTalk(
                    DiceName(TutorialStage.EvolveTarget) + "(으)로 변신했어요!\n바깥을 눌러 창을 닫아요.",
                    where: UITutorialOverlay.TalkPlacement.Top);
                await Until(() => !detail.isEnter);
            }
            else
            {
                Debug.LogError("[튜토리얼] 4성 번개를 놓지 못했거나 상세 패널이 없어 진화 단계를 건너뛴다.");
            }

            Enter(TutorialStep.StartWave4);
            Guide(playButton, "보스를 잡으러 가요!", dim: false);
            await Until(() => game.inGameState == InGameState.Wave);

            // 보스 ───────────────────────────────────────────────────
            Enter(TutorialStep.BossWave);
            overlay.ClearAll();
            await Until(() => battle.Tutorial.IsCleared);

            Enter(TutorialStep.Complete);
            TutorialProgressManager.Instance?.MarkInGameCompleted();
            Time.timeScale = 1f;
            overlay.SetDim();
            await TalkAsync("튜토리얼 완료!\n변신한 " + DiceName(TutorialStage.EvolveTarget) + "은(는) 이제 당신 거예요.");

            overlay.ClearAll();
            overlay.Exit();

            // 본편 스테이지를 깼을 때와 같은 결과창을 띄운다. 닫으면 로비로 간다.
            // 오버레이를 먼저 닫아야 한다 — 그 창은 매 프레임 맨 위로 올라와 결과창을 덮는다.
            //
            // <b>재화 보상은 없다.</b> 튜토리얼은 재화를 주지 않고, 지급하지 않은 것을 결과창에 그리면
            // 거짓말이 된다. 대신 실제로 준 Time 다이스 한 칸을 그린다(아래 AppendItemReward).
            UIStageResultDialog result = GameContainer.UI?.Get<UIStageResultDialog>();
            if (result == null)
            {
                SceneFlowManager.LoadLobby();
                return;
            }

            GameContainer.UI?.Hide<UIWaveRewardPreviewDialog>();

            int stageIndex = game.CurrentStageData != null ? game.CurrentStageData.stageIndex : 1;

            result.Open(
                isWin: true,
                stageIndex: stageIndex,
                reachedWaveCount: game.CurrentWaveIndex,
                totalWaves: TutorialStage.TotalWaves,
                // 튜토리얼은 스테이지 기록에 남지 않으므로 이번 판이 곧 최고 기록이다.
                bestWave: game.CurrentWaveIndex,
                rewards: new List<PointRewardEntry>(),
                onClose: SceneFlowManager.LoadLobby);

            // 튜토리얼 판의 StageData 는 성장식 때문에 stageIndex 가 1 이지만 1스테이지가 아니다.
            result.SetStageLabel("Tutorial");

            // 이 판의 보상은 진화로 만든 다이스다. 재화 칸 자리에 그 다이스를 그린다.
            result.AppendItemReward(DiceMetaDataProvider.GetIcon(TutorialStage.EvolveTarget), 1);
        }

        /// <summary>
        /// 3웨이브 앞의 손가락 가이드 셋. <b>강제가 아니다</b> — 언제든 웨이브를 시작하면
        /// 남은 안내를 건너뛴다. 손을 놓는 구간이라 막지도 기다리지도 않는다.
        /// </summary>
        private async UniTask RunHintsAsync(
            GameManager game, UIDiceSummonSystem summon,
            RectTransform summonButton, RectTransform playButton)
        {
            Enter(TutorialStep.SummonHint);
            if (game.inGameState == InGameState.Setting && summon.currentSP >= summon.summonCost)
            {
                int before = summon.currentSP;
                Guide(summonButton, "웨이브를 막을 때마다 SP가 쌓여요.\n소환해서 주사위를 늘려 봐요!", dim: false);
                await Until(() => summon.currentSP < before || game.inGameState == InGameState.Wave);
            }

            Enter(TutorialStep.StartWave3);
            if (game.inGameState == InGameState.Setting)
            {
                Guide(playButton, "준비되면 웨이브를 시작해요.", dim: false);
                await Until(() => game.inGameState == InGameState.Wave);
            }
        }

        /// <summary>
        /// 속성강화 한 번 — 강화 버튼 → 번개의 속성 줄 강화 → 창 닫기. 셋 다 딤드다.
        /// 고르는 속성은 머지로 만든 번개의 속성이다. 보드에서 가장 센 다이스라 효과가 바로 보이고,
        /// 뒤에서 진화시킬 4성도 같은 계통이다.
        /// </summary>
        private async UniTask RunUpgradeAsync()
        {
            ElementUpgradeManager upgrade = battle.ElementUpgrade;
            UIElementUpgradePanel panel = GameContainer.UI?.Get<UIElementUpgradePanel>();
            if (upgrade == null || panel == null || upgrade.OpenButtonRect == null)
            {
                Debug.LogError("[튜토리얼] 속성강화 버튼이나 창이 없어 강화 단계를 건너뛴다.");
                return;
            }

            ElementType element = ElementOf(TutorialStage.MergeResult);

            Enter(TutorialStep.UpgradeOpen);
            Guide(upgrade.OpenButtonRect, "웨이브를 막으면 마석도 받아요.\n마석으로 속성을 강화할 수 있어요!", dim: true);
            await Until(() => panel.isEnter);

            // 창이 막 열린 프레임에는 줄이 아직 없을 수 있다(UIElementUpgradePanel.BuildIfNeeded).
            await Until(() => panel.GetUpgradeButtonRect(element) != null);

            Enter(TutorialStep.UpgradeLevelUp);
            int before = upgrade.GetLevel(element);
            Guide(panel.GetUpgradeButtonRect(element), "번개 주사위의 속성을 강화해 봐요.", dim: true);
            await Until(() => upgrade.GetLevel(element) > before);

            Enter(TutorialStep.UpgradeClose);
            RectTransform close = panel.FindExitButtonRect("Exit");
            if (close != null)
            {
                Guide(close, "강화한 만큼 같은 속성 주사위가 모두 세져요.\n창을 닫아요.", dim: true);
            }
            else
            {
                overlay.ClearAll();
                overlay.SetTalk("강화한 만큼 같은 속성 주사위가 모두 세져요.\n창을 닫아요.", where: UITutorialOverlay.TalkPlacement.Top);
            }

            await Until(() => !panel.isEnter);
            overlay.ClearAll();
        }

        private static ElementType ElementOf(DiceType type)
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(type);
            return meta != null && meta.elementType != null && meta.elementType.Length > 0
                ? meta.elementType[0]
                : ElementType.Light;
        }

        private async UniTask HandleFailureAsync()
        {
            Enter(TutorialStep.Failed);

            try
            {
                Time.timeScale = 1f;
                overlay.ClearAll();
                overlay.SetDim();
                overlay.SetTalk("아쉬워요!\n처음부터 다시 해 봐요.", tapToContinue: true);
                await UniTask.WaitUntil(overlay.ConsumeTap, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            TutorialProgressManager.Instance?.RequestInGameRun();
            SceneFlowManager.LoadBattle();
        }

        // ── 진화 선물 ─────────────────────────────────────────────────

        /// <summary>
        /// 보스 앞의 선물 둘 — 마석, 4성 번개. 놓은 4성을 돌려준다(못 놓으면 null).
        ///
        /// <b>보유를 지급하던 줄이 사라졌다(2026-10-14).</b> <c>MergeSystem.TryEvolve</c> 가
        /// 대상 보유를 보는 것은 그대로지만, 진화 대상(<c>TutorialStage.EvolveTarget</c>)이
        /// 포함된 특수 5종이 <b>처음부터 보유</b>로 바뀌었다
        /// (<c>DiceEvolution.IsOwnedFromStart</c>). 줄 것이 없으니 지급도 환급도 없다.
        /// </summary>
        private UIDice GrantEvolveGift()
        {
            // 마석은 "맞춘다". 더하면 앞 웨이브 수급에 따라 진화 뒤 남는 양이 달라진다.
            // 이미 더 있으면 깎지 않는다 — 번 것을 튜토리얼이 가져가지 않는다.
            int cost = DiceEvolution.GetEvolveCost(TutorialStage.GiftDice);
            int stones = battle.BattlePoints.Get(BattlePointType.EnhanceStone);
            if (stones < cost)
                battle.BattlePoints.Set(BattlePointType.EnhanceStone, cost);

            int slot = FindGiftSlot();
            if (slot < 0)
                return null;

            battle.DiceStars.OnDiceSpawn(TutorialStage.GiftDice, MergeSystem.MaxStar);
            battle.Board.SpawnDice(TutorialStage.GiftDice, MergeSystem.MaxStar, slot);
            return battle.Board.GetDice(slot);
        }

        private int FindGiftSlot()
        {
            IReadOnlyList<int> candidates = TutorialStage.GiftSlotCandidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (battle.Board.GetDice(candidates[i]) == null)
                    return candidates[i];
            }

            int total = battle.Board.rows * battle.Board.cols;
            for (int i = 0; i < total; i++)
            {
                if (battle.Board.GetDice(i) == null)
                    return i;
            }

            return -1;
        }

        // ── 도우미 ────────────────────────────────────────────────────

        private void Enter(TutorialStep step)
        {
            Step = step;
            Debug.Log("[튜토리얼] " + step);
        }

        /// <summary>대상을 가리키고 말한다. <paramref name="dim"/> 이면 대상 밖을 막는다(딤드 가이드).</summary>
        private void Guide(RectTransform target, string text, bool dim)
        {
            if (target == null)
                Debug.LogError("[튜토리얼] " + Step + " 단계의 대상을 못 찾았다. 화면 전체를 막은 채 멈출 수 있다.");

            if (dim)
                overlay.SetDim(target);
            else
                overlay.ClearDim();

            overlay.SetFinger(target);
            overlay.SetTalk(text);
        }

        /// <summary>말풍선을 띄우고 화면을 한 번 누를 때까지 기다린다. 이미 깔린 딤드는 그대로 둔다.</summary>
        private async UniTask TalkAsync(string text)
        {
            overlay.SetFinger(null);
            overlay.SetTalk(text, tapToContinue: true);
            await Until(overlay.ConsumeTap);
            overlay.SetTalk(null);
        }

        private async UniTask Until(Func<bool> condition)
        {
            await UniTask.WaitUntil(
                () => battle.Tutorial.IsFailed || condition(),
                PlayerLoopTiming.Update,
                token);

            if (battle.Tutorial.IsFailed)
                throw new TutorialFailedException();
        }

        private int CountDice(DiceType type, int star)
        {
            UIDice[] map = battle.Board.diceMap;
            if (map == null)
                return 0;

            int count = 0;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] != null && map[i].Type == type && map[i].Star == star)
                    count++;
            }

            return count;
        }

        private static RectTransform RectOf(Component component)
        {
            if (component == null)
                return null;

            // 창(DialogBase)은 루트가 화면 전체로 늘어나 있는 경우가 많다. 보이는 것은 dialogView 다.
            if (component is DialogBase dialog && dialog.dialogView != null)
                return dialog.dialogView.transform as RectTransform;

            return component.transform as RectTransform;
        }

        private static string DiceName(DiceType type)
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(type);
            return meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : type.ToString();
        }
    }
}
