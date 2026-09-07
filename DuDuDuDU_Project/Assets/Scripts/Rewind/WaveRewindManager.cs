using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Analytics;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Hunting;
using OJ.Point;
using OJ.Relic;

namespace OJ.Rewind
{
    /// <summary>어느 몫으로 되돌리는가.</summary>
    public enum RewindSource
    {
        Free = 0,
        Ad = 1,
    }

    /// <summary>
    /// 무엇이 되돌리기를 불렀는가. <b>기록에만 쓴다</b> — 동작은 둘이 완전히 같다.
    ///
    /// 나누는 이유는 나중에 답해야 할 질문이 이 둘을 구분하기 때문이다.
    /// "죽고 나서야 되돌리는가, 죽기 전에 스스로 물러나는가" 는 되돌리기가
    /// 위기를 <b>만드는지</b> 아니면 패배를 <b>늦추기만</b> 하는지를 가른다.
    /// </summary>
    public enum RewindTrigger
    {
        Wave = 0,
        Death = 1,
    }

    /// <summary>
    /// 웨이브 되돌리기. 웨이브가 시작되기 직전 상태를 떠 두었다가 그 자리로 되감는다.
    ///
    /// <b>왜 있는가.</b> "더 강해져야겠다" 는 욕구는 아슬아슬하게 지는 경험에서 나오는데,
    /// 이 게임은 한 판의 호흡이 길어 그 경험 1회의 비용이 크다. 되돌리기는 판 전체가 아니라
    /// <b>위기 구간만</b> 다시 하게 해서 그 비용을 낮춘다.
    ///
    /// <b>그래서 유한해야 한다.</b> 무제한이면 패배가 영영 오지 않고, 욕구의 방아쇠인
    /// 패배 자체가 사라진다 — 기능이 정확히 반대로 작동한다. 남은 횟수를 화면에 띄우는 것도
    /// 같은 이유다. "이제 진짜 마지막 한 번" 이 긴장을 만든다.
    ///
    /// <b>MonoBehaviour 가 아니다.</b> 만지는 것이 상태 한 덩이라 씬에 놓을 이유가 없고,
    /// 놓으려면 <c>BattleScene.unity</c> 를 편집해야 한다(AGENTS 절대 규칙 3).
    /// <c>BountyManager</c>·<c>DamageContributionTracker</c> 와 같은 방식으로
    /// 배틀 스코프가 만든다.
    ///
    /// <b>무한의 탑에서는 통째로 꺼진다.</b> 탑에는 관리 단계가 없어서(기획서 3.2)
    /// 되돌아갈 자리가 없고, 재도전이 무비용이라 되돌리기가 이미 하는 일을 한다.
    /// </summary>
    [Preserve]
    public sealed class WaveRewindManager
    {
        /// <summary>
        /// 배틀 매니저로 가는 창구. 생성자에서 받아 들고만 있고, 실제로 읽는 것은
        /// 웨이브가 시작된 뒤다 — <c>BountyManager</c> 와 같은 규약이다.
        /// </summary>
        private readonly IBattleRefs battle;

        private WaveRewindSnapshot snapshot;
        private int freeUsed;
        private int adUsed;

        /// <summary>이번 웨이브에 죽은 것들. 되감기 연출이 읽는다.</summary>
        private readonly WaveDeathLog deathLog = new WaveDeathLog();

        /// <summary>
        /// 이번 웨이브가 시작된 <b>실제 시각</b>. 되감기 길이의 기준이다.
        ///
        /// <b><c>Time.unscaledTime</c> 인 이유.</b> 되감기는 "유저가 본 것" 을 거꾸로 트는
        /// 것이라 기준도 유저가 본 시간이어야 한다. 게임 시간을 쓰면 3배속으로 20초를 본
        /// 웨이브가 60초로 잡혀 되감기만 3배 길어진다.
        /// </summary>
        private float waveStartRealTime;

        /// <summary>
        /// 이번 웨이브 동안 <b>멈춰 있던</b> 시간의 합. 웨이브 길이에서 뺀다.
        ///
        /// <b>확인 창이 그 시간이다.</b> 되돌릴지 묻는 동안 <c>timeScale</c> 은 0 이지만
        /// 실제 시계는 계속 간다. 이걸 안 빼면 <b>10초 고민한 사람의 되감기가 10초 길어진다</b> —
        /// 화면에서는 아무 일도 없던 시간인데 되감기는 그만큼 더 돈다.
        /// </summary>
        private float pausedTotal;

