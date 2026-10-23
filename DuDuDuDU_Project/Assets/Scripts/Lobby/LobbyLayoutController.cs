using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using OJ.ContentUnlock;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Hunting;
using OJ.Relic;
using OJ.SceneFlow;
using OJ.Shop;
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
        /// <summary>
        /// 지금 열려 있는 하단 탭. 해금 가이드가 "유저가 홈으로 돌아왔는가" 를 보는 데 쓴다
        /// (<c>LobbyUnlockDirector</c>) — 가이드로 연 화면을 닫고 나와야 다음 가이드를 건다.
        /// 로비 씬이 새로 뜰 때마다 홈으로 되돌린다.
        /// </summary>
        public static LobbyTab CurrentTab { get; private set; } = LobbyTab.Home;

        [Header("Top / Middle")]
        [SerializeField] private Button enterStageButton;
        [SerializeField] private TMP_Text selectedStageText;
        [SerializeField] private Image selectedStageImage;
        [SerializeField] private TMP_Text stageNameText;
        [SerializeField] private TMP_Text stageClearWaveText;
        [SerializeField] private TMP_Text stageSummaryText;

        [Tooltip("입장 버튼의 \"x5\" 글자. 멤버십이면 \"무료\" 로 바뀐다. MembershipAssetBuilder 가 잇는다.")]
        [SerializeField] private TMP_Text enterCostText;

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

        /// <summary>스테이지 입장 버튼. 해금 가이드가 가이드 뒤에 손가락으로 가리킨다(<c>LobbyUnlockDirector</c>).</summary>
        public Button EnterStageButton => enterStageButton;

        private int selectedStageIndex = 1;

        private void Awake()
        {
            CurrentTab = LobbyTab.Home;

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
            // 멤버십을 사거나 만료되면 입장료와 버튼 상태가 바뀐다.
            if (OJ.Save.EntitlementManager.Instance != null)
                OJ.Save.EntitlementManager.Instance.OnChanged += RefreshStageUI;

            // 고기(스태미나)가 들어오면 입장 버튼을 다시 살린다. 예전에는 로비를 다시 열기 전까지
            // 꺼진 채로 남아, 고기를 받고도 들어갈 수 없는 것처럼 보였다(방치 보상·상점·우편 등).
            if (OJ.Point.PointManager.Instance != null)
                OJ.Point.PointManager.Instance.OnPointChanged += OnPointChanged;

            selectedStageIndex = StageProgressManager.Instance != null ? StageProgressManager.Instance.GetHighestUnlockedStageIndex() : 1;
            ShowTab(defaultTab);
            RefreshStageUI();
        }

        private void OnDisable()
        {
            if (OJ.Save.EntitlementManager.Instance != null)
                OJ.Save.EntitlementManager.Instance.OnChanged -= RefreshStageUI;

            if (OJ.Point.PointManager.Instance != null)
                OJ.Point.PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            // 다른 재화가 바뀔 때마다 스테이지 칸 전체를 다시 그릴 이유는 없다.
            if (pointType == PointType.Stamina)
                RefreshStageUI();
        }

        /// <summary>
        /// 지금 입장료. 고기 멤버십이면 0 이다(기획서 BMDesign 5.2-2).
        /// <b>표시와 지불이 이 한 함수를 본다</b> — 둘이 따로 계산하면 "무료" 라고 적고 5 를 뺀다.
        /// </summary>
        private static int CurrentEntryCost()
        {
            OJ.Save.EntitlementManager entitlements = OJ.Save.EntitlementManager.Instance;
            return entitlements != null
                ? entitlements.StageEntryCost(StageEntryRules.StaminaCost)
                : StageEntryRules.StaminaCost;
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
            }

            // 입장료를 <b>씬을 띄우기 전에</b> 뺀다. 전환이 시작된 뒤에 빼면 모자랄 때
            // 되돌릴 방법이 없다 — 이미 전투에 들어가 있다.
            //
            // <b>SelectStage 보다도 앞이다.</b> 고기가 모자라 못 들어가는데 선택만
            // 바뀌면, 다음에 로비를 열었을 때 고르지도 않은 스테이지가 선택돼 있다.
            OJ.Point.PointManager points = OJ.Point.PointManager.Instance;
            int cost = CurrentEntryCost();
            if (points == null || (cost > 0 && !points.TrySpend(PointType.Stamina, cost)))
            {
                Debug.Log("[스테이지] 고기가 모자라 입장하지 못했다. 필요 " +
                          cost +
                          " / 보유 " + (points != null ? points.Get(PointType.Stamina) : 0));
                RefreshStageUI();
                return;
            }

            StageProgressManager.Instance?.SelectStage(selectedStageIndex);

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
        /// 상점 탭은 <c>UIShopPage</c> 가 받는다(기획서 7장). 카탈로그에 등재되지 않았으면
        /// <see cref="UIService"/> 가 사유를 로그로 남긴다 — 예전처럼 조용히 빈 화면이 되지 않는다.
        /// </summary>
        public void ShowTab(LobbyTab tab)
        {
            // 아직 안 열린 탭은 넘어가지 않고 언제 열리는지만 알린다(Docs/ContentUnlock.md).
            if (TryExplainLockedTab(tab))
                return;

            CurrentTab = tab;

            SetPageActive<UIShopPage>(tab == LobbyTab.Shop);
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

        /// <summary>
        /// 잠긴 탭이면 안내 창을 띄우고 true. 로비 밖(문지기 없음)이나 표에 없는 탭은 열린 것으로 본다.
        /// </summary>
        private static bool TryExplainLockedTab(LobbyTab tab)
        {
            LobbyContentGate gate = LobbyContentGate.Current;
            if (gate == null || !LobbyContentGate.TryGetContentOfTab(tab, out OJ.Core.ContentId id) || gate.IsAccessible(id))
                return false;

            OJ.Core.ContentUnlockDefinition definition = ContentUnlockManager.Instance?.GetDefinition(id);
            string name = definition != null ? definition.displayName : tab.ToString();
            string message = definition != null && definition.requiredWave > 0
                ? "1스테이지 " + definition.requiredWave + "웨이브를 클리어하면 열려요."
                : definition != null && definition.requiredClear > 0
                    ? definition.requiredClear + "스테이지를 클리어하면 열려요."
                    : "곧 열려요.";

            UIConfirmDialog dialog = GameContainer.UI?.Get<UIConfirmDialog>();
            if (dialog != null)
                dialog.Open(name, message, "확인", null, null);

            return true;
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

                    // 탑이 뒤처지면 들어가기 전에 알린다. 전투에서 갑자기 아래 줄이 막혀 있는 것보다 낫다.
                    int towerCleared = OJ.Tower.TowerProgressManager.Instance != null
                        ? OJ.Tower.TowerProgressManager.Instance.HighestClearedFloor
                        : 0;
                    if (TowerGateRules.IsBottomRowLocked(selectedStageIndex, towerCleared))
                    {
                        int required = TowerGateRules.RequiredTowerFloor(selectedStageIndex);
                        stageSummaryText.SetText(
                            stageSummaryText.text + $"\n<color=#FFD24D>탑 {required}층 필요 · 현재 {towerCleared}층 (미달 시 맨 아래 줄 잠김)</color>");
                    }
                }
            }

            // 해금됐어도 고기가 모자라면 못 들어간다. 버튼을 살려 두면 눌렀을 때
            // 아무 일도 안 일어나는 자리가 되고, 그건 고장으로 읽힌다.
            int ownedStamina = OJ.Point.PointManager.Instance != null
                ? OJ.Point.PointManager.Instance.Get(PointType.Stamina)
                : 0;

            int cost = CurrentEntryCost();

            if (enterStageButton != null)
                enterStageButton.interactable = isUnlocked && ownedStamina >= cost;

            if (enterCostText != null)
                enterCostText.SetText(cost > 0 ? "x" + cost : "무료");
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
