using UnityEngine;
using OJ.Utils;

namespace OJ.SeasonPass
{
    /// <summary>
    /// <see cref="SeasonPassDatabase"/> 로 가는 유일한 창구.
    /// <c>BountyDatabaseProvider</c> 와 같은 형태이고 이유도 같다 —
    /// 이 프로젝트에서 SO 를 얻는 방법이 여러 가지가 되면 어느 것이 정본인지 알 수 없다.
    ///
    /// <b>폴백을 캐시하지 않는다.</b> 캐시하면 나중에 <c>StaticResource</c> 가 살아나도
    /// 영영 코드 기본값을 쓰게 된다.
    /// </summary>
    public static class SeasonPassDatabaseProvider
    {
        private static SeasonPassDatabase database;
        private static SeasonPassDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static SeasonPassDatabase Database
        {
            get
            {
                if (database != null)
                    return database;

                StaticResource resource = StaticResource.Instance;
                if (resource != null && resource.SeasonPassDatabase != null)
                {
                    database = resource.SeasonPassDatabase;
                    return database;
                }

                LogMissingDatabaseOnce();

                if (fallbackDatabase == null)
                {
                    fallbackDatabase = ScriptableObject.CreateInstance<SeasonPassDatabase>();
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
                "SeasonPassDatabase 를 찾지 못해 코드 기본값으로 대체한다. " +
                "OJ/개발/시즌패스/데이터베이스 에셋 만들기 를 돌리면 에셋이 생기고 " +
                "StaticResource 빈 슬롯 채우기가 자동으로 잇는다.");
        }
    }
}
