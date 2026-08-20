using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace OJ
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        private bool isGameOver = false;

        public int WallHp;

        public Wall wall;

        public InGameState inGameState = InGameState.None;

        public int WaveMonsterCount = 20;
        public int WaveMonsterDeadCount = 0;
        public int CurrentWaveIndex { get; private set; } = 0;
        public StageData CurrentStageData { get; private set; }

        [Header("Stage Theme")]
        [SerializeField] private SpriteRenderer stageBackground;
        [Header("Craft")]
        [SerializeField] private UIDiceCraftProgressDialog craftProgressDialog;
        [Header("Reward Preview")]
        [SerializeField] private UIWaveRewardPreviewDialog waveRewardPreviewDialog;
        [Header("Result")]
        [SerializeField] private UIStageResultDialog stageResultDialog;
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
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            PlayUI.onClick.AddListener(OnClick_PlayUI);
            Pause.onClick.AddListener(OnClick_Pause);
            Speed.onClick.AddListener(OnClick_Speed);
        }

        private void OnDestroy()
        {
            if (PlayUI != null) PlayUI.onClick.RemoveListener(OnClick_PlayUI);
            if (Pause != null) Pause.onClick.RemoveListener(OnClick_Pause);
            if (Speed != null) Speed.onClick.RemoveListener(OnClick_Speed);

            if (Instance == this)
                Instance = null;
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
            craftProgressDialog?.SetActive(state == InGameState.Setting);
            RemainMonster?.gameObject.SetActive(state == InGameState.Wave);
            RemainMonsterGauge?.gameObject.SetActive(state == InGameState.Wave);
            WaveText?.gameObject.SetActive(state == InGameState.Wave || state == InGameState.Setting);


            if (state == InGameState.Wave)
            {
                isPause = false;
                CurrentWaveIndex++;
                RelicManager.Instance?.BeginWave(CurrentWaveIndex);
                WaveMonsterCount = GetWaveTargetCount();
                UpdateWaveText();
                SetRemainMonster(0);
                MonsterSpawner.Instance.PlayWave();
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

            WallHp = CurrentStageData.wallHp;
            WaveMonsterCount = CurrentStageData.monstersPerWave;
            CurrentWaveIndex = 0;
            WaveMonsterDeadCount = 0;
            isGameOver = false;

            ElementUpgradeManager.Instance?.ResetRunState();
            wall.SetInit(WallHp);
            int startSpBonus = RelicManager.Instance != null ? RelicManager.Instance.GetStageStartSpBonus() : 0;
            UIDiceSummonSystem.Instance?.SetStageStartSp(CurrentStageData.initialSP + startSpBonus);
            RunHistoryManager.Instance?.StartRun(CurrentStageData, WallHp);
            UpdateWaveText();
        }

        private void ApplyStageTheme()
        {
            StageThemeResource themeResource = StaticResource.Instance.GetStageThemeResource(CurrentStageData.theme);
            if (stageBackground != null && themeResource != null && themeResource.MapBackground != null)
                stageBackground.sprite = themeResource.MapBackground;

            MonsterSpawner.Instance?.ConfigureTheme(CurrentStageData.theme);
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
                UIDiceSummonSystem.Instance?.AddSP(CurrentStageData.waveClearSP);
                ShowWaveRewardPreview();
            }

            RunHistoryManager.Instance?.RecordWaveComplete(
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0,
                UIDiceSummonSystem.Instance != null ? UIDiceSummonSystem.Instance.currentSP : 0);

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
            if (waveRewardPreviewDialog == null || CurrentStageData == null)
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

            waveRewardPreviewDialog.ShowGoldGain(
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
            if (stageResultDialog == null)
            {
                StartCoroutine(CoReturnToLobby());
                return;
            }

            waveRewardPreviewDialog?.Exit();

            int bestStageIndex = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetHighestUnlockedStageIndex()
                : stageIndex;

            stageResultDialog.Open(
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