        /// <summary>지금 멈춘 시각. 0 이면 안 멈춰 있다.</summary>
        private float pauseStartedAt;

        /// <summary>
        /// 되감기 연출. <b>한 번만 만든다</b> — 안에 유령 풀이 있어서 되돌릴 때마다
        /// 새로 만들면 쓰고 버린 오브젝트 무더기가 판마다 쌓인다.
        /// </summary>
        private WaveRewindPresenter presenter;

        /// <summary>
        /// 되돌릴 수 있는지가 바뀌었다. 버튼이 듣는다.
        ///
        /// <b>버튼이 매 프레임 물어보지 않게 하려고 있다.</b> 폴링으로 두면
        /// <c>ChangeState</c> 한 곳에 모아 둔 "상태를 아는 곳은 하나" 규약이 깨진다.
        /// </summary>
        public event Action OnAvailabilityChanged;

        public WaveRewindManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        private static WaveRewindSettings Settings => WaveRewindSettingsProvider.Settings;

        /// <summary>뜬 상태가 있는가. 웨이브를 한 번도 시작하지 않았으면 없다.</summary>
        public bool HasSnapshot => snapshot != null;

        public int RemainingFree => Mathf.Max(0, Settings.freeCountPerRun - freeUsed);

        /// <summary>
        /// 광고로 남은 횟수.
        ///
        /// <b>볼 광고가 없으면 0 이다</b> — 설정의 <c>adCountPerRun</c> 이 몇이든 상관없다.
        /// 지금 이 프로젝트에는 광고 SDK 가 0 줄이라 <see cref="NullRewardedAdService"/> 가
        /// 꽂혀 있고, 따라서 이 값은 언제나 0 이다. 설정값을 그대로 돌려주면
        /// <b>눌러도 아무 일이 없는 버튼</b>이 생기고 그건 고장으로 읽힌다.
        /// </summary>
        public int RemainingAd
        {
            get
            {
                IRewardedAdService ads = GameContainer.RewardedAds;
                if (ads == null || !ads.IsAvailable)
                    return 0;

                return Mathf.Max(0, Settings.adCountPerRun - adUsed);
            }
        }

        public int RemainingCount => RemainingFree + RemainingAd;

        /// <summary>
        /// 지금 되돌릴 수 있는가. <b>발동 경로 둘이 공유하는 조건</b>이고,
        /// 경로별 조건(<c>allowDuringWave</c>·<c>allowOnDeath</c>)은 각자 더 본다.
        /// </summary>
        public bool CanRewind
        {
            get
            {
                if (!Settings.rewindEnabled)
                    return false;

                // 탑에는 되돌아갈 관리 단계가 없다. 창구가 비었을 수도 있으므로
                // (씬을 내리는 중) null 을 사고로 보지 않고 그냥 못 한다고 답한다.
                if (battle == null || battle.Tower == null || battle.Tower.IsActive)
                    return false;

                return HasSnapshot && RemainingCount > 0;
            }
        }

        public bool CanRewindDuringWave => CanRewind && Settings.allowDuringWave;

        public bool CanRewindOnDeath => CanRewind && Settings.allowOnDeath;

        /// <summary>
        /// 판을 새로 시작한다. <c>GameManager.InitializeStage</c> 가 부른다.
        ///
        /// <b>횟수는 판이 소유한다.</b> 씬을 다시 로드하면 이 객체도 새로 태어나 우연히
        /// 초기화되지만, 그 우연에 기대면 씬 재로드 없이 판을 다시 시작하게 되는 날
        /// 지난 판에 쓴 횟수가 그대로 따라온다.
        /// </summary>
        public void BeginRun()
        {
            snapshot = null;
            freeUsed = 0;
            adUsed = 0;
            deathLog.Clear();
            ResetWaveClock();
            OnAvailabilityChanged?.Invoke();
        }

        private void ResetWaveClock()
        {
            waveStartRealTime = Time.unscaledTime;
            pausedTotal = 0f;
            pauseStartedAt = 0f;
        }

        /// <summary>
        /// 웨이브가 시작된 뒤 <b>실제로 흐른</b> 시간. 멈춰 있던 만큼은 빠져 있다.
        /// </summary>
        private float WaveElapsedAt(float realTime)
        {
            return Mathf.Max(0f, realTime - waveStartRealTime - pausedTotal);
        }

