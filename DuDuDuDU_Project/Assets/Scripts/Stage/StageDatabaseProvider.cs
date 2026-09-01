using UnityEngine;
using OJ.Relic;
using OJ.Utils;

namespace OJ.Stage
{
    public static class StageDatabaseProvider
    {
        // 진짜 에셋만 캐시한다. 폴백을 여기 넣으면 나중에 StaticResource 가 살아나도
        // 영영 기본값을 쓰게 된다 — 옛 RelicDatabaseProvider 가 이름으로 되돌리려던 그 함정.
        private static StageDatabase database;
        private static StageDatabase fallbackDatabase;
        private static bool missingDatabaseLogged;

        public static StageDatabase GetDatabase()
        {
            if (database != null)
                return database;

            // StaticResource.isAlive 가드를 뺐다. isAlive 는 _instance != null 일 뿐
            // 인스턴스를 만들지 않으므로, 아무도 StaticResource 를 건드리기 전에 여기가
            // 먼저 불리면 1단을 통째로 건너뛰고 기본값으로 내려갔다 — 폴백이 호출 순서에
            // 좌우됐다는 뜻이다. 이제는 늘 1단을 시도한다. (2.2)
            StaticResource resource = StaticResource.Instance;
            if (resource != null && resource.StageDatabase != null)
            {
                database = resource.StageDatabase;
                return database;
            }

            // 폴백은 남기되 조용하지 않게 한다. 이 단계의 목적은 "기본값으로 흐르는 것"을
            // 막는 게 아니라 그것이 <b>보이지 않는 것</b>을 막는 데 있다. 소비처는 5~8단계에서
            // 다시 쓰이므로 지금 null 을 던져 NRE 를 만들 이유가 없다.
            LogMissingDatabaseOnce();

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

        /// <summary>
        /// 지금 돌려주는 것이 <b>진짜 에셋</b>인가. 폴백(코드 기본값 30스테이지)이면 false.
        ///
        /// <b>왜 필요한가.</b> 폴백은 "화면이 뜨긴 한다" 수준의 대체물이지 <b>진실이 아니다.</b>
        /// 읽기(표시·계산)에는 써도 되지만, 이 값을 경계로 삼아 <b>세이브를 잘라내면</b>
        /// 그 순간 진행도가 영구히 사라진다 — 45스테이지까지 깬 유저가 데이터베이스 로드에
        /// 한 번 실패하면 30으로 잘려서 저장된다.
        ///
        /// 그래서 "폴백인지"를 물어볼 수 있어야 한다. <c>GetDatabase()</c> 를 먼저 불러
        /// 1단 시도를 강제한 뒤 판정한다 — 아직 아무도 안 건드린 상태에서 물으면 언제나
        /// 폴백이라고 답하게 되는데, 그건 <c>isAlive</c> 가드가 2.2 에서 일으켰던 것과 같은
        /// "호출 순서에 좌우되는 판정"이다.
        /// </summary>
        public static bool HasRealDatabase
        {
            get
            {
                GetDatabase();
                return database != null;
            }
        }

        private static void LogMissingDatabaseOnce()
        {
            if (missingDatabaseLogged)
                return;

            missingDatabaseLogged = true;
            Debug.LogError(
                "StageDatabase 를 찾지 못해 코드 기본값 30스테이지로 대체한다. StaticResource " +
                "프리팹의 StageDatabase 슬롯이 비었거나 StaticResource 자체가 만들어지지 않았다. " +
                "에셋의 스테이지 구성은 지금 전혀 반영되지 않고 있다.");
        }
    }
}
