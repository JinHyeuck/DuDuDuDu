using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using VContainer;
using OJ.Core;
using OJ.Analytics;
using OJ.Bounty;
using OJ.Battle;
using OJ.DI;
using OJ.Dice;
using OJ.Element;
using OJ.Mission;
using OJ.Point;
using OJ.Relic;
using OJ.Rewind;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.Tower;
using OJ.Tutorial;
using OJ.UI;
using OJ.Utils;

namespace OJ.Hunting
{
    public class GameManager : MonoBehaviour
    {
        /// <summary>
        /// (8.3b) BattleScene 매니저들로 가는 창구. 배틀 스코프는 씬의 모든 <c>Awake</c> 뒤,
        /// 모든 <c>Start</c> 앞에 빌드되므로 <b><c>Awake</c> 에서는 아직 null 이고
        /// <c>Start</c> 이후로는 절대 null 이 아니다.</b> 그래서 아래 호출들에는
        /// <c>?.</c> 를 쓰지 않는다 — 여기서 null 이면 그것은 사고이고 울어야 한다.
        /// </summary>
        [Inject] private IBattleRefs battle;

        /// <summary>카드 상점의 보유 판정. 머지 시스템이 쓰는 것과 같은 루트 서비스다.</summary>
        [Inject] private DiceOwnershipManager ownership;

        private bool isGameOver { get => Run.IsGameOver; set => Run.IsGameOver = value; }

        public int WallHp => Run.WallHp;

        public Wall wall;

        public InGameState inGameState = InGameState.None;

        /// <summary>
        /// <b>레거시 준비 단계 흐름</b>(시작 버튼을 눌러야 웨이브가 열리고 웨이브 사이에 준비 단계가 있다).
        /// 일반 판은 실시간이라 꺼져 있고, <c>BalanceAutoPlay</c> 가 측정하는 동안만 켠다 —
        /// 탑은 <see cref="IsContinuousWave"/> 가 알아서 이 흐름을 탄다(튜토리얼은 실시간이다).
        /// </summary>
        public static bool LegacySetupFlow;

        /// <summary>영혼 보너스(적을 잡을수록 영혼이 쌓이고, 목표를 채우면 SP/마석 중 하나를 고른다). 실시간 판에서만 쓰인다.</summary>
        public OJ.Economy.SoulBonusManager SoulBonus { get; private set; }

        /// <summary>행운 주사위(SP → 마석 1~10개). 실시간 판에서만 쓰인다.</summary>
        public OJ.Economy.LuckyDiceManager LuckyDice { get; private set; }

        /// <summary>
        /// 이 판이 <b>실시간 진행</b>인가. 웨이브가 끝나도 준비 단계로 내려가지 않고 곧바로 다음 웨이브가
        /// 시작되며, 웨이브 중에도 소환·머지·진화·교환·속성강화·자리구매가 열려 있다.
        /// 판에 들어오면 3초 카운트다운 뒤 첫 웨이브가 저절로 열린다.
        ///
        /// 탑(층 하나짜리)은 해당하지 않는다. <b>튜토리얼은 실시간이다</b> — 가르치는 내용이 실시간 규칙이라서다.
        /// 다만 카운트다운은 디렉터가 연다(<see cref="BeginTutorialWaves"/>): 소환·머지를 먼저 가르쳐야 한다.
        /// </summary>
        public bool IsContinuousWave =>
            !LegacySetupFlow && !battle.Tower.IsActive;

        /// <summary>
        /// 지금 보드를 만질 수 있는가(소환·머지·진화·교환·속성강화). 평소에는 관리 단계뿐이고,
        /// 연속 웨이브 테스트 중에는 웨이브 중에도 된다.
        /// </summary>
        public bool CanManageBoard =>
            inGameState == InGameState.Setting
            || (inGameState == InGameState.Wave && IsContinuousWave);

        /// <summary>웨이브라서 보드 조작이 막혀 있는가. 상태가 None 인 경우는 각 호출부의 기존 판단을 따른다.</summary>
        /// <summary>
        /// 연속 웨이브에서 이번 웨이브의 클리어 SP 를 지금까지 얼마나 나눠 줬나.
        /// 웨이브가 끝나면 한꺼번에 주는 대신 처치마다 n등분해 준다.
        /// </summary>
        private int waveSpGranted;

        public bool IsBoardLockedByWave =>
            inGameState == InGameState.Wave && !IsContinuousWave;

        public int WaveMonsterCount => Run.WaveMonsterCount;
        public int WaveMonsterDeadCount { get => Run.WaveMonsterDeadCount; set => Run.WaveMonsterDeadCount = value; }
        /// <summary>
        /// 이 판의 상태. (6.1) 예전에는 벽 HP·웨이브·몬스터 수가 각각 public 필드였고
        /// 씬에도 직렬화돼 있었다. 그런데 <c>InitializeStage</c> 가 매번 스테이지 데이터로
        /// 덮어써서 씬 값은 죽은 값이었다 — 인스펙터에서 고쳐도 아무 일도 일어나지 않았다.
        ///
        /// 이제 소유자가 하나다. 판을 리셋하려면 <c>Run.BeginRun</c> 한 번이면 되고,
        /// 필드를 늘려도 리셋을 빠뜨릴 수 없다.
        /// </summary>
        public RunState Run { get; } = new RunState();

        public int CurrentWaveIndex => Run.WaveIndex;
        public StageData CurrentStageData { get; private set; }

        [Header("Stage Theme")]
        [SerializeField] private SpriteRenderer stageBackground;
        public Button PlayUI;
        public Image PlayUI_Field;
        public Button Pause;
        public Button Speed;
        public TMP_Text SpeedText;
        public TMP_Text WaveText;
        public TMP_Text RemainMonster;
        public RectTransform RemainMonsterGauge;
        public float RemainMonsterGauge_Width = 705.0f;

        /// <summary>
        /// 사망 시 되돌리기 제안 창이 떠 있는가.
        ///
        /// <b>없으면 창이 두 번 뜬다.</b> 벽이 0 이 된 프레임에 몬스터 둘이 같이 때리면
        /// <see cref="OnWallDestroyed"/> 가 두 번 불리는데, 그때 <c>isGameOver</c> 는 아직
        /// false 다 — 예전에는 <c>GameOver()</c> 가 그 자리에서 불려 그 플래그가 막아 줬고,
        /// 이제는 확인을 기다리느라 그 사이가 벌어졌다.
        /// </summary>
        private bool deathRewindPromptOpen;

        private float timeSpeed = 1.0f;

        /// <summary>
        /// 지금 배속(1·2·3). 되돌리기가 되감기 속도를 여기서 뽑는다
        /// (<see cref="OJ.Core.WaveRewindFormula.PlaybackSpeed"/>).
        ///
        /// <b><c>Time.timeScale</c> 을 읽지 않는다.</b> 확인 창이 떠 있는 동안 그 값은 0 이라,
        /// 되돌리기를 누른 시점에 물으면 언제나 0 이 돌아온다.
        /// </summary>
        public float CurrentTimeSpeed => timeSpeed;
        [SerializeField] private float returnToLobbyDelay = 1.0f;

        /// <summary>
        /// 판이 끝나고 결과창이 뜨기까지의 뜸(초).
        ///
        /// <b>0 이면 갑작스럽다.</b> 마지막 몬스터가 죽는 순간·벽이 부서지는 순간에
        /// 결과창이 곧바로 덮으면 <b>방금 무슨 일이 있었는지 볼 시간이 없다</b> —
        /// 마지막 타격의 이펙트도, 벽이 무너지는 것도 결과창 뒤로 사라진다.
        ///
        /// <b>실제 시간으로 잰다.</b> 배속(x2/x3)에서 <c>Time.timeScale</c> 이 3이면
        /// 게임 시간 1초는 실제 0.33초라 뜸이 거의 없는 것과 같아진다 —
        /// "잠깐 보여 준다" 는 목적이 배속에 따라 달라지면 안 된다.
        /// </summary>
        [SerializeField] private float resultPopupDelay = 0.9f;

        void Awake()
        {
            PlayUI.onClick.AddListener(OnClick_PlayUI);
            Pause.onClick.AddListener(OnClick_Pause);
            Speed.onClick.AddListener(OnClick_Speed);
        }

        private void OnDestroy()
        {
            // 창구는 스코프가 파괴될 때 비워지는데 그 순서가 정해져 있지 않다.
            // 이미 비었으면 뗄 것도 없으므로 ?. 를 쓴다 — 여기는 사고가 아니라 정리 경로다.
            if (battle != null && battle.Bounty != null)
            {
                battle.Bounty.OnSpawned -= OnBountySpawned;
            }

            if (battle != null && battle.Spawner != null)
                battle.Spawner.OnBossSpawned -= OnBossSpawned;

            if (PlayUI != null) PlayUI.onClick.RemoveListener(OnClick_PlayUI);
            if (Pause != null) Pause.onClick.RemoveListener(OnClick_Pause);
            if (Speed != null) Speed.onClick.RemoveListener(OnClick_Speed);

        }

