using UnityEngine;
using OJ.DI;
using OJ.Dice;

namespace OJ.Hunting
{
    public class TimeDiceEffect : DiceEffectBase
    {
        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스(DiceEffectBase)가 protected 로
        // 들고 있으니 여기서 새로 만들지 않고 base 로 넘기기만 한다.
        public TimeDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.Time;

        /// <summary>
        /// <b>피해를 준다.</b> 진화 개편 전에는 false 였다 — 타임 다이스는 ★2 두 개로 만드는
        /// 곁가지라 순수 유틸이어도 됐다. 지금은 4성 썬더가 진화해 도달하는 상위 단계이고,
        /// 피해가 0 이면 재화 10 개를 내고 딜을 잃는 진화가 된다.
        /// </summary>
        public override bool ShouldApplyDamage => true;

        /// <summary>
        /// <b>쿨타임 감소는 여기서 하지 않는다.</b> <c>PlayerController.ShootAtClosest</c> 가
        /// 총알을 쏘기 전에 이미 했다.
        ///
        /// 옮긴 이유는 <c>sourceDice</c> 다. 여기서는 어느 다이스가 쐈는지 알 수 없어
        /// <c>ReduceRemainingCooldownPercentForOtherDice</c> 를 인자 없이 부를 수밖에 없었고,
        /// 그러면 <b>자기 자신의 쿨타임까지 후보에 든다.</b> 예전에는 이 경로가 아예 안 돌아서
        /// (타임은 총알을 쏘지 않고 PlayerController 에서 끝났다) 드러나지 않았을 뿐이다.
        /// 프레임 중복을 막던 <c>_lastCastFrame</c> 도 같이 사라졌다 — 한 번만 도는 자리로
        /// 옮겼으니 여러 대상에 나눠 불릴 일이 없다.
        /// </summary>
        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target != null)
                PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
