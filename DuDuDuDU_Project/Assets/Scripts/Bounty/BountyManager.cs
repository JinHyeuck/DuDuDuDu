using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.Battle;
using OJ.DI;
using OJ.Hunting;
using OJ.Mission;
using OJ.Point;
using OJ.Stage;

namespace OJ.Bounty
{
    /// <summary>현상금 버튼의 상태. Open 은 선택 창이 열려 있는 동안 UI 가 덧입히는 것이라 매니저는 Ready/Already 만 안다.</summary>
    public enum BountyButtonState
    {
        /// <summary>쿨타임이 끝났다 — 눌러서 고를 수 있다(인터랙션을 건다).</summary>
        Ready = 0,

        /// <summary>선택 창이 열려 있다.</summary>
        Open = 1,

        /// <summary>쿨타임 중이다 — 남은 시간을 보여 준다.</summary>
        Already = 2,
    }

    /// <summary>
    /// 현상금 시스템의 두뇌. <b>MonoBehaviour 가 아니다.</b>
    ///
    /// <b>실시간 현상금.</b> 웨이브마다 등급을 고르던 방식을 걷고, 언제든 아이콘을 눌러 등급을 고르면
    /// <b>그 자리에서 소환</b>한다. 소환한 뒤에는 <see cref="BountyFormula.CooldownSeconds"/> 초 동안 다시
    /// 못 부르고, 쿨타임이 끝나면 Ready 가 되어 유저에게 다시 고르라고 알린다.
    ///
    /// <b>웨이브와 분리돼 있다.</b> 현상금은 웨이브 목표 수에 들지 않고 웨이브 종료 조건에도 끼지 않는다
    /// (<c>Monster</c> 가 현상금을 <c>UnregisterMonster(…, false)</c> 로 센다). 보상은 <b>잡는 즉시</b> 들어온다 —
    /// 예전에는 "관리 단계에 쓰라고" 웨이브 끝까지 쥐고 있었지만, 관리 단계가 없어졌다.
    ///
    /// <b>상태를 자기 필드에 들지 않는다.</b> 쿨타임·최고 처치 등급은 <c>RunState</c> 가 소유한다.
    /// 여기 따로 들면 <c>BeginRun</c> 이 리셋하지 못하는 사본이 생기고, 그것이 곧 "다음 판에 이전 값이
    /// 새어 나가는" 사고다. 이 클래스가 들고 있는 것은 <b>지금 화면에 살아 있는 개체</b> 뿐이다.
    /// </summary>
    [Preserve]
    public sealed class BountyManager
    {
        private readonly IBattleRefs battle;

        /// <summary>쿨타임·해금·소환이 바뀌었을 때 운다. 버튼과 선택 창이 듣는다.</summary>
        public event Action OnChanged;

        /// <summary>
        /// 현상금이 화면에 나온 순간 운다. 경고 띠(<c>UIBattleWarning</c>)가 이걸 듣는다.
        ///
        /// 현상금은 일반 몬스터가 쏟아지는 틈에 섞여 나온다. 느린 걸음과 큰 덩치로 구분은 되지만
        /// <b>나온 순간</b>은 놓치기 쉬워서 한 번 짚어 준다.
        /// </summary>
        public event Action<BountyDefinition> OnSpawned;

        /// <summary>쿨타임이 끝난 순간 운다(Ready 가 되었다). 버튼이 인터랙션 연출을 건다.</summary>
        public event Action OnReady;

        // 지금 살아 있는 현상금 → 등급. 잡았을 때 어느 등급인지 알려면 개체마다 들고 있어야 한다
        // (쿨타임 30초 < 처치 시간이면 서로 다른 등급이 동시에 살 수 있다).
        private readonly Dictionary<Monster, int> active = new Dictionary<Monster, int>();

        public BountyManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        private RunState Run => battle.Game.Run;

        // ──────────────────────────────────────────────────────────────
        // 상태
        // ──────────────────────────────────────────────────────────────

        /// <summary>남은 쿨타임(초). 0 이면 준비됨.</summary>
        public float CooldownRemaining => battle != null && battle.Game != null ? Run.BountyCooldown : 0f;

        /// <summary>쿨타임이 끝났는가.</summary>
        public bool IsReady => BountyFormula.IsReady(CooldownRemaining);

        /// <summary>지금 화면에 살아 있는 현상금 수.</summary>
        public int ActiveCount => active.Count;

        /// <summary>이번 판에 잡아낸 최고 등급. 해금의 유일한 근거다.</summary>
        public int HighestDefeatedGrade => Run.HighestDefeatedBountyGrade;

        /// <summary>이 등급을 지금 고를 수 있는가. 1 등급부터이고 앞 등급을 잡아야 다음이 열린다.</summary>
        public bool IsSelectable(int grade)
        {
            return grade >= 1 && BountyFormula.IsSelectable(grade, HighestDefeatedGrade);
        }

