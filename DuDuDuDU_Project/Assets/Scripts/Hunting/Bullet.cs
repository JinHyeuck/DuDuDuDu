using UnityEngine;

namespace OJ
{
    public class Bullet : MonoBehaviour
    {
        public SpriteRenderer bulletImage;

        public float speed = 8f;
        private Vector2 moveDir;
        private DiceType _diceType = DiceType.Normal;
        private int _diceStar = 1;

        public void SetBulletStat(DiceType diceType, int diceStar)
        {
            _diceType = diceType;

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
            transform.Translate(Vector2.up * speed * Time.deltaTime);

            if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
                BulletPool.Instance.PoolBullet(this);
        }

        void OnTriggerEnter2D(Collider2D col)
        {
            if (col.CompareTag("Monster"))
            {
                Monster monster = col.GetComponent<Monster>();

                AttackContent.Instance.PlayHit(monster, _diceType, _diceStar);

                BulletPool.Instance.PoolBullet(this);
            }
        }
    }

}
