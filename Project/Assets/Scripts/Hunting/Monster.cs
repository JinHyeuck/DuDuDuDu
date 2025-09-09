using UnityEngine;
using System.Collections;

public class Monster : MonoBehaviour
{
    public int _hp = 3;
    public int attackDamage = 1;
    public float attackInterval = 0.5f;
    public float moveSpeed = 2f; // 이동 속도
    private bool isAttacking = false;

    private void OnEnable()
    {
        MonsterManager.Instance.RegisterMonster(this);
    }

    private void OnDisable()
    {
        MonsterManager.Instance.UnregisterMonster(this);
        UIDiceSummonSystem.Instance?.AddSP(10);
    }

    void Update()
    {
        // Wall을 향해 계속 이동 (아직 공격하지 않을 때만)
        if (!isAttacking)
        {
            transform.Translate(Vector2.down * moveSpeed * Time.deltaTime);
        }
    }

    public void TakeDamage(int dmg)
    {
        _hp -= dmg;
        if (_hp <= 0)
            gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Wall") && !isAttacking)
        {
            isAttacking = true;
            StartCoroutine(AttackWall(col.GetComponent<Wall>()));
        }
    }

    IEnumerator AttackWall(Wall wall)
    {
        while (wall != null && wall.hp > 0)
        {
            wall.TakeDamage(attackDamage);

            // 데미지 UI 표시
            GameObject dtObj = DamageTextPool.Instance.GetDamageText();
            dtObj.transform.position = transform.position; // 몬스터 위치
            dtObj.GetComponent<DamageText>().SetText(attackDamage);

            yield return new WaitForSeconds(attackInterval);
        }
        gameObject.SetActive(false);
    }

    public void SetHp(int hp)
    {
        _hp = hp;
    }
}
