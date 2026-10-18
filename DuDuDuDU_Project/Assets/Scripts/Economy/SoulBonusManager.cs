using System;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Hunting;
using OJ.Stage;

namespace OJ.Economy
{
    /// <summary>
    /// 영혼 보너스. 적을 처치하면 영혼이 저금통으로 날아가 쌓이고, <b>목표 마릿수</b>(<see cref="SoulBonusRules"/>)에 닿으면
    /// 팝업이 저절로 떠서 <b>SP 또는 마석 중 하나를 꼭 고른다</b>. 팝업이 떠 있는 동안 게임은 멈춘다.
    /// <b>MonoBehaviour 가 아니다</b>(<c>BountyManager</c> 와 같은 이유 — 씬에 놓을 이유가 없다).
    ///
    /// <b>데이터와 화면을 가른다.</b> 처치한 마릿수(<c>RunState.SoulKills</c>)는 죽는 순간 오르고 되돌리기 스냅샷이 같이 되돌린다.
    /// 화면의 n/n 은 <b>도착한 영혼 수</b>(<see cref="DisplayKills"/>)라 날아오는 동안은 아직 안 센다 — 쌓여가는 것이 보이게 하려는 것이다.
    /// 팝업은 <b>둘 다 목표를 채웠을 때</b> 뜬다(영혼이 아직 날아오는 중에 팝업이 뜨면 모이는 장면이 잘린다).
    ///
    /// 처치로 세는 것은 <b>웨이브 일반 몬스터의 실제 죽음</b>이다. 현상금(따로 보상이 있다)·벽을 넘어 사라진 것(처치가 아니다)은
    /// 세지 않는다. 탑과 밸런스 봇(<c>LegacySetupFlow</c>)에서는 꺼진다 — 봇이 팝업에 막혀 멈추는 것을 막는다.
    /// </summary>
    [Preserve]
    public sealed class SoulBonusManager
    {
        private readonly IBattleRefs battle;

        /// <summary>목표·진행·오퍼가 바뀌었을 때 운다. 버튼이 듣는다.</summary>
        public event Action OnChanged;

        /// <summary>영혼 하나가 저금통에 닿았다. 버튼이 돼지를 튀긴다.</summary>
        public event Action OnSoulArrived;

        /// <summary>
        /// 영혼을 날리는 쪽(버튼)이 등록한다. 인자는 죽은 자리(월드). <b>등록돼 있지 않으면 바로 도착한 것으로 센다</b> —
        /// 버튼이 아직 안 떴거나(튜토리얼) 사라졌을 때 영혼이 허공에 갇혀 팝업이 영영 안 뜨는 일을 막는다.
        /// </summary>
        public Action<Vector3> FlightRequest { get; set; }

        /// <summary>팝업이 떠 있는가(게임이 멈춰 있다).</summary>
        public bool IsPopupOpen { get; private set; }

        private int displayKills;

        public SoulBonusManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        private RunState Run => battle.Game.Run;

        // ── 상태 ──────────────────────────────────────────────────

        /// <summary>이 판에서 영혼 보너스가 도는가. 실시간 판(튜토리얼 포함)이어야 한다. 탑·밸런스 봇에서는 꺼진다.</summary>
        public bool Enabled =>
            battle != null && battle.Game != null && battle.Tower != null
            && !battle.Tower.IsActive && battle.Game.IsContinuousWave;

        /// <summary>지금 목표 마릿수.</summary>
        public int Goal => battle != null && battle.Game != null ? SoulBonusRules.GoalFor(Run.SoulGoalIndex) : SoulBonusRules.FirstGoal;

        /// <summary>화면에 보이는 도착한 영혼 수(0~목표). 데이터(<see cref="Kills"/>)보다 늦거나 같다.</summary>
        public int DisplayKills => battle != null && battle.Game != null ? Mathf.Min(displayKills, Run.SoulKills) : 0;

        /// <summary>처치한 마릿수(데이터). 목표를 넘으면 이월된다.</summary>
        public int Kills => battle != null && battle.Game != null ? Run.SoulKills : 0;

        /// <summary>팝업에 떠 있는 SP 보상. 팝업이 뜨기 전에는 0.</summary>
        public int OfferSp => battle != null && battle.Game != null ? Run.SoulOfferSp : 0;

        /// <summary>팝업에 떠 있는 마석 보상(개). 팝업이 뜨기 전에는 0.</summary>
        public int OfferStones => battle != null && battle.Game != null ? Run.SoulOfferStones : 0;

        // ── 처치 → 영혼 ───────────────────────────────────────────

        /// <summary>
        /// 몬스터가 <b>죽었다</b>. 마릿수를 올리고 영혼을 날린다. 현상금은 부르는 쪽(<c>Monster</c>)이 걸러서 부르지 않는다.
        /// </summary>
        public void OnMonsterKilled(Vector3 deathPosition)
        {
            if (!Enabled)
                return;

            Run.SoulKills++;

            if (FlightRequest != null)
            {
                FlightRequest(deathPosition);
            }
            else
            {
                // 날릴 곳이 없다 — 바로 도착한 것으로 센다.
                displayKills = Mathf.Min(displayKills + 1, Run.SoulKills);
            }

            OnChanged?.Invoke();
        }

