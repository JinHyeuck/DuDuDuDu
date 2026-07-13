using UnityEngine;

namespace OJ
{
    public static class RelicDatabaseProvider
    {
        private static RelicDatabase database;

        public static RelicDatabase Database
        {
            get
            {
                if ((database == null || database.name == "RuntimeRelicDatabase")
                    && StaticResource.isAlive
                    && StaticResource.Instance != null
                    && StaticResource.Instance.RelicDatabase != null)
                {
                    database = StaticResource.Instance.RelicDatabase;
                }

                if (database == null)
                    database = Resources.Load<RelicDatabase>("RelicDatabase");

                if (database == null || database.relics == null || database.relics.Count == 0)
                    database = RelicDatabase.CreateRuntimeDefault();

                return database;
            }
        }

        public static void ClearCache()
        {
            database = null;
        }
    }
}