        public BountyDefinition GetDefinition(int grade)
        {
            if (grade < 1)
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
        // 소환
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 지금 소환할 수 있는가. <b>웨이브가 진행 중일 때만이다</b> — 몬스터가 내려오는 필드가 열려 있어야 하고,
        /// 판이 끝난 뒤(결과창)나 카운트다운 중에는 부를 이유가 없다. 튜토리얼·탑에는 현상금이 없다.
        /// </summary>
        public bool CanSummonNow
        {
            get
            {
                if (battle == null || battle.Game == null)
                    return false;

                if (battle.Tutorial.IsActive || battle.Tower.IsActive)
                    return false;

                return battle.Game.inGameState == InGameState.Wave && IsReady;
            }
        }

        /// <summary>
        /// 등급을 골라 <b>그 자리에서</b> 소환한다. 성공하면 쿨타임이 시작된다.
        ///
        /// <b>고를 수 없는 등급은 조용히 무시하지 않고 거절을 알린다</b> — 버튼이 안 먹는 것처럼 보이는 것이
        /// 가장 나쁜 실패다. UI 는 잠긴 칸을 눌리지 않게 그리므로 여기 걸리면 그것 자체가 배선 사고다.
        /// </summary>
        public bool TrySummon(int grade)
        {
            if (!CanSummonNow)
                return false;

            if (!IsSelectable(grade))
            {
                Debug.LogWarning("[현상금] 아직 못 고르는 등급이다: " + grade +
                                 " (해금 상한 " + BountyFormula.HighestSelectableGrade(HighestDefeatedGrade) + ")");
                return false;
            }

            BountyDefinition definition = GetDefinition(grade);
            if (definition == null)
            {
                Debug.LogError("[현상금] 등급 " + grade + " 의 정의가 없어 소환하지 않는다.");
                return false;
            }

            Monster monster = battle.Spawner.SpawnBountyNow(grade);
            if (monster == null)
                return false;

            active[monster] = grade;
            Run.BountyCooldown = BountyFormula.CooldownSeconds;

            OnSpawned?.Invoke(definition);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 쿨타임을 흘린다. <c>GameManager.Update</c> 가 웨이브 중에만 부른다 — 일시정지(timeScale 0)에서는
        /// <c>Time.deltaTime</c> 이 0 이라 저절로 멈추고, 배속에서는 그만큼 빨리 흐른다(게임 시간).
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (battle == null || battle.Game == null)
                return;

            float before = Run.BountyCooldown;
            if (before <= 0f)
                return;

            float after = BountyFormula.Tick(before, deltaTime);
            Run.BountyCooldown = after;

            if (after <= 0f)
            {
                OnChanged?.Invoke();
                OnReady?.Invoke();
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 결과
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 잡았다. <b>보상은 그 자리에서 준다.</b> 한 등급을 잡으면 다음 등급이 열린다.
        /// 죽어서 사라진 경우에는 이 호출이 개체를 비워 두므로 뒤따르는 <see cref="NotifyEscaped"/> 는
        /// 그냥 지나간다 — 보상이 두 번 서지 않는다.
        /// </summary>
        public void NotifyDefeated(Monster monster)
        {
            if (monster == null || !active.TryGetValue(monster, out int grade))
                return;

            active.Remove(monster);
            BountyDefinition definition = GetDefinition(grade);

            // <b>정의가 없어도 센다.</b> 아래에서 보상 없이 돌아가는 갈래가 있는데, 그건 데이터 사고이지
            // "안 잡은 것" 이 아니다 — 유저는 분명히 잡았다.
            MissionManager.Instance?.Notify(MissionAction.BountyKill);

            if (definition == null)
            {
                Debug.LogError("[현상금] 등급 " + grade + " 의 정의가 없어 보상을 주지 못했다.");
                OnChanged?.Invoke();
                return;
            }

            switch (definition.rewardKind)
            {
                case BattlePointType.SummonPoint:
                    battle.Summon.AddSP(definition.rewardAmount);
                    break;
                case BattlePointType.EnhanceStone:
                    battle.BattlePoints?.Add(BattlePointType.EnhanceStone, definition.rewardAmount);
                    break;
            }

            if (grade > Run.HighestDefeatedBountyGrade)
                Run.HighestDefeatedBountyGrade = grade;

            OnChanged?.Invoke();
        }

        /// <summary>벽을 한 대 때리고 사라졌다(또는 어떤 경로로든 사라졌다). 보상은 없다.</summary>
        public void NotifyEscaped(Monster monster)
        {
            if (monster == null || !active.Remove(monster))
                return;

            OnChanged?.Invoke();
        }

        /// <summary>
        /// 살아 있는 현상금 추적을 비운다. 판이 끝나거나 되돌릴 때 부른다. 쿨타임·해금은 <c>RunState</c> 가
        /// 소유하므로(되돌리기 스냅샷이 복원한다) 여기서 건드리지 않는다.
        /// </summary>
        public void ResetWaveState()
        {
            active.Clear();
        }
    }
}
