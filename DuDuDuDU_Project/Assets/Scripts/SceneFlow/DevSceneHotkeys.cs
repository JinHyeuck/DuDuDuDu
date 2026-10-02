#if UNITY_EDITOR || DEV_DEFINE
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Hunting;
using OJ.IdleReward;
using OJ.Point;
using OJ.Relic;
using OJ.Save;
using OJ.Stage;
using OJ.StageReward;
using OJ.Utils;

namespace OJ.SceneFlow
{
    /// <summary>
    /// 개발용 씬 전환·진단 핫키. 에디터와 DEV_DEFINE 빌드에서만 존재한다.
    ///
    /// 왜 필요한가: 지금 Battle 에서 로비로 가는 유일한 길이 정지 버튼(OnClick_Pause)이고,
    /// 그 버튼은 InGameState.Wave 일 때만 켜진다. 즉 <b>웨이브를 시작하지 못하면 로비로
    /// 돌아갈 방법이 없어</b> 플레이를 멈췄다 다시 켜야 한다. 리팩토링 중에는 그 왕복이
    /// 잦으므로 씬 전환을 상태와 무관하게 뚫어 둔다.
    ///
    ///   F1 / F2 / F3   Title / Lobby / Battle
    ///   F4             시간 2시간 당기기 (고기 1세트 · 자동전투 2회). Shift 면 24시간
    ///   F5             현재 씬 다시 로드
    ///   F6             배선 진단 덤프 (StaticResource 와 데이터베이스 6종)
    ///   F7             계산식 골든 기준선 뜨기 (3단계)
    ///   F8             클릭 대상 추적 토글 (버튼이 안 눌릴 때)
    ///   F9             자가 진단 — 컨테이너·서비스·DB·저장 왕복을 한 번에 찍는다
    ///   F10            세이브 대조 — 매니저 실제 값과 통합 세이브에 담길 값을 비교 (8.7)
    ///   F11            미션 기록 전부 지우기. Shift 면 오늘치만(자정 흉내)
    ///
    /// 9단계에서 SceneRouter 가 생기면 전환은 그쪽을 타도록 바꾼다.
    /// </summary>
    internal sealed class DevSceneHotkeys : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject(nameof(DevSceneHotkeys));
            go.AddComponent<DevSceneHotkeys>();
            DontDestroyOnLoad(go);
            Debug.Log("[Dev] 씬 핫키: F1 Title / F2 Lobby / F3 Battle / F4 시간당기기 / F5 재로드 / " +
                      "F6 배선 진단 / F11 미션 초기화(Shift=오늘치만)");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                SceneFlowManager.LoadTitle();
            else if (Input.GetKeyDown(KeyCode.F2))
                SceneFlowManager.LoadLobby();
            else if (Input.GetKeyDown(KeyCode.F3))
                SceneFlowManager.LoadBattle();
            else if (Input.GetKeyDown(KeyCode.F4))
                AdvanceIdleTime();
            else if (Input.GetKeyDown(KeyCode.F5))
                SceneFlowManager.Reload();
            else if (Input.GetKeyDown(KeyCode.F6))
                DumpWiring();
            else if (Input.GetKeyDown(KeyCode.F7))
                GoldenBaselineDumper.Dump();
            else if (Input.GetKeyDown(KeyCode.F8))
                ToggleClickTrace();
            else if (Input.GetKeyDown(KeyCode.F9))
                SelfCheck.Run();
            else if (Input.GetKeyDown(KeyCode.F10))
                SaveVerifier.Run();
            else if (Input.GetKeyDown(KeyCode.F11))
                ResetMissions();

