using System;

namespace OJ.Core
{
    /// <summary>
    /// 시간 소스. (MIGRATION_BASELINE 5.5)
    ///
    /// 이 프로젝트는 시간을 세 갈래로 읽고 있고, 셋이 서로 다른 것을 잰다.
    /// 섞어 쓰면 배속(<c>Time.timeScale</c> 1/2/3)에서 조용히 어긋난다.
    ///
    /// <list type="table">
    /// <item><term><see cref="GameTime"/></term><description>
    ///   <c>Time.time</c>. <b>배속과 일시정지의 영향을 받는다.</b> 쿨타임·상태이상 만료·
    ///   스폰 간격처럼 "게임 안에서 흐르는" 것은 전부 이쪽이어야 한다.
    /// </description></item>
    /// <item><term><see cref="RealTime"/></term><description>
    ///   <c>Time.unscaledTime</c>. 배속과 무관하다. 연출 대기·UI 애니메이션처럼
    ///   게임이 멈춰도 흘러야 하는 것.
    /// </description></item>
    /// <item><term><see cref="UtcNow"/></term><description>
    ///   <c>DateTime.UtcNow</c>. <b>벽시계다.</b> 방치 보상처럼 앱을 껐다 켠 사이의 시간을
    ///   재는 것에만 쓴다. 기기 시간 조작에 취약하다는 것을 알고 쓸 것.
    /// </description></item>
    /// </list>
    ///
    /// <b>왜 인터페이스인가.</b> 순수 규칙을 EditMode 에서 돌리려면 시간을 주입할 수 있어야
    /// 한다. 지금 <c>Monster</c> 의 상태이상 만료 판정처럼 시간을 <b>인자로 받도록</b> 이미
    /// 고쳐 둔 곳들이 있는데, 그 인자를 만들어 주는 쪽이 여기다.
    ///
    /// <b>지금 당장 78곳을 다 바꾸지 않는다.</b> 5단계는 경계를 세우는 것까지다.
    /// <c>Time.deltaTime</c> 직산은 6단계에서 <c>RunState</c> 가 프레임을 소유하게 되면
    /// 그때 함께 정리한다.
    /// </summary>
    public interface IClock
    {
        /// <summary>배속·일시정지의 영향을 받는 게임 시간(초).</summary>
        float GameTime { get; }

        /// <summary>배속과 무관한 실시간(초).</summary>
        float RealTime { get; }

        /// <summary>벽시계. 앱을 껐다 켠 사이를 재는 용도.</summary>
        DateTime UtcNow { get; }
    }
}
