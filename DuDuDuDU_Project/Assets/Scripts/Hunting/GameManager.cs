using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using VContainer;
using OJ.Core;
using OJ.Analytics;
using OJ.Bounty;
using OJ.DI;
using OJ.Dice;
using OJ.Element;
using OJ.Point;
using OJ.Relic;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.Tower;
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

        private bool isGameOver { get => Run.IsGameOver; set => Run.IsGameOver = value; }

        public int WallHp => Run.WallHp;

        public Wall wall;

        public InGameState inGameState = InGameState.None;

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

        private float timeSpeed = 1.0f;
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
                battle.Bounty.OnWaveResolved -= OnBountyResolved;
                battle.Bounty.OnSpawned -= OnBountySpawned;
            }

            if (PlayUI != null) PlayUI.onClick.RemoveListener(OnClick_PlayUI);
            if (Pause != null) Pause.onClick.RemoveListener(OnClick_Pause);
            if (Speed != null) Speed.onClick.RemoveListener(OnClick_Speed);

        }

        private void Start()
        {
            // 배틀 스코프는 모든 Start 앞에 빌드되므로 여기서 battle.Bounty 는 살아 있다.
            // 구독을 Awake 로 올리면 그때는 아직 null 이다.
            battle.Bounty.OnWaveResolved += OnBountyResolved;
            battle.Bounty.OnSpawned += OnBountySpawned;

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
            StartCoroutine(CoApplyStageStartRelics());
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
        /// 현상금이 정리됐다. <b>일반 몬스터를 먼저 다 잡은 웨이브</b>에서는 이것이
        /// 웨이브를 끝내는 마지막 조각이다 — 그 순서에서는 아무도
        /// <see cref="RemoveMonsterDeadCount"/> 를 다시 부르지 않기 때문이다.
        /// </summary>
        private void OnBountyResolved()
        {
            TryCompleteWave();
        }

        /// <summary>
        /// 현상금이 화면에 나왔다. 알림 띠를 잠깐 띄운다.
        ///
        /// <c>Show</c> 가 아니라 <c>Get</c> 으로 받는 것은 <c>Play</c> 가 내용을 채우고
        /// <c>Enter</c> 까지 스스로 부르기 때문이다 — <c>ShowWaveRewardPreview</c> 와 같은 이유다.
        /// </summary>
        private void OnBountySpawned(OJ.Bounty.BountyDefinition definition)
        {
            UIBountyCallout callout = GameContainer.UI?.Get<UIBountyCallout>();
            callout?.Play(definition, battle.Bounty.GetHp(battle.Bounty.ActiveGrade));
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

            PlayUI?.gameObject.SetActive(state == InGameState.Setting);
            PlayUI_Field?.gameObject.SetActive(state == InGameState.Setting);
            // 관리 단계에서도 나갈 수 있어야 한다. 웨이브 사이에 그만두려는 사람이
            // 다음 웨이브를 억지로 시작해야 했던 것이 예전 동작이다.
            Pause?.gameObject.SetActive(state == InGameState.Wave || state == InGameState.Setting);
            Speed?.gameObject.SetActive(state == InGameState.Wave);
            RemainMonster?.gameObject.SetActive(state == InGameState.Wave);
            RemainMonsterGauge?.gameObject.SetActive(state == InGameState.Wave);
            WaveText?.gameObject.SetActive(state == InGameState.Wave || state == InGameState.Setting);

            // 현상금 띠는 관리 단계에만 뜬다. 웨이브 중에는 바꿀 수도 없고 몬스터가
            // 내려오는 길 한가운데를 가린다.
            //
            // <b>Show/Hide 를 여기 두는 이유.</b> 상태를 아는 곳이 여기 하나뿐이다.
            // 띠가 스스로 판단하게 하면 매 프레임 inGameState 를 들여다보게 되고,
            // 그것은 이벤트로 바꿔 놓은 것을 다시 폴링으로 되돌리는 일이다.
            if (state == InGameState.Setting)
            {
                GameContainer.UI?.Show<UIBountyBanner>();
                // 등장 알림은 스스로 1.8초 뒤에 사라지지만, 그 전에 웨이브가 끝나면
                // 관리 단계까지 따라 들어온다. 남은 시간을 기다리지 않고 여기서 거둔다.
                GameContainer.UI?.Hide<UIBountyCallout>();
            }
            else
            {
                GameContainer.UI?.Hide<UIBountyBanner>();
                // 선택 창이 열린 채 웨이브가 시작되는 경로는 지금 없지만(창이 화면을
                // 덮어 시작 버튼을 누를 수 없다) 닫아 두는 편이 싸다.
                GameContainer.UI?.Hide<UIBountySelectDialog>();
            }

            // 속성강화도 관리 단계 전용이다. 소환·머지·진화가 전부 그런데 이것만
            // 전투 중에 열려 있었다 — 같은 강화석을 쓰면서 규칙이 혼자 달랐다.
            //
            // 실제 차단은 ElementUpgradeManager.TryLevelUp 이 하고, 여기서는 버튼을
            // 흐리게 만든다(치우지 않는다 — 빈 자리는 기능이 사라진 것처럼 보인다).
            // 창이 열린 채 웨이브가 시작될 수 있으므로(그 창은 전체 화면을 덮지 않는다)
            // 닫는 것까지 해야 한다.
            battle.ElementUpgrade.SetUpgradeUIAvailable(state == InGameState.Setting);
            if (state != InGameState.Setting)
                GameContainer.UI?.Hide<UIElementUpgradePanel>();

            // 관리 단계마다 자동으로 뜨던 조합 진행도 창(UIDiceCraftProgressDialog)은
            // 조합식과 함께 사라졌다. 상위 다이스로 가는 길은 이제 목록을 띄워 재고를
            // 세는 것이 아니라, 다이스를 눌러 그 자리에서 진화시키는 것이다 —
            // UIBattleDiceDetailPanel 참조.

            if (state == InGameState.Wave)
            {
                Run.WaveIndex++;
                RelicManager.Instance?.BeginWave(CurrentWaveIndex);

                // 기여도를 웨이브마다 0 으로 돌린다. 판 전체로 누적하면 뒤 웨이브로 갈수록
                // 앞 웨이브의 몫에 묻혀, <b>관리 단계에서 방금 바꾼 편성의 효과가 안 보인다</b>.
                //
                // <b>본편·탑 공통이라 여기 한 곳에서만 리셋한다.</b> 탑은 한 층이 곧
                // 한 웨이브라(기획서 3.2) 이 줄이 그대로 층 리셋이 된다.
                battle.Contribution.BeginWave();

                // 탑에는 현상금이 없다. 기획서 2장이 두 콘텐츠의 축을 분리하라고 했고,
                // 현상금은 "관리 단계에 무엇을 부를까" 를 고르는 시스템이라 관리 단계가
                // 없는 탑에서는 고를 자리 자체가 없다.
                //
                // 지금은 SelectedGrade 가 0(소환 X)이라 불러도 아무 일이 없지만,
                // 기본값 하나에 기대는 것과 규칙으로 끊는 것은 다르다.
                if (!battle.Tower.IsActive)
                {
                    // 스포너가 첫 Update 를 돌기 전에 이번 웨이브의 현상금 등급을 확정한다.
                    // PlayWave 뒤로 미루면 그 사이에 ShouldSpawn 이 지난 웨이브 값을 답한다.
                    battle.Bounty.BeginWave(CurrentWaveIndex);
                }
                Run.WaveMonsterCount = GetWaveTargetCount();
                UpdateWaveText();
                SetRemainMonster(0);
                battle.Spawner.PlayWave();
                Time.timeScale = timeSpeed;
                WaveMonsterDeadCount = 0;
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
        }

        public void RemoveMonsterDeadCount()
        {
            if (inGameState != InGameState.Wave)
                return;
            WaveMonsterDeadCount++;

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
            GameContainer.UI?.Hide<UIBountyBanner>();
            GameContainer.UI?.Hide<UIBountySelectDialog>();
            GameContainer.UI?.Hide<UIBountyCallout>();
            GameContainer.UI?.Hide<UIElementUpgradePanel>();
            battle.ElementUpgrade.SetUpgradeUIAvailable(false);
        }

        private bool TryCompleteWave()
        {
            if (inGameState != InGameState.Wave)
                return false;

            if (WaveMonsterDeadCount < WaveMonsterCount)
                return false;

            if (!battle.Bounty.IsWaveResolved)
            {
                // 일반 몬스터는 다 잡았고 현상금만 남았다. 게이지는 가득 찬 채로 두고
                // 현상금이 벽에 닿거나 죽기를 기다린다 — 면역 덕에 반드시 그중 하나로 끝난다.
                SetRemainMonster(WaveMonsterDeadCount);
                return false;
            }

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

        public void GameOver()
        {
            if (isGameOver) return;
            isGameOver = true;
            inGameState = InGameState.None;
            CloseBattlePhaseUI();
            RelicManager.Instance?.EndWave();

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

            // 탑 예약이 있으면 그것이 이 판이다. <b>예약은 한 번만 나온다</b> —
            // 남겨 두면 다음에 본편 스테이지로 들어갈 때 탑 층이 열린다.
            TowerRunRequest towerRun = TowerProgressManager.Instance?.ConsumePendingRun();
            if (towerRun != null)
            {
                InitializeTowerFloor(towerRun);
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

            int targetCount = CurrentStageData.monstersPerWave;
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

            PointManager.Instance?.Add(PointType.BattleEnhanceStone, 1);
            RelicManager.Instance?.ApplyWaveClearRelics(wall);

            // 현상금 보상은 웨이브가 끝나는 이 자리에서만 들어온다. 잡은 순간 주면
            // 전투 중에 SP 로 소환이 되어 "관리 단계에 쓰라"는 뜻이 무너진다.
            battle.Bounty.GrantPendingRewards();

            if (CurrentStageData != null)
            {
                battle.Summon.AddSP(CurrentStageData.waveClearSP);
                ShowWaveRewardPreview();
            }

            RunHistoryManager.Instance?.RecordWaveComplete(
                CurrentWaveIndex,
                wall != null ? wall.CurrentHp : 0,
                battle.Summon.currentSP);

            if (CurrentStageData != null)
                StageProgressManager.Instance?.RecordClearedWave(CurrentStageData.stageIndex, CurrentWaveIndex);

            if (CurrentStageData != null && CurrentWaveIndex >= CurrentStageData.totalWaves)
            {
                RelicManager.Instance?.EndWave();
                ClearStage();
                return;
            }

            RelicManager.Instance?.EndWave();
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

            if (isClear)
            {
                if (progress != null)
                {
                    firstClear = progress.RecordClear(floor, elapsedMs, out newRecord);
                    if (firstClear)
                        unlocked = progress.GetUnlockGrantedByFloor(floor);
                }
            }
            else if (progress != null)
            {
                newRecord = progress.RecordFail(floor, remainingPercent);
            }

            List<PointRewardEntry> rewards = TowerRunManager.BuildClearRewards(floor, firstClear);
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

            int bestStageIndex = StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetHighestUnlockedStageIndex()
                : stageIndex;

            resultDialog.Open(
                isWin,
                stageIndex,
                reachedWaveCount,
                bestStageIndex,
                rewards,
                SceneFlowManager.LoadLobby);
        }

        public void OnApplicationQuit()
        {
            Application.Quit();
        }
    }

}
