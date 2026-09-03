using UnityEngine;
using OJ.Utils;

namespace OJ.Bounty
{
    /// <summary>
    /// <see cref="BountyDatabase"/> 로 가는 유일한 창구.
    /// <c>StageDatabaseProvider</c>·<c>RelicDatabaseProvider</c> 와 같은 형태로 둔다 —
    /// 이 프로젝트에서 SO 를 얻는 방법이 세 가지가 되면 어느 것이 정본인지 알 수 없게 된다.
    ///
    /// <b>폴백을 캐시하지 않는다.</b> 넣으면 나중에 <c>StaticResource</c> 가 살아나도
    /// 영영 코드 기본값을 쓰게 된다. 그 함정은 이미 두 Provider 의 주석에 적혀 있다.
    /// </summary>
    public static class BountyDatabaseProvider
    {
        private static BountyDatabase database;
        private static BountyDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static BountyDatabase Database
        {
            get
            {
                if (database != null)
                    return database;

                StaticResource resource = StaticResource.Instance;
                if (resource != null && resource.BountyDatabase != null)
                {
                    database = resource.BountyDatabase;
                    return database;
                }

                LogMissingDatabaseOnce();

                if (fallbackDatabase == null)
                {
                    fallbackDatabase = ScriptableObject.CreateInstance<BountyDatabase>();
                    fallbackDatabase.PopulateDefaults();
                }

                return fallbackDatabase;
            }
        }

        public static BountyDefinition Get(int grade)
        {
            return Database.Get(grade);
        }

        /// <summary>
        /// 지금 돌려주는 것이 <b>진짜 에셋</b>인가. 진단(F9)과 에디터 도구가 쓴다.
        /// getter 를 먼저 밟아 1단 시도를 강제한다 — 안 그러면 아무도 안 건드린
        /// 상태에서 물었을 때 언제나 "폴백" 이라고 답하는, 호출 순서에 좌우되는 판정이 된다.
        /// </summary>
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
                "BountyDatabase 를 찾지 못해 코드 기본값 5등급으로 대체한다. " +
                "OJ/개발/현상금/데이터베이스 에셋 만들기 를 돌리면 에셋이 생기고 " +
                "StaticResource 빈 슬롯 채우기가 자동으로 잇는다.");
        }
    }
}
