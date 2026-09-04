using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;

namespace OJ.IdleReward
{
    public class UIIdleRewardLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject redDot;

        private float nextRefreshTime;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

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
            bool canClaim = manager != null &&
                (manager.CanClaimAutoBattle() || manager.GetStoredMeatSetCount() > 0);

            if (redDot != null)
                redDot.SetActive(canClaim);

            if (progressText != null && manager != null)
            {
                int meatSets = manager.GetStoredMeatSetCount();
                progressText.SetText(meatSets > 0 ? "고기 {0}세트" : FormatTime(manager.GetAutoBattleElapsed()));
            }
        }

        private static string FormatTime(System.TimeSpan time)
        {
            int totalHours = Mathf.FloorToInt((float)time.TotalHours);
            return string.Format("{0:00}:{1:00}:{2:00}", totalHours, time.Minutes, time.Seconds);
        }
    }
}
