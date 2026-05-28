using UnityEngine;

namespace OJ
{
    public static class StageRewardDatabaseProvider
    {
        private static StageRewardDatabase fallbackDatabase;

        public static StageRewardDatabase GetDatabase()
        {
            if (StaticResource.isAlive && StaticResource.Instance != null && StaticResource.Instance.StageRewardDatabase != null)
                return StaticResource.Instance.StageRewardDatabase;

            if (fallbackDatabase == null)
            {
                fallbackDatabase = ScriptableObject.CreateInstance<StageRewardDatabase>();
                fallbackDatabase.PopulateDefaults(10);
            }

            return fallbackDatabase;
        }
    }
}
