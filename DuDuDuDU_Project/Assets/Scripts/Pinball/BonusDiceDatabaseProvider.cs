using UnityEngine;
using OJ.Utils;

namespace OJ.Pinball
{
    /// <summary>
    /// 보상 라운드 수치표를 얻는 <b>유일한 창구</b>. <see cref="PinballDatabaseProvider"/> 와 같은 구조다.
    ///
    /// <b>폴백을 캐시로 굳히지 않는다.</b> 진짜 에셋과 코드 기본값을 따로 들고 있어서,
    /// <c>StaticResource</c> 가 나중에 살아나면 그때부터 진짜를 쓴다.
    ///
    /// <b>씬이 선 뒤에만 부를 것.</b> <c>StaticResource</c> 를 깨우므로
    /// <c>BeforeSceneLoad</c> 에서 부르면 거짓 LogError 가 찍힌다.
    /// </summary>
    public static class BonusDiceDatabaseProvider
    {
        private static BonusDiceDatabase database;
        private static BonusDiceDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static BonusDiceDatabase GetDatabase()
        {
            if (database != null)
                return database;

            StaticResource resource = StaticResource.Instance;
            if (resource != null && resource.BonusDiceDatabase != null)
            {
                database = resource.BonusDiceDatabase;
                return database;
            }

            LogMissingDatabaseOnce();

            if (fallbackDatabase == null)
            {
                fallbackDatabase = ScriptableObject.CreateInstance<BonusDiceDatabase>();
                fallbackDatabase.PopulateDefaults();
            }

            return fallbackDatabase;
        }

        private static void LogMissingDatabaseOnce()
        {
            if (missingDatabaseLogged)
                return;

            missingDatabaseLogged = true;
            Debug.LogError(
                "BonusDiceDatabase 를 찾지 못해 코드 기본값으로 대체한다. 보상 라운드가 " +
                "그럴듯하게 돌아가면서 실제 표와 전혀 다른 것을 지급하는 상태다. " +
                "StaticResource 배선을 확인할 것.");
        }
    }
}
