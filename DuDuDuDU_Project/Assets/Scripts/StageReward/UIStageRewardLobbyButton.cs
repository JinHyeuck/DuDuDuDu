using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIStageRewardLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject redDot;
        [FormerlySerializedAs("chapterRewardDialog")]
        [SerializeField] private UIStageRewardDialog stageRewardDialog;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
                button.onClick.AddListener(OpenStageReward);
        }

        private void OnEnable()
        {
            if (StageRewardManager.Instance != null)
                StageRewardManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (StageRewardManager.Instance != null)
                StageRewardManager.Instance.OnChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OpenStageReward);
        }

        public void Refresh()
        {
            StageRewardManager manager = StageRewardManager.Instance;
            int current = manager != null ? manager.GetProgressIndex() : 0;
            int total = manager != null ? manager.GetTotalCount() : 0;

            if (progressText != null)
                progressText.SetText("스테이지 {0}/{1}", current, total);

            if (redDot != null)
                redDot.SetActive(manager != null && manager.HasClaimableReward());
        }

        private void OpenStageReward()
        {
            ResolveDialogIfNeeded();
            if (stageRewardDialog != null)
                stageRewardDialog.Open();
        }

        private void ResolveDialogIfNeeded()
        {
            if (stageRewardDialog != null)
                return;

#if UNITY_2023_1_OR_NEWER
            stageRewardDialog = Object.FindFirstObjectByType<UIStageRewardDialog>(FindObjectsInactive.Include);
#else
            stageRewardDialog = Object.FindObjectOfType<UIStageRewardDialog>(true);
#endif
        }
    }
}
