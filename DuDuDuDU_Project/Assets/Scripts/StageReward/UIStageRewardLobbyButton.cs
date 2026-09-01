using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.UI;

namespace OJ.StageReward
{
    public class UIStageRewardLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject redDot;

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

        /// <summary>
        /// 카탈로그에서 꺼내 띄운다. (10.4)
        ///
        /// 예전에는 씬 인스턴스를 <c>[SerializeField]</c> 로 직접 가리켰고, 그 참조가 비면
        /// <c>FindFirstObjectByType</c> 으로 씬을 뒤져 때웠다. 두 경로 다 <b>실패가 조용하다</b> —
        /// 참조가 <c>None</c> 이면 아무 로그 없이 창이 안 열리고, 탐색이 실패해도 마찬가지다.
        /// 게다가 그 탐색은 씬에 인스턴스가 상주해야만 성립하는 방식이라, 팝업을 필요할 때
        /// 만드는 구조와 양립하지 않는다.
        ///
        /// <see cref="UIService"/> 는 못 열면 사유를 로그로 남긴다.
        /// </summary>
        private void OpenStageReward()
        {
            GameContainer.UI?.Show<UIStageRewardDialog>();
        }
    }
}
