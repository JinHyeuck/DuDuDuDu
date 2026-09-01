using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using VContainer;
using OJ.Core;
using OJ.Analytics;
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
        private bool isPause = false;

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
            if (PlayUI != null) PlayUI.onClick.RemoveListener(OnClick_PlayUI);
            if (Pause != null) Pause.onClick.RemoveListener(OnClick_Pause);
            if (Speed != null) Speed.onClick.RemoveListener(OnClick_Speed);

        }

        private void Start()
        {
            InitializeStage();
            ChangeState(InGameState.Setting);
            StartCoroutine(CoApplyStageStartRelics());
        }

        public void OnClick_PlayUI()
        {
            ChangeState(InGameState.Wave);
        }

        public void OnClick_Pause()
        {
            StartCoroutine(CoReturnToLobby());
            return;

            if (isPause == false)
            {
                Time.timeScale = 0;
                isPause = true;
            }
            else
            {
                Time.timeScale = timeSpeed;
                isPause = false;
            }
        }

        public void OnClick_Speed()
        {
            if (isPause == true)
                return;

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
            Pause?.gameObject.SetActive(state == InGameState.Wave);
            Speed?.gameObject.SetActive(state == InGameState.Wave);
            RemainMonster?.gameObject.SetActive(state == InGameState.Wave);
            RemainMonsterGauge?.gameObject.SetActive(state == InGameState.Wave);
            WaveText?.gameObject.SetActive(state == InGameState.Wave || state == InGameState.Setting);

            // (10.4) 이 파일이 여는 팝업 셋은 예전에 씬 인스턴스였고 [SerializeField] 로 직접
            // 가리켰다. 그 참조가 None 이 되면 아무 로그 없이 창이 안 열렸다 — 실패가 조용했다.
            // UIService 는 못 열면 사유를 로그로 남긴다.
            // 닫을 때 Hide 인 이유는 이미 만들어 둔 것만 건드리기 때문이다 —
            // 닫으려고 프리팹을 새로 찍는 일이 없다.
            if (state == InGameState.Setting)
                GameContainer.UI?.Show<UIDiceCraftProgressDialog>();
            else
                GameContainer.UI?.Hide<UIDiceCraftProgressDialog>();

            if (state == InGameState.Wave)
            {
                isPause = false;
                Run.WaveIndex++;
                RelicManager.Instance?.BeginWave(CurrentWaveIndex);
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
                isPause = false;
                Time.timeScale = 1;
                UpdateWaveText();
            }
        }

        public void RemoveMonsterDeadCount()
        {
            if (inGameState != InGameState.Wave)
                return;
            WaveMonsterDeadCount++;

            if (WaveMonsterDeadCount >= WaveMonsterCount)
            {
                HandleWaveCompleted();
                return;
            }

            SetRemainMonster(WaveMonsterDeadCount);
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
            PointManager.Instance?.Add(PointType.Coin, 1);
            RelicManager.Instance?.ApplyWaveClearRelics(wall);

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
