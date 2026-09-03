using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using VContainer;
using OJ.Core;
using OJ.Analytics;
using OJ.Bounty;
using OJ.DI;
using OJ.Dice;
using OJ.Element;
using OJ.Point;
using OJ.Relic;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.UI;
using OJ.Utils;

namespace OJ.Hunting
{
    public class GameManager : MonoBehaviour
    {
        /// <summary>
        /// (8.3b) BattleScene 매니저들로 가는 창구. 배틀 스코프는 씬의 모든 <c>Awake</c> 뒤,
        /// 모든 <c>Start</c> 앞에 빌드되므로 <b><c>Awake</c> 에서는 아직 null 이고
        /// <c>Start</c> 이후로는 절대 null 이 아니다.</b> 그래서 아래 호출들에는
        /// <c>?.</c> 를 쓰지 않는다 — 여기서 null 이면 그것은 사고이고 울어야 한다.
        /// </summary>
        [Inject] private IBattleRefs battle;

        private bool isGameOver { get => Run.IsGameOver; set => Run.IsGameOver = value; }

        public int WallHp => Run.WallHp;

        public Wall wall;

        public InGameState inGameState = InGameState.None;

        public int WaveMonsterCount => Run.WaveMonsterCount;
        public int WaveMonsterDeadCount { get => Run.WaveMonsterDeadCount; set => Run.WaveMonsterDeadCount = value; }
        /// <summary>
        /// 이 판의 상태. (6.1) 예전에는 벽 HP·웨이브·몬스터 수가 각각 public 필드였고
        /// 씬에도 직렬화돼 있었다. 그런데 <c>InitializeStage</c> 가 매번 스테이지 데이터로
        /// 덮어써서 씬 값은 죽은 값이었다 — 인스펙터에서 고쳐도 아무 일도 일어나지 않았다.
        ///
        /// 이제 소유자가 하나다. 판을 리셋하려면 <c>Run.BeginRun</c> 한 번이면 되고,
        /// 필드를 늘려도 리셋을 빠뜨릴 수 없다.
        /// </summary>
        public RunState Run { get; } = new RunState();

        public int CurrentWaveIndex => Run.WaveIndex;
        public StageData CurrentStageData { get; private set; }

        [Header("Stage Theme")]
        [SerializeField] private SpriteRenderer stageBackground;
        public Button PlayUI;
        public Image PlayUI_Field;
        public Button Pause;
        public Button Speed;
        public TMP_Text SpeedText;
        public TMP_Text WaveText;
        public TMP_Text RemainMonster;
        public RectTransform RemainMonsterGauge;
        public float RemainMonsterGauge_Width = 705.0f;

        private float timeSpeed = 1.0f;
        [SerializeField] private float returnToLobbyDelay = 1.0f;

        void Awake()
        {
            PlayUI.onClick.AddListener(OnClick_PlayUI);
            Pause.onClick.AddListener(OnClick_Pause);
            Speed.onClick.AddListener(OnClick_Speed);
        }

        private void OnDestroy()
        {
            // 창구는 스코프가 파괴될 때 비워지는데 그 순서가 정해져 있지 않다.
            // 이미 비었으면 뗄 것도 없으므로 ?. 를 쓴다 — 여기는 사고가 아니라 정리 경로다.
            if (battle != null && battle.Bounty != null)
                battle.Bounty.OnWaveResolved -= OnBountyResolved;

            if (PlayUI != null) PlayUI.onClick.RemoveListener(OnClick_PlayUI);
            if (Pause != null) Pause.onClick.RemoveListener(OnClick_Pause);
            if (Speed != null) Speed.onClick.RemoveListener(OnClick_Speed);

        }

        private void Start()
        {
            // 배틀 스코프는 모든 Start 앞에 빌드되므로 여기서 battle.Bounty 는 살아 있다.
            // 구독을 Awake 로 올리면 그때는 아직 null 이다.
            battle.Bounty.OnWaveResolved += OnBountyResolved;

            InitializeStage();
            ChangeState(InGameState.Setting);
            StartCoroutine(CoApplyStageStartRelics());
        }

        /// <summary>
        /// 현상금이 정리됐다. <b>일반 몬스터를 먼저 다 잡은 웨이브</b>에서는 이것이
        /// 웨이브를 끝내는 마지막 조각이다 — 그 순서에서는 아무도
        /// <see cref="RemoveMonsterDeadCount"/> 를 다시 부르지 않기 때문이다.
        /// </summary>
        private void OnBountyResolved()
        {
            TryCompleteWave();
        }

