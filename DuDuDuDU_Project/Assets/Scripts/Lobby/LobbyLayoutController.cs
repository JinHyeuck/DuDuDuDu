using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Hunting;
using OJ.Relic;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.UI;
using OJ.Utils;

namespace OJ.Lobby
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
        [SerializeField] private TMP_Text selectedStageText;
        [SerializeField] private Image selectedStageImage;
        [SerializeField] private TMP_Text stageNameText;
        [SerializeField] private TMP_Text stageClearWaveText;
        [SerializeField] private TMP_Text stageSummaryText;

        [Header("Bottom Buttons")]
        [SerializeField] private List<LobbyBottomBtn> bottomButtons;

        [Header("Tab Panels")]

        /// <summary>
        /// 탭 내용물이 올라갈 자리. 페이지는 이제 카탈로그에서 만들어 여기 붙인다.
        ///
        /// <b>팝업 루트가 아니라 여기여야 한다.</b> 페이지는 화면 위에 떠야 하는 것이 아니라
        /// 로비 레이아웃의 <c>Content</c> 영역 <b>안에</b> 들어가야 한다. 팝업 루트에 붙이면
        /// 전체 화면을 덮는다.
        /// </summary>
        [SerializeField] private RectTransform pageRoot;

        [SerializeField] private LobbyTab defaultTab = LobbyTab.Home;

        private int selectedStageIndex = 1;

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
            selectedStageIndex = StageProgressManager.Instance != null ? StageProgressManager.Instance.GetHighestUnlockedStageIndex() : 1;
            ShowTab(defaultTab);
            RefreshStageUI();
        }

        private void OnDestroy()
        {
            if (enterStageButton != null) enterStageButton.onClick.RemoveListener(OnClickEnterStage);
        }

        public void OnClickEnterStage()
        {
            if (StageProgressManager.Instance != null)
            {
                if (!StageProgressManager.Instance.IsStageUnlocked(selectedStageIndex))
                    return;

                StageProgressManager.Instance.SelectStage(selectedStageIndex);
            }

            SceneFlowManager.LoadBattle();
        }

        // 여기 있던 OnClickPreviousStage / OnClickNextStage / SelectStage 를 지웠다.
        //
        // 로비에서 스테이지를 넘기는 버튼은 씬에 배선된 적이 없고(previousStageButton /
        // nextStageButton 이 None), 앞으로도 붙이지 않기로 정했다. 스테이지를 고르는 경로는
        // 별의 시련 하나다. 배선되지 않은 필드에 걸린 코드는 "있는데 안 도는" 상태로 남아
        // 읽는 사람마다 이게 기능인지 사고인지 다시 확인하게 만든다.
        //
        // selectedStageIndex 는 남는다 — OnEnable 이 해금 상한으로 세우고
        // OnClickEnterStage 와 RefreshStageUI 가 쓴다.

        /// <summary>
        /// 탭을 바꾼다. 페이지는 <see cref="UIService"/> 가 카탈로그에서 만들어 준다. (10.4)
        ///
        /// <b>예전에는 씬 참조와 자식 탐색 폴백이 섞여 있었다.</b> 넷 중 둘은 씬에 꽂혀
        /// 있었고, <c>equipmentPanel</c> 은 <b>꽂혀 있지 않은데</b>
        /// <c>GetComponentInChildren</c> 폴백 덕분에 우연히 동작하고 있었다.
        /// <c>shopPanel</c> 은 그 폴백조차 없어서 상점 탭은 아무것도 열지 않는다.
        /// 어느 쪽이든 배선이 맞는지 코드만 보고는 알 수 없었다.
        ///
        /// 상점 탭은 <b>지금도 비어 있다.</b> 페이지가 아직 없기 때문이고, 그 사실을
        /// 폴백으로 감추지 않는다.
        /// </summary>
        public void ShowTab(LobbyTab tab)
        {
            SetPageActive<UIEquipmentPage>(tab == LobbyTab.Equipment);
            SetPageActive<UIDiceGrowthPage>(tab == LobbyTab.Bullet);
            SetPageActive<UIRelicDialog>(tab == LobbyTab.Helper);

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

        private void RefreshStageUI()
        {
            StageData stageData = StageDatabaseProvider.GetStage(selectedStageIndex);
            bool isUnlocked = StageProgressManager.Instance == null || StageProgressManager.Instance.IsStageUnlocked(selectedStageIndex);
            int highestUnlockedStage = StageProgressManager.Instance != null ? StageProgressManager.Instance.GetHighestUnlockedStageIndex() : 1;
            StageClearGrade bestGrade = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetBestClearGrade(selectedStageIndex)
                : StageClearGrade.None;

            if (selectedStageImage != null)
            {
                Sprite stageSprite = StaticResource.Instance.GetStageBanner(
                    stageData != null ? stageData.theme : StageTheme.DarkForest);
                selectedStageImage.sprite = stageSprite;
            }

            if (selectedStageText != null)
            {
                selectedStageText.SetText(
                    isUnlocked
                        ? $"Stage {selectedStageIndex}"
                        : $"Stage {selectedStageIndex} Locked");
            }

            if (stageNameText != null)
                stageNameText.SetText(StageData.GetStageDisplayName(stageData.stageIndex));

            if (stageClearWaveText != null)
                stageClearWaveText.SetText($"{StageProgressManager.Instance.GetBestClearedWave(selectedStageIndex)}/{stageData.totalWaves}");

            if (stageSummaryText != null)
            {
                if (stageData == null)
                {
                    stageSummaryText.SetText("No Stage Data");
                }
                else
                {
                    stageSummaryText.SetText(
                        $"Wave {stageData.totalWaves} / Start SP {stageData.initialSP} / Wave SP {stageData.waveClearSP} / Best {bestGrade}");
                }
            }

            if (enterStageButton != null)
                enterStageButton.interactable = isUnlocked;
        }

        /// <summary>
        /// 페이지를 켜거나 끈다.
        ///
        /// <b>켤 때만 만든다.</b> 끄려고 인스턴스를 새로 찍으면 한 번도 안 연 탭까지
        /// 로비를 열자마자 전부 생성된다 — 씬에 상주하던 예전 방식과 같아져 버린다.
        /// </summary>
        /// <summary>
        /// 홈 탭으로 돌아간다. 열려 있던 페이지는 <see cref="ShowTab"/> 이 끈다.
        /// </summary>
        private void GoHome()
        {
            ShowTab(LobbyTab.Home);
        }

        private void SetPageActive<T>(bool active) where T : DialogBase
        {
            if (!active)
            {
                GameContainer.UI?.Hide<T>();
                return;
            }

            T page = GameContainer.UI?.Get<T>(pageRoot);
            if (page == null)
                return;


            // 백키는 페이지를 닫는 것이 아니라 홈 탭으로 돌아가야 한다.
            // 그냥 닫으면 탭 버튼은 눌린 채로 내용물만 사라져 빈 화면이 된다.
            page.BackKeyOverride = GoHome;
            page.Enter();
        }
    }
}
