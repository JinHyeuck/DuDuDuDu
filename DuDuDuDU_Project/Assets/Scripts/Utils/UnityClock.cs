using System;
using OJ.Core;
using UnityEngine;
using OJ.Hunting;

namespace OJ.Utils
{
    /// <summary>
    /// 실제 Unity 시간을 <see cref="IClock"/> 으로 노출한다. (MIGRATION_BASELINE 5.5)
    ///
    /// 게임 코드는 이걸 쓰고, 테스트는 <see cref="TestClock"/> 을 쓴다.
    ///
    /// <b>Time.time 과 Time.unscaledTime 을 섞지 마라.</b> 이 프로젝트는
    /// <c>GameManager.OnClick_Speed</c> 가 <c>Time.timeScale</c> 을 1/2/3 으로 바꾼다.
    /// 쿨타임·상태이상 만료는 <see cref="GameTime"/>(배속 영향 받음), 연출 대기는
    /// <see cref="RealTime"/>(영향 안 받음)이다. 반대로 쓰면 배속에서 조용히 어긋난다.
    ///
    /// <c>Instance</c> 는 정적 필드일 뿐 MonoSingleton 이 아니다 — 상태가 없으니 인스턴스를
    /// 만들 이유도, 씬에 둘 이유도 없다.
    /// </summary>
    public sealed class UnityClock : IClock
    {
        public static readonly UnityClock Instance = new UnityClock();

        private UnityClock()
        {
        }

        public float GameTime => Time.time;

        public float RealTime => Time.unscaledTime;

        public DateTime UtcNow => DateTime.UtcNow;
    }
}