        private void Start()
        {
            // 배틀 스코프는 모든 Start 앞에 빌드되므로 여기서 battle.Bounty 는 살아 있다.
            // 구독을 Awake 로 올리면 그때는 아직 null 이다.
            battle.Bounty.OnSpawned += OnBountySpawned;
            battle.Spawner.OnBossSpawned += OnBossSpawned;

            SoulBonus = new OJ.Economy.SoulBonusManager(battle);
            LuckyDice = new OJ.Economy.LuckyDiceManager(battle);

            InitializeStage();

            // 기여도 패널은 본편·탑 둘 다에서 뜬다. 접어 둔 사람에게는 접힌 채로
            // 나타난다(그 상태는 UIDamageContributionPanel 이 세션 동안 기억한다).
            GameContainer.UI?.Show<UIDamageContributionPanel>();

            if (battle.Tower.IsActive)
            {
                // 탑에는 관리 단계가 없다. 편성은 로비에서 이미 끝났고(기획서 5.1),
                // 여기서 Setting 을 거치면 <b>아무것도 할 수 없는 화면에서 시작 버튼을
                // 한 번 더 누르는</b> 단계가 생긴다 — 30초 안에 결과를 본다는 전제를
                // 그 한 번의 탭이 깎아먹는다.
                //
                // 다음 프레임에 시작한다. UIBoard.Start 가 아직 안 돌았을 수 있어
                // (Start 끼리의 순서는 정해져 있지 않다) 이 프레임에 다이스를 놓으면
                // 슬롯이 없다.
                StartCoroutine(CoBeginTowerFloor());
                return;
            }

            ChangeState(InGameState.Setting);

            if (battle.Tutorial.IsActive)
            {
                // 튜토리얼 판에서는 유물 선제 배치를 하지 않는다. 대본이 첫 소환 여섯 칸을
                // 정해 두었는데, 유물이 먼저 그 칸을 채우면 머지 단계의 구멍이 엉뚱한 다이스를 연다.
                // 삭제 영역은 실시간 판과 같이 걷는다. 확률업·영혼 보너스·행운 주사위·현상금 버튼은
                // 디렉터가 가르칠 차례에 하나씩 연다(처음부터 다 보이면 무엇부터 눌러야 할지 모른다).
                HideDeleteArea();
                new TutorialDirector(battle).RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
                return;
            }

            // 실시간: 삭제 영역을 걷고(씬은 건드리지 않고 끈다) 3초 카운트다운 뒤 웨이브를 연다.
            if (IsContinuousWave)
            {
                HideDeleteArea();

                // 삭제 버튼 자리의 확률업 버튼과, 소환 버튼 위의 확률 띠.
                GameContainer.UI?.Show<UIProbabilityUpButton>();
                GameContainer.UI?.Show<UISummonRateStrip>();
                GameContainer.UI?.Show<OJ.Economy.UISoulBonusButton>();
                GameContainer.UI?.Show<OJ.Economy.UILuckyDiceButton>();

                StartCoroutine(CoRealtimeStart());
            }
            else
            {
                StartCoroutine(CoApplyStageStartRelics());
            }
        }

        /// <summary>
        /// 탑의 층을 시작한다. 편성한 다이스를 보드에 놓고 곧바로 웨이브로 넘어간다.
        ///
        /// <b>한 프레임 기다리는 것이 핵심이다.</b> <c>UIBoard.CreateBoard</c> 는
        /// <c>UIBoard.Start</c> 에서 도는데 <c>Start</c> 끼리의 호출 순서는 정해져 있지 않다.
        /// 여기서 바로 놓으면 보드가 아직 없는 판에서 <b>다이스가 하나도 안 나온 채</b>
        /// 층이 시작된다 — 화면에는 "왜 아무것도 안 쏘지" 로만 보인다.
        /// (<see cref="CoApplyStageStartRelics"/> 가 같은 이유로 같은 모양이다.)
        /// </summary>
        private IEnumerator CoBeginTowerFloor()
        {
            yield return null;

            PlaceTowerLoadout();
            ChangeState(InGameState.Wave);
            battle.Tower.StartTimer();
        }

        /// <summary>
        /// 편성한 다이스를 보드 앞줄부터 놓는다.
        ///
        /// <b>자리는 아무 뜻이 없다.</b> <c>PlayerController</c> 는 쿨다운이 찬 다이스를
        /// 골라 쏘지 자리를 보지 않는다. 기획서 9장이 "다이스 배치 순서가 공격 순서나
        /// 위치에 영향을 주는지" 를 열린 항목으로 남겨 두었으므로, 여기서 뜻을 만들지
        /// 않고 <b>보이는 순서만</b> 편성 화면의 슬롯 바와 맞춘다.
        /// </summary>
        private void PlaceTowerLoadout()
        {
            List<TowerLoadoutEntry> entries = battle.Tower.Loadout.ToOrderedList();
            for (int i = 0; i < entries.Count; i++)
            {
                TowerLoadoutEntry entry = entries[i];

                // 성급 UI 는 성급을 아는 매니저를 거쳐야 맞는다. 본편의 소환 경로도
                // 같은 순서(DiceStars → SpawnDice)라 여기만 다르게 하면 별 표시가 어긋난다.
                battle.DiceStars.OnDiceSpawn(entry.DiceType, entry.Star);
                battle.Board.SpawnDice(entry.DiceType, entry.Star, i);
            }
        }

        /// <summary>
        /// 현상금이 화면에 나왔다. 경고 띠(보라)를 잠깐 띄운다.
        ///
        /// <c>Show</c> 가 아니라 <c>Get</c> 으로 받는 것은 <c>Play</c> 가 내용을 채우고
        /// <c>Enter</c> 까지 스스로 부르기 때문이다 — <c>ShowWaveRewardPreview</c> 와 같은 이유다.
        /// </summary>
        private void OnBountySpawned(OJ.Bounty.BountyDefinition definition)
        {
            // 정의가 없으면 띄우지 않는다. 스폰 자체가 데이터 사고이고, 띠가 그 사고를 가린다.
            if (definition == null)
                return;

            GameContainer.UI?.Get<UIBattleWarning>()?.Play(BattleWarningKind.Bounty);
        }

        /// <summary>보스 웨이브의 보스가 나왔다. 경고 띠(빨강)를 띄운다.</summary>
        private void OnBossSpawned()
        {
            GameContainer.UI?.Get<UIBattleWarning>()?.Play(BattleWarningKind.Boss);
        }

        public void OnClick_PlayUI()
        {
            ChangeState(InGameState.Wave);
        }

        /// <summary>
        /// 전투를 그만둘지 묻는다. <b>버튼 자체는 아무것도 끝내지 않는다.</b>
        ///
        /// 예전에는 누르는 즉시 <c>CoReturnToLobby()</c> 로 로비에 나갔다 — 확인도 없고
        /// 보상도 없었다. 오조작 한 번에 판이 통째로 날아가는 자리였다.
        ///
        /// 확인을 받으면 <b>패배와 같은 경로</b>(<see cref="GameOver"/>)를 탄다. 새 종료 경로를
        /// 만들지 않는 것이 중요하다 — 보상 계산·기록·결과창이 전부 거기 모여 있고,
        /// 갈래를 늘리면 한쪽만 고치는 사고가 난다.
        /// </summary>
        public void OnClick_Pause()
        {
            UIConfirmDialog confirm = GameContainer.UI?.Get<UIConfirmDialog>();
            if (confirm == null)
            {
                // 창을 못 열었는데 조용히 넘어가면 버튼이 죽은 것처럼 보인다.
                // 그렇다고 확인 없이 판을 끝낼 수는 없으므로 여기서 멈춘다.
                Debug.LogError("[전투] 확인 창을 열지 못했다. 카탈로그에 UIConfirmDialog 가 있는지 볼 것.");
                return;
            }

            // 탑은 나가는 대가가 다르다. 웨이브 비율 보상이 없고(층은 하나뿐이다)
            // 스태미나도 쓰지 않으므로, 본편 문구를 그대로 쓰면 <b>있지도 않은 손해</b>를
            // 경고하게 된다. 실제로 잃는 것은 이번 시도뿐이고 재도전은 무비용이다.
            if (battle.Tower.IsActive)
            {
                confirm.Open(
                    "도전을 그만둘까요?",
                    battle.Tower.Plan.Floor + "층 도전이 실패로 기록돼요." + Environment.NewLine +
                    "재도전에는 아무 비용도 들지 않아요.",
                    "그만둘게요",
                    "더 해볼게요",
                    GameOver);
                return;
            }

            confirm.Open(
                "전투를 마칠까요?",
                "지금까지 클리어한 웨이브만큼 보상을 받고 나가요." + Environment.NewLine +
                "사용한 스태미나는 돌아오지 않아요.",
                "여기까지 할게요",
                "더 해볼게요",
                GameOver);
        }

        /// <summary>
        /// 이번 웨이브를 시작 직전으로 되돌릴지 묻는다.
        ///
        /// <b>버튼 자체는 아무것도 되돌리지 않는다.</b> <see cref="OnClick_Pause"/> 와 같은
        /// 모양이다 — 판을 크게 바꾸는 것은 전부 확인을 거친다.
        ///
        /// <b>왜 이 판단이 버튼이 아니라 여기 있나.</b> 확인을 기다리는 동안 시간을
        /// 멈춰야 하는데, 되돌리는 <i>시간</i>은 <c>timeSpeed</c> 이고 그 값을 아는 것은
        /// 이 클래스뿐이다. 버튼이 직접 <c>timeScale = 1</c> 로 되돌리면 <b>배속 3배로
        /// 놀던 사람이 취소 한 번에 1배로 떨어진다.</b>
        /// </summary>
        public void OnClick_Rewind()
        {
            if (!battle.Rewind.CanRewindDuringWave)
                return;

            UIConfirmDialog confirm = GameContainer.UI?.Get<UIConfirmDialog>();
            if (confirm == null)
            {
                // 창을 못 열었는데 조용히 넘어가면 버튼이 죽은 것처럼 보인다.
                // OnClick_Pause 와 같은 판단이다.
                Debug.LogError("[전투] 확인 창을 열지 못했다. 카탈로그에 UIConfirmDialog 가 있는지 볼 것.");
                return;
            }

            // 확인을 기다리는 동안 몬스터가 계속 내려오면 "되돌릴까" 를 고민하는 사이에
            // 벽이 부서진다. 되돌리기가 가장 필요한 순간이 정확히 그 순간이다.
            //
            // 멈추기 <b>직전에</b> 알린다. 여기서부터 창이 닫힐 때까지는 웨이브 길이에서
            // 빠져야 한다 — 안 그러면 오래 고민할수록 되감기가 길어진다.
            battle.Rewind.NotifyRewindOffered();
            Time.timeScale = 0f;

            int remaining = battle.Rewind.RemainingCount;
            confirm.Open(
                "이번 웨이브를 되돌릴까요?",
                "웨이브를 시작하기 직전으로 돌아가요." + Environment.NewLine +
                "쓰고 나면 " + Mathf.Max(0, remaining - 1) + "번 남아요.",
                "되돌릴게요",
                "계속할게요",
                ConfirmRewind,
                CancelRewind);
        }

