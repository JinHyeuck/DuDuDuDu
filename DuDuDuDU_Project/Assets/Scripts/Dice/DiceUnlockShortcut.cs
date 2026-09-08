using UnityEngine;
using OJ.DI;
using OJ.Lobby;
using OJ.StageStar;
using OJ.Tower;

namespace OJ.Dice
{
    /// <summary>
    /// "획득 미션" 의 <b>진행</b> 버튼이 데려다 주는 곳.
    ///
    /// <b>왜 따로 두나.</b> 상세창은 다이스를 그리는 화면이지 로비를 아는 화면이 아니다.
    /// 세 컨텐츠로 가는 길을 거기 적으면 그 창이 로비 구조에 묶여, 다른 곳에서 못 띄운다.
    ///
    /// <b>여기서 열 수 있는 것만 연다.</b> 못 가는 경우(창이 카탈로그에 없거나 로비가 아님)
    /// 조용히 아무 일도 하지 않는 대신 사유를 남긴다 — 버튼이 먹통인 것과
    /// "여긴 갈 데가 없다" 는 화면상 구별이 안 되기 때문이다.
    /// </summary>
    public static class DiceUnlockShortcut
    {
        /// <summary>이 정의로 갈 수 있는 곳이 있는가. 버튼을 띄울지 정하는 데 쓴다.</summary>
        public static bool CanGo(DiceUnlockDefinition definition)
        {
            return definition != null && definition.HasContentSource;
        }

        /// <summary>
        /// 가장 가까운 획득처로 보낸다.
        ///
        /// 한 다이스에 경로가 둘 이상 붙어 있으면 <b>별 → 스테이지 → 탑</b> 순으로 고른다.
        /// 앞의 것일수록 초반에 닿는 컨텐츠라, 지금 당장 할 수 있는 쪽을 먼저 준다.
        /// </summary>
        public static void Go(DiceUnlockDefinition definition)
        {
            if (definition == null)
                return;

            if (definition.starRequirement > 0)
            {
                OpenStarTrial();
                return;
            }

            if (definition.stageRequirement > 0)
            {
                OpenStageSelect();
                return;
            }

            if (definition.towerFloor > 0)
                OpenTower();
        }

        private static void OpenStarTrial()
        {
            if (GameContainer.UI?.Show<UIStageStarDialog>() == null)
                LogMissing("별의 시련");
        }

        private static void OpenTower()
        {
            UITowerFloorSelectDialog dialog = GameContainer.UI?.Get<UITowerFloorSelectDialog>();
            if (dialog == null)
            {
                LogMissing("무한의 탑 층 선택");
                return;
            }

            dialog.Enter();
        }

        /// <summary>
        /// 스테이지는 창이 아니라 <b>로비의 홈 탭</b>이다.
        ///
        /// <b>그 스테이지를 골라 주지는 않는다.</b> 선택 인덱스는 로비가 해금 상한으로
        /// 세우는 값이고, 밖에서 밀어 넣으면 잠긴 스테이지를 고른 상태가 만들어질 수 있다.
        /// 여기서는 고르는 화면까지만 데려다 준다.
        ///
        /// <c>FindFirstObjectByType</c> 을 쓰는 것은 로비 컨트롤러가 씬에 사는
        /// 레이아웃이라 컨테이너에 등록돼 있지 않기 때문이다. 버튼 한 번에 한 번 도는
        /// 조회라 비용도 문제가 되지 않는다.
        /// </summary>
        private static void OpenStageSelect()
        {
            var lobby = Object.FindFirstObjectByType<LobbyLayoutController>();
            if (lobby == null)
            {
                LogMissing("로비 홈 탭");
                return;
            }

            lobby.ShowTab(LobbyTab.Home);
        }

        private static void LogMissing(string what)
        {
            Debug.LogError("[다이스 언락] " + what + " 으로 이동하지 못했다. " +
                           "OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌렸는지 볼 것.");
        }
    }
}
