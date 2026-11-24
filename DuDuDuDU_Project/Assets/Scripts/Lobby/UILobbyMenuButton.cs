using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Mission;

namespace OJ.Lobby
{
    /// <summary>
    /// 로비 왼쪽 위 메뉴 버튼. 누르면 <see cref="UILobbyMenuDialog"/> 를 연다.
    /// 메뉴 안에 받을 것이 있으면 빨간 점이 뜬다.
    /// </summary>
    public sealed class UILobbyMenuButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject redDot;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(Open);

            MissionManager missions = MissionManager.Instance;
            if (missions != null)
                missions.OnClaimableChanged += RefreshDot;

            RefreshDot();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Open);

            MissionManager missions = MissionManager.Instance;
            if (missions != null)
                missions.OnClaimableChanged -= RefreshDot;
        }

        private void RefreshDot()
        {
            if (redDot != null)
                redDot.SetActive(UILobbyMenuDialog.HasNews());
        }

        private void Open()
        {
            UILobbyMenuDialog dialog = GameContainer.UI?.Show<UILobbyMenuDialog>();
            if (dialog == null)
                Debug.LogError("[메뉴] 옆 메뉴를 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
        }
    }
}
