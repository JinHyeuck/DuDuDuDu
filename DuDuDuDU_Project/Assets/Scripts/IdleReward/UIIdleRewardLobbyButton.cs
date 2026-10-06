using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;

namespace OJ.IdleReward
{
    /// <summary>
    /// 로비의 자동 전투(방치 보상) 입구 — 돼지 아이콘. 아래 글자는 <b>누적된 자동 전투 시간</b>이다
    /// (창의 "누적 HH:MM:SS" 와 같은 값, 한도에서 멈춘다).
    ///
    /// 고기 축제는 로비 위젯(<c>UIMeatFestivalLobbyButton</c>)으로 빠졌으므로 여기서는 보지 않는다 —
    /// 예전처럼 "고기 n세트" 를 적거나 빨간 점을 켜면 두 입구가 같은 것을 두 번 알린다.
    /// </summary>
    public class UIIdleRewardLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("돼지 아래 \"00:00:00\" 글자. 비어 있으면 Awake 에서 자식 중 그 글자를 찾아 잇는다.")]
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject redDot;

        private float nextRefreshTime;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            // 씬에서 이 칸이 비어 있었다 — 그래서 로비 글자가 "00:00:00" 에 멈춰 있었다.
            // 씬 연결이 다시 끊겨도 같은 사고가 안 나게, 비어 있으면 시간 형식의 자식 글자를 찾는다.
            if (progressText == null)
                progressText = FindTimeLabel();

            if (button != null)
                button.onClick.AddListener(Open);
        }

        private void OnEnable()
        {
            if (IdleRewardManager.Instance != null)
                IdleRewardManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + 1f;
            Refresh();
        }

        private void OnDisable()
        {
            if (IdleRewardManager.Instance != null)
                IdleRewardManager.Instance.OnChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(Open);
        }

        /// <summary>
        /// 카탈로그에서 꺼내 띄운다. (10.5)
        ///
        /// 예전에는 이 버튼이 창을 <b>직접 지었다</b> — 부모 캔버스를 찾고, 코드로 계층을
        /// 만들고, 폰트를 씬에서 주워 왔다. 캔버스를 못 찾으면 조용히 반환해서
        /// 버튼이 먹통이 되고 아무 로그도 남지 않았다.
        /// </summary>
        private void Open()
        {
            GameContainer.UI?.Show<UIIdleRewardDialog>();
        }

        private void Refresh()
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            bool canClaim = manager != null && manager.CanClaimAutoBattle();

            if (redDot != null)
                redDot.SetActive(canClaim);

            if (progressText != null && manager != null)
                progressText.SetText(FormatTime(manager.GetAutoBattleElapsed()));
        }

        private TMP_Text FindTimeLabel()
        {
            foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text != null && label.text.Contains(":"))
                    return label;
            }

            return null;
        }

        private static string FormatTime(System.TimeSpan time)
        {
            int totalHours = Mathf.FloorToInt((float)time.TotalHours);
            return string.Format("{0:00}:{1:00}:{2:00}", totalHours, time.Minutes, time.Seconds);
        }
    }
}
