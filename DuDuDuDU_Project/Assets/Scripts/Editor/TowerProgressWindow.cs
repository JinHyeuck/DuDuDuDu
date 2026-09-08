using UnityEditor;
using UnityEngine;
using OJ.Core;
using OJ.Dice;
using OJ.Tower;

namespace OJ.EditorTools
{
    /// <summary>
    /// 무한의 탑 진행도를 손으로 맞추는 개발 창.
    ///
    /// <b>왜 창인가.</b> 메뉴 항목으로는 <b>숫자를 받을 수 없다.</b> 층을 임의로 넣는 것이
    /// 이 도구의 전부라, 고정 메뉴 몇 개로는 "181층 3개 조합을 보고 싶다" 를 못 한다.
    ///
    /// <b>플레이 중에만 동작한다.</b> <c>TowerProgressManager</c> 는 컨테이너가 플레이
    /// 시작 시 만드는 것이라 에디트 모드에는 존재하지 않는다. 세이브 파일을 직접 고쳐
    /// 에디트 모드도 지원할 수 있지만, 그러면 <b>같은 상태를 만드는 길이 둘</b>이 되고
    /// 둘이 어긋날 때 어느 쪽이 맞는지 알 수 없다 — 도구가 만드는 상태는 진짜와
    /// 구별되지 않아야 한다.
    /// </summary>
    public sealed class TowerProgressWindow : EditorWindow
    {
        private int floorInput = 31;

        [MenuItem("OJ/개발/무한의 탑/진행도 설정")]
        private static void Open()
        {
            var window = GetWindow<TowerProgressWindow>(false, "무한의 탑 진행도");
            window.minSize = new Vector2(340f, 420f);
            window.Show();
        }

        /// <summary>
        /// 플레이 상태가 바뀌면 다시 그린다. 안 그러면 플레이를 눌러도 창이
        /// "플레이 중에만 쓸 수 있다" 를 계속 띄우고 있어 <b>도구가 고장 난 것처럼 보인다.</b>
        /// </summary>
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange _)
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("무한의 탑 진행도", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "플레이 중에만 쓸 수 있습니다.\n" +
                    "진행도 매니저는 컨테이너가 플레이 시작 시 만듭니다.",
                    MessageType.Info);
                return;
            }

            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
            {
                EditorGUILayout.HelpBox(
                    "TowerProgressManager 가 없습니다.\n" +
                    "GameContainer 부트스트랩이 돌았는지 확인하세요.",
                    MessageType.Warning);
                return;
            }

            DrawCurrentState(progress);
            EditorGUILayout.Space(8f);

            DrawFloorInput();
            EditorGUILayout.Space(8f);

            DrawMilestones();
            EditorGUILayout.Space(8f);

            DrawReset();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "진짜 세이브에 씁니다. 플레이를 멈춰도 남습니다.\n" +
                "되돌리려면 아래 '처음부터' 를 누르세요.",
                MessageType.Warning);
        }

        private static void DrawCurrentState(TowerProgressManager progress)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("클리어한 최고 층", progress.HighestClearedFloor + "층");
                EditorGUILayout.LabelField("지금 도전 가능", progress.HighestUnlockedFloor + "층");

                TowerFloorPlan plan = TowerDatabaseProvider.GetPlan(progress.HighestUnlockedFloor);
                EditorGUILayout.LabelField("그 층의 구성", plan.DisplayName);
                EditorGUILayout.LabelField(
                    "적",
                    plan.MonsterCount + "마리 × HP " + plan.MonsterHp +
                    " / 방어 " + plan.MonsterDefense);
                EditorGUILayout.LabelField("목표 처치 수", plan.KillTarget + "마리");

                DiceUnlockDefinition next = progress.GetNextUnlock();
                EditorGUILayout.LabelField(
                    "다음 해금",
                    next != null ? next.towerFloor + "층 · " + next.diceType : "전부 해금");
            }
        }

        private void DrawFloorInput()
        {
            EditorGUILayout.LabelField("층 지정", EditorStyles.boldLabel);

            floorInput = EditorGUILayout.IntSlider("층", floorInput, 1, TowerFormula.TotalFloors);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("이 층 바로 도전"))
                    TowerProgressCheat.StartFloorNow(floorInput);

                if (GUILayout.Button("진행도만 맞추기"))
                    TowerProgressCheat.SetClearedFloor(floorInput - 1);
            }

            EditorGUILayout.LabelField(
                "'바로 도전' 은 추천 편성으로 전투 씬까지 엽니다.",
                EditorStyles.miniLabel);
        }

        /// <summary>
        /// 기믹이 처음 나오는 층으로 가는 버튼들.
        ///
        /// <b>층 번호를 여기 적어 두지 않는다.</b> 구간 배치는 <c>TowerDatabase</c> 가
        /// 콘셉트 순환으로 만들므로, 그 순환을 고치면 이 버튼도 따라 움직여야 한다 —
        /// <see cref="TowerProgressCheat.FindFirstFloorOf"/> 가 데이터에서 찾는다.
        /// </summary>
        private void DrawMilestones()
        {
            EditorGUILayout.LabelField("기믹이 처음 나오는 층", EditorStyles.boldLabel);

            foreach (TowerFloorConcept concept in TowerProgressCheat.Milestones)
            {
                int floor = TowerProgressCheat.FindFirstFloorOf(concept);
                if (floor <= 0)
                    continue;

                DrawMilestoneRow(TowerConceptText.NameOf(concept), floor);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("콘셉트 겹침", EditorStyles.boldLabel);

            int twoConcepts = TowerProgressCheat.FindFirstFloorWithConceptCount(2);
            if (twoConcepts > 0)
                DrawMilestoneRow("2개 조합", twoConcepts);

            int threeConcepts = TowerProgressCheat.FindFirstFloorWithConceptCount(3);
            if (threeConcepts > 0)
                DrawMilestoneRow("3개 조합", threeConcepts);

            DrawMilestoneRow("꼭대기", TowerFormula.TotalFloors);
        }

        private static void DrawMilestoneRow(string label, int floor)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label + "  (" + floor + "층)", GUILayout.Width(180f));

                if (GUILayout.Button("바로 도전"))
                    TowerProgressCheat.StartFloorNow(floor);
            }
        }

        private static void DrawReset()
        {
            if (!GUILayout.Button("처음부터 (진행도 0)"))
                return;

            // 되돌릴 수 없는 조작이라 한 번 묻는다. 세이브 초기화 치트가 같은 규약이다.
            bool confirmed = EditorUtility.DisplayDialog(
                "탑 진행도 초기화",
                "탑 진행도와 층 기록을 전부 지웁니다.\n" +
                "다이스 해금도 같이 잠깁니다.\n\n" +
                "본편 스테이지 진행도는 건드리지 않습니다.",
                "지운다",
                "취소");

            if (confirmed)
                TowerProgressCheat.SetClearedFloor(0);
        }
    }
}
