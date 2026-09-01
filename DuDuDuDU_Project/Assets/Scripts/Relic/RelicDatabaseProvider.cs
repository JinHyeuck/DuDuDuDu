using UnityEngine;
using OJ.Utils;

namespace OJ.Relic
{
    public static class RelicDatabaseProvider
    {
        // 진짜 에셋만 캐시한다. 옛 코드가 database.name == "RuntimeRelicDatabase" 로
        // 되돌리려 했던 것이 바로 "폴백이 캐시에 눌러앉는" 문제였다. 폴백을 캐시에
        // 넣지 않으면 그 우회가 필요 없다.
        private static RelicDatabase database;
        private static RelicDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static RelicDatabase Database
        {
            get
            {
                if (database != null)
                    return database;

                // StaticResource.isAlive 가드를 뺐다. isAlive 는 인스턴스를 만들지 않으므로,
                // RelicManager 가 BeforeSceneLoad 에서 먼저 불리면 1단을 건너뛰고 코드로 만든
                // 유물 24종을 쓰게 됐다 — 에셋을 고쳐도 반영되지 않으면서 화면에는 유물이
                // 멀쩡히 뜨므로 아무도 알아채지 못한다. (2.2)
                StaticResource resource = StaticResource.Instance;
                if (resource != null && resource.RelicDatabase != null)
                {
                    database = resource.RelicDatabase;
                    return database;
                }

                // 폴백은 남기되 조용하지 않게 한다. RelicManager 는 Database 를 무방비로
                // 역참조하는 곳이 7군데라 여기서 null 을 내보내면 NRE 로 번진다. 그쪽은
                // 5.3 에서 상태·규칙을 분리하며 다시 쓰이므로 지금 손대지 않는다.
                LogMissingDatabaseOnce();

                if (fallbackDatabase == null)
                    fallbackDatabase = RelicDatabase.CreateRuntimeDefault();

                return fallbackDatabase;
            }
        }

        public static void ClearCache()
        {
            database = null;
            fallbackDatabase = null;
            missingDatabaseLogged = false;
        }

        private static void LogMissingDatabaseOnce()
        {
            if (missingDatabaseLogged)
                return;

            missingDatabaseLogged = true;
            Debug.LogError(
                "RelicDatabase 를 찾지 못해 코드 기본값(CreateRuntimeDefault)으로 대체한다. " +
                "StaticResource 프리팹의 RelicDatabase 슬롯이 비었거나 StaticResource 자체가 " +
                "만들어지지 않았다. 이 상태에서는 RelicDatabase.asset 을 고쳐도 반영되지 않는다.");
        }
    }
}
