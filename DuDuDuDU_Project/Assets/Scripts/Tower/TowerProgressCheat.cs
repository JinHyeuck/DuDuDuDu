#if UNITY_EDITOR || DEV_DEFINE
using UnityEngine;
using OJ.Core;
using OJ.SceneFlow;

namespace OJ.Tower
{
    /// <summary>
    /// 개발용 탑 진행도 조작. (<c>SaveResetCheat</c> 와 같은 자리·같은 형태)
    ///
    /// <b>왜 필요한가.</b> 21층 이후의 기믹 넷(혼합·회복·분열·보호막)은 <b>손으로 올라가서는
    /// 확인할 수 없다</b> — 분열형이 처음 나오는 것이 31층이고, 3개 조합은 181층이다.
    /// 그 층까지 서른 판을 이겨야 한다면 그 기믹들은 사실상 테스트되지 않는다.
    ///
    /// <b>특히 분열형이 급하다.</b> 목표 처치 수를 자식까지 미리 세는 구조라, 그 계산이
    /// 어긋나면 <b>층이 영영 안 끝난다</b>(다 잡았는데 결과창이 안 뜬다). 그 종류의 사고는
    /// 실제로 돌려 보기 전에는 드러나지 않는다.
    ///
    /// <b>에디터와 DEV_DEFINE 빌드에만 존재한다.</b> 릴리스 빌드에는 컴파일되지 않는다.
    /// </summary>
    public static class TowerProgressCheat
    {
        /// <summary>
        /// 기믹이 처음 나오는 층. 창의 버튼이 이 표를 그대로 쓴다.
        ///
        /// <b>여기서 손으로 세지 않는다.</b> 구간 배치는 <c>TowerDatabase</c> 가
        /// 콘셉트 순환으로 만들고, 그 순환을 고치면 이 숫자도 같이 움직여야 한다.
        /// 그래서 <see cref="FindFirstFloorOf"/> 가 데이터베이스를 직접 훑어 찾는다.
        /// </summary>
        public static readonly TowerFloorConcept[] Milestones =
        {
            TowerFloorConcept.Mixed,
            TowerFloorConcept.Regenerating,
            TowerFloorConcept.Splitting,
            TowerFloorConcept.Shielded,
        };

        /// <summary>
        /// 이 콘셉트가 <b>처음</b> 나오는 층. 못 찾으면 0.
        ///
        /// 학습 구간(1~20층)도 포함해 찾는다 — 물량·정예·속도·방어는 거기서 먼저 나온다.
        /// </summary>
        public static int FindFirstFloorOf(TowerFloorConcept concept)
        {
            TowerDatabase database = TowerDatabaseProvider.Database;

            for (int band = 1; band <= TowerFormula.BandCount; band++)
            {
                TowerBandDefinition definition = database.GetBand(band);
                if (definition == null)
                    continue;

                if (definition.ResolveConcepts().Contains(concept))
                    return TowerFormula.BandStartFloor(band);
            }

            return 0;
        }

        /// <summary>
        /// 콘셉트가 <paramref name="count"/> 개 이상 겹치는 첫 층. 못 찾으면 0.
        /// "2개 조합은 몇 층부터인가" 를 표에 적어 두지 않고 데이터에서 찾는다.
        /// </summary>
        public static int FindFirstFloorWithConceptCount(int count)
        {
            TowerDatabase database = TowerDatabaseProvider.Database;

            for (int band = 1; band <= TowerFormula.BandCount; band++)
            {
                TowerBandDefinition definition = database.GetBand(band);
                if (definition == null)
                    continue;

                if (definition.ResolveConcepts().Count >= count)
                    return TowerFormula.BandStartFloor(band);
            }

            return 0;
        }

        /// <summary>
        /// 진행도를 이 층까지 깬 상태로 만든다. <paramref name="clearedFloor"/> 가 0 이면
        /// 한 번도 안 깬 상태다.
        ///
        /// <b>다이스 해금도 같이 따라온다.</b> 해금은 층 번호의 함수라
        /// (<c>TowerProgressManager.IsDiceUnlocked</c>) 따로 켜 줄 것이 없다 —
        /// 30층으로 맞추면 회오리·방깎·킹파이어가 함께 열린다.
        ///
        /// <b>진짜 세이브에 쓴다.</b> 플레이를 멈춰도 남는다. 되돌리려면 0 으로 다시 부르거나
        /// <c>OJ/개발/세이브 전부 지우기</c> 를 쓸 것.
        /// </summary>
        public static void SetClearedFloor(int clearedFloor)
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
            {
                Debug.LogWarning(
                    "[Dev] 탑 진행도를 바꿀 수 없다 — TowerProgressManager 가 없다. " +
                    "이 치트는 플레이 중에만 쓸 수 있다(컨테이너가 플레이 시작 시 만든다).");
                return;
            }

            progress.DevSetClearedFloor(clearedFloor);

            Debug.LogWarning(
                "[Dev] 탑 진행도를 " + progress.HighestClearedFloor + "층 클리어로 맞췄다. " +
                "이제 도전 가능한 층은 " + progress.HighestUnlockedFloor + "층이다.");
        }

        /// <summary>
        /// 이 층을 <b>지금 바로</b> 도전한다. 진행도를 그 앞 층까지 맞추고, 추천 편성으로
        /// 출전을 예약한 뒤 전투 씬을 연다.
        ///
        /// <b>로비를 거치지 않는 것이 요점이다.</b> 기믹 하나를 보려고 로비 → 탑 → 층 선택 →
        /// 편성 → 시작을 매번 누르면, 고치고 다시 보는 한 바퀴가 그만큼 길어진다.
        /// </summary>
        public static void StartFloorNow(int floor)
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
            {
                Debug.LogWarning(
                    "[Dev] 층을 시작할 수 없다 — TowerProgressManager 가 없다. " +
                    "이 치트는 플레이 중에만 쓸 수 있다.");
                return;
            }

            int validFloor = TowerFormula.ClampFloor(floor);

            // 그 층이 도전 가능해야 한다. 편성 화면이 도전 불가한 층을 눌러 주므로
            // (UITowerLoadoutDialog.Open) 여기서 맞춰 두지 않으면 엉뚱한 층이 열린다.
            progress.DevSetClearedFloor(validFloor - 1);

            TowerFloorPlan plan = TowerDatabaseProvider.GetPlan(validFloor);
            progress.RequestRun(validFloor, progress.BuildRecommendedLoadout(plan));

            Debug.LogWarning(
                "[Dev] " + validFloor + "층을 추천 편성으로 바로 시작한다 — " +
                plan.DisplayName + " / " + plan.MonsterCount + "마리 × HP " + plan.MonsterHp +
                " (목표 처치 " + plan.KillTarget + ")");

            SceneFlowManager.LoadBattle();
        }
    }
}
#endif
