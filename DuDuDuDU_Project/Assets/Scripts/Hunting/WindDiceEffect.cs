using System.Collections.Generic;
using UnityEngine;
using OJ.DI;
using OJ.Dice;
using OJ.Relic;

namespace OJ.Hunting
{
    public class WindDiceEffect : DiceEffectBase
    {
        private const float BasePushDistance = 0.7f;

        // 8.3b: 컨테이너가 만들지 않고 AttackContent 가 new 로 찍는 순수 C# 클래스라
        // 생성자로 창구를 받는다. 창구 필드는 기반 클래스(DiceEffectBase)가 protected 로
        // 들고 있으니 여기서 새로 만들지 않고 base 로 넘기기만 한다 — 여기서 따로 필드를
        // 두면 기반의 것을 가려서(CS0108) 기반의 PlayEffectAt 이 빈 창구를 보게 된다.
        public WindDiceEffect(IBattleRefs battle) : base(battle)
        {
        }

        public override DiceType DiceType => DiceType.Wind;

        /// <summary>
        /// <b>"바람은 피해가 없다"는 뜻이 아니다.</b> 진화 개편 이후 바람 다이스는 피해를 준다 —
        /// <see cref="TryCastWithoutTarget"/> 이 직접 <c>HitMonster</c> 를 부른다.
        ///
        /// 이 플래그가 그대로 <c>false</c> 인 것은 <b>그것을 읽는 자리가 총알 경로뿐</b>이기
        /// 때문이다(<c>AttackContent.PlayHit</c>). 바람은 대상 없이 캐스트되어 총알을 아예
        /// 쏘지 않으므로 그 경로를 지나지 않고, 따라서 이 값은 바람에게 조회되지 않는다.
        /// <c>true</c> 로 바꿔도 동작은 같지만, 그러면 "총알이 맞으면 피해를 준다"는 약속을
        /// 지킬 수 없는 다이스가 참이라고 말하게 된다.
        ///
        /// <b>형제인 타임 다이스는 반대다.</b> 그쪽은 총알을 쏘도록 바꿨으므로
        /// <c>ShouldApplyDamage</c> 가 <c>true</c> 여야 실제로 피해가 들어간다.
        /// 두 다이스가 같은 목적(상위 단계다운 피해)을 <b>다른 경로로</b> 달성한다.
        /// </summary>
        public override bool ShouldApplyDamage => false;

        public override bool TryCastWithoutTarget(AttackContent attackContent, int shotDicePip)
        {
            // 8.3b: MonsterManager 는 전투 씬 안에서 null 이 될 수 없으므로 매니저 자체를
            // 검사하던 부분은 지운다. activeMonsters 는 매니저가 들고 있는 데이터라 그대로 본다.
            if (attackContent == null || battle.Monsters.activeMonsters == null)
                return false;

            // 8.3b: GameManager 는 살아 있는 게 보장되지만 wall 은 스테이지 상태에 따라
            // 비어 있을 수 있는 데이터 필드라 null 검사를 남긴다.
            Wall wall = battle.Game.wall;
            if (wall == null)
                return false;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            int targetCount = DiceMetaDataProvider.GetWindTargetCount(level);
            float chancePercent = DiceMetaDataProvider.GetWindPushChancePercent(DiceType, level);
            if (RelicManager.Instance != null)
                chancePercent += RelicManager.Instance.GetWindPushChanceBonusPercent();
            float distance = BasePushDistance * DiceMetaDataProvider.GetWindDistanceMultiplier(level);

            Vector2 areaCenter = GetWallFrontAreaCenter(wall);
            Vector3 effectPosition = new Vector3(areaCenter.x, areaCenter.y, wall.transform.position.z);
            List<Monster> candidates = GetWallFrontTargets(attackContent, wall, targetCount * 3);

            if (candidates.Count == 0)
                return false;

            Shuffle(candidates);
            int castCount = Mathf.Min(targetCount, candidates.Count);

            // 진화 개편에서 바람 다이스가 <b>피해를 주기 시작했다.</b>
            //
            // 예전에는 baseAttack 이 0 이었고 ShouldApplyDamage 도 false 라 순수 유틸이었다.
            // 그래도 됐던 것은 특수 다이스가 ★2 두 개로 만드는 <b>곁가지</b>였기 때문이다.
            // 지금은 4성 아이스가 진화해 도달하는 <b>상위 단계</b>다 — 피해가 0 이면
            // 재화 10 개를 내고 딜을 통째로 잃는 셈이라, 아무도 아이스를 진화시키지 않는다.
            //
            // <b>총알 경로를 타지 않는다.</b> 이 다이스는 PlayerController 에서 대상 없이
            // 캐스트되고(벽 앞 띠 전체를 민다) 그래서 AttackContent.PlayHit 를 지나지 않는다.
            // 그 구조를 바꾸는 대신 여기서 직접 때린다 — 미는 대상이 곧 맞는 대상이다.
            //
            // 피해는 <b>밀기 판정과 무관하게</b> 들어간다. 밀기는 확률이라, 피해까지 거기
            // 묶으면 같은 다이스가 어떤 발사에서는 0 딜이 되어 표시 수치를 신뢰할 수 없다.
            int damage = DiceMetaDataProvider.CalculateDamage(DiceType, Mathf.Max(1, shotDicePip), level);

            bool castAny = false;
            for (int i = 0; i < castCount; i++)
            {
                Monster monster = candidates[i];

                if (damage > 0)
                    attackContent.HitMonster(monster, DiceType, damage);

                castAny = true;

                if (Random.value * 100f > chancePercent)
                    continue;

                monster.PushBy(Vector2.up, distance);
                if (level >= 6)
                    monster.ApplyWindDamageTakenBonus(10, 3f);
                if (RelicManager.Instance != null)
                    monster.ApplyRelicDamageTakenBonus(RelicManager.Instance.GetWindDamageTakenBonusPercent(), 3f);
                PlayEffectAt(DiceType, effectPosition);
            }

            return castAny;
        }

        private List<Monster> GetWallFrontTargets(AttackContent attackContent, Wall wall, int maxTargets)
        {
            Collider2D wallCollider = wall != null ? wall.GetComponent<Collider2D>() : null;
            Bounds bounds = wallCollider != null ? wallCollider.bounds : new Bounds(wall.transform.position, new Vector3(3f, 1f, 0f));
            Vector2 origin = GetWallFrontAreaCenter(wall);
            float halfLength = 0.6f;
            float halfWidth = Mathf.Max(0.8f, bounds.extents.x * 0.6f);

            attackContent.SetWindRangeGizmo(bounds.min.x, bounds.max.x, origin.y, halfLength);
            List<Monster> targets = attackContent.GetMonstersInOrientedBox(
                origin,
                Vector2.up,
                halfLength,
                halfWidth,
                maxTargets,
                null);

            return targets;
        }

        private static Vector2 GetWallFrontAreaCenter(Wall wall)
        {
            if (wall == null)
                return Vector2.zero;

            Collider2D wallCollider = wall.GetComponent<Collider2D>();
            if (wallCollider == null)
                return wall.transform.position;

            Bounds bounds = wallCollider.bounds;
            return new Vector2(bounds.center.x, bounds.max.y + 0.15f);
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
