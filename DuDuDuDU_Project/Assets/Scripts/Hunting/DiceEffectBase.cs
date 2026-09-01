using System.Collections.Generic;
using OJ.DI;
using UnityEngine;

namespace OJ.Hunting
{
    public abstract class DiceEffectBase
    {
        /// <summary>
        /// 배틀 매니저로 가는 창구. (8.3b)
        ///
        /// <b>왜 private 이 아니라 protected 인가.</b> 파생 15개 중 다섯(Thunder·KingThunder·Wind·
        /// Time·Tornado)이 자기 코드 안에서 <c>battle.Monsters</c>·<c>battle.Game</c>·
        /// <c>battle.Player</c>·<c>battle.BulletEffects</c> 를 직접 읽는다. 기반이 창구를 protected 로
        /// 들고 있으면 파생은 같은 이름의 필드를 다시 만들 필요 없이 <c>base(battle)</c> 로 넘기기만
        /// 하면 된다 — 기반 클래스에서 할 수 있는 가장 적은 변경이다.
        /// 반대로 파생이 자기 필드를 또 선언하면 기반 것을 가려서(CS0108) 기반의 <c>PlayEffectAt</c>·
        /// <c>PlayLineEffect</c> 만 빈 창구를 보게 되고, 이펙트를 찍는 그 순간에야 NRE 로 터진다.
        ///
        /// 파생에 남아 있는 <c>DiceLevelManager</c>·<c>RelicManager</c>·<c>EquipmentManager</c> 의
        /// <c>.Instance</c> 는 이 창구와 무관하다 — 배틀 씬 매니저 14개가 아니라 루트 서비스라
        /// <c>IBattleRefs</c> 가 들고 있지 않다.
        ///
        /// <b>여기서 null 은 사고다.</b> 이 객체는 <c>AttackContent</c> 가 BattleScene 안에서만
        /// <c>new</c> 하므로 전투 밖에서는 존재조차 하지 않는다. 그래서 <c>?.</c> 로 감싸지 않는다 —
        /// 창구가 비어 있으면 조용히 넘어가는 대신 울어야 한다.
        /// </summary>
        protected readonly IBattleRefs battle;

        /// <summary>
        /// 이 클래스는 컨테이너가 만들지 않고 <c>AttackContent</c> 가 직접 <c>new</c> 하는 순수 C# 이다.
        /// 그래서 <c>[Inject]</c> 필드가 아니라 생성자로 창구를 받는다 — 만드는 쪽이 넘겨줘야 한다.
        /// </summary>
        protected DiceEffectBase(IBattleRefs battle)
        {
            this.battle = battle;
        }

        public abstract DiceType DiceType { get; }
        public virtual bool ShouldApplyDamage => true;

        public virtual void BuildTargets(AttackContent attackContent, Monster rootTarget, List<Monster> hitMonsters)
        {
        }

        public virtual void ApplyOnHit(AttackContent attackContent, Monster target)
        {
        }

        public virtual bool TryCastWithoutTarget(AttackContent attackContent, int shotDicePip)
        {
            return false;
        }

        protected void PlayEffectAt(DiceType diceType, Vector3 position, EffectID effectId = EffectID.S)
        {
            // 아래 null 검사는 풀이 비어 돌려줄 이펙트가 없을 때를 막는 것이지 창구를 막는 것이 아니다.
            // 그래서 그대로 둔다 — 지우면 풀 고갈 시 동작이 바뀐다.
            BulletEffect effect = battle.BulletEffects.GetBullet(diceType, effectId);
            if (effect == null)
                return;

            effect.transform.position = position;
            effect.PlayEffect();
        }

        protected void PlayLineEffect(DiceType diceType, Vector3 startPos, Vector3 endPos, EffectID effectId = EffectID.S)
        {
            BulletEffect effect = battle.BulletEffects.GetBullet(diceType, effectId);
            if (effect == null)
                return;

            effect.PlayLineEffect(startPos, endPos);
        }
    }
}
