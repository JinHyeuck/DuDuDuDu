using UnityEngine;

namespace OJ
{
    public static class ChapterRewardDatabaseProvider
    {
        private static ChapterRewardDatabase fallbackDatabase;

        public static ChapterRewardDatabase GetDatabase()
        {
            if (StaticResource.isAlive && StaticResource.Instance != null && StaticResource.Instance.ChapterRewardDatabase != null)
                return StaticResource.Instance.ChapterRewardDatabase;

            if (fallbackDatabase == null)
            {
                fallbackDatabase = ScriptableObject.CreateInstance<ChapterRewardDatabase>();
                fallbackDatabase.PopulateDefaults(10);
            }

            return fallbackDatabase;
        }
    }
}
