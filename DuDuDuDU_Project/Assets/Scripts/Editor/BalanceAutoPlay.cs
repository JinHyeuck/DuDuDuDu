using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Hunting;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.Tutorial;
using UnityEngine.SceneManagement;

namespace OJ.EditorTools
{
    /// <summary>
    /// 밸런스 측정용 자동 플레이. <b>에디터 전용이라 빌드에 안 들어간다</b>
    /// (<c>OJ.Game.Editor.asmdef</c> 가 <c>includePlatforms: ["Editor"]</c>).
    ///
    /// <b>왜 필요한가.</b> 스테이지 수치를 고치고 "이게 깰 수 있는 난이도인가" 를 아는
    /// 방법이 사람이 10분씩 직접 플레이하는 것뿐이었다. 그래서 스테이지 열 개를 조정하려면
    /// 두 시간이 들고, 한 번 고치면 다시 재 볼 수 없어 <b>수치가 측정 없이 결정된다.</b>
    /// 이 도구는 열 스테이지를 2~3분에 재서, 수치를 고칠 때마다 다시 잴 수 있게 한다.
    ///
    /// <b>봇은 사람이 아니다.</b> 정책이 단순하다 — SP 가 되면 소환하고, 같은 종류·성급
    /// 쌍이 있으면 머지하고, 4성이 생기면 진화시키고, 더 할 것이 없으면 웨이브를 시작한다.
    /// 속성 강화·현상금·배치·제거는 아예 안 쓴다. 그래서 측정값은 <b>"대충 플레이한
    /// 하한"</b>이고 사람은 이보다 잘 버틴다. 수치를 이 결과에 딱 맞추지 말고 여유를 두고
    /// 읽을 것 — 봇이 벽 80% 로 깨는 스테이지가 사람에게는 퍼펙트다.
    ///
    /// 조작은 게임의 공개 경로를 탄다(소환 버튼 <c>onClick</c>,
    /// <c>MergeSystem.TryMerge/TryEvolve</c>, <c>GameManager.OnClick_PlayUI</c>) —
    /// 내부 상태를 직접 쓰지 않으므로 측정이 실제 플레이와 갈라지지 않는다. 유일한 예외가
    /// <c>ForceFreshPower</c> 의 리플렉션이고, 그것은 "신규 설치 상태" 를 만드는 준비이지
    /// 플레이 조작이 아니다.
    ///
    /// <b>한 프레임에 한 동작만 한다.</b> <c>Destroy</c> 가 프레임 끝에 실행되므로 한
    /// 프레임에 머지를 두 번 하면 이미 사라진 다이스를 또 집는다.
    ///
    /// 사용법(플레이 모드가 이미 돌고 있어야 한다):
    /// <code>
    ///   BalanceAutoPlay.Start(3)              // 3스테이지 한 판
    ///   BalanceAutoPlay.StartRange(1, 10)     // 1~10 연속. 로그가 한 파일에 쌓인다
    /// </code>
    /// </summary>
    public static class BalanceAutoPlay
    {
        private const int DefaultMinBoardDice = 8;

        /// <summary>
        /// 판이 끝난 뒤 <b>게임이 스스로 로비로 돌아올 때까지</b> 기다려 주는 상한(에디터 프레임).
        ///
        /// <b>여기서 씬을 직접 밀어넣지 않는다.</b> 그렇게 했다가 실제로 깨졌다 —
        /// 판이 끝나면 게임도 결과창 뒤에 <c>CoReturnToLobby</c> 로 로비로 가는데, 거기에
        /// 봇의 <c>LoadLobby</c> + <c>LoadBattle</c> 이 겹치면 <b>스코프 없는 BattleScene</b>
        /// 이 떠서 씬의 모든 컴포넌트가 프레임마다 NRE 를 쏟는다(<c>[Inject] battle</c> 이 null).
        /// 사람이 게임 뷰를 한 번 클릭해 전환이 하나 더 끼는 것만으로도 그 상태가 된다.
        ///
        /// 그래서 순서를 뒤집었다 — <b>기다리다가, 로비이고 전환이 끝났을 때만</b> 다음 판을
        /// 연다. 이 상한을 넘기면 그때 한 번 직접 요청한다(그 경우에만 위험을 감수한다).
        /// </summary>
        private const int LobbyWaitFrames = 1800;