        /// <summary>영혼 하나가 저금통에 닿았다. 날리는 쪽이 부른다.</summary>
        public void NotifySoulArrived()
        {
            if (battle == null || battle.Game == null)
                return;

            displayKills = Mathf.Min(displayKills + 1, Run.SoulKills);
            OnSoulArrived?.Invoke();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 날리던 쪽이 사라질 때 부른다 — 아직 날아오는 중인 영혼을 도착한 것으로 친다.
        /// 안 그러면 화면 수가 데이터를 영영 못 따라가 팝업이 안 뜬다.
        /// </summary>
        public void FlushDisplay()
        {
            if (battle == null || battle.Game == null)
                return;

            displayKills = Run.SoulKills;
            OnChanged?.Invoke();
        }

        // ── 팝업 ──────────────────────────────────────────────────

        /// <summary>
        /// 매 프레임 <c>GameManager.Update</c> 가 부른다. 목표를 채웠고(데이터·화면 모두) 지금 띄워도 되면 팝업을 연다.
        /// <b>시간이 흐르는 동안에만 연다</b>(<c>Time.timeScale &gt; 0</c>) — 튜토리얼이 게임을 세워 둔 동안이나 일시정지 중에 끼어들지 않는다.
        /// </summary>
        public void Tick()
        {
            if (IsPopupOpen || !Enabled)
                return;

            if (battle.Game.inGameState != InGameState.Wave || Run.IsGameOver || Time.timeScale <= 0f)
                return;

            int goal = Goal;
            if (Run.SoulKills < goal || DisplayKills < goal)
                return;

            // 다른 전체 화면 연출이 도는 동안은 기다린다(겹치면 둘 다 입력을 막아 엉킨다).
            if (UILuckyDiceResultPopup.IsPlaying || IsMythicCinematicPlaying())
                return;

            OpenPopup();
        }

        private static bool IsMythicCinematicPlaying()
        {
            UIMythicSummonCinematic cinematic = GameContainer.UI?.Get<UIMythicSummonCinematic>();
            return cinematic != null && cinematic.isEnter;
        }

        private void OpenPopup()
        {
            // 보상을 한 번 굴려 고정한다. 이미 굴려져 있으면(되돌리기 등) 그대로 쓴다.
            if (Run.SoulOfferStones <= 0 || Run.SoulOfferSp <= 0)
            {
                StageData stage = battle.Game.CurrentStageData;
                int total = stage != null
                    ? SoulBonusRules.StageSpTotal(stage.initialSP, stage.waveClearSP, stage.totalWaves)
                    : 0;

                Run.SoulOfferSp = SoulBonusRules.RollSp(total, UnityEngine.Random.Range(0, 1000));
                Run.SoulOfferStones = SoulBonusRules.RollStones(UnityEngine.Random.Range(0, 1000));
            }

            IsPopupOpen = true;

            // 밖의 게임을 멈춘다. 복원은 선택한 뒤 <see cref="Choose"/> 가 한다.
            Time.timeScale = 0f;

            GameContainer.UI?.Show<UISoulBonusDialog>();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 보상을 고른다(true = SP, false = 마석). <b>지급 → 목표 이월 → 다음 목표 → 팝업 닫기 → 게임 재개</b> 순서다.
        /// 한 번에 목표를 두 번 넘긴 경우 남은 마릿수가 이월되어 곧바로 다음 팝업이 이어서 뜬다.
        /// </summary>
        public bool Choose(bool sp)
        {
            if (!IsPopupOpen || battle == null || battle.Game == null)
                return false;

            if (sp)
                battle.Summon.AddSP(Run.SoulOfferSp);
            else
                battle.BattlePoints?.Add(BattlePointType.EnhanceStone, Run.SoulOfferStones);

            int goal = Goal;
            Run.SoulKills = Mathf.Max(0, Run.SoulKills - goal);
            displayKills = Mathf.Max(0, Mathf.Min(displayKills - goal, Run.SoulKills));
            Run.SoulGoalIndex++;
            Run.SoulOfferSp = 0;
            Run.SoulOfferStones = 0;

            IsPopupOpen = false;
            GameContainer.UI?.Hide<UISoulBonusDialog>();

            Time.timeScale = battle.Game.CurrentTimeSpeed;

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 판이 끝나거나 창이 사라질 때 멈춘 시간을 되돌린다. 팝업이 떠 있지 않으면 아무것도 안 한다.
        /// </summary>
        public void ReleasePause()
        {
            if (!IsPopupOpen)
                return;

            IsPopupOpen = false;
            if (battle != null && battle.Game != null)
                Time.timeScale = battle.Game.CurrentTimeSpeed;
        }
    }
}
