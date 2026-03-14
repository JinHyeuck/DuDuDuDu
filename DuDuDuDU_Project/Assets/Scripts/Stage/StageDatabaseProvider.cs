using UnityEngine;

namespace OJ
{
    public static class StageDatabaseProvider
    {
        private static StageDatabase fallbackDatabase;

        public static StageDatabase GetDatabase()
        {
            if (StaticResource.isAlive && StaticResource.Instance != null && StaticResource.Instance.StageDatabase != null)
                return StaticResource.Instance.StageDatabase;

            if (fallbackDatabase == null)
            {
                fallbackDatabase = ScriptableObject.CreateInstance<StageDatabase>();
                fallbackDatabase.PopulateDefaults(30);
            }

            return fallbackDatabase;
        }

        public static StageData GetStage(int stageIndex)
        {
            return GetDatabase().GetStage(stageIndex);
        }
    }
}
