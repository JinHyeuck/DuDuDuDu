using System;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.Stage;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금 시스템의 두뇌. <b>MonoBehaviour 가 아니다.</b>
    ///
    /// <b>왜 씬 컴포넌트가 아닌가.</b> 이 매니저가 만지는 것은 <see cref="RunState"/> 의
    /// 필드 넷과 <see cref="BountyDatabase"/> 뿐이라 씬에 놓을 이유가 없고,
    /// 놓으려면 <c>BattleScene.unity</c> 를 편집해야 하는데 씬 YAML 편집은 이 프로젝트의
    /// 절대 규칙이 금지한다(AGENTS 3). 순수 객체로 두면 배틀 스코프가 코드로 만든다.
    ///
    /// <b>상태를 자기 필드에 들지 않는다.</b> 선택 등급·해금 등급·보류 보상은 전부
    /// <c>RunState</c> 가 소유한다. 여기 따로 들면 <c>BeginRun</c> 이 리셋하지 못하는
    /// 사본이 생기고, 그것이 곧 "다음 판에 이전 값이 새어 나가는" 사고다 —
    /// <c>RunState</c> 가 존재하는 이유 그 자체다.
    ///
    /// <b>웨이브 안에서만 사는 것은 여기 있다.</b> 이번 웨이브에 실제로 나온 등급과
    /// 그 개체가 정리됐는지는 웨이브가 끝나면 의미가 없으므로 런에 올리지 않는다.
    /// </summary>
    [Preserve]
    public sealed class BountyManager
    {
        private readonly IBattleRefs battle;

        /// <summary>
        /// 선택·해금이 바뀌었을 때 운다. 배너와 선택 창이 듣는다.
        /// 폴링을 쓰지 않는 이유는 관리 단계가 매 프레임 도는 화면이기 때문이다.
        /// </summary>
        public event Action OnChanged;

        /// <summary>
        /// 이번 웨이브의 현상금이 정리된 순간 운다(죽었거나 벽을 때리고 물러났거나).
        ///
        /// <b>왜 이벤트가 필요한가.</b> 웨이브 종료 판정은 지금까지 "마지막 몬스터가
        /// 죽었을 때" 한 곳에서만 돌았다. 현상금이 조건에 끼면 <b>일반 몬스터를 먼저 다
        /// 잡고 현상금이 나중에 정리되는 순서</b>가 생기는데, 그때는 아무도 판정을
        /// 다시 부르지 않아 웨이브가 멈춘 채로 남는다. 그 두 번째 계기가 이 이벤트다.
        /// </summary>
        public event Action OnWaveResolved;

        /// <summary>이번 웨이브에 실제로 나온(또는 나올) 등급. 0 이면 이번 웨이브엔 없다.</summary>
        public int ActiveGrade { get; private set; }

        private Monster activeMonster;
        private bool spawnedThisWave;
        private bool resolvedThisWave = true;

        public BountyManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        private RunState Run => battle.Game.Run;

        // ──────────────────────────────────────────────────────────────
        // 선택 (관리 단계)
        // ──────────────────────────────────────────────────────────────

        /// <summary>이번 판에 켜 둔 등급. 0 이면 "소환 X".</summary>
        public int SelectedGrade => Run.SelectedBountyGrade;

        /// <summary>이번 판에 잡아낸 최고 등급. 해금의 유일한 근거다.</summary>
        public int HighestDefeatedGrade => Run.HighestDefeatedBountyGrade;

        public bool IsSelectable(int grade)
        {
            return BountyFormula.IsSelectable(grade, HighestDefeatedGrade);
        }

        /// <summary>
        /// 등급을 켠다. <b>고를 수 없는 등급은 조용히 무시하지 않고 거절을 알린다</b> —
        /// 버튼이 안 먹는 것처럼 보이는 것이 가장 나쁜 실패다. UI 는 잠긴 칸을
        /// 눌리지 않게 그리므로 여기 걸리면 그것 자체가 배선 사고다.
        /// </summary>
        public bool Select(int grade)
        {
            if (!IsSelectable(grade))
            {
                Debug.LogWarning("[현상금] 아직 못 고르는 등급이다: " + grade +
                                 " (해금 상한 " + BountyFormula.HighestSelectableGrade(HighestDefeatedGrade) + ")");
                return false;
            }

            if (Run.SelectedBountyGrade == grade)
                return true;

            Run.SelectedBountyGrade = grade;
            OnChanged?.Invoke();
            return true;
        }

        public BountyDefinition GetDefinition(int grade)
        {
            if (grade == BountyFormula.NoneGrade)
                return null;

            return BountyDatabaseProvider.Get(grade);
        }

        /// <summary>
        /// 이 등급이 지금 스테이지에서 갖게 될 체력. 선택 창이 카드에 그대로 띄운다.
        ///
        /// <b>스테이지 데이터가 없으면 0 을 돌린다.</b> 임의의 숫자를 만들어 내면
        /// 화면에는 그럴듯한 값이 뜨는데 실제로 나오는 몬스터와 다르다.
        /// </summary>
        public int GetHp(int grade)
        {
            BountyDefinition definition = GetDefinition(grade);
            StageData stage = battle.Game.CurrentStageData;
            if (definition == null || stage == null)
                return 0;

            int referenceWave = BountyFormula.ReferenceWave(stage.totalWaves, definition.referenceWaveRatio);
            return BountyFormula.Hp(stage.GetMonsterHpForWave(referenceWave), definition.hpMultiplier);
        }

        // ──────────────────────────────────────────────────────────────
        // 웨이브 수명
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 웨이브가 시작될 때 <see cref="GameManager.ChangeState"/> 가 부른다.
        /// 이번 웨이브에 나올 등급을 <b>여기서 한 번</b> 확정한다 — 웨이브 도중에 선택이
        /// 바뀌어도 이미 나온 몬스터가 바뀌지 않게 하려는 것이다. 지금은 관리 단계에서만
        /// 바꿀 수 있어 일어나지 않지만, 그 불변식을 코드가 들고 있어야 나중에 안 깨진다.
        /// </summary>
        public void BeginWave(int waveIndex)
        {
            activeMonster = null;
            spawnedThisWave = false;

            StageData stage = battle.Game.CurrentStageData;
            int totalWaves = stage != null ? stage.totalWaves : 1;

            ActiveGrade = BountyFormula.CanSpawnOnWave(waveIndex, totalWaves)
                ? SelectedGrade
                : BountyFormula.NoneGrade;

            // 나올 것이 없으면 처음부터 "정리됨" 이다. 이 초기값이 틀리면
            // 현상금을 안 켠 판에서 웨이브가 영영 안 끝난다.
            resolvedThisWave = ActiveGrade == BountyFormula.NoneGrade;
        }

        /// <summary>이번 웨이브에 현상금을 아직 안 내보냈는가. 스포너가 묻는다.</summary>
        public bool ShouldSpawn => ActiveGrade != BountyFormula.NoneGrade && !spawnedThisWave;

        public void NotifySpawned(Monster monster)
        {
            activeMonster = monster;
            spawnedThisWave = true;
            resolvedThisWave = false;
        }

        /// <summary>
        /// 현상금이 정리됐는가 — 죽었거나, 벽을 때리고 사라졌거나, 애초에 없었거나.
        ///
        /// <b>웨이브 종료 조건의 절반이다.</b> 일반 몬스터를 다 잡아도 이것이 false 면
        /// 웨이브는 끝나지 않는다. 반대로 이 값이 영영 false 로 남으면 판이 멈추므로,
        /// 현상금은 이동 제약에 면역이어서 <b>반드시 벽에 닿는다</b> — 그 면역은 연출이
        /// 아니라 이 데드락을 막는 안전장치다(<c>Monster.IsBounty</c> 참조).
        /// </summary>
        public bool IsWaveResolved => resolvedThisWave;

        /// <summary>
        /// 잡았다. 보상은 <b>지금 주지 않고</b> 웨이브 끝에 준다 — 전투 중에 SP 가
        /// 들어오면 그 자리에서 소환이 되어 "관리 단계에 쓰라"는 뜻이 무너진다.
        /// </summary>
        public void NotifyDefeated(Monster monster)
        {
            if (monster == null || monster != activeMonster)
                return;

            int grade = ActiveGrade;
            BountyDefinition definition = GetDefinition(grade);
            activeMonster = null;
            resolvedThisWave = true;

            // 정의가 없어도 <b>정리됐다는 사실은 반드시 알린다.</b> 여기서 그냥 돌아가면
            // 웨이브 종료 판정을 다시 부를 계기가 사라져 판이 멈춘다 —
            // 데이터 사고가 "게임이 안 넘어감" 으로 번지는 자리다.
            if (definition == null)
            {
                Debug.LogError("[현상금] 등급 " + grade + " 의 정의가 없어 보상을 주지 못했다.");
                OnChanged?.Invoke();
                OnWaveResolved?.Invoke();
                return;
            }

            switch (definition.rewardKind)
            {
                case BountyRewardKind.SummonPoint:
                    Run.PendingBountySummonPoint += definition.rewardAmount;
                    break;
                case BountyRewardKind.EnhanceStone:
                    Run.PendingBountyEnhanceStone += definition.rewardAmount;
                    break;
            }

            if (grade > Run.HighestDefeatedBountyGrade)
            {
                Run.HighestDefeatedBountyGrade = grade;

                // 다음 등급이 열렸으니 선택도 그리로 올려 준다. 안 그러면 잡을 때마다
                // 선택 창을 열어 한 칸 오른쪽을 눌러야 하고, 그 반복은 선택이 아니라 잡일이다.
                // 켜 둔 것을 함부로 바꾸는 것이 아니다 — 방금 그 등급을 잡아냈을 때만,
                // 그리고 위로만 움직인다.
                if (Run.SelectedBountyGrade == grade
                    && BountyFormula.IsSelectable(grade + 1, Run.HighestDefeatedBountyGrade))
                {
                    Run.SelectedBountyGrade = grade + 1;
                }
            }

            OnChanged?.Invoke();
            OnWaveResolved?.Invoke();
        }

        /// <summary>벽을 한 대 때리고 사라졌다. 보상은 없다.</summary>
        public void NotifyEscaped(Monster monster)
        {
            if (monster == null || monster != activeMonster)
                return;

            activeMonster = null;
            resolvedThisWave = true;
            OnChanged?.Invoke();
            OnWaveResolved?.Invoke();
        }

        /// <summary>
        /// 웨이브가 끝날 때 보류 보상을 실제로 지급한다.
        /// <see cref="GameManager"/> 의 웨이브 클리어 처리에서 딱 한 번 불린다.
        /// </summary>
        public void GrantPendingRewards()
        {
            int summonPoint = Run.PendingBountySummonPoint;
            int enhanceStone = Run.PendingBountyEnhanceStone;

            Run.PendingBountySummonPoint = 0;
            Run.PendingBountyEnhanceStone = 0;

            if (summonPoint > 0)
                battle.Summon.AddSP(summonPoint);

            if (enhanceStone > 0)
                PointManager.Instance?.Add(PointType.BattleEnhanceStone, enhanceStone);

            if (summonPoint > 0 || enhanceStone > 0)
                Debug.Log("[현상금] 보상 지급 — SP " + summonPoint + " / 강화석 " + enhanceStone);
        }

        /// <summary>
        /// 웨이브 상태만 되돌린다. 런 상태는 <c>RunState.BeginRun</c> 이 지운다 —
        /// 두 곳에서 지우면 어느 쪽이 정본인지 알 수 없게 된다.
        /// </summary>
        public void ResetWaveState()
        {
            ActiveGrade = BountyFormula.NoneGrade;
            activeMonster = null;
            spawnedThisWave = false;
            resolvedThisWave = true;
        }
    }
}
