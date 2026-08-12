using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIIdleRewardLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject redDot;

        private UIIdleRewardDialog dialog;
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

        private void Open()
        {
            if (dialog == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null)
                    return;

                dialog = UIIdleRewardDialog.Create(canvas.rootCanvas.transform);
            }

            dialog.Open();
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
