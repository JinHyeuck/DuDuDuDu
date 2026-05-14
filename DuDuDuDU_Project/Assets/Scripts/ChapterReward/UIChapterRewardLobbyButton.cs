using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIChapterRewardLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject redDot;
        [SerializeField] private UIChapterRewardDialog chapterRewardDialog;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
                button.onClick.AddListener(OpenChapterReward);
        }

        private void OnEnable()
        {
            if (ChapterRewardManager.Instance != null)
                ChapterRewardManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (ChapterRewardManager.Instance != null)
                ChapterRewardManager.Instance.OnChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OpenChapterReward);
        }

        public void Refresh()
        {
            ChapterRewardManager manager = ChapterRewardManager.Instance;
            int current = manager != null ? manager.GetProgressIndex() : 0;
            int total = manager != null ? manager.GetTotalCount() : 0;

            if (progressText != null)
                progressText.SetText("챕터 {0}/{1}", current, total);

            if (redDot != null)
                redDot.SetActive(manager != null && manager.HasClaimableReward());
        }

        private void OpenChapterReward()
        {
            ResolveDialogIfNeeded();
            if (chapterRewardDialog != null)
                chapterRewardDialog.Open();
        }

        private void ResolveDialogIfNeeded()
        {
            if (chapterRewardDialog != null)
                return;

#if UNITY_2023_1_OR_NEWER
            chapterRewardDialog = Object.FindFirstObjectByType<UIChapterRewardDialog>(FindObjectsInactive.Include);
#else
            chapterRewardDialog = Object.FindObjectOfType<UIChapterRewardDialog>(true);
#endif
        }
    }
}
