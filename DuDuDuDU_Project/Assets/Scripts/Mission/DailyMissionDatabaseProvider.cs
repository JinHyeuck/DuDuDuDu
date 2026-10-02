using UnityEngine;
using OJ.Utils;

namespace OJ.Mission
{
    /// <summary>
    /// <see cref="DailyMissionDatabase"/> 로 가는 유일한 창구.
    /// <c>BountyDatabaseProvider</c>·<c>TowerDatabaseProvider</c> 와 같은 형태다 —
    /// 이 프로젝트에서 SO 를 얻는 방법이 여러 가지가 되면 어느 것이 정본인지 알 수 없게 된다.
    ///
    /// <b>폴백을 캐시하지 않는다.</b> 캐시하면 나중에 <c>StaticResource</c> 가 살아나도
    /// 영영 코드 기본값을 쓰게 된다.
    /// </summary>
    public static class DailyMissionDatabaseProvider
    {
        private static DailyMissionDatabase database;
        private static DailyMissionDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static DailyMissionDatabase Database
        {
            get
            {
                if (database != null)
                    return database;

                StaticResource resource = StaticResource.Instance;
                if (resource != null && resource.DailyMissionDatabase != null)
                {
                    database = resource.DailyMissionDatabase;
                    return database;
                }

                LogMissingDatabaseOnce();

                if (fallbackDatabase == null)
                {
                    fallbackDatabase = ScriptableObject.CreateInstance<DailyMissionDatabase>();
                    fallbackDatabase.PopulateDefaults();
                }

                return fallbackDatabase;
            }
        }

        /// <summary>
        /// 지금 돌려주는 것이 <b>진짜 에셋</b>인가. 진단과 에디터 도구가 쓴다.
        /// getter 를 먼저 밟아 1단 시도를 강제한다 — 안 그러면 아무도 안 건드린
        /// 상태에서 물었을 때 언제나 "폴백" 이라고 답하는, 호출 순서에 좌우되는 판정이 된다.
        /// </summary>
        public static bool HasRealDatabase
        {
            get
            {
                _ = Database;
                return database != null;
            }
        }

        private static void LogMissingDatabaseOnce()
        {
            if (missingDatabaseLogged)
                return;

            missingDatabaseLogged = true;
            Debug.LogError(
                "DailyMissionDatabase 를 찾지 못해 코드 기본값으로 대체한다. " +
                "OJ/개발/미션/데이터베이스 에셋 만들기 를 돌리면 에셋이 생기고 " +
                "StaticResource 빈 슬롯 채우기가 자동으로 잇는다.");
        }
    }
}
