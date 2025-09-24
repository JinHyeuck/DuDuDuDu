using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        public Button PlayUI;
        public Button Pause;
        public Button Speed;
        public TMP_Text SpeedText;
        public TMP_Text RemainMonster;

        private bool isPause = false;

        private float timeSpeed = 1.0f;

        void Awake() 
        {
            PlayUI.onClick.AddListener(OnClick_PlayUI);
            Pause.onClick.AddListener(OnClick_Pause);
            Speed.onClick.AddListener(OnClick_Speed);

            Instance = this; 
        }

        private void Start()
        {
            wall.SetInit(WallHp);
            ChangeState(InGameState.Setting);
        }

        public void OnClick_PlayUI()
        {
            ChangeState(InGameState.Wave);
        }

        public void OnClick_Pause()
        {
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
            if(isPause == true) 
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
            Pause?.gameObject.SetActive(state == InGameState.Wave);
            Speed?.gameObject.SetActive(state == InGameState.Wave);

            RemainMonster?.gameObject.SetActive(state == InGameState.Wave);

            if (state == InGameState.Wave)
            {
                isPause = false;
                SetRemainMonster(0);
                MonsterSpawner.Instance.PlayWave();
                Time.timeScale = timeSpeed;
                WaveMonsterDeadCount = 0;
                SetSpeedText();
            }
            else
            {
                isPause = false;
                Time.timeScale = 1;
            }
        }

        public void RemoveMonsterDeadCount()
        {
            if (inGameState != InGameState.Wave)
                return;
            WaveMonsterDeadCount++;

            if (WaveMonsterDeadCount >= WaveMonsterCount)
                ChangeState(InGameState.Setting);

            SetRemainMonster(WaveMonsterDeadCount);
        }

        public void SetRemainMonster(int currentKillMonster)
        {
            RemainMonster?.SetText(string.Format("({0}/{1})", currentKillMonster, WaveMonsterCount));
        }

        public void GameOver()
        {
            if (isGameOver) return;
            isGameOver = true;
            Debug.Log("Game Over!");
        }
    }

}
