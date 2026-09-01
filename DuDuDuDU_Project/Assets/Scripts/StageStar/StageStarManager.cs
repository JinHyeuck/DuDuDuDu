using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.Point;
using OJ.Save;
using OJ.Stage;
using OJ.StageReward;
using OJ.Utils;

namespace OJ.StageStar
{
    /// <summary>
    /// 스테이지 별 수집·별 보상. (MIGRATION_BASELINE 8.3a)
    ///
    /// <c>StageRewardManager</c> 와 같은 이유로 <c>StageProgressManager</c> 를 생성자로 받는다 —
    /// 예전에는 <c>Awake</c>·<c>Start</c> 두 곳에서 구독을 시도했다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class StageStarManager : ISaveStateOwner, IDisposable
    {
        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// 호출부를 전부 주입으로 옮기면 사라진다.
        /// </summary>
        public static StageStarManager Instance { get; internal set; }

        public event Action OnChanged;

        // 수령 기록. 필드 초기화라 생성자 본문보다 먼저, 조건 없이 만들어진다 —
        // 구 PlayerPrefs Load() 안에서 만들어지던 것이 아니라서 그 경로를 지워도 신규 설치가
        // 빈 참조를 만나지 않는다. 세이브 파일이 있으면 ReadFrom 이 이 위에 덮고, 첫 실행처럼
        // 파일이 없으면 ReadFrom 은 <b>아예 불리지 않는다</b>(SaveService.TryLoadAll 이 owners
        // 루프에 들어가기 전에 돌아간다). 그래서 초기 상태를 만드는 책임은 로드 경로 밖에 있어야 한다.
        private readonly HashSet<int> claimedRewardIndices = new HashSet<int>();
        private readonly StageProgressManager stageProgress;

        public StageStarManager(StageProgressManager stageProgress)
        {
            this.stageProgress = stageProgress;
            stageProgress.OnProgressChanged += HandleStageProgressChanged;
        }

        /// <summary>
        /// 컨테이너가 파기될 때 VContainer 가 부른다.
        ///
        /// <b>여기서 저장하지 않는다.</b> 보상 수령 시점에 이미 통합 세이브를 썼고, 앱이 멈출 때는
        /// <see cref="SaveService"/> 가 <c>ISaveOnApplicationLifecycle</c> 로 한 번 더 쓴다.
        /// 파기 시점에 매니저마다 또 쓰면 같은 파일을 매니저 수만큼 중복해서 쓰게 된다.
        /// </summary>
        public void Dispose()
        {
            stageProgress.OnProgressChanged -= HandleStageProgressChanged;
        }

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        public void WriteTo(OJ.Core.SaveState state)
        {
            // 별 개수·보상 개수는 스테이지 기록과 DB 에서 다시 계산되는 파생값이라 같이 저장하지 않는다.
            // 저장해 두면 스테이지가 추가될 때 두 값이 어긋나고, 어느 쪽이 옳은지 알 방법이 없어진다.
            state.Stage.ClaimedStarRewardIndices.Clear();
            foreach (int rewardIndex in claimedRewardIndices)
                state.Stage.ClaimedStarRewardIndices.Add(rewardIndex);
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            claimedRewardIndices.Clear();

            List<int> savedIndices = state.Stage.ClaimedStarRewardIndices;
            for (int i = 0; i < savedIndices.Count; i++)
            {
                int rewardIndex = savedIndices[i];

                // 음수는 어떤 보상에도 대응하지 않는다. 걸러 두지 않으면 저장할 때마다 그대로 되쓰여
                // 세이브에 영영 남는다. 반대로 위쪽 경계는 자르지 않는다 — 보상 개수는 스테이지 DB 가
                // 커지면 같이 늘어나서, 지금 기준으로 잘라 내면 나중에 유효해질 수령 기록을 잃는다.
                if (rewardIndex >= 0)
                    claimedRewardIndices.Add(rewardIndex);
            }
        }

        public int GetStageStarCount(int stageIndex)
        {
            return StageStarUtility.GetStarCount(stageProgress.GetBestClearGrade(stageIndex));
        }

        public int GetTotalStarCount()
        {
            int total = 0;
            int stageCount = GetStageCount();
            for (int i = 1; i <= stageCount; i++)
                total += GetStageStarCount(i);

            return total;
        }

        public int GetMaxStarCount()
        {
            return GetStageCount() * StageStarUtility.MaxStarsPerStage;
        }

        public int GetRewardCount()
        {
            return GetMaxStarCount() / StageStarUtility.StarsPerReward;
        }

        public int GetRequiredStars(int rewardIndex)
        {
            return (Mathf.Max(0, rewardIndex) + 1) * StageStarUtility.StarsPerReward;
        }

        public bool IsRewardClaimed(int rewardIndex)
        {
            return claimedRewardIndices.Contains(rewardIndex);
        }

        public bool IsRewardClaimable(int rewardIndex)
        {
            return !IsRewardClaimed(rewardIndex) && GetTotalStarCount() >= GetRequiredStars(rewardIndex);
        }

        public bool HasClaimableReward()
        {
            int rewardCount = GetRewardCount();
            for (int i = 0; i < rewardCount; i++)
            {
                if (IsRewardClaimable(i))
                    return true;
            }

            return false;
        }

        public bool TryClaimReward(int rewardIndex, out List<PointRewardEntry> grantedRewards)
        {
            grantedRewards = new List<PointRewardEntry>();

            if (rewardIndex < 0 || rewardIndex >= GetRewardCount())
                return false;

            if (!IsRewardClaimable(rewardIndex))
                return false;

            grantedRewards.Add(new PointRewardEntry(PointType.Dia, StageStarUtility.DiaRewardAmount));
            claimedRewardIndices.Add(rewardIndex);
            PointRewardUtility.GrantRewards(grantedRewards);

            // 보상 수령은 되돌릴 수 없는 거래다. 저장 매체가 PlayerPrefs 에서 통합 세이브로
            // 바뀌었어도 이 호출 지점은 그대로 남겨야 한다 — 여기서 즉시 쓰지 않으면 앱이
            // 백그라운드로 갈 때까지 수령 기록이 메모리에만 있고, 모바일에서 OS 가 프로세스를
            // 죽이는 것은 일상이다. 그렇게 잃으면 같은 보상을 다시 받을 수 있는 상태가 된다.
            Save();
            OnChanged?.Invoke();
            return true;
        }

        private void HandleStageProgressChanged()
        {
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 거래 시점에 진행도를 파일로 굳힌다.
        ///
        /// <c>?.</c> 가 필요하다 — 매니저 생성자가 도는 시점에는 <see cref="GameContainer.SaveService"/>
        /// 가 아직 없다(컨테이너가 매니저를 다 만든 뒤에 SaveService 를 해석한다). 생성자에서
        /// 간접적으로 여기까지 오는 경로가 생겨도 조용히 건너뛰는 것이 맞다.
        /// </summary>
        private void Save() => GameContainer.SaveService?.SaveAll();

        private static int GetStageCount()
        {
            return Mathf.Max(1, StageDatabaseProvider.GetDatabase().StageCount);
        }
    }
}