        private void ConfirmRewind()
        {
            // 되돌리기가 성공하면 그 안에서 timeScale 을 되살리고 관리 단계까지 간다.
            // 실패하는 경우(창이 떠 있는 사이에 조건이 깨진 경우)에만 여기서 시간을 푼다.
            if (!battle.Rewind.TryRewind(RewindSource.Free, RewindTrigger.Wave))
                CancelRewind();
        }

        /// <summary>
        /// 되돌리지 않기로 했다. <b>1 이 아니라 <c>timeSpeed</c> 로 되돌린다</b> —
        /// 배속을 켜 둔 사람의 설정을 취소 한 번으로 뺏지 않는다.
        /// </summary>
        private void CancelRewind()
        {
            battle.Rewind.NotifyOfferClosed();
            Time.timeScale = timeSpeed;
        }

        public void OnClick_Speed()
        {
            if (timeSpeed == 1)
                timeSpeed = 2;
            else if (timeSpeed == 2)
                timeSpeed = 3;
            else
                timeSpeed = 1;

            Time.timeScale = timeSpeed;

            SetSpeedText();
        }

        private void SetSpeedText()
        {
            SpeedText?.SetText(string.Format("x{0:0.#}", timeSpeed));
        }

        public void ChangeState(InGameState state)
        {
            inGameState = state;

            bool legacyStart = state == InGameState.Setting && !IsContinuousWave;
            PlayUI?.gameObject.SetActive(legacyStart);
            PlayUI_Field?.gameObject.SetActive(legacyStart || countdownActive);
            // 관리 단계에서도 나갈 수 있어야 한다. 웨이브 사이에 그만두려는 사람이
            // 다음 웨이브를 억지로 시작해야 했던 것이 예전 동작이다.
            //
            // 튜토리얼 판에서는 감춘다. 나가면 튜토리얼을 마치지 않은 채 로비에 서게 되고,
            // 다음 실행에서 처음부터 다시 해야 한다 — 나갈 이유를 주지 않는 편이 낫다.
            Pause?.gameObject.SetActive(
                (state == InGameState.Wave || state == InGameState.Setting) && !battle.Tutorial.IsActive);
            // 튜토리얼에서는 배속을 감춘다 — 가르치는 중에 속도가 바뀌면 단계 타이밍이 어긋난다.
            Speed?.gameObject.SetActive(state == InGameState.Wave && !battle.Tutorial.IsActive);
            RemainMonster?.gameObject.SetActive(state == InGameState.Wave);
            RemainMonsterGauge?.gameObject.SetActive(state == InGameState.Wave);
            WaveText?.gameObject.SetActive(state == InGameState.Wave || state == InGameState.Setting);

            // 경고 띠는 스스로 2초쯤 뒤에 사라지지만, 그 전에 웨이브가 끝나 준비 단계까지 따라 들어오면
            // 남은 시간을 기다리지 않고 여기서 거둔다.
            if (state == InGameState.Setting)
                GameContainer.UI?.Hide<UIBattleWarning>();

            // 현상금 버튼은 실시간 판의 웨이브 중에만 뜬다(아이콘을 눌러 즉시 소환하는 시스템이라 필드가 열려 있을 때만 뜻이 있다).
            // <b>Show/Hide 를 여기 두는 이유.</b> 상태를 아는 곳이 여기 하나뿐이다 — 버튼이 스스로 판단하게 하면
            // 매 프레임 inGameState 를 들여다보게 되고, 그것은 이벤트로 바꿔 놓은 것을 폴링으로 되돌리는 일이다.
            // 튜토리얼에서는 현상금을 가르칠 차례가 오기 전까지 감춘다(TutorialRunManager.IsBountyButtonAllowed).
            if (state == InGameState.Wave && IsContinuousWave && battle.Tutorial.IsBountyButtonAllowed)
            {
                GameContainer.UI?.Show<UIBountyButton>();
            }
            else
            {
                GameContainer.UI?.Hide<UIBountyButton>();
                GameContainer.UI?.Hide<UIBountySelectDialog>();
            }

            // 속성강화도 관리 단계 전용이다. 소환·머지·진화가 전부 그런데 이것만
            // 전투 중에 열려 있었다 — 같은 강화석을 쓰면서 규칙이 혼자 달랐다.
            //
            // 실제 차단은 ElementUpgradeManager.TryLevelUp 이 하고, 여기서는 버튼을
            // 흐리게 만든다(치우지 않는다 — 빈 자리는 기능이 사라진 것처럼 보인다).
            // 창이 열린 채 웨이브가 시작될 수 있으므로(그 창은 전체 화면을 덮지 않는다)
            // 닫는 것까지 해야 한다.
            battle.ElementUpgrade.SetUpgradeUIAvailable(CanManageBoard);
            if (!CanManageBoard)
                GameContainer.UI?.Hide<UIElementUpgradePanel>();

            // 관리 단계마다 자동으로 뜨던 조합 진행도 창(UIDiceCraftProgressDialog)은
            // 조합식과 함께 사라졌다. 상위 다이스로 가는 길은 이제 목록을 띄워 재고를
            // 세는 것이 아니라, 다이스를 눌러 그 자리에서 진화시키는 것이다 —
            // UIBattleDiceDetailPanel 참조.

            if (state == InGameState.Wave)
            {
                // <b>WaveIndex++ 보다 먼저다.</b> 되돌리기는 "시작 버튼을 누르기 직전"
                // 으로 되감는 기능이라, 뒤에서 뜨면 되돌린 판의 웨이브 번호가 하나 앞선다.
                // 탑에서는 이 호출이 첫 줄에서 그냥 돌아간다.
                battle.Rewind.Capture();

                Run.WaveIndex++;
                RelicManager.Instance?.BeginWave(CurrentWaveIndex);

                // 기여도를 웨이브마다 0 으로 돌린다. 판 전체로 누적하면 뒤 웨이브로 갈수록
                // 앞 웨이브의 몫에 묻혀, <b>관리 단계에서 방금 바꾼 편성의 효과가 안 보인다</b>.
                //
                // <b>본편·탑 공통이라 여기 한 곳에서만 리셋한다.</b> 탑은 한 층이 곧
                // 한 웨이브라(기획서 3.2) 이 줄이 그대로 층 리셋이 된다.
                battle.Contribution.BeginWave();

                Run.WaveMonsterCount = GetWaveTargetCount();
                UpdateWaveText();
                SetRemainMonster(0);
                battle.Spawner.PlayWave();
                Time.timeScale = timeSpeed;
                WaveMonsterDeadCount = 0;
                waveSpGranted = 0;
                SetSpeedText();
                RunHistoryManager.Instance?.RecordWaveStart(
                    CurrentWaveIndex,
                    wall != null ? wall.CurrentHp : 0,
                    GetCurrentWaveMonsterHp(),
                    GetCurrentWaveMonsterDefense());
            }
            else
            {
                Time.timeScale = 1;
                UpdateWaveText();
            }

            // 되돌리기 버튼은 웨이브 중에만, 그리고 실제로 되돌릴 수 있을 때만 뜬다.
            // 남은 횟수가 0 이면 아예 없는 편이 낫다 — 눌리지 않는 버튼은 고장으로 읽힌다.
            //
            // <b>반드시 위 분기 <i>뒤</i>여야 한다.</b> 되돌릴 수 있는지는 스냅샷이 떠 있는지에
            // 걸려 있고, 그 스냅샷은 바로 위 웨이브 진입에서 떠진다. 앞에 두면 <b>첫 웨이브에만
            // 버튼이 안 뜬다</b> — 그때만 아직 뜬 것이 없기 때문이고, 두 번째 웨이브부터는
            // 지난 웨이브의 스냅샷이 남아 있어 멀쩡해 보인다. 그래서 눈으로 잡기 나쁘다.
            //
            // <b>여기서 판단하는 이유.</b> 상태를 아는 곳이 이 함수 하나뿐이라는 규약이
            // 위 현상금 띠 주석에 이미 적혀 있다. 버튼이 스스로 판단하게 두면 매 프레임
            // inGameState 를 들여다보게 되고, 그건 이벤트로 바꿔 놓은 것을 폴링으로
            // 되돌리는 일이다.
            //
            // 튜토리얼 판에서는 되돌리기를 가르치는 단계 전까지 감춘다(TutorialRunManager.IsRewindButtonAllowed).
            if (state == InGameState.Wave && battle.Rewind.CanRewindDuringWave && battle.Tutorial.IsRewindButtonAllowed)
                GameContainer.UI?.Show<UIWaveRewindButton>();
            else
                GameContainer.UI?.Hide<UIWaveRewindButton>();
        }

        /// <summary>
        /// 탑의 시계를 돌린다. <b>이 <c>Update</c> 는 탑을 위해 새로 생긴 것이다</b> —
        /// 본편 전투에는 프레임마다 할 일이 없었다.
        ///
        /// <c>TowerRunManager</c> 가 MonoBehaviour 가 아니라(<c>BountyManager</c> 와 같은
        /// 이유) 스스로 돌 수 없어서, 이미 씬에 있는 이 컴포넌트가 대신 흘려 준다.
        /// 탑이 아니면 <c>Tick</c> 이 첫 줄에서 돌아간다.
        /// </summary>
        private void Update()
        {
            battle.Tower.Tick(Time.deltaTime);

            // 현상금 쿨타임은 웨이브 중에만 흐른다. 게임 시간이라 일시정지에서는 멈추고 배속에서는 빨리 흐른다.
            if (inGameState == InGameState.Wave)
                battle.Bounty.Tick(Time.deltaTime);


        }