        public void OnClick_PlayUI()
        {
            ChangeState(InGameState.Wave);
        }

        /// <summary>
        /// 전투를 그만둘지 묻는다. <b>버튼 자체는 아무것도 끝내지 않는다.</b>
        ///
        /// 예전에는 누르는 즉시 <c>CoReturnToLobby()</c> 로 로비에 나갔다 — 확인도 없고
        /// 보상도 없었다. 오조작 한 번에 판이 통째로 날아가는 자리였다.
        ///
        /// 확인을 받으면 <b>패배와 같은 경로</b>(<see cref="GameOver"/>)를 탄다. 새 종료 경로를
        /// 만들지 않는 것이 중요하다 — 보상 계산·기록·결과창이 전부 거기 모여 있고,
        /// 갈래를 늘리면 한쪽만 고치는 사고가 난다.
        /// </summary>
        public void OnClick_Pause()
        {
            UIConfirmDialog confirm = GameContainer.UI?.Get<UIConfirmDialog>();
            if (confirm == null)
            {
                // 창을 못 열었는데 조용히 넘어가면 버튼이 죽은 것처럼 보인다.
                // 그렇다고 확인 없이 판을 끝낼 수는 없으므로 여기서 멈춘다.
                Debug.LogError("[전투] 확인 창을 열지 못했다. 카탈로그에 UIConfirmDialog 가 있는지 볼 것.");
                return;
            }

            confirm.Open(
                "전투를 마칠까요?",
                "지금까지 클리어한 웨이브만큼 보상을 받고 나가요." + Environment.NewLine +
                "사용한 스태미나는 돌아오지 않아요.",
                "여기까지 할게요",
                "더 해볼게요",
                GameOver);
        }

        public void OnClick_Speed()
        {
            if (timeSpeed == 1)
                timeSpeed = 2;
            else if (timeSpeed == 2)
                timeSpeed = 3;
            else
                timeSpeed = 1;

            Time.timeScale = timeSpeed;

            SetSpeedText();
        }

        private void SetSpeedText()
        {
            SpeedText?.SetText(string.Format("x{0:0.#}", timeSpeed));
        }

        public void ChangeState(InGameState state)
        {
            inGameState = state;

            PlayUI?.gameObject.SetActive(state == InGameState.Setting);
            PlayUI_Field?.gameObject.SetActive(state == InGameState.Setting);
            // 관리 단계에서도 나갈 수 있어야 한다. 웨이브 사이에 그만두려는 사람이
            // 다음 웨이브를 억지로 시작해야 했던 것이 예전 동작이다.
            Pause?.gameObject.SetActive(state == InGameState.Wave || state == InGameState.Setting);
            Speed?.gameObject.SetActive(state == InGameState.Wave);
            RemainMonster?.gameObject.SetActive(state == InGameState.Wave);
            RemainMonsterGauge?.gameObject.SetActive(state == InGameState.Wave);
            WaveText?.gameObject.SetActive(state == InGameState.Wave || state == InGameState.Setting);

            // 현상금 띠는 관리 단계에만 뜬다. 웨이브 중에는 바꿀 수도 없고 몬스터가
            // 내려오는 길 한가운데를 가린다.
            //
            // <b>Show/Hide 를 여기 두는 이유.</b> 상태를 아는 곳이 여기 하나뿐이다.
            // 띠가 스스로 판단하게 하면 매 프레임 inGameState 를 들여다보게 되고,
            // 그것은 이벤트로 바꿔 놓은 것을 다시 폴링으로 되돌리는 일이다.
            if (state == InGameState.Setting)
            {
                GameContainer.UI?.Show<UIBountyBanner>();
            }
            else
            {
                GameContainer.UI?.Hide<UIBountyBanner>();
                // 선택 창이 열린 채 웨이브가 시작되는 경로는 지금 없지만(창이 화면을
                // 덮어 시작 버튼을 누를 수 없다) 닫아 두는 편이 싸다.
                GameContainer.UI?.Hide<UIBountySelectDialog>();
            }

            // 관리 단계마다 자동으로 뜨던 조합 진행도 창(UIDiceCraftProgressDialog)은
            // 조합식과 함께 사라졌다. 상위 다이스로 가는 길은 이제 목록을 띄워 재고를
            // 세는 것이 아니라, 다이스를 눌러 그 자리에서 진화시키는 것이다 —
            // UIBattleDiceDetailPanel 참조.

            if (state == InGameState.Wave)
            {
                Run.WaveIndex++;
                RelicManager.Instance?.BeginWave(CurrentWaveIndex);
                // 스포너가 첫 Update 를 돌기 전에 이번 웨이브의 현상금 등급을 확정한다.
                // PlayWave 뒤로 미루면 그 사이에 ShouldSpawn 이 지난 웨이브 값을 답한다.
                battle.Bounty.BeginWave(CurrentWaveIndex);
                Run.WaveMonsterCount = GetWaveTargetCount();
                UpdateWaveText();
                SetRemainMonster(0);
                battle.Spawner.PlayWave();
                Time.timeScale = timeSpeed;
                WaveMonsterDeadCount = 0;
                SetSpeedText();
                RunHistoryManager.Instance?.RecordWaveStart(
                    CurrentWaveIndex,
                    wall != null ? wall.CurrentHp : 0,
                    GetCurrentWaveMonsterHp(),
                    GetCurrentWaveMonsterDefense());
            }
            else
            {
                Time.timeScale = 1;
                UpdateWaveText();
            }
        }

