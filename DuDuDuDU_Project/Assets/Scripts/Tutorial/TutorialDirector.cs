using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using OJ.Bounty;
using OJ.Core;
using OJ.Economy;
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

            // 판 시작 전: 소환 · 머지 ─────────────────────────────────
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

            // 실시간 시작 ────────────────────────────────────────────
            // 웨이브 사이에 쉬는 시간이 없다. 시작 버튼도 없다 — 3 · 2 · 1 이 끝나면 몬스터가 내려온다.
            Enter(TutorialStep.StartWaves);
            overlay.SetDim();
            await TalkAsync("번개 주사위가 됐어요! 합치면 별이 오르고 종류가 바뀌어요.\n이제 몬스터가 쉬지 않고 몰려와요. 준비!");
            overlay.ClearAll();
            game.BeginTutorialWaves();
            await Until(() => game.inGameState == InGameState.Wave);

            // 몬스터가 내려오는 것이 보이도록 잠깐 둔다. 가르치는 동안에는 게임을 세워 둔다.
            await DelaySeconds(2f);

            // 1웨이브: 자리 구매 · 확률업 · 영혼 보너스 ───────────────────────
            await RunSlotUnlockAsync();
            await RunProbabilityAsync(game, summon);
            await RunSoulBonusAsync(game);

            // 2웨이브: 현상금 · 행운 주사위 · 속성강화 ─────────────────
            await Until(() => game.CurrentWaveIndex >= TutorialStage.BountyWave);
            await DelaySeconds(2f);

            await RunBountyAsync(game);
            await RunLuckyDiceAsync(game);
            await RunUpgradeAsync(game);

            // 3웨이브: 진화(변신) ─────────────────────────────────────
            await Until(() => game.CurrentWaveIndex >= TutorialStage.EvolveWave);
            await DelaySeconds(2f);

            await RunEvolveAsync(game);

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
        /// 자리 구매 — 첫 줄 밖의 칸은 잠겨 있다. 잠긴 칸을 눌러 SP 로 연다(여는 값은 구매마다 오른다).
        /// SP 가 모자라면 채워 준다.
        /// </summary>
        private async UniTask RunSlotUnlockAsync()
        {
            UIBoard board = battle.Board;
            RectTransform slot = board.GetSlotRect(TutorialStage.UnlockSlot);
            if (slot == null || !board.IsSlotLockActive || board.IsSlotUnlocked(TutorialStage.UnlockSlot))
            {
                Debug.LogError("[튜토리얼] 잠긴 칸을 못 찾아 자리 구매 단계를 건너뛴다.");
                return;
            }

            Enter(TutorialStep.SlotUnlock);
            EnsureSp(board.NextSlotUnlockCost);
            FreezeGame();

            Guide(slot, "자리가 모자라면 SP로 열 수 있어요.\n잠긴 칸을 눌러 열어 봐요!", dim: true);
            await Until(() => board.IsSlotUnlocked(TutorialStage.UnlockSlot));

            Enter(TutorialStep.SlotUnlocked);
            overlay.ClearAll();
            overlay.SetDim();
            await TalkAsync("자리를 열 때마다 다음 칸은 조금씩 비싸져요.");

            overlay.ClearAll();
            ThawGame();
        }

        /// <summary>
        /// 확률업 — 버튼을 누르고, 소환 버튼 위 확률 띠를 짚는다. SP 가 모자라면 채워 준다
        /// (가르치는 동안 SP 가 몇이냐는 플레이 솜씨에 달려 있어 단계가 막히면 안 된다).
        /// </summary>
        private async UniTask RunProbabilityAsync(GameManager game, UIDiceSummonSystem summon)
        {
            Enter(TutorialStep.ProbUp);
            GameContainer.UI?.Show<UIProbabilityUpButton>();
            GameContainer.UI?.Show<UISummonRateStrip>();
            UIProbabilityUpButton button = GameContainer.UI?.Get<UIProbabilityUpButton>();

            EnsureSp(summon.NextProbUpgradeCost);
            FreezeGame();

            int level = summon.ProbLevel;
            Guide(RectOf(button), "확률UP에 SP를 쓰면\n높은 성급의 주사위가 더 자주 나와요!", dim: true);
            await Until(() => summon.ProbLevel > level);

            Enter(TutorialStep.ProbRates);
            overlay.SetDim(RectOf(GameContainer.UI?.Get<UISummonRateStrip>()));
            await TalkAsync("소환 버튼 위에서 성급별 확률을 볼 수 있어요.");

            overlay.ClearAll();
            ThawGame();
        }

        /// <summary>
        /// 영혼 보너스 — 버튼을 짚어 "적을 잡으면 영혼이 모인다" 를 알리고, 목표를 채워 <b>팝업이 저절로 뜰 때까지</b>
        /// 게임을 돌려 두었다가(몬스터를 잡는 동안 영혼이 날아오는 것이 보인다), 팝업이 뜨면 두 버튼을 짚고 고르게 한다.
        /// 팝업은 게임을 스스로 멈추므로 이 단계는 <see cref="FreezeGame"/> 을 쓰지 않는다.
        /// </summary>
        private async UniTask RunSoulBonusAsync(GameManager game)
        {
            Enter(TutorialStep.SoulBonus);
            GameContainer.UI?.Show<UISoulBonusButton>();
            UISoulBonusButton button = GameContainer.UI?.Get<UISoulBonusButton>();

            FreezeGame();
            overlay.SetDim(RectOf(button));
            overlay.SetFinger(null);
            await TalkAsync("적을 잡으면 영혼이 저금통에 모여요!\n다 모으면 보상을 골라요.");
            overlay.ClearAll();
            ThawGame();

            // 목표(첫 10마리)를 채울 때까지 기다린다. 그 사이 몬스터가 내려오는 대로 평소처럼 진행된다.
            await Until(() => game.SoulBonus.IsReady);

            // 채웠다 — 저금통을 눌러 받게 한다(게임은 멈추지 않으니 잠깐 세운다).
            FreezeGame();
            Guide(RectOf(button), "영혼이 가득 찼어요!\n저금통을 눌러 보상을 받아요.", dim: true);
            await Until(() => UISoulBonusDialog.IsOpen);

            Enter(TutorialStep.SoulBonusReward);
            UISoulBonusDialog dialog = GameContainer.UI?.Get<UISoulBonusDialog>();
            overlay.SetFinger(null);
            overlay.SetDim(dialog != null ? dialog.GetChoiceRect(true) : null,
                           dialog != null ? dialog.GetChoiceRect(false) : null);
            overlay.SetTalk("SP와 마석 중 하나를 골라요!\n마석은 속성 강화와 진화에 쓰여요.",
                where: UITutorialOverlay.TalkPlacement.Top);

            await Until(() => !UISoulBonusDialog.IsOpen);
            overlay.ClearAll();
            ThawGame();
        }

        /// <summary>현상금 — 아이콘을 눌러 1단계를 고르면 그 자리에서 나온다. 쿨타임이 있다는 것까지 알린다.</summary>
        private async UniTask RunBountyAsync(GameManager game)
        {
            Enter(TutorialStep.BountyOpen);
            battle.Tutorial.UnlockBounty();
            GameContainer.UI?.Show<UIBountyButton>();
            UIBountyButton button = GameContainer.UI?.Get<UIBountyButton>();

            FreezeGame();
            Guide(RectOf(button), "현상금 아이콘을 눌러요.\n강한 몬스터를 불러서 보상을 받아요!", dim: true);
            await Until(() => UIBountySelectDialog.IsOpen);

            Enter(TutorialStep.BountySelect);
            UIBountySelectDialog dialog = GameContainer.UI?.Get<UIBountySelectDialog>();
            Guide(dialog != null ? dialog.GetSlotRect(1) : null, "1단계를 골라요.\n잡으면 보상을 바로 받아요!", dim: true);
            await Until(() => battle.Bounty.CooldownRemaining > 0f);

            Enter(TutorialStep.BountySet);
            overlay.ClearAll();
            overlay.SetDim();
            await TalkAsync("현상금 몬스터가 나왔어요!\n다음 현상금은 " + (int)BountyFormula.CooldownSeconds + "초 뒤에 다시 부를 수 있어요.");

            overlay.ClearAll();
            ThawGame();
        }

        /// <summary>
        /// 행운 주사위 — SP 를 내고 마석을 뽑는다. 굴리는 동안 "?" 는 막아 둔다(확률표 창은 딤드 밖이라
        /// 열리면 닫을 수 없다). 굴린 뒤에 확률표를 따로 짚는다.
        /// </summary>
        private async UniTask RunLuckyDiceAsync(GameManager game)
        {
            Enter(TutorialStep.LuckyDice);
            GameContainer.UI?.Show<UILuckyDiceButton>();
            UILuckyDiceButton button = GameContainer.UI?.Get<UILuckyDiceButton>();

            EnsureSp(LuckyDiceRules.CostSp);
            int stones = battle.BattlePoints.Get(BattlePointType.EnhanceStone);
            FreezeGame();

            button?.SetHelpInteractable(false);
            Guide(RectOf(button),
                "행운 주사위는 " + LuckyDiceRules.CostSp + " SP로\n마석 " + LuckyDiceRules.MinStones + "~" + LuckyDiceRules.MaxStones + "개를 뽑아요!",
                dim: true);
            await Until(() => battle.BattlePoints.Get(BattlePointType.EnhanceStone) > stones);

            Enter(TutorialStep.LuckyDiceResult);

            // 결과 연출(전체 화면)이 끝나기를 기다린다. 오버레이를 걷어 두어야 연출이 입력을 받고, 딤드에 안 가려진다.
            overlay.ClearAll();
            await Until(() => UILuckyDiceResultPopup.IsPlaying);
            await Until(() => !UILuckyDiceResultPopup.IsPlaying);

            overlay.SetDim();
            await TalkAsync("마석을 얻었어요!\n마석은 속성 강화와 진화에 쓰여요.");

            // 확률표. 판 부분만 구멍으로 열어 보여 주고, 탭하면 닫는다.
            button?.SetHelpInteractable(true);
            GameContainer.UI?.Show<UILuckyDiceOddsTooltip>();
            UILuckyDiceOddsTooltip tooltip = GameContainer.UI?.Get<UILuckyDiceOddsTooltip>();
            overlay.SetDim(tooltip != null ? tooltip.PanelRect : null);
            await TalkAsync("낮은 눈이 자주, 높은 눈은 드물게 나와요.\n옆의 ? 버튼으로 언제든 볼 수 있어요.");
            GameContainer.UI?.Hide<UILuckyDiceOddsTooltip>();

            overlay.ClearAll();
            ThawGame();
        }

        /// <summary>
        /// 3성 진화 — 선물받은 3성을 눌러 변신시킨다. 진화 조건이 3성이라 선물도 3성이다.
        /// 마석은 진화 비용까지 맞춰 주므로 앞에서 속성강화에 써도 막히지 않는다.
        /// </summary>
        private async UniTask RunEvolveAsync(GameManager game)
        {
            Enter(TutorialStep.EvolveGift);
            FreezeGame();
            overlay.SetDim();
            await TalkAsync("보스가 다가와요!\n특별히 번개 " + TutorialStage.GiftStar + "성과 마석을 줄게요.");

            // 말풍선을 닫는 순간에 준다. 미리 놓아 두면 "줄게요" 라는 말보다 선물이 먼저 와 있다.
            UIDice gift = GrantEvolveGift();
            if (gift != null)
            {
                overlay.SetDim(RectOf(gift));

                // 팝은 <c>UIBoard.SpawnDice</c> 가 이미 걸었다(<c>DiceFx.PlaySummon</c>).
                // 여기서 또 걸면 두 쪽이 같은 <c>localScale</c> 에 써서 서로를 덮는다 —
                // 기다리기만 한다. 뒤의 0.5초는 자리를 잡은 선물을 잠깐 보여 주는 시간이다
                // (연출이 끝나자마자 다음 말풍선이 뜨면 어색하다).
                float resume = Time.unscaledTime + DiceFx.SummonSeconds + 0.5f;
                await Until(() => Time.unscaledTime >= resume);
            }

            await TalkAsync("주사위가 " + TutorialStage.GiftStar + "성이 되면\n마석으로 변신시킬 수 있어요.");

            UIBattleDiceDetailPanel detail = GameContainer.UI?.Get<UIBattleDiceDetailPanel>();
            if (gift != null && detail != null)
            {
                Enter(TutorialStep.EvolveSelect);
                Guide(RectOf(gift), "번개 " + TutorialStage.GiftStar + "성을 눌러 보세요.", dim: true);
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
                Debug.LogError("[튜토리얼] 선물 다이스를 놓지 못했거나 상세 패널이 없어 진화 단계를 건너뛴다.");
            }

            overlay.ClearAll();
            ThawGame();
        }

        /// <summary>
        /// 속성강화 한 번 — 강화 버튼을 누르면 모든 속성이 곧바로 한 단계 오른다. 창은 없다.
        /// 마석이 첫 강화 값에 모자라면 그만큼만 채워 준다(많으면 깎지 않는다 — 번 것을 가져가지 않는다).
        /// 진화 선물이 진화 비용까지 마석을 다시 맞춰 주므로 여기서 쓴 만큼 뒤가 막히지 않는다.
        /// </summary>
        private async UniTask RunUpgradeAsync(GameManager game)
        {
            ElementUpgradeManager upgrade = battle.ElementUpgrade;
            if (upgrade == null || upgrade.UpgradeButtonRect == null)
            {
                Debug.LogError("[튜토리얼] 속성강화 버튼이 없어 강화 단계를 건너뛴다.");
                return;
            }

            int cost = upgrade.NextUpgradeCost;
            if (battle.BattlePoints.Get(BattlePointType.EnhanceStone) < cost)
                battle.BattlePoints.Set(BattlePointType.EnhanceStone, cost);

            FreezeGame();

            Enter(TutorialStep.UpgradePress);
            int before = upgrade.UpgradeCount;
            Guide(upgrade.UpgradeButtonRect,
                "마석으로 모든 속성을 한 번에 강화할 수 있어요!\n눌러 보세요.", dim: true);
            await Until(() => upgrade.UpgradeCount > before);

            // 버튼이 튕기고 "+10%" 가 뜨는 연출을 잠깐 보여 준 뒤 설명한다.
            overlay.ClearAll();
            await DelaySeconds(0.6f);

            Enter(TutorialStep.UpgradeDone);
            overlay.SetDim();
            await TalkAsync("모든 속성 주사위가 " + ElementUpgradeRules.BonusPercentPerUpgrade + "% 세졌어요!\n강화할수록 마석이 조금씩 더 들어요.");

            overlay.ClearAll();
            ThawGame();
        }

        // ── 게임 멈춤 ─────────────────────────────────────────────────

        /// <summary>
        /// 가르치는 동안 게임을 세운다. 실시간이라 몬스터는 기다려 주지 않는다 — 읽고 누르는 사이에
        /// 벽이 깎이면 안 된다. UI 와 이 디렉터는 실제 시간으로 돌아 영향이 없다.
        /// </summary>
        private void FreezeGame()
        {
            Time.timeScale = 0f;
        }

        private void ThawGame()
        {
            Time.timeScale = battle.Game.CurrentTimeSpeed;
        }

        /// <summary>SP 가 <paramref name="needed"/> 에 모자라면 채워 준다. 가르치는 단계가 솜씨에 따라 막히지 않게 한다.</summary>
        private void EnsureSp(int needed)
        {
            int missing = needed - battle.Summon.currentSP;
            if (missing > 0)
                battle.Summon.AddSP(missing);
        }

        private async UniTask DelaySeconds(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            await Until(() => Time.unscaledTime >= end);
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
        /// 보스 앞의 선물 둘 — 마석, 3성 번개. 놓은 4성을 돌려준다(못 놓으면 null).
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

            battle.DiceStars.OnDiceSpawn(TutorialStage.GiftDice, TutorialStage.GiftStar);
            battle.Board.SpawnDice(TutorialStage.GiftDice, TutorialStage.GiftStar, slot);
            return battle.Board.GetDice(slot);
        }

        private int FindGiftSlot()
        {
            // 잠긴 칸에는 놓을 수 없다(UIBoard.SpawnDice). 열린 빈 칸만 쓴다.
            IReadOnlyList<int> candidates = TutorialStage.GiftSlotCandidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (battle.Board.GetDice(candidates[i]) == null && battle.Board.IsSlotUnlocked(candidates[i]))
                    return candidates[i];
            }

            int total = battle.Board.rows * battle.Board.cols;
            for (int i = 0; i < total; i++)
            {
                if (battle.Board.GetDice(i) == null && battle.Board.IsSlotUnlocked(i))
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
