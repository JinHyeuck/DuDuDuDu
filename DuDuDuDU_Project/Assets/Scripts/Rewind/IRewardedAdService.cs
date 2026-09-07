using System;
using UnityEngine.Scripting;

namespace OJ.Rewind
{
    /// <summary>
    /// 리워드 광고로 가는 포트.
    ///
    /// <b>이 프로젝트에는 광고 SDK 가 아직 0 줄이다.</b> 그래서 지금 붙어 있는 구현은
    /// <see cref="NullRewardedAdService"/> 하나이고, 되돌리기의 광고 몫은 화면에 뜨지 않는다.
    /// 그래도 포트를 먼저 두는 이유는 <b>붙이는 날 바꿀 곳을 하나로 묶어 두기 위해서</b>다 —
    /// SDK 를 호출부에 직접 심으면 나중에 그 호출이 UI·매니저·설정에 흩어진 채로 발견된다.
    ///
    /// <b>UI 는 <see cref="IsAvailable"/> 이 false 면 광고 버튼을 아예 그리지 않는다.</b>
    /// 눌리지 않는 버튼을 남기는 것은 "광고가 안 나온다" 는 고장으로 읽힌다.
    /// </summary>
    public interface IRewardedAdService
    {
        /// <summary>지금 보여 줄 광고가 있는가. 없으면 광고 경로는 화면에 없다.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// 광고를 보여 준다. 끝까지 본 경우에만 <paramref name="onRewarded"/> 를 부른다.
        ///
        /// <b>중간에 닫은 것과 실패한 것을 구분하지 않는다.</b> 둘 다 "보상 없음" 이고,
        /// 되돌리기는 그 둘에 다르게 반응할 이유가 없다. 구분이 필요해지는 날
        /// 그때 인자를 늘린다.
        /// </summary>
        void Show(Action onRewarded, Action onFailed = null);
    }

    /// <summary>
    /// 광고가 없는 동안의 구현. <b>언제나 없다고 답한다.</b>
    ///
    /// 조용히 성공시키지 않는 것이 중요하다 — 그러면 개발 중에는 광고 몫이 공짜로
    /// 늘어나 밸런스가 실제와 달라지고, 그 상태로 수치를 정하게 된다.
    /// </summary>
    [Preserve]
    public sealed class NullRewardedAdService : IRewardedAdService
    {
        public bool IsAvailable => false;

        public void Show(Action onRewarded, Action onFailed = null)
        {
            onFailed?.Invoke();
        }
    }
}
