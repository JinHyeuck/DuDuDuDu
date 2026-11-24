using System;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace OJ.Dice
{
    /// <summary>
    /// <see cref="PromotionManager.Tick(DateTime)"/> 을 매 프레임 부르는 박자. 날짜가 바뀐 프레임에만 일이 있고
    /// 나머지는 정수 비교 하나로 끝난다. 플레이 중에 이벤트가 바뀌는 순간 남은 재화를 환급하려는 것이다.
    ///
    /// <b>매니저에 <c>ITickable</c> 을 직접 붙이지 않은 이유.</b> 헤드리스 테스트 러너에는 VContainer 가 없어서
    /// 그 인터페이스를 구현한 클래스는 로드조차 못 한다 — 매니저를 테스트하려면 박자를 떼어 놓아야 한다.
    /// </summary>
    [Preserve]
    public sealed class PromotionTicker : ITickable
    {
        private readonly PromotionManager manager;

        public PromotionTicker(PromotionManager manager)
        {
            this.manager = manager;
        }

        public void Tick()
        {
            manager.Tick(DateTime.UtcNow);
        }
    }
}