        /// <summary>
        /// 되돌릴지 묻기 시작했다. <b>두 발동 경로가 시간을 멈추기 직전에 부른다</b>
        /// (<c>GameManager.OnClick_Rewind</c> · <c>OnWallDestroyed</c>).
        ///
        /// 여기서부터 <see cref="NotifyOfferClosed"/> 까지가 웨이브 길이에서 빠질 구간이다.
        /// </summary>
        public void NotifyRewindOffered()
        {
            pauseStartedAt = Time.unscaledTime;
        }

        /// <summary>
        /// 물어본 것이 닫혔는데 <b>되돌리지는 않았다</b>(취소). 멈춰 있던 만큼을 적립한다.
        ///
        /// 되돌린 경우에는 부를 필요가 없다 — 그 판의 웨이브 시계는 어차피 거기서 끝난다.
        /// </summary>
        public void NotifyOfferClosed()
        {
            if (pauseStartedAt <= 0f)
                return;

            pausedTotal += Mathf.Max(0f, Time.unscaledTime - pauseStartedAt);
            pauseStartedAt = 0f;
        }

        /// <summary>
        /// 몬스터가 죽었다. <c>Monster.TakeDamage</c> 가 풀에 돌려보내기 <b>전에</b> 부른다.
        ///
        /// <b>연출을 끄면 아예 적지 않는다.</b> 이 기록의 유일한 용처가 되감기 그림이라,
        /// 안 쓸 것을 웨이브 내내 쌓아 둘 이유가 없다.
        /// </summary>
        public void RecordDeath(Monster monster, Vector3 deathPosition)
        {
            if (!Settings.rewindEnabled || !Settings.playRewindPresentation)
                return;

            if (battle == null || battle.Tower == null || battle.Tower.IsActive)
                return;

            // 웨이브 시작을 0 으로 놓은 상대 시각으로 바꿔 넘긴다. 연출은 커서 하나로
            // 이 타임라인을 거꾸로 훑으므로, 절대 시각을 넘기면 되감기 시작점을
            // 연출이 다시 계산해야 한다.
            deathLog.Record(
                monster,
                deathPosition,
                spawnTime: WaveElapsedAt(monster.SpawnRealTime),
                deathTime: WaveElapsedAt(Time.unscaledTime));
        }

        /// <summary>
        /// 웨이브 시작 직전 상태를 뜬다. <c>GameManager.ChangeState</c> 의 웨이브 진입에서,
        /// <b><c>Run.WaveIndex++</c> 보다 먼저</b> 부른다 — 그 한 줄이 순서의 전부다.
        /// 뒤에서 부르면 되돌린 뒤 웨이브 번호가 하나 앞서 있다.
        ///
        /// <b>여기 담기지 않는 것과 그 이유.</b>
        /// <list type="bullet">
        /// <item><c>DiceTypeStarManager</c> 집계 — 보드를 되살리면서 다시 세운다.
        ///   두 벌로 들고 있으면 어긋날 자리가 생긴다.</item>
        /// <item><c>PlayerController</c> 쿨다운 — 되돌리면 어차피 전부 판다.</item>
        /// <item>골드·다이아·스태미나 — 웨이브 중에 변하지 않는다.</item>
        /// <item>장비·유물 레벨·다이스 레벨·스테이지 진행도 — 판을 넘어 사는 영구 상태다.</item>
        /// <item><c>DamageContributionTracker</c> — <b>일부러 남긴다.</b> 되돌리기의 목적이
        ///   편성을 다시 짜게 하는 것인데, 방금 웨이브에서 어떤 다이스가 일했는지가
        ///   그 판단의 유일한 근거다. 다시 시작하면 <c>ChangeState</c> 가 알아서 0 으로 돌린다.</item>
        /// </list>
        /// </summary>
        public void Capture()
        {
            if (battle == null || !battle.IsActive)
                return;

            // 탑에서는 뜰 이유가 없다. 떠 두면 HasSnapshot 이 true 가 되어 아래 조건들이
            // 탑 검사 하나에만 기대게 된다 — 조건 하나에 기대는 것과 애초에 없는 것은 다르다.
            if (battle.Tower != null && battle.Tower.IsActive)
                return;

            var captured = new WaveRewindSnapshot
            {
                Run = battle.Game.Run.CaptureWaveSnapshot(),
                Summon = battle.Summon.CaptureSnapshot(),
                RelicFlags = RelicManager.Instance != null
                    ? RelicManager.Instance.CaptureRunFlags()
                    : default,
                EnhanceStone = PointManager.Instance != null
                    ? PointManager.Instance.Get(PointType.BattleEnhanceStone)
                    : 0,
                ElementLevels = CaptureElementLevels(),
                Board = CaptureBoard(),
            };

            snapshot = captured;

            // 지난 웨이브의 주검은 이번 되감기의 대상이 아니다. 안 지우면 되돌릴 때
            // 앞 웨이브에서 죽은 것까지 같이 살아난다.
            deathLog.Clear();
            ResetWaveClock();

            OnAvailabilityChanged?.Invoke();
        }

