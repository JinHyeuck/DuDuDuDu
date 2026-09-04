#if UNITY_EDITOR || DEV_DEFINE
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using OJ.Analytics;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Hunting;
using OJ.IdleReward;
using OJ.Point;
using OJ.Relic;
using OJ.Save;
using OJ.Stage;
using OJ.StageReward;
using OJ.StageStar;
using OJ.UI;
using OJ.Utils;

namespace OJ.SceneFlow
{
    /// <summary>
    /// 한 번 눌러서 시스템 전체 상태를 찍는다. (F9)
    ///
    /// <b>왜 만드나.</b> 리팩토링 중 확인이 필요한 것들은 대부분 "컴파일은 되는데 런타임에
    /// 배선이 맞나"이고, 그건 헤드리스로 검증할 수 없어 사람이 플레이해 줘야 한다.
    /// 그런데 매니저를 하나 옮길 때마다 확인을 요청하면 사람 손이 계속 든다.
    ///
    /// 그래서 <b>확인을 몰아서</b> 할 수 있게 한다. 이 한 번의 출력에 컨테이너 구성,
    /// 다리(<c>Instance</c>) 배선, 저장 대상 등록, 데이터베이스 로드, 세이브 왕복까지
    /// 다 들어간다. 사람은 F9 한 번만 누르면 되고 판독은 로그를 읽어서 한다.
    ///
    /// <b>읽는 법.</b> 줄 앞의 표시가 전부다.
    /// <c>OK</c> 는 정상, <c>!!</c> 는 사고, <c>--</c> 는 지금 씬에서는 판단할 수 없음
    /// (예: 로비에서 전투 매니저를 물으면 없는 것이 정상이다).
    /// </summary>
    public static class SelfCheck
    {
        private const string Ok = "OK";
        private const string Bad = "!!";
        private const string NA = "--";

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================ [자가 진단] ================");
            sb.AppendLine("씬: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            Section(sb, "컨테이너");
            CheckContainer(sb);

            Section(sb, "영구 서비스 (컨테이너 소유)");
            CheckPersistentServices(sb);

            Section(sb, "데이터베이스");
            CheckDatabases(sb);

            Section(sb, "저장 왕복 (7단계 스키마)");
            CheckSaveRoundTrip(sb);

            Section(sb, "씬 흐름 (9단계)");
            CheckSceneFlow(sb);

            Section(sb, "팝업 (10단계)");
            CheckUI(sb);

            Section(sb, "배틀 스코프 (8.3b)");
            CheckBattleScope(sb);

            Section(sb, "전투 매니저 (BattleScene 전용)");
            CheckBattleServices(sb);

            // 판정은 <b>출력한 글자를 세어서</b> 한다. 따로 카운터를 두면 출력과
            // 어긋날 수 있다 — 누군가 Line 을 거치지 않고 sb 에 직접 "!!" 를 적으면
            // 카운터는 0 인데 화면에는 사고가 찍힌다. 사람이 읽는 것과 같은 것을
            // 세는 편이 언제나 정확하다.
            string report = sb.ToString();
            int bad = CountBad(report);

            sb.AppendLine();
            sb.AppendLine(bad == 0 ? "  전부 정상." : "  !! 사고 " + bad + "건");
            sb.AppendLine("=============================================");
            report = sb.ToString();

            // 결과와 무관하게 항상 LogWarning 이면 색이 아무것도 알려주지 않는다.
            // 통과한 F9 가 노란색을 차지해 옆의 진짜 경고까지 묻어 버린다.
            if (bad > 0)
                Debug.LogError(report);
            else
                Debug.Log(report);
        }