            if (traceClicks && Input.GetMouseButtonDown(0))
                TraceClick();
        }

        // --- 미션 초기화 (F11) ---------------------------------------------------------

        /// <summary>
        /// 미션·업적 기록을 지운다. Shift 를 누르면 오늘치만 지워 <b>자정이 지난 것</b>을 흉내 낸다.
        ///
        /// <b>세이브 전체를 지우지 않는다</b>(그건 F 키가 아니라 메뉴의 "세이브 전부 지우기" 다).
        /// 재화와 스테이지 진행도는 그대로 두고 미션 조각만 비운다.
        /// </summary>
        private static void ResetMissions()
        {
            bool todayOnly = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (todayOnly)
                OJ.Mission.MissionCheat.ResetToday();
            else
                OJ.Mission.MissionCheat.ResetAll();
        }

        // --- 시간 당기기 (F4) ----------------------------------------------------------

        /// <summary>
        /// 방치 보상 타이머를 과거로 밀어 시간이 흐른 것처럼 만든다.
        ///
        /// <b>기기 시계를 돌리지 않는다.</b> 시계를 돌리면 상점 일일 리셋·출석 같은 다른
        /// 날짜 판정까지 같이 움직여서 무엇이 왜 바뀌었는지 알 수 없게 된다.
        ///
        /// 기본 2시간인 것은 그것이 <b>고기 한 세트</b>이기 때문이다
        /// (<c>IdleRewardManager.MeatSetIntervalSeconds</c>). 한 번 누를 때마다 한 세트씩
        /// 늘어나는 편이 "얼마나 당겼는지"를 세기 쉽다. Shift 를 누르면 하루치다.
        /// </summary>
        private static void AdvanceIdleTime()
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[Dev] IdleRewardManager 가 없다. 로비에서 눌러야 한다.");
                return;
            }

            bool big = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            // <b>System.TimeSpan 으로 적는다.</b> 이 파일은 using System 을 일부러 빼 두었다 —
            // 넣으면 System.Object 와 UnityEngine.Object 가 부딪혀 아래 Describe 가 깨진다.
            System.TimeSpan amount = big
                ? System.TimeSpan.FromHours(24d)
                : System.TimeSpan.FromSeconds(IdleRewardManager.MeatSetIntervalSeconds);

            manager.DevAdvanceTime(amount);

            int sets = manager.GetStoredMeatSetCount();
            int meat = sets * IdleRewardManager.MeatPerSet;
            Debug.Log(
                $"[Dev] 시간 {amount.TotalHours:0.#}시간 당겼다 | " +
                $"고기 {sets}/{IdleRewardManager.MaxMeatSetCount}세트 ({meat}개, 소탕 {meat / 5}회분) | " +
                $"자동전투 누적 {manager.GetAutoBattleElapsed().TotalHours:0.#}시간");
        }

        // --- 클릭 대상 추적 (F8) --------------------------------------------------------
        //
        // "버튼이 보이는데 눌러도 아무 일이 없다"를 코드만 읽어서 가리는 것은 거의 불가능하다.
        // 원인 후보가 uGUI 전체에 흩어져 있기 때문이다 — RaycastTarget 꺼짐, 위를 덮는 투명
        // Graphic, CanvasGroup.blocksRaycasts, Mask 의 레이캐스트 필터, ScrollRect 의 드래그
        // 가로채기, Canvas 정렬 순서, EventSystem 부재. 전부 "화면에는 멀쩡히 보인다"와 양립한다.
        //
        // 그래서 추측 대신 <b>실제로 무엇에 맞았는지</b>를 찍는다. EventSystem.RaycastAll 은
        // uGUI 가 클릭을 배분할 때 쓰는 바로 그 경로라, 여기 1등으로 찍힌 것이 곧 클릭을 먹은
        // 오브젝트다.

        private bool traceClicks;

        private void ToggleClickTrace()
        {
            traceClicks = !traceClicks;
            Debug.LogWarning("[Dev] 클릭 대상 추적 " + (traceClicks ? "켬 — 이제 클릭할 때마다 맞은 대상을 찍는다" : "끔"));
        }

        private void TraceClick()
        {
            if (EventSystem.current == null)
            {
                Debug.LogWarning("[Dev] EventSystem 이 없다. uGUI 클릭이 아예 동작하지 않는다.");
                return;
            }

            var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);

            var sb = new StringBuilder();
            sb.AppendLine("[Dev] 클릭 대상 " + hits.Count + "개 (위에서부터. 1번이 클릭을 먹는다)");

            if (hits.Count == 0)
                sb.AppendLine("  (아무것도 안 맞았다 — 이 지점에 RaycastTarget 이 켜진 Graphic 이 없다)");

            for (int i = 0; i < hits.Count; i++)
            {
                GameObject go = hits[i].gameObject;
                var selectable = go.GetComponentInParent<UnityEngine.UI.Selectable>();
                sb.AppendFormat("  {0}. {1}", i + 1, Path(go.transform));
                if (selectable != null)
                {
                    sb.AppendFormat("   [{0} on {1}, interactable={2}]",
                        selectable.GetType().Name, selectable.name, selectable.interactable);
                }

                sb.AppendLine();
            }

            Debug.LogWarning(sb.ToString());
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");

            return sb.ToString();
        }

        /// <summary>
        /// 2단계에서 폴백을 걷어낸 뒤 "무엇이 null 인가"를 눈으로 확인하는 용도.
        /// 조용한 폴백이 사라졌으므로 여기서 null 로 찍히는 것이 곧 배선 사고다.
        /// </summary>
        private static void DumpWiring()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Dev] 배선 진단 — 씬: " + SceneManager.GetActiveScene().name);

            StaticResource resource = StaticResource.Instance;
            sb.AppendLine("  StaticResource.Instance : " + Describe(resource));

            if (resource != null)
            {
                sb.AppendLine("    PointMetadataDatabase : " + Describe(resource.PointMetadataDatabase));
                sb.AppendLine("    DiceMetaDataDatabase  : " + Describe(resource.DiceMetaDataDatabase));
                sb.AppendLine("    GemDefinitionDatabase : " + Describe(resource.GemDefinitionDatabase));
                sb.AppendLine("    RelicDatabase         : " + Describe(resource.RelicDatabase));
                sb.AppendLine("    StageDatabase         : " + Describe(resource.StageDatabase));
                sb.AppendLine("    StageRewardDatabase   : " + Describe(resource.StageRewardDatabase));
                sb.AppendLine("    ElementResources      : " + CountOf(resource.ElementResources));
                sb.AppendLine("    RarityResources       : " + CountOf(resource.RarityResources));
                sb.AppendLine("    EquipmentResources    : " + CountOf(resource.EquipmentResources));
                sb.AppendLine("    StageThemeResources   : " + CountOf(resource.StageThemeResources));
            }

            sb.AppendLine("  DiceMetaDataProvider.Database   : " + Describe(DiceMetaDataProvider.Database));
            sb.AppendLine("  RelicDatabaseProvider.Database  : " + Describe(RelicDatabaseProvider.Database));
            sb.AppendLine("  StageDatabaseProvider           : " + Describe(StageDatabaseProvider.GetDatabase()));
            sb.AppendLine("  StageRewardDatabaseProvider     : " + Describe(StageRewardDatabaseProvider.GetDatabase()));

            GameManager gameManager = GameContainer.Battle.Game;
            sb.AppendLine("  Battle.Game                     : " + Describe(gameManager));
            if (gameManager != null)
                sb.AppendLine("    CurrentStageData : " + (gameManager.CurrentStageData != null ? "있음" : "null"));

            Debug.Log(sb.ToString());
        }

        private static string Describe(Object target)
        {
            return target != null ? target.name : "<null>";
        }

        private static string CountOf<T>(System.Collections.Generic.List<T> list)
        {
            return list != null ? list.Count + "개" : "<null>";
        }
    }
}
#endif
