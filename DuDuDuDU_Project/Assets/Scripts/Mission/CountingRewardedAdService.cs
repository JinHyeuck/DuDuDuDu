using System;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.Rewind;

namespace OJ.Mission
{
    /// <summary>
    /// 광고를 끝까지 본 것을 세고 <b>진짜 구현에 그대로 넘기는</b> 껍데기.
    ///
    /// <b>왜 호출부에 <c>Notify</c> 를 심지 않았나.</b> 지금 광고를 띄우는 곳이 셋이고
    /// (<c>BonusDiceManager</c>·<c>UIBonusDicePanel</c>·<c>WaveRewindManager</c>) 앞으로 는다.
    /// 각자 세면 <b>새 광고 지점을 추가할 때 세는 것을 빠뜨리는 쪽이 기본값</b>이 되고,
    /// 그 사고는 "광고를 봤는데 미션이 안 오른다" 로만 드러난다 — 재현도 어렵다.
    /// 포트를 한 번 감싸면 빠질 수가 없다.
    ///
    /// <b>중간에 닫은 것은 세지 않는다.</b> <c>onRewarded</c> 가 불린 경우에만 올린다 —
    /// 그것이 <c>IRewardedAdService</c> 가 "끝까지 봤다" 를 표현하는 유일한 방법이다.
    ///
    /// <b>SDK 를 붙이는 날 고칠 곳은 <c>GameContainer</c> 의 등록 한 줄</b>이다.
    /// 여기 감싸인 <c>NullRewardedAdService</c> 자리에 진짜 구현을 넣으면
    /// 카운트는 저절로 따라온다.
    /// </summary>
    [Preserve]
    public sealed class CountingRewardedAdService : IRewardedAdService
    {
        private readonly IRewardedAdService inner;
        private readonly MissionManager missions;

        public CountingRewardedAdService(IRewardedAdService inner, MissionManager missions)
        {
            this.inner = inner;
            this.missions = missions;
        }

        public bool IsAvailable => inner != null && inner.IsAvailable;

        public void Show(Action onRewarded, Action onFailed = null)
        {
            if (inner == null)
            {
                onFailed?.Invoke();
                return;
            }

            inner.Show(
                () =>
                {
                    // 세는 것을 먼저 한다. 보상 콜백이 씬을 바꾸거나 창을 닫는 경우가 있는데,
                    // 그 뒤에 세려 들면 이 대리자가 죽은 오브젝트와 함께 묻힌다.
                    missions?.Notify(MissionAction.AdWatch);
                    onRewarded?.Invoke();
                },
                onFailed);
        }
    }
}