        public void RemoveMonsterDeadCount()
        {
            if (inGameState != InGameState.Wave)
                return;
            WaveMonsterDeadCount++;

            if (TryCompleteWave())
                return;

            SetRemainMonster(WaveMonsterDeadCount);
        }

        /// <summary>
        /// 웨이브를 끝낼 수 있으면 끝낸다. <b>조건이 둘</b>이고 <b>계기도 둘</b>이라
        /// 판정을 한 곳에 모은다 — 마지막 일반 몬스터가 죽었을 때와, 현상금이 나중에
        /// 정리됐을 때. 두 곳에 같은 조건을 적으면 한쪽만 고치는 사고가 난다.
        ///
        /// <b>현상금은 처치 수에 세지 않지만 웨이브를 붙잡는다.</b> 카운트에 넣으면
        /// 못 잡았을 때 목표를 채울 방법이 없어져 웨이브가 영영 안 끝나고, 아예 조건에서
        /// 빼면 현상금이 화면에 남은 채로 다음 관리 단계가 열린다.
        /// </summary>
        private bool TryCompleteWave()
        {
            if (inGameState != InGameState.Wave)
                return false;

            if (WaveMonsterDeadCount < WaveMonsterCount)
                return false;

            if (!battle.Bounty.IsWaveResolved)
            {
                // 일반 몬스터는 다 잡았고 현상금만 남았다. 게이지는 가득 찬 채로 두고
                // 현상금이 벽에 닿거나 죽기를 기다린다 — 면역 덕에 반드시 그중 하나로 끝난다.
                SetRemainMonster(WaveMonsterDeadCount);
                return false;
            }

            HandleWaveCompleted();
            return true;
        }

        public void SetRemainMonster(int currentKillMonster)
        {
            RemainMonster?.SetText(string.Format("{0}/{1}", currentKillMonster, WaveMonsterCount));

            float ratio = (float)currentKillMonster / (float)WaveMonsterCount;
            if (ratio < 0)
                ratio = 0;

            Vector2 vector2 = RemainMonsterGauge.sizeDelta;
            vector2.x = RemainMonsterGauge_Width * ratio;
            RemainMonsterGauge.sizeDelta = vector2;
        }

        public void GameOver()
        {
            if (isGameOver) return;
            isGameOver = true;
            inGameState = InGameState.None;
            RelicManager.Instance?.EndWave();

            int stageIndex = CurrentStageData != null ? CurrentStageData.stageIndex : 1;
            int totalWaves = CurrentStageData != null ? Mathf.Max(1, CurrentStageData.totalWaves) : 1;
            int clearedWaves = Mathf.Clamp(CurrentWaveIndex - 1, 0, totalWaves);
            float rewardRatio = (float)clearedWaves / totalWaves;

            List<PointRewardEntry> partialRewards = StageRewardCalculator.ScaleRewards(
                StageRewardCalculator.BuildNormalClearRewards(stageIndex),
                rewardRatio);
            PointRewardUtility.GrantRewards(partialRewards);

            RunHistoryManager.Instance?.EndRun(
                RunResultType.Fail,
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0);
            Debug.Log(
                $"Stage {stageIndex} Failed | Cleared Waves: {clearedWaves}/{totalWaves} | Ratio: {rewardRatio:0.##} | Partial Normal: {PointRewardUtility.BuildRewardSummary(partialRewards)}");
            ShowStageResult(false, stageIndex, clearedWaves, partialRewards);
        }

        public int GetCurrentWaveMonsterHp()
        {
            if (CurrentStageData == null)
                return 1;

            return CurrentStageData.GetMonsterHpForWave(CurrentWaveIndex);
        }

