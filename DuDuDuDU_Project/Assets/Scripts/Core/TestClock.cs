using System;

namespace OJ.Core
{
    /// <summary>
    /// 테스트·헤드리스용 시계. 값을 직접 세운다. (MIGRATION_BASELINE 5.5)
    ///
    /// <c>OJ.Core</c> 안에 두는 이유는 테스트 어셈블리가 <c>Assembly-CSharp</c> 을 참조할 수
    /// 없기 때문이다. 여기 있어야 EditMode 에서 쓸 수 있다.
    ///
    /// 기본값이 0 이 아니라 <see cref="DefaultGameTime"/> 인 것은 의도다. Unity 의
    /// <c>Time.time</c> 은 씬 시작 시 0 이지만, 상태이상 만료 판정이 <c>until</c> 필드를
    /// -1f 로 초기화해 두고 <c>now &lt; until</c> 로 보기 때문에 <b>now 가 0 이면 "만료됨"과
    /// "아직 시작 안 함"이 구분되지 않는다.</b> 0 이 필요하면 명시적으로 넣어라.
    /// </summary>
    public sealed class TestClock : IClock
    {
        public const float DefaultGameTime = 100f;

        public TestClock()
        {
            GameTime = DefaultGameTime;
            RealTime = DefaultGameTime;
            UtcNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        public float GameTime { get; set; }

        public float RealTime { get; set; }

        public DateTime UtcNow { get; set; }

        /// <summary>게임 시간과 실시간을 함께 민다. 배속을 재현하려면 따로 세워라.</summary>
        public void Advance(float seconds)
        {
            GameTime += seconds;
            RealTime += seconds;
        }
    }
}
