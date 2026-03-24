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

        [Header("Craft")]
        [SerializeField] private UIDiceCraftProgressDialog craftProgressDialog;
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
            RunHistoryManager.Instance?.EndRun(
                RunResultType.Fail,
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0);
            Debug.Log("Game Over!");
            StartCoroutine(CoReturnToLobby());
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
            CurrentStageData = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetSelectedStage()
                : StageDatabaseProvider.GetStage(1);

            if (CurrentStageData == null)
            {
                CurrentStageData = new StageData();
            }

            WallHp = CurrentStageData.wallHp;
            WaveMonsterCount = CurrentStageData.monstersPerWave;
            CurrentWaveIndex = 0;
            WaveMonsterDeadCount = 0;
            isGameOver = false;

            ElementUpgradeManager.Instance?.ResetRunState();
            wall.SetInit(WallHp);
            UIDiceSummonSystem.Instance?.SetStageStartSp(CurrentStageData.initialSP);
            RunHistoryManager.Instance?.StartRun(CurrentStageData, WallHp);
            UpdateWaveText();
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

            if (CurrentStageData != null)
                UIDiceSummonSystem.Instance?.AddSP(CurrentStageData.waveClearSP);

            RunHistoryManager.Instance?.RecordWaveComplete(
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0,
                UIDiceSummonSystem.Instance != null ? UIDiceSummonSystem.Instance.currentSP : 0);

            if (CurrentStageData != null && CurrentWaveIndex >= CurrentStageData.totalWaves)
            {
                ClearStage();
                return;
            }

            ChangeState(InGameState.Setting);
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

            List<StageRewardEntry> normalRewards = StageRewardCalculator.BuildNormalClearRewards(stageIndex);
            StageRewardCalculator.GrantRewards(normalRewards);

            StageRewardTierFlags newFlags = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.RecordStageClear(stageIndex, clearGrade)
                : StageRewardCalculator.GetRewardFlagsForGrade(clearGrade);

            List<StageRewardEntry> bonusRewards = StageRewardCalculator.BuildBonusRewards(stageIndex, newFlags);
            StageRewardCalculator.GrantRewards(bonusRewards);

            Debug.Log(
                $"Stage {stageIndex} Clear ({clearGrade}) | Normal: {StageRewardCalculator.BuildRewardSummary(normalRewards)} | Bonus: {StageRewardCalculator.BuildRewardSummary(bonusRewards)}");

            RunHistoryManager.Instance?.EndRun(
                RunResultType.Clear,
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0);

            StartCoroutine(CoReturnToLobby());
        }

        private IEnumerator CoReturnToLobby()
        {
            inGameState = InGameState.None;
            yield return new WaitForSecondsRealtime(returnToLobbyDelay);
            SceneFlowManager.LoadLobby();
        }

        public void OnApplicationQuit()
        {
            Application.Quit();
        }
    }

}