        private int[] CaptureElementLevels()
        {
            var levels = new int[(int)ElementType.Max];
            for (int i = 0; i < levels.Length; i++)
                levels[i] = battle.ElementUpgrade.GetLevel((ElementType)i);

            return levels;
        }

        private WaveRewindSnapshot.BoardDice[] CaptureBoard()
        {
            var placed = new List<WaveRewindSnapshot.BoardDice>();

            int total = battle.Board.rows * battle.Board.cols;
            for (int i = 0; i < total; i++)
            {
                UIDice dice = battle.Board.GetDice(i);
                if (dice == null)
                    continue;

                placed.Add(new WaveRewindSnapshot.BoardDice
                {
                    SlotIndex = i,
                    Type = dice.Type,
                    Star = dice.Star,
                });
            }

            return placed.ToArray();
        }

        /// <summary>
        /// 되돌린다. 한 번 쓰면 그 몫이 준다.
        ///
        /// 성공하면 판은 관리 단계에 서 있고, 화면에는 몬스터도 총알도 없다.
        /// </summary>
        public bool TryRewind(RewindSource source, RewindTrigger trigger)
        {
            if (!CanRewind)
                return false;

            if (source == RewindSource.Free)
            {
                if (RemainingFree <= 0)
                    return false;

                freeUsed++;
            }
            else
            {
                if (RemainingAd <= 0)
                    return false;

                adUsed++;
            }

            // <b>되감기 전에 적는다.</b> Restore 가 웨이브 번호와 벽 체력을 스냅샷 값으로
            // 덮으므로, 뒤에서 적으면 "되돌리기 직전이 어땠나" 라는 이 기록의 요점이 사라진다.
            RunHistoryManager.Instance?.RecordRewind(
                battle.Game.CurrentWaveIndex,
                battle.Game.wall != null ? battle.Game.wall.CurrentHp : 0,
                trigger == RewindTrigger.Death ? "death" : "wave",
                source == RewindSource.Ad ? "ad" : "free",
                RemainingCount);

            Restore();
            OnAvailabilityChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 실제 되감기. <b>순서가 곧 규칙이라</b> 줄을 옮기면 조용히 깨진다 —
        /// 각 묶음 위의 주석이 그 줄이 그 자리에 있는 이유다.
        ///
        /// <b>두 토막으로 나뉜다.</b> 여기까지가 즉시 도는 부분(웨이브를 닫고 화면을 치운다)이고,
        /// 상태를 되돌리는 나머지는 <see cref="FinishRestore"/> 에 있다. 사이에 되감기 연출이
        /// 끼기 때문인데, 연출을 끄면 그 자리가 비어 두 토막이 연달아 돈다.
        ///
        /// <b>시간은 여기서 안 만진다.</b> 부르는 쪽(<c>OnClick_Rewind</c>·<c>OnWallDestroyed</c>)이
        /// 이미 0 으로 멈춰 두었고, 마지막의 <c>ChangeState(Setting)</c> 이 1 로 되돌린다.
        /// 중간에 1 로 올리면 연출이 도는 동안 판이 다시 움직인다.
        /// </summary>
        private void Restore()
        {
            GameManager game = battle.Game;

            // ── 1. 웨이브를 먼저 닫는다 ────────────────────────────────────────────
            // 아래에서 몬스터를 끄면 Monster.OnDisable 이 Bounty.NotifyEscaped 를 부르고
            // 그게 OnWaveResolved 를 쏴서 GameManager.TryCompleteWave 를 깨운다.
            // 그 함수는 첫 줄에서 inGameState 를 보므로, 여기서 None 으로 빼 두면
            // 경로가 통째로 잠긴다. MonsterSpawner.Update 도 같은 조건이라
            // 추가 스폰이 같이 멈춘다 — 스포너를 따로 세울 필요가 없다.
            game.inGameState = InGameState.None;

            // ── 2. 되감지 않는 것을 먼저 치운다 ────────────────────────────────────
            // 지연 폭발(불·왕불 계열)이 전부 AttackContent 위에서 도는 코루틴이라
            // 이 한 줄이 유일한 일괄 지렛대다. 안 하면 되돌린 뒤 관리 단계에서
            // 존재하지 않는 몬스터에게 피해가 들어간다.
            battle.Attack.StopAllCoroutines();

            // 보드를 지우기 전에 쿨다운을 판다. 저 딕셔너리가 UIDice 를 키로 들고 있어서,
            // 먼저 지우면 파괴된 참조를 한 프레임 동안 읽는다.
            battle.Player.ResetAllDiceCooldowns();

            // 탄환·이펙트·데미지 텍스트는 되감지 않고 그냥 사라진다. 날아가던 화살까지
            // 거꾸로 빨려 들어가면 화면이 안 읽히고, 유저가 되돌리려는 대상도 아니다.
            battle.Bullets.ReleaseAll();
            battle.BulletEffects.ReleaseAll();
            battle.DamageTexts.ReleaseAll();

            // ── 3. 되감기를 보여 준다 ─────────────────────────────────────────────
            if (Settings.playRewindPresentation)
            {
                // 살아 있는 몬스터는 여기서 거두지 않는다 — 연출이 소환 자리까지
                // 올려 보낸 뒤 자기가 풀에 넣는다.
                PlayPresentationThenFinish().Forget();
                return;
            }

            ReleaseMonsters();
            FinishRestore();
        }

        /// <summary>
        /// 연출을 돌리고 나머지를 마친다.
        ///
        /// <b><c>UniTaskVoid</c> + <c>Forget</c> 인 이유.</b> <see cref="TryRewind"/> 는
        /// bool 을 돌려주는 동기 함수이고 부르는 쪽(확인 창 콜백)은 그 값으로 실패만 가린다.
        /// 여기를 기다리게 만들려면 그 경로 전체가 async 가 되어야 하는데, 얻는 것이 없다 —
        /// 연출이 도는 동안 판은 <c>InGameState.None</c> 이라 아무 일도 일어나지 않는다.
        /// </summary>
        private async UniTaskVoid PlayPresentationThenFinish()
        {
            presenter ??= new WaveRewindPresenter(battle);

            // 되감기 길이는 <b>웨이브를 실제로 본 시간</b>에서 나온다. 다만 상한이 있어서
            // 아주 긴 웨이브는 길어지는 대신 <b>더 빨리</b> 감긴다 — 되감기는 판단을 다시
            // 하러 돌아가는 길이지 감상하는 시간이 아니다.
            // <b>묻기 시작한 시각</b>으로 잰다. 지금(Time.unscaledTime)으로 재면 유저가
            // 확인 창을 들여다본 시간이 웨이브 길이에 섞인다.
            float waveElapsed = WaveElapsedAt(pauseStartedAt > 0f ? pauseStartedAt : Time.unscaledTime);
            float playbackSpeed = WaveRewindFormula.PlaybackSpeed(battle.Game.CurrentTimeSpeed);
            float duration = WaveRewindFormula.Duration(
                waveElapsed, playbackSpeed, Settings.maxRewindSeconds);

            await presenter.PlayAsync(deathLog, waveElapsed, duration, snapshot.Run.WallHp);

            // 연출을 기다리는 사이에 씬이 내려갔을 수 있다. 그러면 되돌릴 판 자체가 없다.
            if (battle == null || !battle.IsActive)
                return;

            FinishRestore();
        }

        /// <summary>
        /// 상태를 되돌리고 관리 단계로 보낸다. 연출이 있든 없든 <b>여기가 마지막</b>이다.
        /// </summary>
        private void FinishRestore()
        {
            GameManager game = battle.Game;

            // 현상금은 이미 있는 리셋을 그대로 쓴다. 몬스터를 거두며 NotifyEscaped 가
            // 먼저 울었을 수 있는데, 그 상태까지 여기서 한 번에 덮인다.
            // <b>몬스터를 거둔 뒤여야 한다</b> — 앞에 두면 그 통보가 리셋을 다시 더럽힌다.
            battle.Bounty.ResetWaveState();

            game.Run.RestoreWaveSnapshot(snapshot.Run);

            // 벽은 SetInit 이 아니라 RestoreHp 다. 저쪽은 TotalHp 까지 덮어
            // 최대 체력을 올리는 유물의 효과를 지운다.
            if (game.wall != null)
                game.wall.RestoreHp(snapshot.Run.WallHp);

            RestoreBoard();
            battle.Summon.RestoreSnapshot(snapshot.Summon);
            RestoreElementLevels();
            RestoreEnhanceStone();

            // 판에 한 번뿐인 유물 표시. 안 되돌리면 되돌린 웨이브에서 터진 '최후의 벽' 이
            // 그 판 내내 다시 안 터진다.
            RelicManager.Instance?.RestoreRunFlags(snapshot.RelicFlags);

            // 되감은 웨이브의 주검은 이제 없던 일이다. 다시 시작할 때 Capture 도 비우지만,
            // 그 사이의 관리 단계 동안 들고 있을 이유가 없다.
            deathLog.Clear();

            // ChangeState 는 Wave 로 들어갈 때만 WaveIndex 를 올리므로, Setting 으로
            // 부르면 위에서 되돌린 번호가 그대로 살아남는다. 관리 단계 UI 와 배속(1)도
            // 여기서 한꺼번에 제자리로 온다.
            game.ChangeState(InGameState.Setting);
        }

        /// <summary>
        /// 살아 있는 몬스터를 전부 풀로 돌린다.
        ///
        /// <b>배열로 복사한 뒤 돈다.</b> <c>PoolMonster</c> → <c>SetActive(false)</c> →
        /// <c>OnDisable</c> → <c>UnregisterMonster</c> 가 지금 순회 중인 그 목록을 고친다.
        /// 그대로 돌면 중간에 열거자가 깨지거나 몇 마리가 화면에 남는다.
        ///
        /// <c>UnregisterMonster</c> 는 <c>countAsKill:false</c> 로 불리므로 처치 수는
        /// 늘지 않는다 — 어차피 위에서 스냅샷으로 덮이지만, 여기서 이미 맞는 편이 낫다.
        /// </summary>
        private void ReleaseMonsters()
        {
            List<Monster> active = battle.Monsters.activeMonsters;
            if (active == null || active.Count == 0)
                return;

            Monster[] snapshotOfActive = active.ToArray();
            for (int i = 0; i < snapshotOfActive.Length; i++)
            {
                Monster monster = snapshotOfActive[i];
                if (monster != null)
                    battle.Spawner.PoolMonster(monster);
            }
        }

        private void RestoreBoard()
        {
            int total = battle.Board.rows * battle.Board.cols;
            for (int i = 0; i < total; i++)
                battle.Board.ClearDice(i);

            // 집계를 먼저 비우고, 놓으면서 다시 센다. 순서(집계 → 배치)는
            // GameManager.PlaceTowerLoadout 과 같게 맞춘다 — 성급 표시가 그 순서에 걸려 있다.
            battle.DiceStars.ResetAll();

            WaveRewindSnapshot.BoardDice[] board = snapshot.Board;
            for (int i = 0; i < board.Length; i++)
            {
                battle.DiceStars.OnDiceSpawn(board[i].Type, board[i].Star);
                battle.Board.SpawnDice(board[i].Type, board[i].Star, board[i].SlotIndex);
            }
        }

        private void RestoreElementLevels()
        {
            int[] levels = snapshot.ElementLevels;
            if (levels == null)
                return;

            for (int i = 0; i < levels.Length && i < (int)ElementType.Max; i++)
                battle.ElementUpgrade.SetLevel((ElementType)i, levels[i]);
        }

        /// <summary>
        /// 강화석을 스냅샷 값으로 되돌린다.
        ///
        /// <b>값이 같으면 아무것도 하지 않는다.</b> <c>PointManager.Set</c> 은 기본이
        /// <b>즉시 파일 저장</b>이라, 안 바뀐 값을 쓰면 되돌릴 때마다 디스크를 한 번씩 친다.
        /// 실제로 웨이브 <i>중</i> 에는 강화석이 늘지도 줄지도 않으므로(지급은 웨이브 클리어,
        /// 소비는 관리 단계) 보통은 여기서 그냥 나간다. 그래도 뜨고 되돌리는 이유는
        /// 그 사실이 <b>오늘의 사실</b>이기 때문이다 — 전투 중 지급 경로가 하나라도 생기면
        /// 이 줄이 자동으로 그것을 막는다.
        /// </summary>
        private void RestoreEnhanceStone()
        {
            PointManager points = PointManager.Instance;
            if (points == null)
                return;

            if (points.Get(PointType.BattleEnhanceStone) == snapshot.EnhanceStone)
                return;

            points.Set(PointType.BattleEnhanceStone, snapshot.EnhanceStone, saveNow: false);
            points.SaveAll();
        }
    }
}
