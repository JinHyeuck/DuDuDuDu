using UnityEngine;
using OJ.DI;
using OJ.Dice;
using VContainer;

namespace OJ.Hunting
{
    public class Bullet : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. 이 총알은 BulletPool 이 런타임에 찍는 프리팹이라
        // 씬 순회로는 안 잡히고, 생성부의 resolver.Instantiate 가 찍는 그 순간에 주입된다.
        // 이 경로는 Awake 에서도 안전하다 — VContainer 의 부모 있는 Instantiate 가
        // 프리팹을 껐다 찍고 주입한 뒤에 켜므로(ObjectResolverUnityExtensions.cs:78-91)
        // 클론의 Awake 는 주입 뒤에 돈다. (씬에 놓인 컴포넌트는 반대로 자기 Awake 뒤에
        // 채워진다 — 같은 [Inject] 라도 태어난 경로에 따라 시점이 갈린다.)
        // 아래 사용처는 전부 Update·OnTriggerEnter2D 라 어느 쪽이든 지난 뒤다.
        // null 이면 그것은 사고이니 ?. 를 쓰지 않는다.
        [Inject] private IBattleRefs battle;

        private const float HitSweepRadius = 0.12f;

        public SpriteRenderer bulletImage;

        public float speed = 8f;
        private Vector2 moveDir;
        private DiceType _diceType = DiceType.Normal;
        private int _diceStar = 1;
        private bool _hasImpacted;
        private readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[8];

        public void SetBulletStat(DiceType diceType, int diceStar)
        {
            _diceType = diceType;
            _hasImpacted = false;

            Sprite sprite = DiceMetaDataProvider.GetProjectileSprite(_diceType);

            bulletImage.sprite = sprite;

            _diceStar = Mathf.Max(1, diceStar);
        }

        public void Shoot(Vector2 dir)
        {
            moveDir = dir;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            gameObject.SetActive(true);
        }

        void Update()
        {
            Vector2 startPos = transform.position;
            float moveDistance = speed * Time.deltaTime;
            Vector2 endPos = startPos + (moveDir.normalized * moveDistance);

            if (TrySweepHit(startPos, endPos))
                return;

            transform.position = endPos;

            if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
                battle.Bullets.PoolBullet(this);
        }

        void OnTriggerEnter2D(Collider2D col)
        {
            if (_hasImpacted)
                return;

            if (col.CompareTag("Monster"))
            {
                Monster monster = col.GetComponent<Monster>();
                Impact(monster);
            }
        }

        private bool TrySweepHit(Vector2 startPos, Vector2 endPos)
        {
            if (_hasImpacted)
                return true;

            Vector2 direction = endPos - startPos;
            float distance = direction.magnitude;
            if (distance <= 0.0001f)
                return false;

            int hitCount = Physics2D.CircleCastNonAlloc(
                startPos,
                HitSweepRadius,
                direction.normalized,
                _hitBuffer,
                distance,
                Physics2D.AllLayers);

            if (hitCount <= 0)
                return false;

            float nearestDistance = float.MaxValue;
            Monster nearestMonster = null;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D collider = _hitBuffer[i].collider;
                if (collider == null || !collider.CompareTag("Monster"))
                    continue;

                Monster monster = collider.GetComponent<Monster>();
                if (monster == null || !monster.gameObject.activeInHierarchy)
                    continue;

                if (_hitBuffer[i].distance < nearestDistance)
                {
                    nearestDistance = _hitBuffer[i].distance;
                    nearestMonster = monster;
                }
            }

            if (nearestMonster == null)
                return false;

            transform.position = nearestMonster.transform.position;
            Impact(nearestMonster);
            return true;
        }

        private void Impact(Monster monster)
        {
            if (_hasImpacted || monster == null)
                return;

            _hasImpacted = true;
            battle.Attack.PlayHit(monster, _diceType, _diceStar);
            battle.Bullets.PoolBullet(this);
        }

        private void OnDisable()
        {
            _hasImpacted = false;
        }
    }

}
