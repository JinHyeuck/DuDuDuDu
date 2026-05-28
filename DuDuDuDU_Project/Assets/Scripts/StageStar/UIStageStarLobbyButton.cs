using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIStageStarLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text totalStarText;
        [SerializeField] private GameObject redDot;
        [SerializeField] private UIStageStarDialog stageStarDialog;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
                button.onClick.AddListener(OpenStageStar);
        }

        private void OnEnable()
        {
            if (StageStarManager.Instance != null)
                StageStarManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (StageStarManager.Instance != null)
                StageStarManager.Instance.OnChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OpenStageStar);
        }

        public void Refresh()
        {
            StageStarManager manager = StageStarManager.Instance;
            int totalStars = manager != null ? manager.GetTotalStarCount() : 0;

            if (totalStarText != null)
                totalStarText.SetText("x{0}", totalStars);

            if (redDot != null)
                redDot.SetActive(manager != null && manager.HasClaimableReward());
        }

        private void OpenStageStar()
        {
            ResolveDialogIfNeeded();
            if (stageStarDialog != null)
                stageStarDialog.Open();
        }

        private void ResolveDialogIfNeeded()
        {
            if (stageStarDialog != null)
                return;

#if UNITY_2023_1_OR_NEWER
            stageStarDialog = Object.FindFirstObjectByType<UIStageStarDialog>(FindObjectsInactive.Include);
#else
            stageStarDialog = Object.FindObjectOfType<UIStageStarDialog>(true);
#endif
        }
    }
}
