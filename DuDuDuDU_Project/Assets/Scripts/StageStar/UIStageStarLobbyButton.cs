using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.UI;

namespace OJ.StageStar
{
    public class UIStageStarLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text totalStarText;
        [SerializeField] private GameObject redDot;

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
        private void OpenStageStar()
        {
            GameContainer.UI?.Show<UIStageStarDialog>();
        }
    }
}