        /// <summary>
        /// 연속 웨이브: 이 웨이브의 클리어 SP 를 처치 수로 n등분해 <b>죽을 때마다</b> 준다.
        /// 누적 비율로 계산해 정수 반올림이 쌓여도 합이 정확히 waveClearSP 가 된다.
        /// </summary>
        private void GrantKillShareSp()
        {
            if (!IsContinuousWave || CurrentStageData == null || WaveMonsterCount <= 0)
                return;

            int total = Mathf.Max(0, WaveClearSpFor(CurrentWaveIndex));
            int due = Mathf.Min(total, Mathf.FloorToInt(
                (float)total * Mathf.Min(WaveMonsterDeadCount, WaveMonsterCount) / WaveMonsterCount));

            int delta = due - waveSpGranted;
            if (delta <= 0)
                return;

            waveSpGranted = due;
            battle.Summon.AddSP(delta);
        }

        /// <summary>
        /// 이 웨이브를 막고 받는 SP. 스테이지의 <c>waveClearSP</c> 에 <b>종 모양 곡선</b>(<see cref="WaveSpCurve"/>)을 곱한다 —
        /// 초반 소환 횟수를 줄이고 중반에 몰아 전략을 생각할 틈을 만든다. 튜토리얼은 "정확히 N회" 로 맞춘 SP 라 곱하지 않는다.
        /// 처치마다 나눠 주는 쪽과 웨이브가 끝났을 때 남은 몫을 주는 쪽이 <b>같은 값</b>을 봐야 합이 맞는다.
        /// </summary>
        public int WaveClearSpFor(int waveIndex)
        {
            if (CurrentStageData == null)
                return 0;

            return WaveSpCurve.WaveClearSp(
                CurrentStageData.waveClearSP, waveIndex, CurrentStageData.totalWaves, !battle.Tutorial.IsActive);
        }

        /// <summary>이 웨이브의 곡선 배율(퍼센트). 튜토리얼은 100.</summary>
        public int WaveSpMultiplierPercent(int waveIndex)
        {
            if (CurrentStageData == null || battle.Tutorial.IsActive)
                return 100;

            return WaveSpCurve.Multiplier(waveIndex, CurrentStageData.totalWaves);
        }

        public void RemoveMonsterDeadCount()
        {
            if (inGameState != InGameState.Wave)
                return;
            WaveMonsterDeadCount++;

            GrantKillShareSp();

#if UNITY_EDITOR || DEV_DEFINE
            // 탑의 목표 처치 수가 안 맞을 때 어디서 새는지 보려고 한 줄씩 찍는다.
            // 릴리스 빌드에는 컴파일되지 않는다.
            if (battle.Tower.IsActive)
                battle.Tower.DevLogKill(WaveMonsterDeadCount, WaveMonsterCount);
#endif

            if (TryCompleteWave())
                return;

            SetRemainMonster(WaveMonsterDeadCount);
        }

        /// <summary>
        /// 웨이브를 끝낼 수 있으면 끝낸다. <b>조건이 둘</b>이고 <b>계기도 둘</b>이라
        /// 판정을 한 곳에 모은다 — 마지막 일반 몬스터가 죽었을 때와, 현상금이 나중에
        /// 정리됐을 때. 두 곳에 같은 조건을 적으면 한쪽만 고치는 사고가 난다.
        ///
        /// <b>현상금은 처치 수에 세지 않지만 웨이브를 붙잡는다.</b> 카운트에 넣으면
        /// 못 잡았을 때 목표를 채울 방법이 없어져 웨이브가 영영 안 끝나고, 아예 조건에서
        /// 빼면 현상금이 화면에 남은 채로 다음 관리 단계가 열린다.
        /// </summary>
        /// <summary>
        /// 판이 끝났을 때 전투 단계 UI 를 거둔다. <see cref="GameOver"/> 와
        /// <see cref="ClearStage"/> 가 부른다.
        ///
        /// <b>왜 따로 필요한가.</b> 그 둘은 <see cref="ChangeState"/> 를 거치지 않고
        /// <c>inGameState</c> 에 <c>None</c> 을 <b>직접 대입한다.</b> 그래서 ChangeState 안에
        /// 모여 있는 정리가 통째로 건너뛰어지고, 관리 단계에서 그만두면 현상금 배너가
        /// 결과창 뒤에 그대로 남는다 — 결과창이 나중에 만들어져 위에 덮이는 바람에
        /// 눈에 띄지 않았을 뿐이다.
        ///
        /// <b>ChangeState 의 정리와 합치지 않는다.</b> 저쪽은 웨이브 중에 등장 알림을
        /// <b>남겨 둬야</b> 한다(그게 알림의 존재 이유다). 여기는 판이 끝난 자리라
        /// 남길 것이 하나도 없다 — 조건이 반대라 한 함수로 묶으면 분기만 늘어난다.
        /// </summary>
        private void CloseBattlePhaseUI()
        {
            // <b>기여도 패널은 여기서 거두지 않는다.</b> 결과창 앞의 뜸(WaitBeforeResult)
            // 동안 마지막 숫자가 보여야 그 시간이 "볼 것이 있는 시간" 이 된다.
            // 거두는 것은 결과창이 실제로 뜨기 직전이다.
            GameContainer.UI?.Hide<UIBountyButton>();
            GameContainer.UI?.Hide<UIBountySelectDialog>();
            GameContainer.UI?.Hide<UIBattleWarning>();
            GameContainer.UI?.Hide<UIProbabilityUpButton>();
            GameContainer.UI?.Hide<UISummonRateStrip>();
            GameContainer.UI?.Hide<OJ.Economy.UISoulBonusButton>();
            GameContainer.UI?.Hide<OJ.Economy.UISoulBonusDialog>();
            GameContainer.UI?.Hide<OJ.Economy.UILuckyDiceButton>();
            GameContainer.UI?.Hide<OJ.Economy.UILuckyDiceOddsTooltip>();
            GameContainer.UI?.Hide<OJ.Economy.UILuckyDiceResultPopup>();
            GameContainer.UI?.Hide<UIElementUpgradePanel>();
            GameContainer.UI?.Hide<UIWaveRewindButton>();
            battle.ElementUpgrade.SetUpgradeUIAvailable(false);
        }

        private bool TryCompleteWave()
        {
            if (inGameState != InGameState.Wave)
                return false;

            if (WaveMonsterDeadCount < WaveMonsterCount)
                return false;

            // <b>끝내기 전에 마지막 처치를 화면에 찍는다.</b> 이 줄이 없으면
            // <c>RemoveMonsterDeadCount</c> 가 여기서 true 를 받고 곧바로 돌아가
            // <c>SetRemainMonster</c> 를 건너뛴다 — 그래서 게이지가 <b>직전 값</b>에
            // 멈춘 채로 웨이브가 끝난다(24마리를 다 잡았는데 23/24 로 보인다).
            //
            // 본편에서도 매 웨이브가 19/20 으로 끝나고 있었다. 관리 단계가 곧바로
            // 이어져 눈에 덜 띄었을 뿐이다.
            SetRemainMonster(WaveMonsterDeadCount);

            HandleWaveCompleted();
            return true;
        }

        /// <summary>
        /// 탑의 목표 처치 수를 다시 읽는다. <c>TowerRunManager</c> 가 분열 부모의 이탈을
        /// 알았을 때 부른다.
        ///
        /// <b>왜 목표가 도중에 바뀌나.</b> 층의 목표는 분열 자식까지 미리 세어 둔 값인데,
        /// 부모가 <b>죽지 않고</b> 화면 밖으로 사라지면 그 자식들은 영영 안 나온다.
        /// 목표를 그대로 두면 <b>나오지도 않을 몬스터를 기다리며 층이 끝나지 않는다</b> —
        /// 현상금에서 같은 형태의 데드락을 이미 한 번 막았다
        /// (<c>BountyFormula.SpawnThreshold</c> 주석 참조).
        ///
        /// 줄인 뒤에 종료 판정을 다시 돌린다. 마지막 부모가 이탈한 순간이라면 그때
        /// 이미 목표를 채운 상태일 수 있고, 아무도 다시 물어보지 않는다.
        /// </summary>
        public void RefreshTowerWaveTarget()
        {
            if (!battle.Tower.IsActive || inGameState != InGameState.Wave)
                return;

            Run.WaveMonsterCount = GetWaveTargetCount();

            if (TryCompleteWave())
                return;

            SetRemainMonster(WaveMonsterDeadCount);
        }

        public void SetRemainMonster(int currentKillMonster)
        {
            RemainMonster?.SetText(string.Format("{0}/{1}", currentKillMonster, WaveMonsterCount));

            float ratio = (float)currentKillMonster / (float)WaveMonsterCount;
            if (ratio < 0)
                ratio = 0;

            Vector2 vector2 = RemainMonsterGauge.sizeDelta;
            vector2.x = RemainMonsterGauge_Width * ratio;
            RemainMonsterGauge.sizeDelta = vector2;
        }