        public int GetCurrentWaveMonsterDefense()
        {
            if (CurrentStageData == null)
                return 0;

            return CurrentStageData.GetMonsterDefenseForWave(CurrentWaveIndex);
        }

        public int GetCurrentWaveBossHp()
        {
            if (CurrentStageData == null)
                return 1;

            return CurrentStageData.GetBossHpForWave(CurrentWaveIndex);
        }

        public int GetCurrentWaveBossDefense()
        {
            if (CurrentStageData == null)
                return 0;

            return CurrentStageData.GetBossDefenseForWave(CurrentWaveIndex);
        }

        public float GetCurrentWaveBossScale()
        {
            if (CurrentStageData == null)
                return 1f;

            return CurrentStageData.bossScaleMultiplier;
        }

        public bool IsBossWave()
        {
            return CurrentStageData != null && CurrentWaveIndex >= CurrentStageData.totalWaves;
        }

        private void InitializeStage()
        {
            RelicManager.Instance?.BeginStageRun();
            CurrentStageData = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetSelectedStage()
                : StageDatabaseProvider.GetStage(1);

            if (CurrentStageData == null)
            {
                CurrentStageData = new StageData();
            }

            ApplyStageTheme();

            // 런 상태를 한 번에 세운다. 예전에는 이 다섯 줄이 각각 다른 필드를 건드렸고,
            // 하나만 빠뜨리면 이전 판 값이 새어 나갔다.
            Run.BeginRun(
                seed: Environment.TickCount,
                stageIndex: CurrentStageData.stageIndex,
                wallMaxHp: CurrentStageData.wallHp,
                monstersPerWave: CurrentStageData.monstersPerWave,
                initialSummonPoint: 0,
                initialSummonCost: 0);

            battle.ElementUpgrade.ResetRunState();
            // 웨이브 범위 상태는 Run.BeginRun 이 못 지운다 — 매니저 안에 있기 때문이다.
            // 지금은 배틀 스코프가 씬마다 새로 만들어 줘서 우연히 깨끗하지만,
            // 씬을 다시 로드하지 않고 판을 다시 시작하게 되는 날 그 우연이 깨진다.
            battle.Bounty.ResetWaveState();
            wall.SetInit(WallHp);
            int startSpBonus = RelicManager.Instance != null ? RelicManager.Instance.GetStageStartSpBonus() : 0;
            battle.Summon.SetStageStartSp(CurrentStageData.initialSP + startSpBonus);
            RunHistoryManager.Instance?.StartRun(CurrentStageData, WallHp);
            UpdateWaveText();
        }

        private void ApplyStageTheme()
        {
            // 여기서 터지면 InitializeStage() 가 중간에 끊겨 WallHp·시작 SP·웨이브 수가
            // 통째로 설정되지 않고, ChangeState(Setting) 도 실행되지 않아 플레이 버튼이
            // 살아나지 않는다. 배경 하나 때문에 스테이지 초기화를 잃을 이유가 없다.
            // StaticResource 가 없다는 사실 자체는 MonoSingleton 이 이미 크게 운다.
            StaticResource resource = StaticResource.Instance;
            StageThemeResource themeResource = resource != null
                ? resource.GetStageThemeResource(CurrentStageData.theme)
                : null;
            if (stageBackground != null && themeResource != null && themeResource.MapBackground != null)
                stageBackground.sprite = themeResource.MapBackground;

            battle.Spawner.ConfigureTheme(CurrentStageData.theme);
        }

        private IEnumerator CoApplyStageStartRelics()
        {
            yield return null;
            RelicManager.Instance?.TryApplyStageStartDice();
        }

        private int GetWaveTargetCount()
        {
            if (CurrentStageData == null)
                return WaveMonsterCount;

            int targetCount = CurrentStageData.monstersPerWave;
            if (IsBossWave())
                targetCount += 1;

            return Mathf.Max(1, targetCount);
        }

        private void HandleWaveCompleted()
        {
            PointManager.Instance?.Add(PointType.BattleEnhanceStone, 1);
            RelicManager.Instance?.ApplyWaveClearRelics(wall);

            // 현상금 보상은 웨이브가 끝나는 이 자리에서만 들어온다. 잡은 순간 주면
            // 전투 중에 SP 로 소환이 되어 "관리 단계에 쓰라"는 뜻이 무너진다.
            battle.Bounty.GrantPendingRewards();

            if (CurrentStageData != null)
            {
                battle.Summon.AddSP(CurrentStageData.waveClearSP);
                ShowWaveRewardPreview();
            }

            RunHistoryManager.Instance?.RecordWaveComplete(
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0,
                battle.Summon.currentSP);

            if (CurrentStageData != null)
                StageProgressManager.Instance?.RecordClearedWave(CurrentStageData.stageIndex, CurrentWaveIndex);

            if (CurrentStageData != null && CurrentWaveIndex >= CurrentStageData.totalWaves)
            {
                RelicManager.Instance?.EndWave();
                ClearStage();
                return;
            }

            RelicManager.Instance?.EndWave();
            ChangeState(InGameState.Setting);
        }

