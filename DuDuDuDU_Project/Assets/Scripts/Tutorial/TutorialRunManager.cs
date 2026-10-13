using System.Collections.Generic;

namespace OJ.Tutorial
{
    /// <summary>
    /// 이번 전투가 튜토리얼 판인지, 그리고 그 판에서 <b>랜덤을 무엇으로 덮는지</b>를 든다.
    /// (Docs/IngameTutorialSteps.md "튜토리얼 중 강제로 고정해야 하는 것")
    ///
    /// <b>MonoBehaviour 가 아니다.</b> 탑·되돌리기·현상금과 같은 이유로 배틀 스코프가
    /// 코드로 만든다 — 씬에 놓으려면 <c>BattleScene.unity</c> 를 편집해야 한다(AGENTS 절대 규칙 3).
    ///
    /// <b>덮는 쪽은 여기서 꺼내 가기만 한다.</b> 소환·머지는 "튜토리얼이면 이렇게" 를 스스로
    /// 판단하지 않고, 여기에 남은 것이 있으면 그것을 쓴다. 그래서 대본이 바닥나면
    /// 그 자리부터 평소 규칙으로 돌아가고, 튜토리얼이 손을 놓는 단계(손가락 가이드)가
    /// 저절로 평소 게임이 된다.
    /// </summary>
    public sealed class TutorialRunManager
    {
        /// <summary>대본에 적힌 소환 한 번.</summary>
        public readonly struct ScriptedSummon
        {
            public ScriptedSummon(DiceType type, int slotIndex)
            {
                Type = type;
                SlotIndex = slotIndex;
            }

            public DiceType Type { get; }

            /// <summary>놓을 칸. 딤드 구멍이 이 칸의 좌표를 알아야 해서 랜덤에 맡기지 않는다.</summary>
            public int SlotIndex { get; }
        }

        private readonly Queue<ScriptedSummon> scriptedSummons = new Queue<ScriptedSummon>();

        private bool hasForcedMergeType;
        private DiceType forcedMergeType;

        /// <summary>이번 전투가 튜토리얼 판인가.</summary>
        public bool IsActive { get; private set; }

        /// <summary>보스까지 잡았다. <c>GameManager.ClearStage</c> 의 튜토리얼 갈래가 세운다.</summary>
        public bool IsCleared { get; private set; }

        /// <summary>벽이 무너졌다. <c>GameManager.GameOver</c> 의 튜토리얼 갈래가 세운다.</summary>
        public bool IsFailed { get; private set; }

        /// <summary>
        /// 되돌리기 버튼을 보여도 되는가. 되돌리기를 가르치기 전에 버튼이 떠 있으면
        /// 1웨이브에서 눌러 버려 판에 하나뿐인 무료 되돌리기를 미리 써 버린다.
        /// 튜토리얼 판이 아니면 언제나 참이다.
        /// </summary>
        public bool IsRewindButtonAllowed => !IsActive || rewindUnlocked;

        private bool rewindUnlocked;

        /// <summary>되돌리기 단계에 들어왔다. 이 뒤로는 평소처럼 버튼이 뜬다.</summary>
        public void UnlockRewind()
        {
            rewindUnlocked = true;
        }

        /// <summary>아직 쓰지 않은 대본 소환 수.</summary>
        public int PendingScriptedSummons => scriptedSummons.Count;

        public void Begin()
        {
            Clear();
            IsActive = true;
        }

        public void Clear()
        {
            IsActive = false;
            IsCleared = false;
            IsFailed = false;
            rewindUnlocked = false;
            scriptedSummons.Clear();
            hasForcedMergeType = false;
        }

        // ── 대본 ──────────────────────────────────────────────────────

        public void ScriptSummons(IReadOnlyList<ScriptedSummon> summons)
        {
            for (int i = 0; i < summons.Count; i++)
                scriptedSummons.Enqueue(summons[i]);
        }

        /// <summary>
        /// 다음 소환을 대본에서 꺼낸다. 남은 것이 없으면 거짓 — 평소처럼 굴린다.
        /// <b>튜토리얼 판이 아니면 언제나 거짓이다</b>(대본을 넣는 곳이 튜토리얼뿐이다).
        /// </summary>
        public bool TryTakeScriptedSummon(out ScriptedSummon summon)
        {
            if (IsActive && scriptedSummons.Count > 0)
            {
                summon = scriptedSummons.Dequeue();
                return true;
            }

            summon = default;
            return false;
        }

        /// <summary>다음 머지 한 번의 결과를 정한다. 한 번 쓰면 사라진다.</summary>
        public void ForceNextMergeType(DiceType type)
        {
            forcedMergeType = type;
            hasForcedMergeType = true;
        }

        public bool TryTakeForcedMergeType(out DiceType type)
        {
            if (IsActive && hasForcedMergeType)
            {
                hasForcedMergeType = false;
                type = forcedMergeType;
                return true;
            }

            type = default;
            return false;
        }

        // ── 판 끝 ─────────────────────────────────────────────────────

        public void NotifyCleared()
        {
            if (IsActive)
                IsCleared = true;
        }

        public void NotifyFailed()
        {
            if (IsActive)
                IsFailed = true;
        }
    }
}