        /// <summary>
        /// 벽이 무너졌다. <see cref="Wall.TakeDamage"/> 가 부른다.
        ///
        /// <b>예전에는 <c>Wall</c> 이 여기서 <c>GameOver()</c> 를 부르고 곧바로 자기
        /// <c>gameObject</c> 를 부쉈다.</b> 되돌리기는 벽이 살아 있어야 하므로 그 두 가지를
        /// 이리로 옮겼다 — 벽은 이제 "무너졌다" 고 알리기만 하고, 무엇을 할지는 여기서 정한다.
        ///
        /// 최후의 벽 유물은 <b>건드리지 않았다.</b> 그건 자동으로 터지는 것이고 되돌리기는
        /// 유저가 고르는 것이라 성격이 다르다. 순서상 유물이 먼저이고, 유물이 살려내면
        /// 이 함수는 애초에 불리지 않는다.
        /// </summary>
        public void OnWallDestroyed()
        {
            if (isGameOver || deathRewindPromptOpen)
                return;

            if (battle.Rewind.CanRewindOnDeath)
            {
                UIConfirmDialog confirm = GameContainer.UI?.Get<UIConfirmDialog>();
                if (confirm != null)
                {
                    deathRewindPromptOpen = true;

                    // 벽이 0 인 채로 시간이 흐르면 고민하는 동안 화면이 계속 움직인다.
                    // 되돌리기를 안 고르면 아래 DeclineDeathRewind 가 배속을 그대로 되살린다.
                    //
                    // 능동 경로와 같은 이유로 멈추기 직전에 알린다 — 여기서 고민한 시간은
                    // 웨이브 길이가 아니다.
                    battle.Rewind.NotifyRewindOffered();
                    Time.timeScale = 0f;

                    int remaining = battle.Rewind.RemainingCount;
                    confirm.Open(
                        "벽이 무너졌어요",
                        "이번 웨이브를 시작하기 직전으로 되돌릴 수 있어요." + Environment.NewLine +
                        "쓰고 나면 " + Mathf.Max(0, remaining - 1) + "번 남아요.",
                        "되돌릴게요",
                        "여기서 끝낼게요",
                        ConfirmDeathRewind,
                        DeclineDeathRewind);
                    return;
                }

                // 창을 못 열었다고 판을 멈춰 세울 수는 없다 — 여기는 이미 진 자리라
                // 그냥 지는 것이 유일하게 안전한 답이다. 대신 크게 운다.
                Debug.LogError("[전투] 확인 창을 열지 못해 되돌리기를 제안하지 못했다. " +
                               "카탈로그에 UIConfirmDialog 가 있는지 볼 것.");
            }

            FinalizeDefeat();
        }

        private void ConfirmDeathRewind()
        {
            deathRewindPromptOpen = false;

            // 되돌리기가 성공하면 그 안에서 시간을 되살리고 관리 단계까지 데려간다.
            // 실패하는 경우는 창이 떠 있는 사이에 조건이 깨진 때뿐이고, 그때는 진다.
            if (!battle.Rewind.TryRewind(RewindSource.Free, RewindTrigger.Death))
                DeclineDeathRewind();
        }

        /// <summary>
        /// 되돌리지 않기로 했다.
        ///
        /// <b>배속을 <c>timeSpeed</c> 로 되살린 뒤에 진다.</b> 1 로 두면 되돌리기가 생기기
        /// 전과 동작이 달라진다 — 예전에는 벽이 부서질 때 배속이 그대로였고, 결과창 앞의
        /// 뜸(<c>WaitBeforeResult</c>)이 그때 1 로 되돌렸다. 그 순서를 그대로 지킨다.
        /// </summary>
        private void DeclineDeathRewind()
        {
            deathRewindPromptOpen = false;
            battle.Rewind.NotifyOfferClosed();
            Time.timeScale = timeSpeed;
            FinalizeDefeat();
        }

        /// <summary>
        /// 진다. <b>순서가 있다</b> — <see cref="GameOver"/> 가 <c>wall.CurrentHp</c> 를
        /// 읽으므로(부분 보상·기록) 벽은 반드시 그 뒤에 부순다.
        /// </summary>
        private void FinalizeDefeat()
        {
            GameOver();

            if (wall != null)
                Destroy(wall.gameObject);
        }

        public void GameOver()
        {
            if (isGameOver) return;
            isGameOver = true;
            inGameState = InGameState.None;
            CloseBattlePhaseUI();
            RelicManager.Instance?.EndWave();

            // 튜토리얼은 지면 보상도 결과창도 없다. 디렉터가 "다시 해 봐요" 를 띄우고 판을 다시 연다.
            if (battle.Tutorial.IsActive)
            {
                RunHistoryManager.Instance?.EndRun(
                    RunResultType.Fail,
                    CurrentWaveIndex,
                    wall != null ? wall.CurrentHp : 0);
                battle.Tutorial.NotifyFailed();
                return;
            }

            // 탑은 실패 처리가 통째로 다르다. 웨이브 비율 보상이 없고(층은 하나뿐이다),
            // 대신 <b>남은 적 체력</b>을 기록해 다음 도전에서 진척을 보여 준다
            // (기획서 5.6 "보스 체력 12% 잔여 · 이전보다 6%p 개선").
            if (battle.Tower.IsActive)
            {
                FinishTowerFloor(false);
                return;
            }

            int stageIndex = CurrentStageData != null ? CurrentStageData.stageIndex : 1;
            int totalWaves = CurrentStageData != null ? Mathf.Max(1, CurrentStageData.totalWaves) : 1;
            int clearedWaves = Mathf.Clamp(CurrentWaveIndex - 1, 0, totalWaves);
            float rewardRatio = (float)clearedWaves / totalWaves;

            List<PointRewardEntry> partialRewards = StageRewardCalculator.ScaleRewards(
                StageRewardCalculator.BuildNormalClearRewards(stageIndex),
                rewardRatio);
            PointRewardUtility.GrantRewards(partialRewards);

            RunHistoryManager.Instance?.EndRun(
                RunResultType.Fail,
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0);
            Debug.Log(
                $"Stage {stageIndex} Failed | Cleared Waves: {clearedWaves}/{totalWaves} | Ratio: {rewardRatio:0.##} | Partial Normal: {PointRewardUtility.BuildRewardSummary(partialRewards)}");
            ShowStageResult(false, stageIndex, clearedWaves, partialRewards);
        }

        public int GetCurrentWaveMonsterHp()
        {
            if (CurrentStageData == null)
                return 1;

            return CurrentStageData.GetMonsterHpForWave(CurrentWaveIndex);
        }

        public int GetCurrentWaveMonsterDefense()
        {
            if (CurrentStageData == null)
                return 0;

            return CurrentStageData.GetMonsterDefenseForWave(CurrentWaveIndex);
        }

        public int GetCurrentWaveBossHp()
        {
            if (CurrentStageData == null)
                return 1;

            return CurrentStageData.GetBossHpForWave(CurrentWaveIndex);
        }

        public int GetCurrentWaveBossDefense()
        {
            if (CurrentStageData == null)
                return 0;

            return CurrentStageData.GetBossDefenseForWave(CurrentWaveIndex);
        }

        public float GetCurrentWaveBossScale()
        {
            if (CurrentStageData == null)
                return 1f;

            return CurrentStageData.bossScaleMultiplier;
        }

        public bool IsBossWave()
        {
            return CurrentStageData != null && CurrentWaveIndex >= CurrentStageData.totalWaves;
        }

        private void InitializeStage()
        {
            RelicManager.Instance?.BeginStageRun();

            // "게임 플레이" 는 <b>입장</b>으로 센다. 클리어로 세면 못 깨는 스테이지에
            // 막힌 유저의 일일 미션이 통째로 멈추고, 그건 미션이 할 일이 아니다.
            // <b>본편과 탑이 둘 다 여기를 지난다</b>(아래 탑 갈래도 이 메서드 안이다).
            // 소탕은 이 경로를 타지 않으므로 저절로 빠진다 — 전투를 돌리지 않기 때문이다.
            MissionManager.Instance?.Notify(MissionAction.GamePlay);

            // 탑 예약이 있으면 그것이 이 판이다. <b>예약은 한 번만 나온다</b> —
            // 남겨 두면 다음에 본편 스테이지로 들어갈 때 탑 층이 열린다.
            TowerRunRequest towerRun = TowerProgressManager.Instance?.ConsumePendingRun();
            if (towerRun != null)
            {
                InitializeTowerFloor(towerRun);
                return;
            }

            // 튜토리얼 예약도 탑과 같은 모양이다. 한 번만 나온다.
            if (TutorialProgressManager.Instance != null && TutorialProgressManager.Instance.ConsumePendingRun())
            {
                InitializeTutorial();
                return;
            }

            CurrentStageData = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetSelectedStage()
                : StageDatabaseProvider.GetStage(1);

            if (CurrentStageData == null)
            {
                CurrentStageData = new StageData();
            }

            ApplyStageTheme();

            // 런 상태를 한 번에 세운다. 예전에는 이 다섯 줄이 각각 다른 필드를 건드렸고,
            // 하나만 빠뜨리면 이전 판 값이 새어 나갔다.
            Run.BeginRun(
                seed: Environment.TickCount,
                stageIndex: CurrentStageData.stageIndex,
                wallMaxHp: CurrentStageData.wallHp,
                monstersPerWave: CurrentStageData.monstersPerWave,
                initialSummonPoint: 0,
                initialSummonCost: 0);

            battle.ElementUpgrade.ResetRunState();
            // 웨이브 범위 상태는 Run.BeginRun 이 못 지운다 — 매니저 안에 있기 때문이다.
            // 지금은 배틀 스코프가 씬마다 새로 만들어 줘서 우연히 깨끗하지만,
            // 씬을 다시 로드하지 않고 판을 다시 시작하게 되는 날 그 우연이 깨진다.
            battle.Bounty.ResetWaveState();
            // 되돌리기 횟수도 그 "우연" 에 기대는 것 중 하나다. 판이 소유하는 값이므로
            // 지난 판에 쓴 횟수가 따라오지 않게 여기서 판다.
            battle.Rewind.BeginRun();
            deathRewindPromptOpen = false;
            wall.SetInit(WallHp);
            int startSpBonus = RelicManager.Instance != null ? RelicManager.Instance.GetStageStartSpBonus() : 0;
            battle.Summon.SetStageStartSp(CurrentStageData.initialSP + startSpBonus);
            RunHistoryManager.Instance?.StartRun(CurrentStageData, WallHp);
            UpdateWaveText();
        }

