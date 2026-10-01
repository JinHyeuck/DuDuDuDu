using UnityEngine;
using OJ.Utils;

namespace OJ.Mission
{
    /// <summary>
    /// <see cref="AchievementDatabase"/> 로 가는 유일한 창구.
    /// 형태와 이유는 <see cref="DailyMissionDatabaseProvider"/> 와 같다.
    /// </summary>
    public static class AchievementDatabaseProvider
    {
        private static AchievementDatabase database;
        private static AchievementDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static AchievementDatabase Database
        {
            get
            {
                if (database != null)
                    return database;

                StaticResource resource = StaticResource.Instance;
                if (resource != null && resource.AchievementDatabase != null)
                {
                    database = resource.AchievementDatabase;
                    return database;
                }

                LogMissingDatabaseOnce();

                if (fallbackDatabase == null)
                {
                    fallbackDatabase = ScriptableObject.CreateInstance<AchievementDatabase>();
                    fallbackDatabase.PopulateDefaults();
                }

                return fallbackDatabase;
            }
        }

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
                "AchievementDatabase 를 찾지 못해 코드 기본값으로 대체한다. " +
                "OJ/개발/미션/데이터베이스 에셋 만들기 를 돌리면 에셋이 생기고 " +
                "StaticResource 빈 슬롯 채우기가 자동으로 잇는다.");
        }
    }
}
