using UnityEngine;

public class Bullet : MonoBehaviour
{
    public SpriteRenderer bulletImage;

    public float speed = 8f;
    public int damage = 1;
    private Vector2 moveDir;

    public void SetBulletStat(DiceType diceType)
    {
        Color typeColor = StaticResource.Instance.DiceTypeResourceManager.GetColor(diceType);
        
        bulletImage.color = typeColor;

        damage = DiceTypeStarManager.Instance.GetTypeStars(diceType);
    }

    public void Shoot(Vector2 dir)
    {
        moveDir = dir;
        gameObject.SetActive(true);
    }

    void Update()
    {
        transform.Translate(moveDir * speed * Time.deltaTime);

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
