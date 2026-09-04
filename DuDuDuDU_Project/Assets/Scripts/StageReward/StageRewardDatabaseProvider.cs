using UnityEngine;
using OJ.Stage;
using OJ.Utils;

namespace OJ.StageReward
{
    public static class StageRewardDatabaseProvider
    {
        // 진짜 에셋만 캐시한다. 폴백을 캐시하면 StaticResource 가 나중에 살아나도
        // 영영 기본값을 쓰게 된다.
        private static StageRewardDatabase database;
        private static StageRewardDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static StageRewardDatabase GetDatabase()
        {
            if (database != null)
                return database;

            // isAlive 가드 제거 이유는 StageDatabaseProvider 와 같다. (2.2)
            StaticResource resource = StaticResource.Instance;
            if (resource != null && resource.StageRewardDatabase != null)
            {
                database = resource.StageRewardDatabase;
                return database;
            }

            LogMissingDatabaseOnce();

            if (fallbackDatabase == null)
            {
                fallbackDatabase = ScriptableObject.CreateInstance<StageRewardDatabase>();
                fallbackDatabase.PopulateDefaults(10);
            }

            return fallbackDatabase;
        }

        private static void LogMissingDatabaseOnce()
        {
            if (missingDatabaseLogged)
                return;

            missingDatabaseLogged = true;
            Debug.LogError(
                "StageRewardDatabase 를 찾지 못해 코드 기본값 마일스톤 10개로 대체한다. " +
                "에셋에는 90개가 들어 있으므로, 보상 화면이 그럴듯하게 뜨면서 실제 보상표와 " +
                "전혀 다른 것을 보여 주는 상태다. StaticResource 배선을 확인할 것.");
        }
    }
}
