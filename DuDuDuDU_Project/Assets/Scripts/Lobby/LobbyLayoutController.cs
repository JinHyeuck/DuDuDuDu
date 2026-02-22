using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public enum LobbyTab
    {
        Home = 0,
        Shop,
        Equipment,
        Bullet,
        Helper
    }

    public class LobbyLayoutController : MonoBehaviour
    {
        [Header("Top / Middle")]
        [SerializeField] private Button enterStageButton;

        [Header("Bottom Buttons")]
        [SerializeField] private Button shopButton;
        [SerializeField] private Button equipmentButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button bulletButton;
        [SerializeField] private Button helperButton;

        [Header("Tab Panels")]
        [SerializeField] private IDialog shopPanel;
        [SerializeField] private IDialog equipmentPanel;
        [SerializeField] private IDialog bulletPanel;
        [SerializeField] private IDialog helperPanel;

        [SerializeField] private LobbyTab defaultTab = LobbyTab.Home;

        private void Awake()
        {
            if (enterStageButton != null) enterStageButton.onClick.AddListener(OnClickEnterStage);
            if (shopButton != null) shopButton.onClick.AddListener(OnClickShop);
            if (equipmentButton != null) equipmentButton.onClick.AddListener(OnClickEquipment);
            if (homeButton != null) homeButton.onClick.AddListener(OnClickHome);
            if (bulletButton != null) bulletButton.onClick.AddListener(OnClickBullet);
            if (helperButton != null) helperButton.onClick.AddListener(OnClickHelper);
        }

        private void OnEnable()
        {
            ShowTab(defaultTab);
        }

        private void OnDestroy()
        {
            if (enterStageButton != null) enterStageButton.onClick.RemoveListener(OnClickEnterStage);
            if (shopButton != null) shopButton.onClick.RemoveListener(OnClickShop);
            if (equipmentButton != null) equipmentButton.onClick.RemoveListener(OnClickEquipment);
            if (homeButton != null) homeButton.onClick.RemoveListener(OnClickHome);
            if (bulletButton != null) bulletButton.onClick.RemoveListener(OnClickBullet);
            if (helperButton != null) helperButton.onClick.RemoveListener(OnClickHelper);
        }

        public void OnClickEnterStage()
        {
            SceneFlowManager.LoadBattle();
        }

        private void OnClickShop() => ShowTab(LobbyTab.Shop);
        private void OnClickEquipment() => ShowTab(LobbyTab.Equipment);
        private void OnClickHome() => ShowTab(LobbyTab.Home);
        private void OnClickBullet() => ShowTab(LobbyTab.Bullet);
        private void OnClickHelper() => ShowTab(LobbyTab.Helper);

        public void ShowTab(LobbyTab tab)
        {
            if (shopPanel != null) shopPanel.SetActive(tab == LobbyTab.Shop);
            if (equipmentPanel != null) equipmentPanel.SetActive(tab == LobbyTab.Equipment);
            if (bulletPanel != null) bulletPanel.SetActive(tab == LobbyTab.Bullet);
            if (helperPanel != null) helperPanel.SetActive(tab == LobbyTab.Helper);
        }
    }
}