        /// <summary>
        /// 탑의 한 층으로 판을 세운다. <see cref="InitializeStage"/> 의 탑 갈래다.
        ///
        /// <b>합성 <see cref="StageData"/> 를 만든다.</b> 씬의 다른 컴포넌트가
        /// <c>CurrentStageData</c> 를 통해 테마·벽 체력·웨이브 수를 읽고 있어서
        /// (스포너·유물·기록), 그 창구를 그대로 두는 편이 분기를 훨씬 덜 만든다.
        /// 값은 층 계획에서 온다 — <b>웨이브 1개</b>가 그 핵심이다(기획서 3.2).
        ///
        /// <c>stageIndex</c> 에 층 번호를 넣지 않는다. 그 값은 본편의 보상·기록 계산이
        /// 읽는 축이라(<c>StageRewardFormula</c>) 층 번호를 흘려보내면 300층 클리어가
        /// 300스테이지의 보상을 받는다. 탑의 보상은 <see cref="TowerFormula"/> 가 따로 낸다.
        /// </summary>
        private void InitializeTowerFloor(TowerRunRequest request)
        {
            TowerFloorPlan plan = TowerDatabaseProvider.GetPlan(request.Floor);
            battle.Tower.BeginRun(plan, request.Loadout);

            CurrentStageData = new StageData
            {
                stageIndex = 1,
                theme = plan.Theme,

                // 층 = 1웨이브. 이 한 줄이 기획서 3.2 의 전투 규칙 전부다.
                totalWaves = 1,
                monstersPerWave = plan.MonsterCount,
                wallHp = plan.WallHp,

                // SP 를 0 으로 둔다. 탑은 소환이 없으므로(기획서 3.2 금지 항목) 이 값이
                // 화면에 뜨더라도 쓸 곳이 없다. 소환 UI 자체는 아래에서 끈다.
                initialSP = 0,
                waveClearSP = 0,

                // 몬스터 스탯은 스포너가 층 계획에서 직접 가져간다. 여기 값은
                // <b>쓰이지 않지만</b> 0 으로 두면 다른 곳의 Max(1, ...) 가 조용히 1 로
                // 바꾼 값을 진짜로 착각하게 만들 수 있어, 계획과 같은 값을 넣어 둔다.
                baseMonsterHp = plan.MonsterHp,
                baseMonsterDefense = plan.MonsterDefense,
            };

            ApplyStageTheme();

            Run.BeginRun(
                seed: Environment.TickCount,
                stageIndex: CurrentStageData.stageIndex,
                wallMaxHp: CurrentStageData.wallHp,
                monstersPerWave: plan.MonsterCount,
                initialSummonPoint: 0,
                initialSummonCost: 0);

            battle.ElementUpgrade.ResetRunState();
            battle.Bounty.ResetWaveState();
            wall.SetInit(WallHp);

            // 탑에서는 SP 를 쓰지 않는다. 0 을 넣어 두면 숫자가 0/10 으로 뜨고,
            // 소환 버튼은 아래 HideBattleOnlyUI 가 통째로 치운다.
            battle.Summon.SetStageStartSp(0);

            HideTowerForbiddenUI();
            UpdateWaveText();

            Debug.Log("[탑] " + plan.Floor + "층 시작 — " + plan.DisplayName +
                      " / " + plan.MonsterCount + "마리 × HP " + plan.MonsterHp +
                      " (목표 처치 " + plan.KillTarget + ")");
        }

        /// <summary>
        /// 탑에서 쓰지 않는 전투 UI 를 끈다. 기획서 3.2 의 금지 항목 넷이 여기서 닫힌다 —
        /// SP · 소환 · 머지 · 전투 중 성장.
        ///
        /// <b>치우는 것과 막는 것을 둘 다 한다.</b> 버튼만 끄면 다른 경로(백키·핫키)로
        /// 여전히 열리고, 규칙만 막으면 눌리는 버튼이 아무 일도 안 해 고장으로 보인다.
        /// 실제 차단은 각 시스템이 <c>battle.Tower.IsActive</c> 로 한다.
        /// </summary>
        /// <summary>
        /// 튜토리얼 판을 세운다. 본편 갈래와 거의 같고, 다른 것은 셋이다 —
        /// StageData 를 코드로 만들고(<see cref="TutorialStage.Build"/>), 유물 시작 SP 를 더하지 않고,
        /// 디렉터가 대본을 쥐도록 <see cref="TutorialRunManager.Begin"/> 을 부른다.
        /// </summary>
        private void InitializeTutorial()
        {
            battle.Tutorial.Begin();

            CurrentStageData = TutorialStage.Build(battle.Summon.SummonsPerCostIncrease);

            ApplyStageTheme();

            Run.BeginRun(
                seed: Environment.TickCount,
                stageIndex: CurrentStageData.stageIndex,
                wallMaxHp: CurrentStageData.wallHp,
                monstersPerWave: CurrentStageData.monstersPerWave,
                initialSummonPoint: 0,
                initialSummonCost: 0);

            battle.ElementUpgrade.ResetRunState();
            battle.Bounty.ResetWaveState();
            battle.Rewind.BeginRun();
            deathRewindPromptOpen = false;
            wall.SetInit(WallHp);

            // 유물 시작 SP 보너스를 더하지 않는다. 시작 SP 는 "정확히 여섯 번 소환" 으로
            // 계산된 값이라(TutorialStage.Build) 한 점이라도 남으면 첫 단계의 딤드가 안 풀린다.
            battle.Summon.SetStageStartSp(CurrentStageData.initialSP, TutorialStage.StartSummonCost);

            RunHistoryManager.Instance?.StartRun(CurrentStageData, WallHp);
            UpdateWaveText();

            Debug.Log("[튜토리얼] 판 시작 — 웨이브 " + CurrentStageData.totalWaves +
                      " / 시작 SP " + CurrentStageData.initialSP +
                      " / 웨이브 클리어 SP " + CurrentStageData.waveClearSP);
        }

        private void HideTowerForbiddenUI()
        {
            // <b>컴포넌트가 붙은 오브젝트를 통째로 끄지 않는다.</b> 그 오브젝트에는 다른
            // 전투 UI 가 같이 붙어 있을 수 있고, 무엇보다 꺼진 오브젝트의 <c>Start</c> 가
            // 안 돌아 <c>UIDiceSummonSystem</c> 의 초기화가 통째로 사라진다.
            // 실제로 치울 것은 눈에 보이는 둘뿐이다.
            if (battle.Summon.summonButton != null)
                battle.Summon.summonButton.gameObject.SetActive(false);

            if (battle.Summon.spText != null)
                battle.Summon.spText.gameObject.SetActive(false);

            battle.ElementUpgrade.SetUpgradeUIAvailable(false);
        }

        private void ApplyStageTheme()
        {
            // 여기서 터지면 InitializeStage() 가 중간에 끊겨 WallHp·시작 SP·웨이브 수가
            // 통째로 설정되지 않고, ChangeState(Setting) 도 실행되지 않아 플레이 버튼이
            // 살아나지 않는다. 배경 하나 때문에 스테이지 초기화를 잃을 이유가 없다.
            // StaticResource 가 없다는 사실 자체는 MonoSingleton 이 이미 크게 운다.
            StaticResource resource = StaticResource.Instance;
            StageThemeResource themeResource = resource != null
                ? resource.GetStageThemeResource(CurrentStageData.theme)
                : null;
            if (stageBackground != null && themeResource != null && themeResource.MapBackground != null)
                stageBackground.sprite = themeResource.MapBackground;

            battle.Spawner.ConfigureTheme(CurrentStageData.theme);
        }

        private IEnumerator CoApplyStageStartRelics()
        {
            yield return null;
            RelicManager.Instance?.TryApplyStageStartDice();
        }

        // ── 실시간 시작 ─────────────────────────────────────────────────

        /// <summary>판에 들어오거나 되돌린 뒤 웨이브가 열리기까지의 카운트다운(초).</summary>
        public const int StartCountdownSeconds = 3;

        private bool countdownActive;

        /// <summary>시작 버튼이 없어진 판에서 쓰는 시작 순서: 유물 선제 배치 → 카운트다운 → 웨이브.</summary>
        private IEnumerator CoRealtimeStart()
        {
            yield return CoApplyStageStartRelics();
            yield return CoCountdownThenWave();
        }

        /// <summary>
        /// 3 · 2 · 1 을 중앙에 띄우고 웨이브를 연다. <b>실제 시간</b>으로 센다 — 되돌리기 직후에는
        /// <c>timeScale</c> 이 0 일 수 있고, 배속을 켜 둔 사람에게도 같은 3초여야 한다.
        ///
        /// 카운트 동안에도 소환·머지·자리구매는 된다(준비 단계 상태라 보드가 열려 있다).
        /// 도중에 판이 끝났으면(나가기를 눌렀다) 웨이브를 열지 않는다 — 끝난 판에서 몬스터가 나오면 안 된다.
        /// </summary>
        private IEnumerator CoCountdownThenWave()
        {
            countdownActive = true;

            Transform iconTransform = PlayUI_Field != null ? PlayUI_Field.transform.Find("Image") : null;
            TMP_Text label = PlayUI_Field != null ? PlayUI_Field.GetComponentInChildren<TMP_Text>(true) : null;
            string originalText = label != null ? label.text : null;
            float originalSize = label != null ? label.fontSize : 0f;

            // 3 · 2 · 1 은 크게 — 원래 글자("Play Wave")는 이 크기로는 멀리서 안 읽힌다.
            if (label != null)
                label.fontSize = originalSize * 3f;

            if (PlayUI_Field != null)
                PlayUI_Field.gameObject.SetActive(true);
            if (iconTransform != null)
                iconTransform.gameObject.SetActive(false);

            for (int n = StartCountdownSeconds; n >= 1; n--)
            {
                if (label != null)
                    label.SetText(n.ToString());

                yield return new WaitForSecondsRealtime(1f);

                if (isGameOver || inGameState == InGameState.None)
                    break;
            }

            countdownActive = false;
            if (label != null && originalText != null)
            {
                label.SetText(originalText);
                label.fontSize = originalSize;
            }
            if (iconTransform != null)
                iconTransform.gameObject.SetActive(true);
            if (PlayUI_Field != null)
                PlayUI_Field.gameObject.SetActive(false);

            if (isGameOver || inGameState == InGameState.None)
                yield break;

            ChangeState(InGameState.Wave);
        }