        private void ShowWaveRewardPreview()
        {
            if (CurrentStageData == null)
                return;

            // ShowGoldGain 이 안에서 Enter 까지 부르므로 Show 가 아니라 Get 으로 받는다.
            UIWaveRewardPreviewDialog previewDialog = GameContainer.UI?.Get<UIWaveRewardPreviewDialog>();
            if (previewDialog == null)
                return;

            int totalWaves = Mathf.Max(1, CurrentStageData.totalWaves);
            int currentAccumulatedGold = StageRewardCalculator.GetAccumulatedGuaranteedGold(
                CurrentStageData.stageIndex,
                CurrentWaveIndex,
                totalWaves);
            int previousAccumulatedGold = StageRewardCalculator.GetAccumulatedGuaranteedGold(
                CurrentStageData.stageIndex,
                CurrentWaveIndex - 1,
                totalWaves);
            int gainedGold = currentAccumulatedGold - previousAccumulatedGold;

            previewDialog.ShowGoldGain(
                gainedGold,
                currentAccumulatedGold,
                StageRewardCalculator.GetGuaranteedNormalGold(CurrentStageData.stageIndex));
        }

        private void UpdateWaveText()
        {
            if (WaveText == null)
                return;

            int totalWaves = CurrentStageData != null ? Mathf.Max(1, CurrentStageData.totalWaves) : 1;
            int currentWave = Mathf.Clamp(CurrentWaveIndex, 0, totalWaves);
            WaveText.SetText("Wave {0}/{1}", currentWave, totalWaves);
        }

        private void ClearStage()
        {
            if (isGameOver)
                return;

            isGameOver = true;
            inGameState = InGameState.None;

            int stageIndex = CurrentStageData != null ? CurrentStageData.stageIndex : 1;
            StageClearGrade clearGrade = StageRewardCalculator.GetClearGrade(wall.CurrentHp, wall.TotalHp);

            List<PointRewardEntry> normalRewards = StageRewardCalculator.BuildNormalClearRewards(stageIndex);
            if (RelicManager.Instance != null)
                normalRewards = RelicManager.Instance.ApplyStageClearRewardBonus(normalRewards);
            PointRewardUtility.GrantRewards(normalRewards);

            StageProgressManager.Instance?.RecordStageClear(stageIndex, clearGrade);

            Debug.Log(
                $"Stage {stageIndex} Clear ({clearGrade}) | Normal: {PointRewardUtility.BuildRewardSummary(normalRewards)}");

            RunHistoryManager.Instance?.EndRun(
                RunResultType.Clear,
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0);

            var resultRewards = new List<PointRewardEntry>(normalRewards.Count);
            resultRewards.AddRange(normalRewards);
            ShowStageResult(true, stageIndex, CurrentWaveIndex, resultRewards);
        }

        private IEnumerator CoReturnToLobby()
        {
            inGameState = InGameState.None;
            yield return new WaitForSecondsRealtime(returnToLobbyDelay);
            SceneFlowManager.LoadLobby();
        }

        private void ShowStageResult(bool isWin, int stageIndex, int reachedWaveCount, IReadOnlyList<PointRewardEntry> rewards)
        {
            // Open 이 값을 채우고 Enter 까지 부르므로 Get 으로 받는다.
            UIStageResultDialog resultDialog = GameContainer.UI?.Get<UIStageResultDialog>();
            if (resultDialog == null)
            {
                // 결과창을 못 띄워도 로비로는 돌아가야 한다. 여기서 멈추면 이미 끝난 판의
                // 전투 씬에 갇힌다. 못 연 사유는 UIService 가 이미 로그로 남겼다.
                StartCoroutine(CoReturnToLobby());
                return;
            }

            GameContainer.UI?.Hide<UIWaveRewardPreviewDialog>();

            int bestStageIndex = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetHighestUnlockedStageIndex()
                : stageIndex;

            resultDialog.Open(
                isWin,
                stageIndex,
                reachedWaveCount,
                bestStageIndex,
                rewards,
                SceneFlowManager.LoadLobby);
        }

        public void OnApplicationQuit()
        {
            Application.Quit();
        }
    }

}
