using System;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Hunting;
using OJ.Stage;
using OJ.Battle;
using OJ.Element;
using OJ.UI;

namespace OJ.Economy
{
    /// <summary>
    /// 영혼 보너스. 적을 처치하면 영혼이 저금통으로 날아가 쌓이고, <b>목표 마릿수</b>(<see cref="SoulBonusRules"/>)에 닿으면
    /// 저금통이 <b>받을 수 있는 상태</b>가 된다. <b>유저가 저금통을 눌러</b> 팝업을 열고 SP 또는 마석 중 하나를 고른다.
    /// <b>게임은 멈추지 않는다</b> — 팝업을 닫아 두고 나중에 받아도 된다(오퍼는 고정이라 닫았다 열어도 같은 값이다).
    /// <b>MonoBehaviour 가 아니다</b>(<c>BountyManager</c> 와 같은 이유 — 씬에 놓을 이유가 없다).
    ///
    /// <b>데이터와 화면을 가른다.</b> 처치한 마릿수(<c>RunState.SoulKills</c>)는 죽는 순간 오르고 되돌리기 스냅샷이 같이 되돌린다.
    /// 화면의 n/n 은 <b>도착한 영혼 수</b>(<see cref="DisplayKills"/>)라 날아오는 동안은 아직 안 센다 — 쌓여가는 것이 보이게 하려는 것이다.
    /// 받을 수 있는 것은 <b>둘 다 목표를 채웠을 때</b>다(영혼이 아직 날아오는 중이면 아직 모으는 중이다).
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

        /// <summary>팝업이 떠 있는가.</summary>
        public bool IsPopupOpen { get; private set; }

        /// <summary>지금까지 보상을 <b>고른</b> 횟수. 웨이브 테마가 "이번 제안에서 골랐는가" 를 이 값의 증가로 안다(창을 그냥 닫으면 안 오른다).</summary>
        public int ChoiceCount { get; private set; }

        /// <summary>
        /// 처치 수와 상관없이 보상 선택 창을 지금 띄운다(웨이브 테마: 웨이브가 끝날 때마다 제안한다). 이미 떠 있으면 거절.
        /// 값은 처음 한 번 굴려 고정되므로 닫았다 다시 열어도 같다.
        /// </summary>
        public bool OfferNow()
        {
            if (IsPopupOpen || battle == null || battle.Game == null)
                return false;

            OpenPopup();
            return true;
        }

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

        /// <summary>목표를 채워 지금 받을 수 있는가(데이터·화면 모두). 버튼이 "받기!" 를 그리는 기준이다.</summary>
        public bool IsReady
        {
            get
            {
                if (!Enabled || battle.Game.inGameState != InGameState.Wave && battle.Game.inGameState != InGameState.Setting)
                    return false;

                int goal = Goal;
                return Run.SoulKills >= goal && DisplayKills >= goal;
            }
        }

        /// <summary>
        /// 유저가 저금통을 눌렀다. 받을 수 있으면 보상을 한 번 굴려 고정하고 팝업을 연다.
        /// <b>게임을 멈추지 않는다.</b> 이미 열려 있거나 아직 목표를 못 채웠으면 거절한다.
        /// </summary>
        public bool TryOpen()
        {
            if (IsPopupOpen || !IsReady)
                return false;

            OpenPopup();
            return true;
        }

        // 재화가 날아가는 연출. 현상금 보상(BountyManager)과 같은 도착점·같은 모양이다 —
        // SP 는 보유 SP 칸으로, 마석은 보유 마석 칸으로 간다(둘 다 울타리 위 한 줄). 값은 이미 지급됐고
        // 이 연출은 보기만 한다 — 먼저 HoldSp/HoldStone 으로 쥐어 두면 숫자가 닿을 때 오른다.
        private const float StoneIconScale = 1.9f;

        /// <summary>
        /// SP 가 화면 좌표에서 보유 SP 칸으로 날아간다(먼저 <c>HoldSp</c> 으로 쥐어 두어야 숫자가 닿을 때 오른다).
        /// 웨이브 이벤트(WaveThemeFlow)도 같은 연출을 쓴다.
        /// </summary>
        public void FlySummonPoint(Vector2 fromScreen, int amount)
        {
            var summon = battle.Summon;
            if (summon == null)
                return;

            UIRewardFly.Fly(fromScreen, BattlePointUtility.GetIcon(BattlePointType.SummonPoint),
                summon.SpRect, amount, "+" + amount + " SP", summon.spText,
                new Color(0.55f, 0.9f, 1f, 1f), summon.ReleaseSp);
        }

