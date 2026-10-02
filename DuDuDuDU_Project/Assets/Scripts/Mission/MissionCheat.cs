#if UNITY_EDITOR || DEV_DEFINE
using UnityEngine;
using OJ.Core;
using OJ.Save;

namespace OJ.Mission
{
    /// <summary>
    /// 개발용 미션 기록 초기화. (<c>SaveResetCheat</c>·<c>TowerProgressCheat</c> 와 같은 자리·같은 형태)
    ///
    /// <b>왜 필요한가.</b> 미션은 한 번 달성하면 그날은 되돌릴 수 없고, 업적은 영영 그렇다.
    /// 그래서 "처음 보는 사람" 화면을 다시 보려면 세이브를 통째로 지우는 수밖에 없었는데,
    /// 그러면 재화·스테이지 진행도까지 같이 날아간다 — 미션 하나 확인하자고 치를 대가가 아니다.
    ///
    /// <b>자정 흉내가 따로 있는 이유.</b> 날짜 리셋은 하루에 한 번만 밟히는 경로다.
    /// 기기 시계를 돌리면 상점 일일 리셋까지 같이 움직여서 무엇이 왜 바뀌었는지 알 수 없다.
    ///
    /// <b>에디터와 DEV_DEFINE 빌드에만 존재한다.</b> 릴리스 빌드에는 컴파일되지 않는다.
    /// </summary>
    public static class MissionCheat
    {
        /// <summary>미션·업적 기록을 전부 지운다. 재화·스테이지 진행도는 건드리지 않는다.</summary>
        public static void ResetAll()
        {
            MissionManager manager = LiveManager();
            if (manager != null)
            {
                manager.DevResetAll();
                return;
            }

            // 플레이 중이 아니다. 메모리에 든 것이 없으니 파일을 직접 고친다 —
            // 여기서 조용히 돌아가면 "메뉴를 눌렀는데 아무 일도 안 일어난다" 가 된다.
            RewriteSaveFile(clearTotals: true);
        }

        /// <summary>오늘치만 지운다(자정 흉내). 누적 카운트와 업적 수령 기록은 남는다.</summary>
        public static void ResetToday()
        {
            MissionManager manager = LiveManager();
            if (manager != null)
            {
                manager.DevResetToday();
                return;
            }

            RewriteSaveFile(clearTotals: false);
        }

        /// <summary>
        /// 지금 <b>살아 있는</b> 매니저. 플레이 중이 아니면 null 이다.
        ///
        /// <b><c>Instance != null</c> 로는 못 가른다.</b> 그 필드는 평범한 C# 참조라
        /// 플레이를 끝내도 도메인 리로드 전까지 남아 있다 — 에디터에서 한 번이라도 플레이한
        /// 뒤에는 편집 모드에서도 계속 non-null 이다. 그 좀비를 붙잡으면 파일은 그대로인데
        /// <b>"지웠다" 로그만 찍힌다.</b> 실제로 그렇게 만들었다가 눌러 보고 잡았다.
        ///
        /// 빌드에서는 <c>isPlaying</c> 이 늘 true 라 언제나 매니저 쪽으로 간다.
        /// </summary>
        private static MissionManager LiveManager()
        {
            return Application.isPlaying ? MissionManager.Instance : null;
        }

        /// <summary>
        /// 플레이 중이 아닐 때의 경로. 세이브 파일에서 미션 조각만 비우고 되쓴다.
        ///
        /// <b>다른 조각은 읽은 그대로 돌려놓는다.</b> 파일 전체를 다시 쓰는 것이므로
        /// 한 칸이라도 빠뜨리면 그 진행도가 사라진다.
        /// </summary>
        private static void RewriteSaveFile(bool clearTotals)
        {
            string path = SavePaths.SaveFilePath;

            SaveLoadResult result = SaveFile.Load(path);
            if (result.State == null)
            {
                Debug.LogWarning("[Dev] 세이브 파일이 없어 지울 것도 없다: " + path);
                return;
            }

            MissionSave missions = result.State.Missions;
            missions.DailyResetDate = string.Empty;
            missions.DailyCounts.Clear();
            missions.ClaimedDailyIds.Clear();
            missions.ClaimedDailyTiers.Clear();

            if (clearTotals)
            {
                missions.TotalCounts.Clear();
                missions.ClaimedAchievementIds.Clear();
            }

            SaveFile.Save(path, result.State);

            Debug.LogWarning(
                "[Dev] (플레이 중이 아님) 세이브 파일의 미션 기록을 " +
                (clearTotals ? "전부" : "오늘치만") + " 지웠다: " + path);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OJ/개발/미션/기록 전부 지우기")]
        private static void ResetAllMenu()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog(
                    "미션 기록 초기화",
                    "일일 미션과 업적 기록을 전부 지운다. 되돌릴 수 없다.\n" +
                    "재화·스테이지 진행도는 건드리지 않는다.",
                    "지운다", "취소"))
            {
                return;
            }

            ResetAll();
        }

        [UnityEditor.MenuItem("OJ/개발/미션/오늘치만 지우기 (자정 흉내)")]
        private static void ResetTodayMenu()
        {
            ResetToday();
        }
#endif
    }
}
#endif
