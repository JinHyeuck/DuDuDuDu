using UnityEngine;

namespace OJ
{
    public class Bullet : MonoBehaviour
    {
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
                BulletPool.Instance.PoolBullet(this);
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
            AttackContent.Instance.PlayHit(monster, _diceType, _diceStar);
            BulletPool.Instance.PoolBullet(this);
        }

        private void OnDisable()
        {
            _hasImpacted = false;
        }
    }

}