        /// <summary>마석이 화면 좌표에서 보유 마석 칸으로 날아간다(먼저 <c>HoldStone</c> 으로 쥐어 두어야 숫자가 닿을 때 오른다).</summary>
        public void FlyEnhanceStone(Vector2 fromScreen, int amount)
        {
            ElementUpgradeManager upgrade = battle.ElementUpgrade;
            if (upgrade == null)
                return;

            UIRewardFly.Fly(fromScreen, BattlePointUtility.GetIcon(BattlePointType.EnhanceStone),
                upgrade.StoneRect, amount, "+" + amount, battle.Summon != null ? battle.Summon.spText : null,
                new Color(0.75f, 0.8f, 1f, 1f), upgrade.ReleaseStone, StoneIconScale);
        }

        /// <summary>팝업이 닫혔다(고르지 않고 닫은 경우 포함). 팝업이 알린다.</summary>
        public void NotifyDialogClosed()
        {
            IsPopupOpen = false;
        }

        private void OpenPopup()
        {
            // 보상을 한 번 굴려 고정한다. 이미 굴려져 있으면(닫았다 다시 열었을 때) 그대로 쓴다 — 좋은 값을 노려 열고 닫는 꼼수를 막는다.
            if (Run.SoulOfferStones <= 0 || Run.SoulOfferSp <= 0)
            {
                StageData stage = battle.Game.CurrentStageData;
                int total = stage != null
                    ? SoulBonusRules.StageSpTotal(stage.initialSP, stage.waveClearSP, stage.totalWaves)
                    : 0;

                Run.SoulOfferSp = SoulBonusRules.RollSp(
                    total, UnityEngine.Random.Range(0, 1000), battle.Game.WaveSpMultiplierPercent(battle.Game.CurrentWaveIndex)) / 2;
                Run.SoulOfferStones = SoulBonusRules.RollStones(UnityEngine.Random.Range(0, 1000));
            }

            IsPopupOpen = true;
            GameContainer.UI?.Show<UISoulBonusDialog>();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 보상을 고른다(true = SP, false = 마석). <b>지급 → 목표 이월 → 다음 목표 → 팝업 닫기</b> 순서다.
        /// 한 번에 목표를 두 번 넘긴 경우 남은 마릿수가 이월되어 저금통이 곧바로 다시 받을 수 있는 상태가 된다.
        /// </summary>
        /// <param name="from">고른 버튼 위의 아이콘. 재화가 여기서 출발해 날아간다(없으면 화면 가운데).</param>
        public bool Choose(bool sp, RectTransform from = null)
        {
            if (!IsPopupOpen || battle == null || battle.Game == null)
                return false;

            // 창이 닫히면 출발 자리를 잴 수 없으니 지급하기 전에 화면 좌표로 바꿔 둔다.
            Vector2 fromScreen = from != null
                ? UIRewardFly.ScreenPointOf(from)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            if (sp)
            {
                int amount = Run.SoulOfferSp;
                battle.Summon.HoldSp(amount);
                battle.Summon.AddSP(amount);
                FlySummonPoint(fromScreen, amount);
            }
            else
            {
                int amount = Run.SoulOfferStones;
                // 먼저 쥐고 나서 넣는다. 데이터는 지금 오르지만 화면 숫자는 아이콘이 닿을 때 오른다(현상금과 같은 연출).
                battle.ElementUpgrade?.HoldStone(amount);
                battle.BattlePoints?.Add(BattlePointType.EnhanceStone, amount);
                FlyEnhanceStone(fromScreen, amount);
            }

            int goal = Goal;
            Run.SoulKills = Mathf.Max(0, Run.SoulKills - goal);
            displayKills = Mathf.Max(0, Mathf.Min(displayKills - goal, Run.SoulKills));
            Run.SoulGoalIndex++;
            Run.SoulOfferSp = 0;
            Run.SoulOfferStones = 0;

            IsPopupOpen = false;
            ChoiceCount++;
            GameContainer.UI?.Hide<UISoulBonusDialog>();

            OnChanged?.Invoke();
            return true;
        }

    }
}