        /// <summary>
        /// 아무 진행이 없을 때 그 판을 포기하는 상한(에디터 프레임, 60fps 기준 약 1분).
        /// 측정은 열 판을 연속으로 도는 것이라 한 판이 멈추면 뒤가 전부 날아간다.
        /// </summary>
        private const int StallFrames = 3600;

        private static bool running;
        private static int stageIndex;
        private static float speed;
        private static bool evolve;
        private static int minBoardDice;
        private static bool battleRequested;
        private static int lastWaveIndex;
        private static bool wasWave;
        private static float waveStartTime;
        private static readonly List<string> lines = new List<string>();
        private static string outPath;

        private static readonly List<int> queue = new List<int>();
        private static int queueIndex;
        private static bool waitingForLobby;
        private static int waitFrames;
        private static int idleFrames;

        /// <summary>한 스테이지만 측정한다.</summary>
        public static string Start(
            int stage,
            float timeScale = 6f,
            bool evolveWhenPossible = true,
            int minDice = DefaultMinBoardDice,
            bool freshPower = true)
        {
            queue.Clear();
            queue.Add(Mathf.Max(1, stage));
            queueIndex = 0;
            lines.Clear();
            outPath = Path.Combine(Path.GetTempPath(), "oj_balance_stage" + Mathf.Max(1, stage) + ".log");
            return Begin(timeScale, evolveWhenPossible, minDice, freshPower);
        }

        /// <summary>
        /// 여러 스테이지를 연속으로 측정한다. 로그는 <c>oj_balance_range.log</c> 한 파일에 쌓인다.
        ///
        /// 판이 끝나면 로비로 돌아가 다음 스테이지를 연다. 매 판마다 장비·다이스 레벨을
        /// 다시 1 로 되돌리므로 <b>열 판이 모두 같은 전투력에서 측정된다</b> —
        /// 측정 중에 성장이 섞이면 "스테이지가 쉬워진 것" 과 "내가 강해진 것" 이 구분되지 않는다.
        /// </summary>
        public static string StartRange(
            int from,
            int to,
            float timeScale = 6f,
            bool evolveWhenPossible = true,
            int minDice = DefaultMinBoardDice,
            bool freshPower = true)
        {
            queue.Clear();
            for (int i = Mathf.Max(1, from); i <= Mathf.Max(1, to); i++)
                queue.Add(i);
            queueIndex = 0;
            lines.Clear();
            outPath = Path.Combine(Path.GetTempPath(), "oj_balance_range.log");
            return Begin(timeScale, evolveWhenPossible, minDice, freshPower);
        }

        private static string Begin(float timeScale, bool evolveWhenPossible, int minDice, bool freshPower)
        {
            if (!Application.isPlaying)
                return "ERROR: 플레이 모드가 아니다. editor_play 를 먼저 할 것.";

            speed = Mathf.Clamp(timeScale, 1f, 10f);
            evolve = evolveWhenPossible;
            minBoardDice = Mathf.Max(1, minDice);
            freshEachStage = freshPower;

            string error = PrepareStage(queue[0]);
            if (error != null)
                return error;

            // 이 도구는 준비 단계에서 소환·머지하고 시작 버튼으로 웨이브를 여는 흐름을 전제로 측정한다.
            // 실시간이 정식 규칙이 되었으므로 도는 동안만 레거시 흐름으로 돌리고 Stop 에서 끈다.
            GameManager.LegacySetupFlow = true;

            running = true;
            waitingForLobby = false;
            waitFrames = 0;
            idleFrames = 0;
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            return "started -> " + outPath;
        }

        private static bool freshEachStage;

