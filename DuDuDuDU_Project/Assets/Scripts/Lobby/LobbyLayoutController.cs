using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
        [SerializeField] private List<LobbyBottomBtn> bottomButtons;

        [Header("Tab Panels")]
        [SerializeField] private IDialog shopPanel;
        [SerializeField] private IDialog equipmentPanel;
        [SerializeField] private IDialog bulletPanel;
        [SerializeField] private IDialog helperPanel;

        [SerializeField] private LobbyTab defaultTab = LobbyTab.Home;

        private void Awake()
        {
            if (enterStageButton != null) enterStageButton.onClick.AddListener(OnClickEnterStage);
            if (bottomButtons != null)
            {
                for (int i = 0; i < bottomButtons.Count; i++)
                {
                    if (bottomButtons[i] == null) continue;
                    bottomButtons[i].Init(ShowTab);
                }
            }
        }

        private void OnEnable()
        {
            ShowTab(defaultTab);
        }

        private void OnDestroy()
        {
            if (enterStageButton != null) enterStageButton.onClick.RemoveListener(OnClickEnterStage);
        }

        public void OnClickEnterStage()
        {
            SceneFlowManager.LoadBattle();
        }

        public void ShowTab(LobbyTab tab)
        {
            if (shopPanel != null) shopPanel.SetActive(tab == LobbyTab.Shop);
            if (equipmentPanel != null) equipmentPanel.SetActive(tab == LobbyTab.Equipment);
            if (bulletPanel != null) bulletPanel.SetActive(tab == LobbyTab.Bullet);
            if (helperPanel != null) helperPanel.SetActive(tab == LobbyTab.Helper);

            if (bottomButtons != null)
            {
                for (int i = 0; i < bottomButtons.Count; i++)
                {
                    LobbyBottomBtn button = bottomButtons[i];
                    if (button == null) continue;
                    button.SetState(button._tab == tab);
                }
            }
        }
    }
}
