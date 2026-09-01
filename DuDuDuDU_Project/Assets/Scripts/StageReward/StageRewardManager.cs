using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.Point;
using OJ.Save;
using OJ.Stage;
using OJ.Utils;

namespace OJ.StageReward
{
    /// <summary>
    /// 스테이지 누적 보상. (MIGRATION_BASELINE 8.3a)
    ///
    /// <c>StageProgressManager</c> 를 생성자로 받는다. 예전에는 <c>Awake</c> 와 <c>Start</c>
    /// 두 곳에서 구독을 시도하고 플래그로 중복을 막았다 — 순서를 믿을 수 없었기 때문이다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class StageRewardManager : ISaveStateOwner, IDisposable
    {
        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// 호출부를 전부 주입으로 옮기면 사라진다.
        /// </summary>
        public static StageRewardManager Instance { get; internal set; }

        public event Action OnChanged;

        /// <summary>
        /// 수령한 마일스톤 id. <b>이 HashSet 이 진행도의 정본이다.</b>
        ///
        /// 필드 초기화라 생성자와 함께 무조건 만들어진다. 세이브 파일이 없는 첫 실행에서는
        /// <see cref="ReadFrom"/> 이 <b>아예 호출되지 않으므로</b>(<c>SaveService.TryLoadAll</c> 이
        /// 파일이 없으면 owners 루프 전에 돌아간다) 여기서 만들어 두지 않으면 신규 설치가
        /// 첫 조회에서 NRE 로 죽는다. 비어 있는 상태가 곧 "아무것도 수령하지 않았다"는 정답이다.
        /// </summary>
        private readonly HashSet<string> claimedRewardIds = new HashSet<string>();
        private readonly StageProgressManager stageProgress;

        public StageRewardManager(StageProgressManager stageProgress)
        {
            this.stageProgress = stageProgress;

            // 7.5 이전에는 여기서 Load() 가 PlayerPrefs 를 읽었다. 이제 로드는 SaveService 가
            // ReadFrom 으로 밀어 넣으므로 생성자는 <b>초기 상태를 세우는 일만</b> 한다.
            // 이 매니저의 초기 상태는 "빈 수령 목록"이고 그건 필드 초기화가 이미 끝내 준다 —
            // 그래서 여기에 따로 옮겨 올 초기화가 없다. 세이브가 있으면 ReadFrom 이 그 위를 덮는다.

            // 예전에는 이 구독이 Awake 와 Start 두 곳에서 시도됐고 isStageProgressSubscribed
            // 플래그로 중복을 막고 있었다. <b>Awake 순서를 믿을 수 없다는 자백</b>이다 —
            // StageProgressManager 가 아직 없으면 구독이 조용히 no-op 이 되어 보상 해금이
            // 통째로 죽는데, 그게 아무 로그 없이 일어났다.
            //
            // 생성자 주입은 그 문제를 없앤다. stageProgress 가 null 일 수 없고(없으면 컨테이너가
            // 앱 시작 시점에 터진다), 여기 한 번만 구독하면 된다. 플래그도 Start 도 사라졌다.
            stageProgress.OnProgressChanged += HandleStageProgressChanged;
        }

        /// <summary>컨테이너가 파기될 때 VContainer 가 부른다.</summary>
        public void Dispose()
        {
            stageProgress.OnProgressChanged -= HandleStageProgressChanged;
        }

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        public void WriteTo(OJ.Core.SaveState state)
        {
            // 수령 id 를 하나도 거르지 않고 그대로 내보낸다. 여기서 무엇이든 걸러 내면 파일에
            // 빠진 id 가 생기고, 다음 실행에 그 마일스톤이 <b>다시 수령 가능</b>해진다 —
            // 같은 보상이 무한히 나오는 사고이고 로그에는 아무것도 남지 않는다.
            //
            // 대입이 아니라 Clear() 후 채우는 이유는 이 목록이 get-only 라서다.
            state.Stage.ClaimedRewardIds.Clear();
            foreach (string rewardId in claimedRewardIds)
                state.Stage.ClaimedRewardIds.Add(rewardId);
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            claimedRewardIds.Clear();

            // 공백 id 를 걸러 낸다. StableId 를 아직 못 채운 마일스톤이 있으면 빈 문자열끼리 맞아떨어져
            // 수령한 적 없는 보상이 영구히 수령 처리된다 — 되돌릴 방법이 없다.
            for (int i = 0; i < state.Stage.ClaimedRewardIds.Count; i++)
            {
                string rewardId = state.Stage.ClaimedRewardIds[i];
                if (!string.IsNullOrWhiteSpace(rewardId))
                    claimedRewardIds.Add(rewardId);
            }

            // OnChanged 를 부르지 않는다. 로드는 초기화라 아직 구독자가 없고, 있더라도
            // 세이브 반영 도중의 상태를 UI 가 먼저 보게 된다.
            //
            // Save() 도 부르지 않는다. 로드 도중에 파일을 쓰면 아직 ReadFrom 을 받지 못한
            // 다른 매니저의 몫이 기본값인 채로 파일에 굳는다 — 남의 진행도를 지우는 짓이다.
        }

        public IReadOnlyList<StageRewardMilestone> GetMilestones()
        {
            return StageRewardDatabaseProvider.GetDatabase().Milestones;
        }

        public bool HasClaimableReward()
        {
            IReadOnlyList<StageRewardMilestone> milestones = GetMilestones();
            for (int i = 0; i < milestones.Count; i++)
            {
                if (GetState(milestones[i]) == StageRewardState.Claimable)
                    return true;
            }

            return false;
        }

        public StageRewardState GetState(StageRewardMilestone milestone)
        {
            if (milestone == null)
                return StageRewardState.Locked;

            if (IsClaimed(milestone))
                return StageRewardState.Claimed;

            return IsUnlocked(milestone) ? StageRewardState.Claimable : StageRewardState.Locked;
        }

        public bool IsClaimed(StageRewardMilestone milestone)
        {
            return milestone != null && claimedRewardIds.Contains(milestone.StableId);
        }

        public bool IsUnlocked(StageRewardMilestone milestone)
        {
            if (milestone == null)
                return false;

            return stageProgress.HasClearedWave(
                milestone.requiredStageIndex,
                milestone.requiredWaveIndex);
        }

        public int GetFocusIndex()
        {
            IReadOnlyList<StageRewardMilestone> milestones = GetMilestones();
            if (milestones == null || milestones.Count == 0)
                return -1;

            for (int i = 0; i < milestones.Count; i++)
            {
                if (GetState(milestones[i]) == StageRewardState.Claimable)
                    return i;
            }

            for (int i = 0; i < milestones.Count; i++)
            {
                if (GetState(milestones[i]) != StageRewardState.Claimed)
                    return i;
            }

            return milestones.Count - 1;
        }

        public int GetProgressIndex()
        {
            IReadOnlyList<StageRewardMilestone> milestones = GetMilestones();
            if (milestones == null || milestones.Count == 0)
                return 0;

            int focusIndex = GetFocusIndex();
            if (focusIndex < 0)
                return 0;

            return Mathf.Clamp(focusIndex + 1, 1, milestones.Count);
        }

        public int GetTotalCount()
        {
            IReadOnlyList<StageRewardMilestone> milestones = GetMilestones();
            return milestones != null ? milestones.Count : 0;
        }

        public bool TryClaim(StageRewardMilestone milestone, out List<PointRewardEntry> grantedRewards)
        {
            grantedRewards = new List<PointRewardEntry>();

            if (GetState(milestone) != StageRewardState.Claimable)
                return false;

            if (milestone.rewards != null)
            {
                for (int i = 0; i < milestone.rewards.Count; i++)
                {
                    StageRewardEntry reward = milestone.rewards[i];
                    if (reward.amount <= 0)
                        continue;

                    grantedRewards.Add(reward.ToPointRewardEntry());
                }
            }

            claimedRewardIds.Add(milestone.StableId);
            PointRewardUtility.GrantRewards(grantedRewards);

            // 지급 직후 <b>즉시</b> 저장한다. 이 호출을 앱 종료 시점으로 미루면 재화만 들어오고
            // "수령했다"는 기록이 없는 채로 OS 가 프로세스를 죽일 수 있고, 그러면 같은 보상을
            // 다시 받게 된다. 모바일에서 백그라운드 프로세스가 죽는 것은 사고가 아니라 일상이다.
            Save();
            OnChanged?.Invoke();
            return true;
        }

        private void HandleStageProgressChanged()
        {
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 지금 상태를 통합 세이브 파일에 즉시 쓴다.
        ///
        /// 7.5: PlayerPrefs 대신 통합 세이브를 쓴다. <b>호출 지점은 그대로 두는 것이 중요하다</b> —
        /// 여기서 즉시 저장하지 않으면 앱이 백그라운드로 갈 때까지 진행도가 메모리에만 남고,
        /// 모바일에서 OS 가 프로세스를 죽이는 것은 일상이다.
        ///
        /// <c>?.</c> 가 필요하다. 매니저 <b>생성자가 도는 시점에는 SaveService 가 아직 없다</b> —
        /// 컨테이너가 매니저를 만든 뒤에 SaveService 를 해석하기 때문이다. 그 시점에 저장이
        /// 간접적으로 불려 오면 조용히 건너뛰는 것이 맞다(아직 쓸 것도 없다).
        /// </summary>
        private void Save() => OJ.DI.GameContainer.SaveService?.SaveAll();
    }
}