        /// <summary>진행도·전투력·선택 스테이지를 세운다. 실패하면 사람이 읽을 이유를 돌려준다.</summary>
        private static string PrepareStage(int stage)
        {
            stageIndex = Mathf.Max(1, stage);
            battleRequested = false;
            lastWaveIndex = 0;
            wasWave = false;

            if (freshEachStage)
                ForceFreshPower();

            // 튜토리얼 예약이 남아 있으면 GameManager 가 내 스테이지 대신 그것을 연다.
            if (TutorialProgressManager.Instance != null)
                TutorialProgressManager.Instance.MarkInGameCompleted();

            StageProgressManager progress = StageProgressManager.Instance;
            if (progress == null)
                return "ERROR: StageProgressManager 가 없다.";

            // 해금돼 있어야 SelectStage 가 먹는다. 앞 스테이지를 최소 클리어로 열어 둔다.
            for (int i = 1; i < stageIndex; i++)
                progress.RecordStageClear(i, StageClearGrade.Minimum);
            progress.SelectStage(stageIndex);

            if (progress.GetSelectedStageIndex() != stageIndex)
                return "ERROR: 스테이지 " + stageIndex + " 를 선택할 수 없다(해금 실패).";

            int eq = EquipmentManager.Instance != null
                ? EquipmentManager.Instance.GetTotalEquipmentAttack()
                : -1;
            Log("# stage " + stageIndex + " eq=" + eq + " speed=" + speed
                + " evolve=" + evolve + " minDice=" + minBoardDice);
            return null;
        }

        public static void Stop()
        {
            GameManager.LegacySetupFlow = false;
            running = false;
            EditorApplication.update -= Update;
        }

        public static string Report()
        {
            return string.Join("\n", lines.ToArray());
        }

        public static bool IsRunning => running;

