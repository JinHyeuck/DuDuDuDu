using UnityEngine;
using OJ.Utils;

namespace OJ.ContentUnlock
{
    /// <summary>
    /// <see cref="ContentUnlockDatabase"/> 로 가는 유일한 창구. <c>DiceUnlockDatabaseProvider</c> 와 같은 형태다.
    ///
    /// <b>폴백을 캐시하지 않는다.</b> 넣으면 나중에 <c>StaticResource</c> 가 살아나도 영영 코드 기본값을 쓴다.
    /// <b>씬이 선 뒤에만 부를 것</b> — <c>StaticResource</c> 를 깨운다. 그래서
    /// <see cref="ContentUnlockManager.ReadFrom"/> 은 여기를 건드리지 않는다.
    /// </summary>
    public static class ContentUnlockDatabaseProvider
    {
        private static ContentUnlockDatabase database;
        private static ContentUnlockDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static ContentUnlockDatabase Database
        {
            get
            {
                if (database != null)
                    return database;

                StaticResource resource = StaticResource.Instance;
                if (resource != null && resource.ContentUnlockDatabase != null)
                {
                    database = resource.ContentUnlockDatabase;
                    return database;
                }

                LogMissingDatabaseOnce();

                if (fallbackDatabase == null)
                {
                    fallbackDatabase = ScriptableObject.CreateInstance<ContentUnlockDatabase>();
                    fallbackDatabase.PopulateDefaults();
                }

                return fallbackDatabase;
            }
        }

        private static void LogMissingDatabaseOnce()
        {
            if (missingDatabaseLogged)
                return;

            missingDatabaseLogged = true;
            Debug.LogError(
                "ContentUnlockDatabase 를 찾지 못해 코드 기본표로 대체한다. " +
                "OJ/개발/컨텐츠 해금/데이터베이스 에셋 만들기 를 돌리면 에셋이 생기고 " +
                "StaticResource 빈 슬롯 채우기가 자동으로 잇는다.");
        }
    }
}