        /// <summary>
        /// 되돌리기가 끝난 뒤의 재개. 레거시 흐름이면 준비 단계에서 시작 버튼을 기다리고,
        /// 실시간이면 같은 3초 카운트다운 뒤 웨이브를 다시 연다.
        /// </summary>
        public void EnterAfterRewind()
        {
            ChangeState(InGameState.Setting);

            if (IsContinuousWave)
                StartCoroutine(CoCountdownThenWave());
        }

        /// <summary>
        /// 튜토리얼에서 소환·머지를 가르친 뒤 웨이브를 연다(3 · 2 · 1 카운트다운 뒤 실시간 웨이브).
        /// 일반 판은 <see cref="CoRealtimeStart"/> 가 알아서 연다.
        /// </summary>
        public void BeginTutorialWaves()
        {
            if (battle.Tutorial.IsActive && inGameState == InGameState.Setting && !countdownActive)
                StartCoroutine(CoCountdownThenWave());
        }

        /// <summary>삭제 영역을 끈다. 씬 YAML 을 손대지 않고 런타임에 끈다 — 드롭 대상이 없어지므로 삭제 동작도 같이 죽는다.</summary>
        private static void HideDeleteArea()
        {
            UIRemoveDice area = FindFirstObjectByType<UIRemoveDice>(FindObjectsInactive.Include);
            if (area != null)
                area.gameObject.SetActive(false);
        }

        /// <summary>
        /// 이 웨이브에 나오는 일반 몬스터 수. 스테이지 값에 <b>초반 3웨이브 완화</b>(<see cref="EarlyWaveRelief"/>)를 적용한다 —
        /// 초반에는 다이스가 한두 개뿐이라 다 못 막는다. 튜토리얼은 대본이 정한 마릿수라 완화하지 않는다.
        /// 처치 목표(<see cref="GetWaveTargetCount"/>)와 스포너가 <b>같은 함수</b>를 읽어야 웨이브가 제때 끝난다.
        /// </summary>
        public int RegularMonstersFor(int waveIndex)
        {
            if (CurrentStageData == null)
                return 1;

            return battle.Tutorial.IsActive
                ? Mathf.Max(1, CurrentStageData.monstersPerWave)
                : EarlyWaveRelief.MonstersFor(CurrentStageData.monstersPerWave, waveIndex);
        }

        /// <summary>이 웨이브의 몬스터 등장 간격 배율(1 = 그대로). 초반 3웨이브는 느리다. 튜토리얼은 1.</summary>
        public float SpawnIntervalScaleFor(int waveIndex)
        {
            return battle.Tutorial.IsActive ? 1f : EarlyWaveRelief.IntervalPercent(waveIndex) / 100f;
        }

        private int GetWaveTargetCount()
        {
            // 탑은 목표가 <c>monstersPerWave</c> 와 다르다. 분열형 적이 죽으면서 자식을
            // 만들고 그 자식도 잡아야 층이 끝나므로, 미리 세어 둔 값을 그대로 쓴다
            // (<see cref="TowerFormula.WaveKillTarget"/>). 여기서 다시 세면 두 곳이
            // 갈리고, 갈리는 순간 층이 안 끝나거나 한 마리 일찍 끝난다.
            if (battle.Tower.IsActive)
                return battle.Tower.EffectiveKillTarget;

            if (CurrentStageData == null)
                return WaveMonsterCount;

            int targetCount = RegularMonstersFor(CurrentWaveIndex);
            if (IsBossWave())
                targetCount += 1;

            return Mathf.Max(1, targetCount);
        }

        private void HandleWaveCompleted()
        {
            // 탑의 웨이브 완료 = 층 클리어다. 아래 본편 경로가 하는 일 — 웨이브 클리어
            // 강화석 1개, 유물 발동, SP 지급, 보상 미리보기, 스테이지 기록 — 은 전부
            // <b>여러 웨이브를 전제</b>한 것이라 층 하나짜리 콘텐츠에 얹으면 뜻이 없거나
            // 본편 진행도를 오염시킨다(스테이지 기록이 특히 그렇다).
            if (battle.Tower.IsActive)
            {
                RelicManager.Instance?.EndWave();
                FinishTowerFloor(true);
                return;
            }

            // 수급량은 스테이지 데이터가 정한다(StageData.waveClearStone). 리터럴 1 이었던
            // 자리다 — 웨이브 수를 줄이면서 한 판의 강화석 예산이 같이 줄어, 진화가 마지막
            // 웨이브에나 가능해졌기 때문이다. 탑은 위에서 이미 돌아갔다.
            int waveClearStone = CurrentStageData != null ? Mathf.Max(0, CurrentStageData.waveClearStone) : 1;
            if (waveClearStone > 0)
                battle.BattlePoints?.Add(BattlePointType.EnhanceStone, waveClearStone);

            // 튜토리얼 판에서는 유물이 끼지 않는다(UIDiceSummonSystem 의 같은 판단).
            if (!battle.Tutorial.IsActive)
                RelicManager.Instance?.ApplyWaveClearRelics(wall);

            if (CurrentStageData != null)
            {
                // 연속 웨이브는 처치마다 나눠 줬으므로 남은 몫(현상금만 남은 끝, 나머지 반올림)만 준다.
                int waveClearSp = IsContinuousWave
                    ? Mathf.Max(0, WaveClearSpFor(CurrentWaveIndex) - waveSpGranted)
                    : WaveClearSpFor(CurrentWaveIndex);
                battle.Summon.AddSP(waveClearSp);
                waveSpGranted = 0;

                // 튜토리얼은 골드를 주지 않으므로 "골드 +N" 미리보기가 거짓말이 된다.
                if (!battle.Tutorial.IsActive)
                    ShowWaveRewardPreview();
            }

            RunHistoryManager.Instance?.RecordWaveComplete(
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0,
                battle.Summon.currentSP);

            // 튜토리얼 판은 스테이지 진행도에 남지 않는다. stageIndex 가 1 이라 그대로 두면
            // 1스테이지를 깬 것으로 적힌다.
            if (CurrentStageData != null && !battle.Tutorial.IsActive)
                StageProgressManager.Instance?.RecordClearedWave(CurrentStageData.stageIndex, CurrentWaveIndex);

            if (CurrentStageData != null && CurrentWaveIndex >= CurrentStageData.totalWaves)
            {
                RelicManager.Instance?.EndWave();
                ClearStage();
                return;
            }

            RelicManager.Instance?.EndWave();

            // 테스트용 연속 웨이브: 관리 단계를 건너뛰고 곧바로 다음 웨이브를 연다.
            // Wave → Wave 진입도 스냅샷·WaveIndex++·현상금·스포너 리셋을 그대로 탄다.
            if (IsContinuousWave)
            {
                ChangeState(InGameState.Wave);
                return;
            }

            ChangeState(InGameState.Setting);
        }

        /// <summary>
        /// 층을 끝낸다. 클리어와 실패가 <b>같은 함수</b>를 지난다.
        ///
        /// <b>갈래를 하나로 두는 것이 중요하다.</b> 시계 정지·기록·보상·결과창이 전부
        /// 여기 모여 있고, 나누면 한쪽만 고치는 사고가 난다 —
        /// <c>OnClick_Pause</c> 가 새 종료 경로를 만들지 않고 <see cref="GameOver"/> 를
        /// 타는 것과 같은 판단이다.
        ///
        /// 실패 경로는 <see cref="GameOver"/> 가, 클리어 경로는
        /// <see cref="HandleWaveCompleted"/> 가 부르며 둘 다 <c>isGameOver</c> 를
        /// 이미 세워 둔 뒤다.
        /// </summary>
        private void FinishTowerFloor(bool isClear)
        {
            isGameOver = true;
            inGameState = InGameState.None;
            battle.Tower.StopTimer();
            CloseBattlePhaseUI();

            TowerFloorPlan plan = battle.Tower.Plan;
            TowerProgressManager progress = TowerProgressManager.Instance;

            int floor = plan.Floor;
            int elapsedMs = battle.Tower.ElapsedMilliseconds;
            int remainingPercent = battle.Tower.RemainingHpPercent;

            // <b>이전 기록을 먼저 읽는다.</b> 아래에서 기록을 갱신하고 나면 "이전" 이
            // 사라진다 — 결과 화면이 보여 줘야 하는 것은 바로 그 이전 값이다(기획서 5.6).
            //
            // 읽는 것이 잔여 체력 하나뿐인 이유: 반복이 없어 같은 층을 두 번 클리어할 수
            // 없으므로, 이 층에 기록이 있다면 그것은 언제나 지난번 실패다.
            int previousRemaining = progress != null ? progress.GetBestRemainingHpPercent(floor) : 100;

            bool firstClear = false;
            bool newRecord = false;
            DiceType unlocked = DiceType.Max;

            // 이미 보유한 다이스를 이 층이 열면 재화로 환급된다. 여기 담아 두었다가
            // 아래에서 층 보상과 <b>함께 한 번에</b> 지급한다 — 지급 경로가 둘이 되면
            // 이중 지급을 막을 자리가 없어진다.
            var unlockRefunds = new List<PointRewardEntry>();

            if (isClear)
            {
                if (progress != null)
                {
                    firstClear = progress.RecordClear(floor, elapsedMs, out newRecord, unlockRefunds);

                    // 소비하지 않고 본다. NEW 배지를 위해 남겨 두는 것은 편성 화면의 몫이다.
                    unlocked = progress.PendingNewUnlock;
                }
            }
            else if (progress != null)
            {
                newRecord = progress.RecordFail(floor, remainingPercent);
            }

            // 이 층이 다이스를 준 경우, 처음 얻은 것인지 환급된 것인지를 결과창에 실어 보낸다.
            // <b>환급 여부는 unlockRefunds 가 말한다</b> — 비어 있으면 새로 얻은 것이다.
            // 따로 조회하지 않는 이유는 그 사이에 값이 바뀌면 준 것과 보여 준 것이 갈리기 때문이다.
            bool hasTowerDice = false;
            DiceRewardView towerDiceView = default;

            if (unlocked != DiceType.Max)
            {
                hasTowerDice = true;
                towerDiceView = DiceRewardView.NewlyOwned(unlocked);
            }
            else if (unlockRefunds.Count > 0 && progress != null)
            {
                DiceType refunded = progress.GetUnlockGrantedByFloor(floor);
                if (refunded != DiceType.Max)
                {
                    hasTowerDice = true;
                    towerDiceView = DiceRewardView.Refunded(
                        refunded, unlockRefunds[0].PointType, unlockRefunds[0].Amount);
                }
            }

            List<PointRewardEntry> rewards = TowerRunManager.BuildClearRewards(floor, firstClear);
            rewards.AddRange(unlockRefunds);
            PointRewardUtility.GrantRewards(rewards);

            RunHistoryManager.Instance?.EndRun(
                isClear ? RunResultType.Clear : RunResultType.Fail,
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0);

            Debug.Log("[탑] " + floor + "층 " + (isClear ? "클리어" : "실패") +
                      " | " + (elapsedMs / 1000f).ToString("0.0") + "초" +
                      " | 남은 적 " + remainingPercent + "%" +
                      " | 보상 " + PointRewardUtility.BuildRewardSummary(rewards));

            ShowTowerResult(new TowerResultData
            {
                Plan = plan,
                IsClear = isClear,
                ElapsedMilliseconds = elapsedMs,
                RemainingHpPercent = remainingPercent,
                PreviousRemainingHpPercent = previousRemaining,
                IsNewRecord = newRecord,
                UnlockedDice = unlocked,
                DiceReward = towerDiceView,
                HasDiceReward = hasTowerDice,
                Rewards = rewards,
                DamageShares = battle.Tower.BuildDamageShares(),
            });
        }