        /// <summary>
        /// 장비·다이스 레벨을 신규 설치 상태(전부 1)로 되돌린다. 장비는 세터가 없어
        /// 리플렉션으로 사전을 직접 쓴다 — 측정 준비 전용이고, 이것 때문에 게임 코드에
        /// 세터를 뚫지 않기 위한 선택이다.
        /// </summary>
        private static void ForceFreshPower()
        {
            DiceLevelManager diceLevels = DiceLevelManager.Instance;
            if (diceLevels != null)
            {
                foreach (DiceType type in Enum.GetValues(typeof(DiceType)))
                {
                    if (type == DiceType.Max)
                        continue;

                    diceLevels.SetLevel(type, 1, false);
                }
            }

            EquipmentManager equipment = EquipmentManager.Instance;
            if (equipment == null)
                return;

            FieldInfo field = typeof(EquipmentManager).GetField(
                "levels", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                return;

            Dictionary<EquipmentType, int> levels = field.GetValue(equipment) as Dictionary<EquipmentType, int>;
            if (levels == null)
                return;

            foreach (EquipmentType type in Enum.GetValues(typeof(EquipmentType)))
                levels[type] = 1;
        }

        private static void Update()
        {
            if (!running)
                return;

            if (!Application.isPlaying)
            {
                Log("# 플레이 모드가 끝났다");
                Stop();
                return;
            }

            // 판이 끝난 뒤. 게임이 스스로 로비로 돌아오기를 기다린다.
            if (waitingForLobby)
            {
                waitFrames++;
                if (IsLobbyIdle())
                {
                    waitingForLobby = false;
                    string error = PrepareStage(queue[queueIndex]);
                    if (error != null)
                    {
                        Log(error);
                        Stop();
                    }

                    return;
                }

                if (waitFrames > LobbyWaitFrames)
                {
                    Log("# 로비로 안 돌아와서 직접 요청한다");
                    waitFrames = 0;
                    SceneFlowManager.LoadLobby();
                }

                return;
            }

            IBattleRefs battle = GameContainer.Battle;
            if (battle == null || !battle.IsActive || battle.Game == null)
            {
                // 전투 씬을 여는 것도 <b>로비에서 전환이 끝난 상태일 때만</b> 한다.
                // 전환 중에 밀어넣으면 스코프 없는 BattleScene 이 뜬다(위 상수 주석).
                if (!battleRequested && IsLobbyIdle())
                {
                    battleRequested = true;
                    SceneFlowManager.LoadBattle();
                }

                WatchStall();
                return;
            }

            GameManager game = battle.Game;
            WatchStall();

            if (game.inGameState == InGameState.None)
            {
                // 첫 프레임(아직 Setting 으로 못 간 상태)과 판이 끝난 상태가 같은 값이다.
                // 웨이브를 하나라도 지났으면 끝난 것이다.
                if (lastWaveIndex > 0)
                {
                    int total = game.CurrentStageData != null ? game.CurrentStageData.totalWaves : 0;
                    Log(string.Format("RESULT stage={0} wave={1}/{2} wallHp={3} grade={4}",
                        stageIndex, game.CurrentWaveIndex, total, game.WallHp, GradeOf(game)));
                    NextStageOrStop();
                }

                return;
            }

            if (game.inGameState == InGameState.Wave)
            {
                // GameManager 가 웨이브 진입에서 timeScale 을 timeSpeed(1) 로 써 버린다.
                // 그 뒤에 덮어써야 측정이 빨라진다.
                if (Time.timeScale < speed - 0.01f)
                    Time.timeScale = speed;

                // <b>벽이 깨지면 여기서 끝낸다.</b> 그냥 두면 사망 되돌리기 팝업이 뜬 채로
                // 봇이 다음 웨이브를 시작해서 <b>벽 0 으로 계속 플레이하는</b> 측정이 된다
                // (실제로 그렇게 나왔다). 어디서 무너졌는지가 이 도구의 답이므로 그 순간을 적는다.
                if (game.WallHp <= 0)
                {
                    Log(string.Format("DEFEAT stage={0} wave={1}/{2} wallHp=0",
                        stageIndex,
                        game.CurrentWaveIndex,
                        game.CurrentStageData != null ? game.CurrentStageData.totalWaves : 0));
                    NextStageOrStop();
                    return;
                }

                wasWave = true;
                return;
            }

            // ── 관리 단계 ──────────────────────────────────────────────────
            // 웨이브가 막 끝난 프레임이면 결과를 먼저 적는다. 웨이브 번호 갱신을 기준으로
            // 적으면 다음 웨이브의 "start" 줄이 먼저 나와 로그가 거꾸로 읽힌다.
            if (wasWave)
            {
                wasWave = false;
                Log(string.Format("wave {0,2} done  wallHp={1,4} sec={2,5:0.0} {3}",
                    game.CurrentWaveIndex,
                    game.WallHp,
                    Time.realtimeSinceStartup - waveStartTime,
                    BoardSummary(battle)));
            }

            lastWaveIndex = game.CurrentWaveIndex;

            if (DoOneSetupAction(battle))
                return;

            Log(string.Format("wave {0,2} start sp={1,4} stone={2,3} {3}",
                game.CurrentWaveIndex + 1,
                battle.BattlePoints.Get(BattlePointType.SummonPoint),
                battle.BattlePoints.Get(BattlePointType.EnhanceStone),
                BoardSummary(battle)));
            waveStartTime = Time.realtimeSinceStartup;
            game.OnClick_PlayUI();
        }

        /// <summary>큐에 남은 스테이지가 있으면 <b>게임이 로비로 돌아오기를 기다린다.</b></summary>
        private static void NextStageOrStop()
        {
            queueIndex++;
            if (queueIndex >= queue.Count)
            {
                Stop();
                return;
            }

            // 배속을 되돌린다. 결과창과 씬 전환은 사람이 보는 속도로 돌아야 한다 —
            // 6배속으로 전환을 돌리면 페이드·코루틴 타이밍이 측정과 무관한 변수가 된다.
            Time.timeScale = 1f;
            waitingForLobby = true;
            waitFrames = 0;
            idleFrames = 0;
        }

        /// <summary>로비이고 씬 전환이 끝났는가. 씬을 여는 모든 판단이 이것을 먼저 본다.</summary>
        private static bool IsLobbyIdle()
        {
            if (SceneCatalog.IdOf(SceneManager.GetActiveScene().name) != SceneId.Lobby)
                return false;

            SceneRouter router = GameContainer.SceneRouter;
            return router == null || !router.IsTransitioning;
        }

        /// <summary>
        /// 진행이 멈춘 판을 포기한다. <b>측정 전체를 지키기 위한 것이다</b> —
        /// 한 판이 멈추면 뒤의 아홉 판이 통째로 날아가고, 그 사실이 로그에 안 남는다.
        /// </summary>
        private static void WatchStall()
        {
            idleFrames++;
            if (idleFrames <= StallFrames)
                return;

            Log(string.Format("STALL stage={0} — 진행이 멈춰 포기한다", stageIndex));
            idleFrames = 0;
            NextStageOrStop();
        }

        private static bool DoOneSetupAction(IBattleRefs battle)
        {
            UIBoard board = battle.Board;
            UIDiceSummonSystem summon = battle.Summon;
            if (board == null || board.diceMap == null || summon == null)
                return false;

            int alive = 0;
            int emptySlots = 0;
            for (int i = 0; i < board.diceMap.Length; i++)
            {
                if (board.diceMap[i] != null)
                    alive++;
                else
                    emptySlots++;
            }

            if (summon.currentSP >= summon.summonCost && emptySlots > 0 && summon.summonButton != null)
            {
                summon.summonButton.onClick.Invoke();
                return true;
            }

            if (alive > minBoardDice && TryMergeOnePair(battle, board))
                return true;

            if (evolve && TryEvolveOne(battle, board))
                return true;

            return false;
        }

        private static bool TryMergeOnePair(IBattleRefs battle, UIBoard board)
        {
            for (int i = 0; i < board.diceMap.Length; i++)
            {
                UIDice a = board.diceMap[i];
                if (a == null || a.Star >= MergeSystem.MaxStar)
                    continue;

                for (int j = i + 1; j < board.diceMap.Length; j++)
                {
                    UIDice b = board.diceMap[j];
                    if (b == null || b.Type != a.Type || b.Star != a.Star)
                        continue;

                    if (battle.Merge.TryMerge(a, b))
                        return true;
                }
            }

            return false;
        }

        private static bool TryEvolveOne(IBattleRefs battle, UIBoard board)
        {
            for (int i = 0; i < board.diceMap.Length; i++)
            {
                UIDice dice = board.diceMap[i];
                if (dice == null || dice.Star < DiceEvolution.EvolveRequiredStar)
                    continue;

                if (battle.Merge.TryEvolve(dice))
                    return true;
            }

            return false;
        }

        private static string BoardSummary(IBattleRefs battle)
        {
            UIBoard board = battle.Board;
            if (board == null || board.diceMap == null)
                return "board=?";

            Dictionary<string, int> counts = new Dictionary<string, int>();
            int total = 0;
            for (int i = 0; i < board.diceMap.Length; i++)
            {
                UIDice dice = board.diceMap[i];
                if (dice == null)
                    continue;

                string key = ShortName(dice.Type) + dice.Star;
                int n;
                counts.TryGetValue(key, out n);
                counts[key] = n + 1;
                total++;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("board(").Append(total).Append(")=");
            foreach (KeyValuePair<string, int> pair in counts)
                sb.Append(pair.Key).Append('x').Append(pair.Value).Append(' ');

            return sb.ToString().TrimEnd();
        }

        private static string ShortName(DiceType type)
        {
            switch (type)
            {
                case DiceType.Normal: return "N";
                case DiceType.Fire: return "F";
                case DiceType.Ice: return "I";
                case DiceType.Thunder: return "T";
                case DiceType.Poison: return "P";
                case DiceType.Tornado: return "TOR";
                case DiceType.Stun: return "STU";
                case DiceType.ArmorBreak: return "ARM";
                case DiceType.Wind: return "WIN";
                case DiceType.Time: return "TIM";
                default: return type.ToString();
            }
        }

        private static string GradeOf(GameManager game)
        {
            int total = game.CurrentStageData != null ? Mathf.Max(1, game.CurrentStageData.wallHp) : 1;
            float ratio = (float)game.WallHp / total;
            if (game.WallHp <= 0)
                return "DEFEAT";
            if (ratio >= 0.999f)
                return "PERFECT";
            if (ratio >= 0.5f)
                return "HALF";

            return "MINIMUM";
        }

        private static void Log(string line)
        {
            // 로그가 찍히는 것이 곧 "진행이 있었다" 다. 감시 타이머를 여기서 되돌린다.
            idleFrames = 0;
            lines.Add(line);
            try
            {
                File.WriteAllText(outPath, string.Join("\n", lines.ToArray()));
            }
            catch (Exception)
            {
                // 파일을 못 써도 측정은 계속한다. Report() 가 메모리 쪽을 돌려준다.
            }
        }
    }
}
