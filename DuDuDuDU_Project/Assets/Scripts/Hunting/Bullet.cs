using UnityEngine;

public class Bullet : MonoBehaviour
{
    public SpriteRenderer bulletImage;

    public float speed = 8f;
    public int damage = 1;
    private Vector2 moveDir;

    public void SetBulletStat(DiceType diceType)
    {
        Sprite sprite = StaticResource.Instance.DiceTypeResourceManager.GetBullet(diceType);

        bulletImage.sprite = sprite;

        damage = DiceTypeStarManager.Instance.GetTypeStars(diceType);
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
            monster.TakeDamage(damage);

            // 데미지 UI 표시
            GameObject dtObj = DamageTextPool.Instance.GetDamageText();
            dtObj.transform.position = col.transform.position; // 몬스터 위치
            dtObj.GetComponent<DamageText>().SetText(damage);

            BulletPool.Instance.PoolBullet(this);
        }
    }
}