        private static int CountBad(string report)
        {
            const string Marker = "  " + Bad + " ";

            int count = 0;
            for (int i = report.IndexOf(Marker, StringComparison.Ordinal); i >= 0;
                 i = report.IndexOf(Marker, i + Marker.Length, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        private static void Section(StringBuilder sb, string title)
        {
            sb.AppendLine();
            sb.AppendLine("--- " + title + " ---");
        }

        private static void Line(StringBuilder sb, string mark, string name, string detail = null)
        {
            sb.AppendFormat("  {0} {1}", mark, name);
            if (!string.IsNullOrEmpty(detail))
                sb.Append("   " + detail);

            sb.AppendLine();
        }

        private static void CheckContainer(StringBuilder sb)
        {
            if (GameContainer.Root == null)
            {
                Line(sb, Bad, "GameContainer.Root", "없다 — 부트스트랩이 돌지 않았다");
                return;
            }

            Line(sb, Ok, "GameContainer.Root", GameContainer.Root.name);
            Line(sb, GameContainer.Root.Container != null ? Ok : Bad, "Container");
        }

        /// <summary>
        /// 컨테이너가 만드는 영구 서비스들. 여기서 보는 것은 <b>다리가 이어졌는가</b>다.
        ///
        /// <c>Instance</c> 가 null 이면 <see cref="GameContainer"/> 의 배선이 빠진 것이다.
        /// 이 검사가 있는 이유는, 배선이 빠져도 <b>대부분의 화면은 멀쩡히 뜨고</b>
        /// 그 매니저를 실제로 쓰는 순간에야 터지기 때문이다.
        /// </summary>
        private static void CheckPersistentServices(StringBuilder sb)
        {
            Check(sb, "PointManager", PointManager.Instance,
                () => "골드 " + PointManager.Instance.Get(PointType.Gold));

            Check(sb, "DiceLevelManager", DiceLevelManager.Instance,
                () => "Normal Lv" + DiceLevelManager.Instance.GetLevel(DiceType.Normal));

            Check(sb, "EquipmentManager", EquipmentManager.Instance,
                () => "무기 Lv" + EquipmentManager.Instance.GetLevel(EquipmentType.Weapon));

            Check(sb, "RelicManager", RelicManager.Instance,
                () => "소환 " + RelicManager.Instance.SummonCount + "회");

            Check(sb, "StageProgressManager", StageProgressManager.Instance,
                () => "선택 " + StageProgressManager.Instance.GetSelectedStageIndex() +
                      " / 해금 " + StageProgressManager.Instance.GetHighestUnlockedStageIndex());

            Check(sb, "StageRewardManager", StageRewardManager.Instance,
                () => "마일스톤 " + StageRewardManager.Instance.GetTotalCount() +
                      " / 수령가능 " + (StageRewardManager.Instance.HasClaimableReward() ? "있음" : "없음"));

            Check(sb, "StageStarManager", StageStarManager.Instance,
                () => "별 " + StageStarManager.Instance.GetTotalStarCount() +
                      "/" + StageStarManager.Instance.GetMaxStarCount());

            Check(sb, "IdleRewardManager", IdleRewardManager.Instance,
                () => "자동전투 " + IdleRewardManager.Instance.GetAutoBattleElapsed().TotalMinutes.ToString("0") + "분");

            Check(sb, "RunHistoryManager", RunHistoryManager.Instance, () => null);
            Check(sb, "AOSBackBtnManager", AOSBackBtnManager.Instance, () => null);
        }

        /// <summary>
        /// 데이터베이스가 <b>진짜 에셋인지 폴백인지</b> 본다.
        ///
        /// 폴백은 화면이 뜨긴 하지만 값이 전부 코드 기본값이라, 그 상태로 플레이하면
        /// "밸런스가 이상하다"로 보인다. 원인이 배선이라는 것을 여기서 알 수 있어야 한다.
        /// </summary>
        private static void CheckDatabases(StringBuilder sb)
        {
            Line(sb, StaticResource.Instance != null ? Ok : Bad, "StaticResource");

            bool realStage = StageDatabaseProvider.HasRealDatabase;
            Line(sb, realStage ? Ok : Bad, "StageDatabase",
                realStage
                    ? "스테이지 " + StageDatabaseProvider.GetDatabase().StageCount + "개"
                    : "폴백(코드 기본값)을 쓰고 있다");
        }

        /// <summary>
        /// 7단계 스키마가 실제로 왕복하는지 본다. <b>파일은 건드리지 않는다</b> —
        /// 직렬화·역직렬화만 메모리에서 돌린다. 진단이 세이브를 바꾸면 안 된다.
        /// </summary>
        private static void CheckSaveRoundTrip(StringBuilder sb)
        {
            try
            {
                var state = new Core.SaveState();
                state.Points["Gold"] = 12345;
                state.Stage.SelectedIndex = 7;

                string json = Core.SaveSerializer.Serialize(state);
                Core.SaveState back = Core.SaveSerializer.Deserialize(json);

                bool same = back.Points["Gold"] == 12345 && back.Stage.SelectedIndex == 7;
                Line(sb, same ? Ok : Bad, "SaveState 왕복", json.Length + "바이트");
                Line(sb, Ok, "저장 경로", SavePaths.SaveFilePath);

                // 7.5 이후 이 파일이 유일한 진행도다. 세이브를 읽지 못한 세션은 쓰지도
                // 않는데(SaveService.WriteBlocked), 그 사실을 알려 주는 에러 로그는
                // <b>세션당 한 번만</b> 찍는다 — 거래마다 찍으면 로그를 못 읽게 되기 때문이다.
                // 그 한 줄을 놓치면 "이번 판이 통째로 저장 안 되고 있다"를 모른 채 계속
                // 플레이하게 되므로, 상태 자체를 여기서 계속 보여 준다.
                SaveService save = GameContainer.SaveService;
                if (save == null)
                    Line(sb, Bad, "SaveService", "없다 — 저장이 아예 안 된다");
                else if (save.WriteBlocked)
                    Line(sb, Bad, "저장 차단됨", "세이브를 읽지 못해 이 세션은 쓰지 않는다");
                else
                    Line(sb, Ok, "저장 가능");
            }
            catch (Exception ex)
            {
                Line(sb, Bad, "SaveState 왕복", ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// 씬 전환 배선. <b>빌드 세팅 누락을 여기서 잡는 것이 핵심이다</b> —
        /// 빌드 목록에서 빠진 씬은 에디터에서는 잘 열리다가 실기에서만 못 연다.
        /// </summary>
        /// <summary>
        /// 배틀 스코프가 섰는지, 창구가 채워졌는지. (8.3b)
        ///
        /// <b>왜 F9 가 봐야 하나.</b> 스코프 빌드 실패는 <c>VContainerException</c> 으로
        /// 시끄럽게 터지지만, <b>스코프가 아예 안 만들어진 경우</b>는 조용하다 —
        /// <c>sceneLoaded</c> 구독이 빠지거나 씬 이름이 어긋나면 아무 일도 일어나지 않고
        /// 게임은 옛 <c>.Instance</c> 경로로 그대로 돈다. 트랜치를 진행하는 동안
        /// <b>그 조용한 실패가 정확히 우리가 못 보는 것</b>이라 여기서 계속 묻는다.
        /// </summary>
        private static void CheckBattleScope(StringBuilder sb)
        {
            IBattleRefs battle = GameContainer.Battle;
            if (battle == null)
            {
                Line(sb, Bad, "BattleContext", "루트에 등록되지 않았다");
                return;
            }

            bool inBattle = SceneCatalog.Current() == SceneId.Battle;
            if (!inBattle)
            {
                // 로비에서 IsActive 가 true 면 스코프가 안 치워진 것이다. 파괴된
                // 오브젝트를 가리키는 상태라 나중에 엉뚱한 곳에서 터진다.
                Line(sb, battle.IsActive ? Bad : NA, "전투 씬이 아니다",
                    battle.IsActive ? "그런데 창구가 살아 있다 — 스코프가 안 치워졌다" : "창구 비어 있음(정상)");
                return;
            }

            if (!battle.IsActive)
            {
                Line(sb, Bad, "배틀 스코프", "BattleScene 인데 창구가 비었다 — 스코프가 안 섰다");
                return;
            }

            Line(sb, Ok, "배틀 스코프", "창구 연결됨");

            // 하나라도 null 이면 Bind 가 반쪽으로 끝난 것이다.
            var refs = new (string Name, object Value)[]
            {
                ("GameManager", battle.Game), ("PlayerController", battle.Player),
                ("MonsterManager", battle.Monsters), ("MonsterSpawner", battle.Spawner),
                ("AttackContent", battle.Attack), ("MergeSystem", battle.Merge),
                ("UIBoard", battle.Board), ("UIDiceBoardUI", battle.BoardUI),
                ("UIDiceSummonSystem", battle.Summon), ("DiceTypeStarManager", battle.DiceStars),
                ("ElementUpgradeManager", battle.ElementUpgrade), ("BulletPool", battle.Bullets),
                ("BulletEffectPool", battle.BulletEffects), ("DamageTextPool", battle.DamageTexts),
            };

            int missing = 0;
            foreach ((string name, object value) in refs)
            {
                if (value == null || value.Equals(null))
                {
                    Line(sb, Bad, name, "창구에 null 이다");
                    missing++;
                }
            }

            if (missing == 0)
                Line(sb, Ok, "매니저 14개", "전부 연결됨");
        }

        private static void CheckSceneFlow(StringBuilder sb)
        {
            SceneRouter router = GameContainer.SceneRouter;
            Line(sb, router != null ? Ok : Bad, "SceneRouter",
                router != null && router.IsTransitioning ? "전환 중" : null);

            var fade = UnityEngine.Object.FindFirstObjectByType<FadeView>();
            Line(sb, fade != null ? Ok : Bad, "FadeView",
                fade != null && fade.IsCovering ? "화면을 덮고 있다" : null);

            SceneId? current = SceneCatalog.Current();
            Line(sb, current.HasValue ? Ok : Bad, "현재 씬 인식",
                current.HasValue ? current.Value.ToString() : "SceneCatalog 에 없는 씬이다");

            foreach (SceneId id in Enum.GetValues(typeof(SceneId)))
            {
                bool inBuild = SceneCatalog.IsInBuild(id);
                Line(sb, inBuild ? Ok : Bad, "빌드 포함: " + id,
                    inBuild ? SceneCatalog.NameOf(id) : "빌드 세팅에 없다 — 실기에서만 못 연다");
            }
        }

        /// <summary>
        /// 팝업 배선. <b>카탈로그 누락을 여기서 잡는 것이 핵심이다</b> —
        /// 등재가 빠지면 그 창은 열 방법이 없는데, 그 사실이 창을 열려고 시도할 때까지
        /// 드러나지 않는다.
        /// </summary>
        private static void CheckUI(StringBuilder sb)
        {
            Line(sb, GameContainer.UI != null ? Ok : Bad, "UIService");

            DialogCatalog catalog = StaticResource.Instance != null
                ? StaticResource.Instance.DialogCatalog
                : null;

            if (catalog == null)
            {
                Line(sb, Bad, "DialogCatalog",
                    "StaticResource 프리팹의 DialogCatalog 슬롯이 비었다 — 팝업이 하나도 안 열린다");
                return;
            }

            Line(sb, Ok, "DialogCatalog", catalog.Entries.Count + "개 등재");

            System.Collections.Generic.List<string> problems = catalog.Validate();
            if (problems.Count == 0)
            {
                Line(sb, Ok, "카탈로그 검사", "문제 없음");
                return;
            }

            foreach (string p in problems)
                Line(sb, Bad, "카탈로그", p);
        }

        private static void CheckBattleServices(StringBuilder sb)
        {
            IBattleRefs battle = GameContainer.Battle;
            bool inBattle = battle.IsActive;
            if (!inBattle)
            {
                Line(sb, NA, "전투 씬이 아니다", "이 구역은 BattleScene 에서만 의미가 있다");
                return;
            }

            Line(sb, Ok, "GameManager",
                "웨이브 " + battle.Game.CurrentWaveIndex +
                " / 상태 " + battle.Game.inGameState);

            Line(sb, battle.Game.wall != null ? Ok : Bad, "Wall",
                battle.Game.wall != null
                    ? battle.Game.wall.CurrentHp + "/" + battle.Game.wall.TotalHp
                    : null);

            Line(sb, battle.Monsters != null ? Ok : Bad, "MonsterManager");
            Line(sb, battle.Board != null ? Ok : Bad, "UIBoard");
            Line(sb, battle.Summon != null ? Ok : Bad, "UIDiceSummonSystem");
            Line(sb, battle.DiceStars != null ? Ok : Bad, "DiceTypeStarManager");
        }

        /// <summary>
        /// 세부 정보를 읽다가 예외가 나도 진단 전체가 죽지 않게 감싼다.
        /// 진단 도구가 진단 중에 터지면 아무것도 못 본다.
        /// </summary>
        private static void Check(StringBuilder sb, string name, object instance, Func<string> detail)
        {
            if (instance == null)
            {
                Line(sb, Bad, name, "Instance 가 null — GameContainer 배선 확인");
                return;
            }

            string text;
            try
            {
                text = detail != null ? detail() : null;
            }
            catch (Exception ex)
            {
                Line(sb, Bad, name, "값을 읽다 예외: " + ex.GetType().Name + " " + ex.Message);
                return;
            }

            Line(sb, Ok, name, text);
        }
    }
}
#endif