        /// <summary>
        /// 탑 결과창을 띄운다. 못 띄우면 로비로 돌려보낸다 —
        /// <see cref="ShowStageResult"/> 와 같은 판단이다. 여기서 멈추면 이미 끝난 판의
        /// 전투 씬에 갇힌다.
        /// </summary>
        private void ShowTowerResult(TowerResultData data)
        {
            StartCoroutine(CoShowTowerResult(data));
        }

        private IEnumerator CoShowTowerResult(TowerResultData data)
        {
            yield return WaitBeforeResult();

            UITowerResultDialog dialog = GameContainer.UI?.Get<UITowerResultDialog>();
            if (dialog == null)
            {
                StartCoroutine(CoReturnToLobby());
                yield break;
            }

            GameContainer.UI?.Hide<UIWaveRewardPreviewDialog>();
            dialog.Open(data, SceneFlowManager.LoadLobby);
        }

        /// <summary>
        /// 결과창 앞의 뜸. <b>배속을 1 로 되돌리고</b> 실제 시간으로 기다린다.
        ///
        /// 되돌리는 이유는 이 뜸이 <b>보여 주기 위한</b> 시간이기 때문이다 — 3배속으로
        /// 지나가는 마지막 순간은 보라고 준 시간이 아니다. 어차피 판이 끝나 조작할 것도
        /// 없고, 씬을 나갈 때 <c>SceneFlowManager</c> 가 어차피 1 로 되돌린다.
        /// </summary>
        private IEnumerator WaitBeforeResult()
        {
            Time.timeScale = 1f;

            if (resultPopupDelay > 0f)
                yield return new WaitForSecondsRealtime(resultPopupDelay);

            // 기여도 패널은 <b>뜸이 끝난 뒤</b> 거둔다. 판이 끝나는 순간에 같이 치우면
            // 그 0.9초가 아무것도 없는 화면이 되고, 뜸을 준 이유가 없어진다.
            GameContainer.UI?.Hide<UIDamageContributionPanel>();
        }

        private void ShowWaveRewardPreview()
        {
            if (CurrentStageData == null)
                return;

            // ShowGoldGain 이 안에서 Enter 까지 부르므로 Show 가 아니라 Get 으로 받는다.
            UIWaveRewardPreviewDialog previewDialog = GameContainer.UI?.Get<UIWaveRewardPreviewDialog>();
            if (previewDialog == null)
                return;

            int totalWaves = Mathf.Max(1, CurrentStageData.totalWaves);
            int currentAccumulatedGold = StageRewardCalculator.GetAccumulatedGuaranteedGold(
                CurrentStageData.stageIndex,
                CurrentWaveIndex,
                totalWaves);
            int previousAccumulatedGold = StageRewardCalculator.GetAccumulatedGuaranteedGold(
                CurrentStageData.stageIndex,
                CurrentWaveIndex - 1,
                totalWaves);
            int gainedGold = currentAccumulatedGold - previousAccumulatedGold;

            previewDialog.ShowGoldGain(
                gainedGold,
                currentAccumulatedGold,
                StageRewardCalculator.GetGuaranteedNormalGold(CurrentStageData.stageIndex));
        }

        private void UpdateWaveText()
        {
            if (WaveText == null)
                return;

            // 탑에서 "Wave 1/1" 은 아무것도 말하지 않는다 — 층이 곧 웨이브라 언제나 1/1 이다.
            // 그 자리에 <b>층과 적 콘셉트</b>를 넣는다.
            //
            // <b>이 줄이 처치 게이지의 숫자를 설명한다.</b> 분열형 층은 화면에 8마리가
            // 나오는데 목표가 24 라 그냥 보면 안 맞아 보인다. 24 는 <b>부모 8 + 자식 16</b>
            // 이고, 그 사실을 알려 주는 것은 "분열형 적" 이라는 이름 하나면 된다.
            if (battle.Tower.IsActive)
            {
                TowerFloorPlan plan = battle.Tower.Plan;
                WaveText.SetText(plan.Floor + "층 · " + plan.DisplayName);
                return;
            }

            int totalWaves = CurrentStageData != null ? Mathf.Max(1, CurrentStageData.totalWaves) : 1;
            int currentWave = Mathf.Clamp(CurrentWaveIndex, 0, totalWaves);
            WaveText.SetText("Wave {0}/{1}", currentWave, totalWaves);
        }

        private void ClearStage()
        {
            if (isGameOver)
                return;

            isGameOver = true;
            inGameState = InGameState.None;
            CloseBattlePhaseUI();

            // 튜토리얼은 보상도 결과창도 없다 — 보상은 진화로 만든 다이스다(디렉터가 이미 줬다).
            // 스테이지 진행도에도 남기지 않는다. 끝맺음은 디렉터가 한다.
            if (battle.Tutorial.IsActive)
            {
                RunHistoryManager.Instance?.EndRun(
                    RunResultType.Clear,
                    CurrentWaveIndex,
                    wall != null ? wall.CurrentHp : 0);
                GameContainer.UI?.Hide<UIDamageContributionPanel>();
                battle.Tutorial.NotifyCleared();
                return;
            }

            int stageIndex = CurrentStageData != null ? CurrentStageData.stageIndex : 1;
            StageClearGrade clearGrade = StageRewardCalculator.GetClearGrade(wall.CurrentHp, wall.TotalHp);

            List<PointRewardEntry> normalRewards = StageRewardCalculator.BuildNormalClearRewards(stageIndex);
            if (RelicManager.Instance != null)
                normalRewards = RelicManager.Instance.ApplyStageClearRewardBonus(normalRewards);
            PointRewardUtility.GrantRewards(normalRewards);

            StageProgressManager.Instance?.RecordStageClear(stageIndex, clearGrade);

            Debug.Log(
                $"Stage {stageIndex} Clear ({clearGrade}) | Normal: {PointRewardUtility.BuildRewardSummary(normalRewards)}");

            RunHistoryManager.Instance?.EndRun(
                RunResultType.Clear,
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0);

            var resultRewards = new List<PointRewardEntry>(normalRewards.Count);
            resultRewards.AddRange(normalRewards);
            ShowStageResult(true, stageIndex, CurrentWaveIndex, resultRewards);
        }

        private IEnumerator CoReturnToLobby()
        {
            inGameState = InGameState.None;
            yield return new WaitForSecondsRealtime(returnToLobbyDelay);
            SceneFlowManager.LoadLobby();
        }

        private void ShowStageResult(bool isWin, int stageIndex, int reachedWaveCount, IReadOnlyList<PointRewardEntry> rewards)
        {
            StartCoroutine(CoShowStageResult(isWin, stageIndex, reachedWaveCount, rewards));
        }

        private IEnumerator CoShowStageResult(
            bool isWin, int stageIndex, int reachedWaveCount, IReadOnlyList<PointRewardEntry> rewards)
        {
            yield return WaitBeforeResult();

            // Open 이 값을 채우고 Enter 까지 부르므로 Get 으로 받는다.
            UIStageResultDialog resultDialog = GameContainer.UI?.Get<UIStageResultDialog>();
            if (resultDialog == null)
            {
                // 결과창을 못 띄워도 로비로는 돌아가야 한다. 여기서 멈추면 이미 끝난 판의
                // 전투 씬에 갇힌다. 못 연 사유는 UIService 가 이미 로그로 남겼다.
                StartCoroutine(CoReturnToLobby());
                yield break;
            }

            GameContainer.UI?.Hide<UIWaveRewardPreviewDialog>();

            // 이 스테이지에서 가장 멀리 간 웨이브. 이번 판은 이미 기록됐다
            // (웨이브마다 RecordClearedWave, 클리어 때 RecordStageClear).
            int bestWave = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetBestClearedWave(stageIndex)
                : reachedWaveCount;

            resultDialog.Open(
                isWin,
                stageIndex,
                reachedWaveCount,
                CurrentStageData != null ? CurrentStageData.totalWaves : reachedWaveCount,
                bestWave,
                rewards,
                SceneFlowManager.LoadLobby);
        }

        public void OnApplicationQuit()
        {
            Application.Quit();
        }
    }

}
